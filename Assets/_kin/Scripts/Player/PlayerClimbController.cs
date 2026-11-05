using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(PlayerStateManager))]
public class PlayerClimbController : MonoBehaviour
{
    [Header("Settings")]
    public float verticalSpeed = 5f;
    public float horizontalSpeed = 3f;
    public float minX = -4f;
    public float maxX = 4f;
    public float maxPlayerHeight = 5f;
    [Range(.1f, 1f), Tooltip("Upward pace only. Downward and sideways speeds are unchanged.")]
    public float upwardSpeedMultiplier = .3f;
    [Min(.1f)] public float climbAcceleration = 10f;
    private Vector3 climbVelocity;

    public static Vector3 RequestedVelocity(float h, float v, float horizontal, float vertical, float upwardMultiplier)
    {
        return new Vector3(h * horizontal, v * vertical * (v > 0 ? upwardMultiplier : 1f), 0);
    }

    [Tooltip("ถ้าน้ำหนักเต็มกระเป๋า จะให้ปีนช้าลงเหลือกี่เปอร์เซ็นต์? (0.4 = 40% ของความเร็วปกติ)")]
    public float minSpeedMultiplier = 0.4f;

    [Header("References")]
    public Transform environmentContainer; 

    private PlayerStateManager stateManager;
    private ClimbingContactIK contactIK;
    
    // 🌟 ตัวแปรเก็บค่าตัวคูณความเร็วแบบเรียลไทม์
    private float currentSpeedMultiplier = 1f;

    private void Awake()
    {
        stateManager = GetComponent<PlayerStateManager>();
        contactIK = GetComponentInChildren<ClimbingContactIK>();
    }

    private void Update()
    {
        if (contactIK != null && stateManager.currentState != PlayerStateManager.State.AutoClimbing) contactIK.SetClimbVelocity(Vector3.zero);
        if (GameManager.Instance == null) { climbVelocity = Vector3.zero; return; }
        
        // 🌟 1. อัปเดตตัวคูณความเร็วตลอดเวลา (Real-time Update) 
        // ไม่ว่าผู้เล่นจะทำอะไรอยู่ ถ้าน้ำหนักเปลี่ยน ความเร็วจะคำนวณรอไว้เลย
        UpdateSpeedMultiplier();

        if (stateManager.currentState != PlayerStateManager.State.Climbing) { climbVelocity = Vector3.zero; return; }

        if (contactIK != null && !contactIK.HasWall) { climbVelocity = Vector3.zero; return; }
        HandleClimbing();
    }

    // ฟังก์ชันสำหรับคำนวณความหนืดจากน้ำหนัก
    private void UpdateSpeedMultiplier()
    {
        if (GameManager.Instance.playerData != null)
        {
            PlayerData data = GameManager.Instance.playerData;
            
            float weightPercent = data.weight / data.maxWeight;
            weightPercent = Mathf.Clamp01(weightPercent); 

            // คำนวณความเร็วแบบเรียลไทม์
            currentSpeedMultiplier = Mathf.Lerp(1f, minSpeedMultiplier, weightPercent);
        }
        else
        {
            currentSpeedMultiplier = 1f; // เซฟตี้: ถ้าไม่มีข้อมูล ให้เร็วปกติ
        }
    }

    public bool useWallWidth = true;
    public Vector2 LastClimbInput { get; private set; }
    public Vector2 ReadClimbInput()
    {
        var walk = GetComponent<PlayerWalkController>();
        Vector2 input = walk && walk.moveAction ? walk.moveAction.action.ReadValue<Vector2>() : Vector2.zero;
        var keyboard = Keyboard.current;
        if(keyboard != null)
        {
            int x=(keyboard.dKey.isPressed||keyboard.rightArrowKey.isPressed?1:0)-(keyboard.aKey.isPressed||keyboard.leftArrowKey.isPressed?1:0);
            int y=(keyboard.wKey.isPressed||keyboard.upArrowKey.isPressed?1:0)-(keyboard.sKey.isPressed||keyboard.downArrowKey.isPressed?1:0);
            if(x!=0 || y!=0)input=new Vector2(x,y);
        }
        LastClimbInput=input;
        return input;
    }
    private void HandleClimbing()
    {
        Vector2 input=ReadClimbInput(); float h=input.x, v=input.y;

                AdvanceClimb(h, v, Time.deltaTime);
    }

