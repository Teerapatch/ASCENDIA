using UnityEngine;

[RequireComponent(typeof(Animator)), DisallowMultipleComponent]
public class ClimbingContactIK : MonoBehaviour
{
    public PlayerStateManager state;
    public Transform playerRoot;
    [Range(0, 1)] public float contactWeight = 1f;
    [Range(0, 1)] public float wristRotationWeight = 0f;
    [Tooltip("Distance climbed per animation cycle, measured at visual scale 1.")]
    public float distancePerCycle = 0.8f;
    public float reachMargin = 0.95f;
    public bool HasWall { get; private set; }
    private Animator animator;
    private Transform wall;
    private ClimbingSurface surface;
    private Vector3 normal;
    private Vector3 requestedVelocity;
    [Range(0f, 1f)] public float phaseOffset = 0f;
    [Range(0f, .8f)] public float pullRhythm = .35f;
    public void SetClimbVelocity(Vector3 velocity) { requestedVelocity = velocity; }
    public float MovementMultiplier
    {
        get
        {
            if (!animator || !animator.GetCurrentAnimatorStateInfo(0).IsTag("Climbing") || animator.IsInTransition(0)) return 0f;
            float phase = animator.GetCurrentAnimatorStateInfo(0).normalizedTime + phaseOffset;
            return 1f - pullRhythm * Mathf.Cos(phase * Mathf.PI * 4f);
        }
    }

