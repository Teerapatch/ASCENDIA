using UnityEngine;

// Humanoid IK is the solver; no animation clip supplies the climbing motion.
[RequireComponent(typeof(Animator)), DisallowMultipleComponent]
public class ClimbingContactIK : MonoBehaviour
{
    public PlayerStateManager state;
    public Transform playerRoot;
    [Range(0, 1)] public float contactWeight = 1f;
    [Range(.6f, .98f)] public float reachMargin = .92f;
    [Header("Procedural climbing")]
    [Range(.1f, .5f)] public float stepLength = .24f;
    [Range(.02f, .2f)] public float liftHeight = .07f;
    [Range(.05f, .5f)] public float stepDuration = .2f;
    public bool HasWall { get; private set; }
    public float MovementMultiplier
    {
        get
        {
            if (!HasWall || !ready || holding || !animator || !wall ||
                !animator.GetCurrentAnimatorStateInfo(0).IsTag("Climbing") || animator.IsInTransition(0)) return 0f;
            // IK may reposition its contacts, but cannot veto a valid movement direction.
            return 1f;
        }
    }
    public static float SupportedTravel(Vector3 delta, Vector3 direction, float radius, float requested)
    {
        if (requested <= 0 || radius <= 0) return 0;
        direction = direction.normalized;
        float along = Vector3.Dot(delta, direction);
        // If an existing contact is overextended, allow motion that reduces the error.
        // Otherwise the player cannot move back towards a reachable pose.
        if (delta.sqrMagnitude > radius*radius && along < 0)
            return Mathf.Min(requested, -along);
        float discriminant = along*along + radius*radius - delta.sqrMagnitude;
        return discriminant <= 0 ? 0 : Mathf.Min(requested, Mathf.Max(0, -along+Mathf.Sqrt(discriminant)));
    }
    public void SetClimbVelocity(Vector3 velocity) { requestedVelocity = velocity; }

    [Range(.02f, .10f)] public float handSurfaceClearance = .045f;
    [Range(.04f, .18f)] public float footSurfaceClearance = .10f;
    float groundHeight;
    bool hasGround;
    float entryLift;
    Animator animator;
    Transform wall;
    ClimbingSurface surface;
    Vector3 normal, requestedVelocity, bodyOffset;
    bool climbingPose, holding, ready;
    float weight, stature = 1.7f, progress;
    int active = -1, next;
    readonly int[] order = {0, 3, 1, 2};
    readonly AvatarIKGoal[] goals = {AvatarIKGoal.LeftHand, AvatarIKGoal.RightHand, AvatarIKGoal.LeftFoot, AvatarIKGoal.RightFoot};
    readonly AvatarIKHint[] hints = {AvatarIKHint.LeftElbow, AvatarIKHint.RightElbow, AvatarIKHint.LeftKnee, AvatarIKHint.RightKnee};
    readonly HumanBodyBones[] roots = {HumanBodyBones.LeftUpperArm, HumanBodyBones.RightUpperArm, HumanBodyBones.LeftUpperLeg, HumanBodyBones.RightUpperLeg};
    readonly HumanBodyBones[] mids = {HumanBodyBones.LeftLowerArm, HumanBodyBones.RightLowerArm, HumanBodyBones.LeftLowerLeg, HumanBodyBones.RightLowerLeg};
    readonly HumanBodyBones[] tips = {HumanBodyBones.LeftHand, HumanBodyBones.RightHand, HumanBodyBones.LeftFoot, HumanBodyBones.RightFoot};
    class Contact { public bool valid; public Vector3 point, start, end, originLocal; public float length; }
    readonly Contact[] contacts = {new Contact(),new Contact(),new Contact(),new Contact()};
    static readonly int Climbing = Animator.StringToHash("IsClimbing"), Rate = Animator.StringToHash("ClimbRate");

