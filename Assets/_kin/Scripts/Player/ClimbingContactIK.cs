using UnityEngine;

[RequireComponent(typeof(Animator)), DisallowMultipleComponent]
public class ClimbingContactIK : MonoBehaviour
{
    public PlayerStateManager state;
    public Transform playerRoot;
    [Range(0,1)] public float contactWeight = 1f;
    [Range(0,1)] public float wristRotationWeight = 0f;
    [Tooltip("Distance climbed per animation cycle, measured at visual scale 1.")]
    public float distancePerCycle = 0.8f;
    public float reachMargin = 0.95f;
    public bool HasWall { get; private set; }
    private Animator animator;
    private Transform wall;
    private ClimbingSurface surface;
    private Vector3 normal, previousLocal;
    private bool tracking;
    private float weight, rate;
    private float scale = 1f;
    private static readonly int Climbing = Animator.StringToHash("IsClimbing");
    private static readonly int Rate = Animator.StringToHash("ClimbRate");
    private class Contact { public bool planted, hasAnchor; public Vector3 localPoint; }
    private readonly Contact[] contacts = {new Contact(),new Contact(),new Contact(),new Contact()};
    private readonly AvatarIKGoal[] goals = {AvatarIKGoal.LeftHand,AvatarIKGoal.RightHand,AvatarIKGoal.LeftFoot,AvatarIKGoal.RightFoot};
    private readonly HumanBodyBones[] tips = {HumanBodyBones.LeftHand,HumanBodyBones.RightHand,HumanBodyBones.LeftFoot,HumanBodyBones.RightFoot};
    private readonly HumanBodyBones[] mids = {HumanBodyBones.LeftLowerArm,HumanBodyBones.RightLowerArm,HumanBodyBones.LeftLowerLeg,HumanBodyBones.RightLowerLeg};
    private readonly HumanBodyBones[] roots = {HumanBodyBones.LeftUpperArm,HumanBodyBones.RightUpperArm,HumanBodyBones.LeftUpperLeg,HumanBodyBones.RightUpperLeg};
    private void Awake()
    {
        animator=GetComponent<Animator>();
        if(!state)state=GetComponentInParent<PlayerStateManager>();
        if(!playerRoot && state)playerRoot=state.transform;
    }
    private void LateUpdate()
    {
        if(!animator||!state||!playerRoot)return;
        bool climbing=state.currentState==PlayerStateManager.State.Climbing;
        scale=Mathf.Max(.1f,Mathf.Abs(transform.lossyScale.y));
        HasWall=false;
        if(climbing && Physics.Raycast(playerRoot.position,playerRoot.forward,out var hit,1.8f*scale,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore)
            && hit.collider.CompareTag("Wall"))
        {
            HasWall=true;
            if(wall!=hit.transform){wall=hit.transform;surface=wall.GetComponentInParent<ClimbingSurface>();tracking=false;ClearContacts();}
            normal=hit.normal;
            Vector3 current=wall.InverseTransformPoint(playerRoot.position);
            if(tracking && Time.deltaTime>0){
                Vector3 motion=wall.TransformVector(current-previousLocal);
                if(motion.magnitude>1f*scale){rate=0;ClearContacts();}
                else {
                    float speed=motion.magnitude/Time.deltaTime;
                    rate=speed<.02f ? 0f : Mathf.Clamp(speed/(distancePerCycle*scale),.15f,1.5f);
                    if(Vector3.Dot(motion,Vector3.up)<-.001f)rate=-rate;
                }
            } else rate=0;
            previousLocal=current;tracking=true;
        }
        else {tracking=false;rate=0;ClearContacts();}
        animator.SetBool(Climbing,climbing);
        animator.SetFloat(Rate,rate,.1f,Time.deltaTime);
        weight=Mathf.MoveTowards(weight,climbing&&HasWall?contactWeight:0f,Time.deltaTime*5f);
    }
    private void ClearContacts(){foreach(var contact in contacts){contact.planted=false;contact.hasAnchor=false;}}
    private void OnAnimatorIK(int layer)
    {
        if(!animator||!playerRoot)return;
        float phase=Mathf.Repeat(animator.GetCurrentAnimatorStateInfo(0).normalizedTime,1f);
        for(int i=0;i<4;i++){
            animator.SetIKPositionWeight(goals[i],0);animator.SetIKRotationWeight(goals[i],0);
            if(weight<=0||!wall||!HasWall)continue;
            var tip=animator.GetBoneTransform(tips[i]);var mid=animator.GetBoneTransform(mids[i]);var root=animator.GetBoneTransform(roots[i]);
            if(!tip||!mid||!root)continue;
            // Probe from the animated pose; contact points remain in wall-local coordinates.
            Vector3 origin=tip.position+normal*.5f*scale;
            if(!Physics.Raycast(origin,-normal,out var hit,1.6f*scale,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore)||hit.transform!=wall){contacts[i].planted=false;continue;}
            Vector3 candidate=surface?surface.NearestHold(hit.point,.16f*scale):hit.point;
            candidate+=normal*(i<2?.025f:.055f)*scale;
            float limbPhase=Mathf.Repeat(phase+(i==0||i==3?0f:.5f),1f);
            bool swing=Mathf.Abs(rate)>.05f&&limbPhase<.45f;
            var contact=contacts[i];
            if(swing)contact.planted=false;
            else if(!contact.planted){contact.localPoint=wall.InverseTransformPoint(candidate);contact.planted=true;contact.hasAnchor=true;}
            Vector3 target=contact.planted?wall.TransformPoint(contact.localPoint):Vector3.Lerp(contact.hasAnchor?wall.TransformPoint(contact.localPoint):candidate,candidate,Mathf.SmoothStep(0,1,limbPhase/.45f))+normal*Mathf.Sin(limbPhase/.45f*Mathf.PI)*.10f*scale;
            float length=Vector3.Distance(root.position,mid.position)+Vector3.Distance(mid.position,tip.position);
            float reach=length*Mathf.Clamp(reachMargin,.5f,1f);
            float distance=Vector3.Distance(root.position,target);
            if(distance>reach){contact.planted=false;target=root.position+Vector3.ClampMagnitude(target-root.position,reach);}
            float influence=weight*(swing?.65f:1f);
            animator.SetIKPositionWeight(goals[i],influence);animator.SetIKPosition(goals[i],target);
            if(i<2&&wristRotationWeight>0){animator.SetIKRotationWeight(goals[i],influence*wristRotationWeight);animator.SetIKRotation(goals[i],Quaternion.LookRotation(-normal,Vector3.up));}
        }
    }
    private void OnDisable(){tracking=false;weight=0;HasWall=false;ClearContacts();}
}