    private float weight, rate;
    private float scale = 1f;
    private static readonly int Climbing = Animator.StringToHash("IsClimbing");
    private static readonly int Rate = Animator.StringToHash("ClimbRate");
    private class Contact { public bool planted, hasAnchor, swinging; public Vector3 localPoint, swingStart, swingEnd; }
    private readonly Contact[] contacts = { new Contact(), new Contact(), new Contact(), new Contact() };
    private readonly AvatarIKGoal[] goals = { AvatarIKGoal.LeftHand, AvatarIKGoal.RightHand, AvatarIKGoal.LeftFoot, AvatarIKGoal.RightFoot };
    private readonly HumanBodyBones[] tips = { HumanBodyBones.LeftHand, HumanBodyBones.RightHand, HumanBodyBones.LeftFoot, HumanBodyBones.RightFoot };
    private readonly HumanBodyBones[] mids = { HumanBodyBones.LeftLowerArm, HumanBodyBones.RightLowerArm, HumanBodyBones.LeftLowerLeg, HumanBodyBones.RightLowerLeg };
    private readonly HumanBodyBones[] roots = { HumanBodyBones.LeftUpperArm, HumanBodyBones.RightUpperArm, HumanBodyBones.LeftUpperLeg, HumanBodyBones.RightUpperLeg };
    private void Awake()
    {
        animator = GetComponent<Animator>();
        if (!state) state = GetComponentInParent<PlayerStateManager>();
        if (!playerRoot && state) playerRoot = state.transform;
    }
    private void LateUpdate()
    {
        if (!animator || !state || !playerRoot) return;
        bool climbing = state.currentState == PlayerStateManager.State.Climbing;
        scale = Mathf.Max(.1f, Mathf.Abs(transform.lossyScale.y));
        HasWall = false;
        if (climbing && Physics.Raycast(playerRoot.position, playerRoot.forward, out var hit, 1.8f * scale, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
            && hit.collider.CompareTag("Wall"))
        {
            HasWall = true;
            if (wall != hit.transform) { wall = hit.transform; surface = wall.GetComponentInParent<ClimbingSurface>(); ClearContacts(); }
            normal = hit.normal;
            // Drive the animation from requested speed, not the pulsed movement it produces.
            float speed = requestedVelocity.magnitude;
            var clips = animator.GetCurrentAnimatorClipInfo(0);
            float duration = clips.Length > 0 ? clips[0].clip.length : 1f;
            rate = speed < .02f ? 0f : Mathf.Min(1.5f, speed * duration / Mathf.Max(.05f, distancePerCycle * scale));
            if (requestedVelocity.y < -.001f) rate = -rate;

        }
        else { rate = 0; ClearContacts(); }
        animator.SetBool(Climbing, climbing);
        animator.SetFloat(Rate, rate);
        weight = Mathf.MoveTowards(weight, climbing && HasWall ? contactWeight : 0f, Time.deltaTime * 5f);
    }
    private void ClearContacts() { foreach (var contact in contacts) { contact.planted = false; contact.hasAnchor = false; contact.swinging = false; } }
    private void OnAnimatorIK(int layer)
    {
        if (!animator || !playerRoot) return;
        float phase = Mathf.Repeat(animator.GetCurrentAnimatorStateInfo(0).normalizedTime + phaseOffset, 1f);
        for (int i = 0; i < 4; i++)
        {
            animator.SetIKPositionWeight(goals[i], 0); animator.SetIKRotationWeight(goals[i], 0);
            if (weight <= 0 || !wall || !HasWall) continue;
            var tip = animator.GetBoneTransform(tips[i]); var mid = animator.GetBoneTransform(mids[i]); var root = animator.GetBoneTransform(roots[i]);
            if (!tip || !mid || !root) continue;
            // Probe from the animated pose; contact points remain in wall-local coordinates.
            Vector3 origin = tip.position + normal * .5f * scale;
            if (!Physics.Raycast(origin, -normal, out var hit, 1.6f * scale, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) || hit.transform != wall) { contacts[i].planted = false; continue; }
            Vector3 candidate = surface ? surface.NearestHold(hit.point, .16f * scale) : hit.point;
            candidate += normal * (i < 2 ? .025f : .055f) * scale;
            float limbPhase = Mathf.Repeat(phase + (i == 0 || i == 3 ? 0f : .5f), 1f);
            bool swing = Mathf.Abs(rate) > .05f && limbPhase < .45f;
            var contact = contacts[i];
            // Freeze both ends of each reach in wall space, including on scrolling walls.
            if (swing && !contact.swinging)
            {
                contact.swingStart = contact.hasAnchor ? contact.localPoint : wall.InverseTransformPoint(tip.position);
                contact.swingEnd = wall.InverseTransformPoint(candidate);
                contact.swinging = true;
                contact.planted = false;
            }
            Vector3 target;
            if (swing)
            {
                float t = Mathf.Clamp01(limbPhase / .45f);
                if (rate < 0f) t = 1f - t;
                target = Vector3.Lerp(wall.TransformPoint(contact.swingStart), wall.TransformPoint(contact.swingEnd), Mathf.SmoothStep(0, 1, t))
                    + normal * Mathf.Sin(t * Mathf.PI) * .04f * scale;
            }
            else
            {
                if (!contact.planted)
                {
                    contact.localPoint = contact.swinging ? contact.swingEnd : wall.InverseTransformPoint(candidate);
                    contact.planted = true;
                    contact.hasAnchor = true;
                }
                contact.swinging = false;
                target = wall.TransformPoint(contact.localPoint);
            }
            float length = Vector3.Distance(root.position, mid.position) + Vector3.Distance(mid.position, tip.position);
            float reach = length * Mathf.Clamp(reachMargin, .5f, 1f);
            float distance = Vector3.Distance(root.position, target);
            if (distance > reach) { contact.planted = false; target = root.position + Vector3.ClampMagnitude(target - root.position, reach); }
            float influence = weight;
            animator.SetIKPositionWeight(goals[i], influence); animator.SetIKPosition(goals[i], target);
            if (i < 2 && wristRotationWeight > 0) { animator.SetIKRotationWeight(goals[i], influence * wristRotationWeight); animator.SetIKRotation(goals[i], Quaternion.LookRotation(-normal, Vector3.up)); }
        }
    }
    private void OnDisable() { requestedVelocity = Vector3.zero; weight = 0; HasWall = false; ClearContacts(); }
}


