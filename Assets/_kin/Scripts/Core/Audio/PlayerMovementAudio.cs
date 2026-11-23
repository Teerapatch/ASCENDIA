using UnityEngine;

// เสียงปีนแบบวน (climb.loop) และเสียงก้าวเดินทีละก้าว (climb.step / climb.run บนหิน) ตามสถานะใน PlayerStateManager และความเร็วจริง
// SoundManager ใส่ให้ตัวผู้เล่นเองเมื่อฉากมี PlayerStateManager (ไม่ต้องวาง) เสียงปีนดัง/เบาตามความเร็ว ก้าวถี่ขึ้นเมื่อเร็วขึ้น หยุดเมื่ออยู่นิ่ง
public class PlayerMovementAudio : MonoBehaviour
{
    [Tooltip("ความเร็ว (หน่วย/วินาที) ที่นับว่ากำลังปีน")]
    public float climbThreshold = 0.4f;
    [Tooltip("ความเร็วแนวราบที่นับว่ากำลังเดิน")]
    public float walkThreshold = 0.6f;
    [Tooltip("ความเร็วที่เสียงดังเต็มที่")]
    public float fullSpeedClimb = 3f;
    public float fullSpeedWalk = 6f;
    [Tooltip("ระยะห่างระหว่างก้าว (วินาที) ตอนเดินช้า / เร็วสุด")]
    public Vector2 stepInterval = new Vector2(0.55f, 0.3f);
    [Tooltip("ความเร็วที่เปลี่ยนเป็นเสียงวิ่ง (สัดส่วนของ fullSpeedWalk)")]
    [Range(0f, 1f)] public float runFraction = 0.75f;
    private float stepTimer;

    private PlayerStateManager state;
    private Vector3 lastPosition;
    private int climbHandle, walkHandle;
    private float smoothSpeed;

    private void Awake()
    {
        state = GetComponent<PlayerStateManager>();
        lastPosition = transform.position;
    }

    private void OnDisable()
    {
        SoundManager.StopLoop(climbHandle, 0.1f);
        SoundManager.StopLoop(walkHandle, 0.1f);
        climbHandle = walkHandle = 0;
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        Vector3 delta = transform.position - lastPosition;
        lastPosition = transform.position;
        bool paused = dt <= 0f || GameExitMenu.IsOpen || PlayerStateManager.InputLocked || state == null;
        bool climbing = !paused && state.currentState == PlayerStateManager.State.Climbing;
        bool walking = !paused && state.currentState == PlayerStateManager.State.Walking;
        Vector3 flat = new Vector3(delta.x, 0f, delta.z);
        float speed = paused ? 0f : (climbing ? delta.magnitude : flat.magnitude) / Mathf.Max(dt, 0.0001f);
        smoothSpeed = Mathf.Lerp(smoothSpeed, speed, 1f - Mathf.Exp(-10f * Mathf.Max(dt, 0.001f)));

        climbHandle = Drive(climbHandle, Sfx.ClimbLoop, climbing && smoothSpeed > climbThreshold, smoothSpeed / fullSpeedClimb);

        // ก้าวเท้า: ก้าวแรกทันทีที่เริ่มเดิน แล้วถี่ขึ้นตามความเร็ว
        if (walking && smoothSpeed > walkThreshold)
        {
            float fraction = Mathf.Clamp01(smoothSpeed / fullSpeedWalk);
            stepTimer -= dt;
            if (stepTimer <= 0f)
            {
                SoundManager.Play(fraction >= runFraction ? Sfx.FootstepRun : Sfx.Footstep, null, Mathf.Lerp(0.7f, 1f, fraction));
                stepTimer = Mathf.Lerp(stepInterval.x, stepInterval.y, fraction);
            }
        }
        else stepTimer = 0f;
    }

    private int Drive(int handle, string id, bool on, float strength)
    {
        if (on)
        {
            if (!SoundManager.IsPlaying(handle)) handle = SoundManager.PlayLoop(id, null, 1f, 0.15f);
            SoundManager.SetLoopVolume(handle, Mathf.Lerp(0.55f, 1f, Mathf.Clamp01(strength)));
        }
        else if (handle != 0)
        {
            SoundManager.StopLoop(handle, 0.25f);
            handle = 0;
        }
        return handle;
    }
}