    public void AdvanceClimb(float h, float v, float deltaTime)
    {
        float left=minX, right=maxX;
        if(useWallWidth && contactIK && contactIK.WallCollider)
        {
            Bounds wallBounds=contactIK.WallCollider.bounds;
            if(wallBounds.size.x>1f){left=wallBounds.min.x+.4f;right=wallBounds.max.x-.4f;}
        }
        if ((h < 0 && transform.position.x <= left) || (h > 0 && transform.position.x >= right)) h = 0;
        if(v < -.01f && TryLand(verticalSpeed * currentSpeedMultiplier * deltaTime))return;
        if (!environmentContainer && (v < 0 || transform.position.y >= maxPlayerHeight)) v = 0;
        Vector3 desired = RequestedVelocity(h, v, horizontalSpeed * currentSpeedMultiplier,
            verticalSpeed * currentSpeedMultiplier, upwardSpeedMultiplier);
        climbVelocity = Vector3.MoveTowards(climbVelocity, desired, climbAcceleration * deltaTime);
        // Release input stops that axis immediately; acceleration only softens starting/reversing.
        if (h == 0) climbVelocity.x = 0;
        if (v == 0) climbVelocity.y = 0;
        if (contactIK)
        {
            contactIK.SetClimbVelocity(climbVelocity);
            if (contactIK.MovementMultiplier <= 0) return;
        }
        Vector3 delta = climbVelocity * deltaTime;
        Vector3 position = transform.position;
        position.x = Mathf.Clamp(position.x + delta.x, left, right);
        if (delta.y > 0)
        {
            float rise = Mathf.Min(delta.y, Mathf.Max(0, maxPlayerHeight - position.y));
            position.y += rise;
            if (environmentContainer) environmentContainer.Translate(Vector3.down * (delta.y - rise), Space.World);
        }
        else if (delta.y < 0 && environmentContainer)
            environmentContainer.Translate(Vector3.up * -delta.y, Space.World);
        transform.position = position;
    }
    public bool TryLand(float step)
    {
        var cc=GetComponent<CharacterController>();
        float half=cc?cc.height*Mathf.Abs(transform.lossyScale.y)*.5f:1f;
        float center=cc?transform.TransformVector(cc.center).y:0;
        if(!ClimbingContactIK.TryGroundHeight(transform,transform.position,half+Mathf.Max(.2f,step)+.3f,out float ground))return false;
        if(transform.position.y+center-half-ground > Mathf.Max(.12f,step))return false;
        Vector3 landing=transform.position-transform.forward*.25f;
        if(!ClimbingContactIK.TryGroundHeight(transform,landing,half+.6f,out float landingGround))return false;
        landing.y=landingGround+half-center+.025f;
        bool enabled=cc && cc.enabled;if(enabled)cc.enabled=false;
        transform.position=landing;transform.forward=-transform.forward;
        if(enabled)cc.enabled=true;
        climbVelocity=Vector3.zero;
        if(contactIK)contactIK.ReleaseWall();
        if(!stateManager)stateManager=GetComponent<PlayerStateManager>();
        stateManager.ChangeState(PlayerStateManager.State.Walking);
        var walk=GetComponent<PlayerWalkController>();if(walk)walk.DelayWallAttachment(1f);
        var cameraController=GetComponent<PlayerCameraController>();if(cameraController)cameraController.SwitchToWalkCam();
        return true;
    }

}