    void Awake()
    {
        animator = GetComponent<Animator>();
        if (!state) state = GetComponentInParent<PlayerStateManager>();
        if (!playerRoot && state) playerRoot = state.transform;
    }
    void LateUpdate()
    {
        if (!animator || !animator.isHuman || !state || !playerRoot) return;
        bool climbing = state.currentState == PlayerStateManager.State.Climbing;
        holding = state.currentState == PlayerStateManager.State.Penalty && climbingPose;
        climbingPose = climbing || holding;
        animator.SetBool(Climbing, climbingPose);
        animator.SetFloat(Rate, 0f);
        if (holding) { requestedVelocity = Vector3.zero; return; }
        if (!climbing) { ResetContacts(); HasWall = false; weight = 0; return; }
        var head = animator.GetBoneTransform(HumanBodyBones.Head);
        var foot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
        var hipBone = animator.GetBoneTransform(HumanBodyBones.Hips);
        var thigh = animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
        var knee = animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
        if (head && foot && hipBone && thigh && knee)
            stature = Mathf.Max(.5f, Vector3.Distance(head.position,hipBone.position)
                + Vector3.Distance(thigh.position,knee.position) + Vector3.Distance(knee.position,foot.position));
        HasWall = Physics.Raycast(playerRoot.position, playerRoot.forward, out var hit,
            Mathf.Max(1.8f, stature), Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) && hit.collider.CompareTag("Wall");
        if (HasWall)
        {
            var hitSurface = hit.collider.GetComponentInParent<ClimbingSurface>();
            var anchorRoot = hitSurface ? hitSurface.transform : hit.transform;
            if (wall != anchorRoot) { ResetContacts(); wall = anchorRoot; }
            surface = hitSurface;
            normal = hit.normal;
        }
        else ResetContacts();
        weight = Mathf.MoveTowards(weight, HasWall ? contactWeight : 0, Time.deltaTime * 5);
    }
    void ResetContacts() { foreach (var c in contacts) c.valid = false; ready = false; active = -1; next = 0; progress = 0; bodyOffset = Vector3.zero; entryLift = 0; }

