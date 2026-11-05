using UnityEngine;
using UnityEngine.Animations.Rigging;

[DefaultExecutionOrder(100), RequireComponent(typeof(Animator))]
public class ClimbingHandRig : MonoBehaviour
{
    [Range(0,1)] public float handWeight = .65f;
    [Min(.01f)] public float probeRadius = .06f;
    public bool Ready { get; private set; }
    Animator animator; ClimbingContactIK source; RigBuilder builder; Rig rig;
    class Hand
    {
        public Transform shoulder, elbow, wrist, target, hint, wall;
        public TwoBoneIKConstraint constraint;
        public Vector3 anchor; public bool planted; public float length;
    }
    readonly Hand[] hands = {new Hand(),new Hand()};
    void Start() { Build(); }
    public void Build()
    {
        if (Ready) return;
        animator=GetComponent<Animator>();source=GetComponent<ClimbingContactIK>();
        if(!animator.isHuman || !source)return;
        builder=GetComponent<RigBuilder>();if(!builder)builder=gameObject.AddComponent<RigBuilder>();
        var holder=new GameObject("ClimbingHandRig");holder.transform.SetParent(transform,false);rig=holder.AddComponent<Rig>();
        for(int i=0;i<2;i++)
        {
            var h=hands[i];bool left=i==0;
            h.shoulder=animator.GetBoneTransform(left?HumanBodyBones.LeftUpperArm:HumanBodyBones.RightUpperArm);
            h.elbow=animator.GetBoneTransform(left?HumanBodyBones.LeftLowerArm:HumanBodyBones.RightLowerArm);
            h.wrist=animator.GetBoneTransform(left?HumanBodyBones.LeftHand:HumanBodyBones.RightHand);
            var node=new GameObject(left?"LeftHandIK":"RightHandIK");node.transform.SetParent(holder.transform,false);
            h.target=new GameObject("Target").transform;h.target.SetParent(node.transform,false);h.target.SetPositionAndRotation(h.wrist.position,h.wrist.rotation);
            h.hint=new GameObject("ElbowHint").transform;h.hint.SetParent(node.transform,false);h.hint.position=h.elbow.position;
            h.constraint=node.AddComponent<TwoBoneIKConstraint>();var data=h.constraint.data;
            data.root=h.shoulder;data.mid=h.elbow;data.tip=h.wrist;data.target=h.target;data.hint=h.hint;
            data.targetPositionWeight=1;data.targetRotationWeight=0;data.hintWeight=.35f;
            h.constraint.data=data;h.constraint.weight=0;
        }
        builder.layers.Add(new RigLayer(rig));Ready=builder.Build();
    }
    void Update()
    {
        if(!Ready||!source||!source.playerRoot)return;
        bool climb=source.state && source.state.currentState==PlayerStateManager.State.Climbing;
        bool penalty=source.state && source.state.currentState==PlayerStateManager.State.Penalty;
        var info=animator.GetCurrentAnimatorStateInfo(0);
        bool active=(climb||penalty)&&info.IsTag("Climbing")&&source.HasWall;
        float phase=Mathf.Repeat(info.normalizedTime,1);
        float rate=animator.GetFloat("ClimbRate");
        float scale=Mathf.Max(.1f,Mathf.Abs(transform.lossyScale.y));
        Vector3 forward=source.playerRoot.forward;
        for(int i=0;i<2;i++)
        {
            var h=hands[i];float wanted=0;
            if(active && penalty && h.planted && h.wall) { h.target.position=h.wall.TransformPoint(h.anchor); continue; }
            if(active)
            {
                h.length=Vector3.Distance(h.shoulder.position,h.elbow.position)+Vector3.Distance(h.elbow.position,h.wrist.position);
                float p=Mathf.Repeat(phase+i*.5f,1);
                float envelope=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.32f,.52f,p))*(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.82f,1,p)));
                if(penalty || Mathf.Abs(rate)<.01f)envelope=h.planted?1:0;
                if(envelope<.02f)h.planted=false;
                if(!h.planted && envelope>.05f)
                {
                    Vector3 origin=h.wrist.position-forward*h.length*.5f;
                    if(Physics.SphereCast(origin,probeRadius*scale,forward,out var hit,h.length*1.8f,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore)
                        && hit.collider.CompareTag("Wall"))
                    {
                        var surface=hit.collider.GetComponentInParent<ClimbingSurface>();
                        Vector3 point=hit.point+hit.normal*.065f*scale;
                        if(surface)point=surface.NearestHold(hit.point,.12f*scale)+hit.normal*.065f*scale;
                        if(Vector3.Distance(h.shoulder.position,point)<h.length*.96f)
                        {
                            h.wall=surface?surface.transform:hit.transform;h.anchor=h.wall.InverseTransformPoint(point);h.planted=true;
                        }
                    }
                }
                if(h.planted && h.wall)
                {
                    Vector3 goal=h.wall.TransformPoint(h.anchor);
                    float reach=Vector3.Distance(h.shoulder.position,goal)/Mathf.Max(.01f,h.length);
                    wanted=handWeight*envelope*(1-Mathf.InverseLerp(.9f,1f,reach));
                    h.target.position=Vector3.Lerp(h.target.position,goal,1-Mathf.Exp(-Time.deltaTime*18));
                    h.hint.position=h.shoulder.position+source.playerRoot.right*(i==0?-.35f:.35f)*h.length-forward*h.length*.25f;
                    if(reach>1.03f)h.planted=false;
                }
            }
            else h.planted=false;
            h.constraint.weight=Mathf.MoveTowards(h.constraint.weight,wanted,Time.deltaTime*4);
            if(!h.planted && h.constraint.weight<.001f)h.target.SetPositionAndRotation(h.wrist.position,h.wrist.rotation);
        }
    }
    void OnDisable(){foreach(var h in hands)if(h.constraint){h.constraint.weight=0;h.planted=false;}}
}
