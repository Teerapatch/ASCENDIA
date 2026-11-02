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
            float speed = requestedVelocity.magnitude;
            if (speed < .001f) return 0f;
            Vector3 direction = requestedVelocity / speed;
            float allowed = speed * Mathf.Max(Time.deltaTime, .001f);
            float requested = allowed;
            for (int i = 0; i < 4; i++)
            {
                if (i == active || !contacts[i].valid) continue;
                Vector3 delta = animator.GetBoneTransform(roots[i]).position + bodyOffset - wall.TransformPoint(contacts[i].point);
                float radius = contacts[i].length * .97f;
                float along = Vector3.Dot(delta, direction);
                float discriminant = along * along + radius * radius - delta.sqrMagnitude;
                float travel = discriminant <= 0 ? 0 : Mathf.Max(0, -along + Mathf.Sqrt(discriminant));
                allowed = Mathf.Min(allowed, travel);
            }
            return Mathf.Clamp01(allowed / requested);
        }
    }
    public void SetClimbVelocity(Vector3 velocity) { requestedVelocity = velocity; }

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
    class Contact { public bool valid; public Vector3 point, start, end; public float length; }
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
        if (head && foot) stature = Mathf.Max(.5f, Vector3.Distance(head.position, foot.position));
        HasWall = Physics.Raycast(playerRoot.position, playerRoot.forward, out var hit,
            Mathf.Max(1.8f, stature), Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) && hit.collider.CompareTag("Wall");
        if (HasWall)
        {
            if (wall != hit.transform) { ResetContacts(); wall = hit.transform; surface = wall.GetComponentInParent<ClimbingSurface>(); }
            normal = hit.normal;
        }
        else ResetContacts();
        weight = Mathf.MoveTowards(weight, HasWall ? contactWeight : 0, Time.deltaTime * 5);
    }
    void ResetContacts() { foreach (var c in contacts) c.valid = false; ready = false; active = -1; next = 0; progress = 0; bodyOffset = Vector3.zero; }

    bool FindContact(int i, Vector3 origin, Vector3 right, Vector3 up, Vector3 lead, out Vector3 point)
    {
        var c = contacts[i]; float side = (i == 0 || i == 2) ? -1 : 1;
        Vector3 desired = origin + up * c.length * (i < 2 ? .42f : -.72f)
            + right * side * c.length * .18f + lead;
        point = default;
        if (!Physics.Raycast(desired + normal * stature, -normal, out var hit, stature * 2.5f,
            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) || hit.transform != wall) return false;
        Vector3 candidate = hit.point + normal * stature * (i < 2 ? .012f : .025f);
        if (surface)
        {
            Vector3 snap = surface.NearestHold(hit.point, stature * .12f) + normal * stature * .012f;
            if (Vector3.Distance(origin, snap) < c.length * reachMargin) candidate = snap;
        }
        if (Vector3.Distance(origin, candidate) > c.length * .99f) return false;
        point = wall.InverseTransformPoint(candidate); return true;
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
        // Keep the torso upright and close enough to the wall for bent arms.
        if (!holding)
        {
            Vector3 hip = animator.GetBoneTransform(HumanBodyBones.Hips).position;
            float distance = Vector3.Dot(hip-wall.position,normal);
            if (Physics.Raycast(hip+normal*stature,-normal,out var bodyHit,stature*3,
                Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore) && bodyHit.transform == wall)
                distance = Vector3.Dot(hip-bodyHit.point,normal);
            Vector3 desiredOffset = normal * Mathf.Clamp(stature*.18f-distance,-stature*.3f,stature*.3f);
            bodyOffset = Vector3.Lerp(bodyOffset,desiredOffset,1-Mathf.Exp(-Time.deltaTime*8));
        }
        animator.bodyPosition += bodyOffset * weight;
        animator.bodyRotation = Quaternion.Slerp(animator.bodyRotation,Quaternion.LookRotation(-normal,up),weight);
        bool all = true;
        for(int i=0;i<4;i++)
        {
            var root=animator.GetBoneTransform(roots[i]); var mid=animator.GetBoneTransform(mids[i]); var tip=animator.GetBoneTransform(tips[i]);
            var c=contacts[i];
            c.length=Vector3.Distance(root.position,mid.position)+Vector3.Distance(mid.position,tip.position);
            if (!c.valid && FindContact(i,root.position+bodyOffset,right,up,Vector3.zero,out var initial)) { c.point=initial; c.valid=true; }
            all &= c.valid;
        }
        ready = all;
        if (ready && speed>.01f && active<0)
        {
            // One limb reaches at a time: the other three remain anchored to the moving wall.
            for(int k=0;k<4;k++)
            {
                int i=order[(next+k)%4]; var c=contacts[i];
                Vector3 lead=velocity.normalized * Mathf.Min(stepLength*stature, c.length*.32f);
                if (!FindContact(i,animator.GetBoneTransform(roots[i]).position+bodyOffset,right,up,lead,out var destination)) continue;
                if (Vector3.Distance(wall.TransformPoint(c.point),wall.TransformPoint(destination))<stature*.06f) continue;
                c.start=c.point;c.end=destination;active=i;progress=0;next=(next+k+1)%4;break;
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
            animator.SetIKHintPosition(hints[i],origin+right*side*c.length*.45f+normal*c.length*.35f-up*c.length*.2f);
        }
    }
    void OnDisable() { ResetContacts(); HasWall=false;climbingPose=false;holding=false;weight=0; }
}