    public static bool TryGroundHeight(Transform owner, Vector3 position, float range, out float height)
    {
        height = float.NegativeInfinity;
        foreach (var hit in Physics.RaycastAll(position + Vector3.up * .2f, Vector3.down, range,
            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            if (hit.collider.transform.IsChildOf(owner) || hit.collider.CompareTag("Wall") || hit.normal.y < .65f) continue;
            height = Mathf.Max(height, hit.point.y);
        }
        return !float.IsNegativeInfinity(height);
    }
    bool SameSurface(Collider collider)
    {
        if (!collider || !collider.CompareTag("Wall")) return false;
        return surface ? collider.GetComponentInParent<ClimbingSurface>() == surface : collider.transform == wall;
    }

    bool FindContact(int i, Vector3 origin, Vector3 right, Vector3 up, Vector3 lead, out Vector3 point)
    {
        var c = contacts[i]; float side = (i == 0 || i == 2) ? -1 : 1;
        point = default;
        // Reach high with the hands; keep feet closer to the hips for bent knees.
        // Try shorter reaches if the wall depth makes the first target unreachable.
        for (int attempt=0; attempt<4; attempt++)
        {
            float fraction = 1f-attempt*.2f;
            float vertical = i < 2 ? (i == 0 ? .70f : .60f) : (i == 2 ? -.80f : -.84f);
            Vector3 desired = origin + up*c.length*vertical*fraction
                + right*side*c.length*(i<2 ? .22f : .26f) + lead*fraction;
            if (i >= 2 && hasGround) desired.y = Mathf.Max(desired.y, groundHeight + stature*.055f);
            if (!Physics.Raycast(desired+normal*stature,-normal,out var hit,stature*2.5f,
                Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore) || !SameSurface(hit.collider)) continue;
            Vector3 candidate=hit.point+normal*stature*(i<2?handSurfaceClearance:footSurfaceClearance);
            if(surface)
            {
                Vector3 snap=surface.NearestHold(hit.point,stature*.12f)+normal*stature*(i<2?handSurfaceClearance:footSurfaceClearance);
                if(Vector3.Distance(origin,snap)<c.length*reachMargin)candidate=snap;
            }
            if(Vector3.Distance(origin,candidate)>c.length*reachMargin)continue;
            point=wall.InverseTransformPoint(candidate);return true;
        }
        return false;
    }

    void OnAnimatorIK(int layer)
    {
        if (layer != 0 || !animator || !playerRoot || !animator.isHuman) return;
        for (int i=0;i<4;i++) { animator.SetIKPositionWeight(goals[i],0); animator.SetIKRotationWeight(goals[i],0); animator.SetIKHintPositionWeight(hints[i],0); }
        if (!climbingPose || !HasWall || !wall || weight <= 0) return;
        Vector3 up = Vector3.ProjectOnPlane(Vector3.up, normal).normalized;
        if (up.sqrMagnitude < .5f) return;
        Vector3 right = Vector3.Cross(up, -normal).normalized;
        Vector3 velocity = Vector3.ProjectOnPlane(requestedVelocity,normal);
        float speed = holding ? 0 : velocity.magnitude;
        hasGround = TryGroundHeight(playerRoot, playerRoot.position + normal * stature*.1f, stature*1.5f, out groundHeight);
        // Keep the torso upright and close enough to the wall for bent arms.
        if (!holding)
        {
            Vector3 hip = animator.GetBoneTransform(HumanBodyBones.Hips).position;
            float distance = Vector3.Dot(hip-wall.position,normal);
            if (Physics.Raycast(hip+normal*stature,-normal,out var bodyHit,stature*3,
                Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore) && SameSurface(bodyHit.collider))
                distance = Vector3.Dot(hip-bodyHit.point,normal);
            Vector3 desiredOffset = normal * Mathf.Clamp(stature*.15f-distance,-stature*.3f,stature*.3f);
            // Shift weight toward the supporting side while a limb is reaching.
            if (active >= 0)
                desiredOffset += right * ((active==0 || active==2) ? 1f : -1f)
                    * stature*.025f*Mathf.Sin(progress*Mathf.PI);
            if (hasGround)
            {
                float lowestFoot = Mathf.Min(animator.GetBoneTransform(HumanBodyBones.LeftFoot).position.y,
                    animator.GetBoneTransform(HumanBodyBones.RightFoot).position.y);
                // Capture sufficient clearance at entry; never alternate between lifting and sinking.
                entryLift = Mathf.Max(entryLift, Mathf.Clamp(groundHeight + stature*.055f - lowestFoot, 0, stature*.4f));
            }
            desiredOffset.y = Mathf.Max(desiredOffset.y, entryLift);
            bodyOffset = Vector3.Lerp(bodyOffset,desiredOffset,1-Mathf.Exp(-Time.deltaTime*8));
        }
        animator.bodyPosition += bodyOffset * weight;
        animator.bodyRotation = Quaternion.Slerp(animator.bodyRotation,Quaternion.LookRotation(-normal,up),weight);
        bool all = true;
        for(int i=0;i<4;i++)
        {
            var root=animator.GetBoneTransform(roots[i]); var mid=animator.GetBoneTransform(mids[i]); var tip=animator.GetBoneTransform(tips[i]);
            var c=contacts[i];
            c.originLocal=playerRoot.InverseTransformPoint(root.position+bodyOffset);
            c.length=Vector3.Distance(root.position,mid.position)+Vector3.Distance(mid.position,tip.position);
            if (!c.valid && FindContact(i,root.position+bodyOffset,right,up,Vector3.zero,out var initial)) { c.point=initial; c.valid=true; }
            all &= c.valid;
        }
        ready = all;
        if (ready && speed>.01f && active<0)
        {
            // Reposition the support that most limits movement, with cyclic order as a tie-breaker.
            int chosen=-1, chosenOrder=0;
            float bestTravel=float.PositiveInfinity;
            Vector3 bestDestination=Vector3.zero;
            for(int k=0;k<4;k++)
            {
                int i=order[(next+k)%4]; var c=contacts[i];
                Vector3 origin=playerRoot.TransformPoint(c.originLocal);
                Vector3 lead=velocity.normalized * Mathf.Min(stepLength*stature,c.length*(i<2?.32f:.16f));
                if(!FindContact(i,origin,right,up,lead,out var destination))continue;
                Vector3 displacement=wall.TransformPoint(destination)-wall.TransformPoint(c.point);
                if(displacement.magnitude<c.length*.015f)continue;
                float travel=SupportedTravel(origin-wall.TransformPoint(c.point),velocity.normalized,c.length*.97f,stature);
                if(travel>=bestTravel-.0001f)continue;
                bestTravel=travel;chosen=i;chosenOrder=k;bestDestination=destination;
            }
            if(chosen>=0)
            {
                var c=contacts[chosen];c.start=c.point;c.end=bestDestination;
                active=chosen;progress=0;next=(next+chosenOrder+1)%4;
            }
        }

        if(active>=0 && speed>.01f)
        {
            var c=contacts[active];
            float duration=Mathf.Min(stepDuration,Mathf.Max(.045f,stepLength*stature/(speed*4)));
            progress=Mathf.Min(1,progress+Time.deltaTime/duration);
            Vector3 world=Vector3.Lerp(wall.TransformPoint(c.start),wall.TransformPoint(c.end),Mathf.SmoothStep(0,1,progress))
                +normal*Mathf.Sin(progress*Mathf.PI)*liftHeight*stature;
            c.point=wall.InverseTransformPoint(world);
            if(progress>=1) { c.point=c.end;active=-1; }
        }
        for(int i=0;i<4;i++)
        {
            var c=contacts[i];if(!c.valid)continue;
            Vector3 origin=animator.GetBoneTransform(roots[i]).position+bodyOffset;
            Vector3 target=wall.TransformPoint(c.point);
            // A hard reach limit prevents hyperextension. Do not move the stored wall anchor.
            target=origin+Vector3.ClampMagnitude(target-origin,c.length*reachMargin);
            animator.SetIKPositionWeight(goals[i],weight);animator.SetIKPosition(goals[i],target);
            float side=(i==0||i==2)?-1:1;
            animator.SetIKHintPositionWeight(hints[i],weight*.75f);
            animator.SetIKHintPosition(hints[i],origin+right*side*c.length*(i<2?.4f:.18f)+normal*c.length*(i<2?.3f:-.18f)-up*c.length*(i<2?.15f:.4f));
        }
    }
    void OnDisable() { ResetContacts(); HasWall=false;climbingPose=false;holding=false;weight=0; }
}
