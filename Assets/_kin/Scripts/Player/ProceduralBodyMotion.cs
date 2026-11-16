using UnityEngine;

// เพิ่มความมีชีวิตให้ท่าจากคลิป: หายใจ โยกตัวเบา ๆ เอนตัวตามความเร็ว เอียงเข้าโค้ง และหันหัวนำตอนเลี้ยว
// หมุนกระดูกเพิ่มหลัง Animator เล่นคลิปแล้ว (LateUpdate) รอบแกนของ axes จึงใช้ได้ทั้ง Humanoid และ Generic
[DefaultExecutionOrder(200)]
public class ProceduralBodyMotion : MonoBehaviour
{
    [Tooltip("แกนอ้างอิงของตัว (ขวา/หน้า/บน) และตัวที่ใช้วัดความเร็ว")]
    public Transform axes;
    [Tooltip("กระดูกลำตัวช่วงบน: ทุกอย่างเหนือเอวต้องเป็นลูกของกระดูกนี้")]
    public Transform spine;
    public Transform head;
    [Range(0f, 1f)] public float targetWeight = 1f;

    [Header("หายใจและโยกตัว")]
    public float breathAngle = 1.2f;
    public float breathsPerSecond = .28f;
    public float swayAngle = .8f;
    [Tooltip("โยกตัวแบบสุ่มช้า ๆ (องศา) ทำให้ท่าไม่วนซ้ำเป๊ะ และแต่ละตัวขยับไม่เหมือนกัน")]
    public float noiseAngle = .6f;
    [Tooltip("หันหัวมองไปมาแบบสุ่มช้า ๆ (องศา)")]
    public float headNoiseAngle = 0f;
    public float noiseSpeed = .35f;

    [Header("เอนตัวตามการเคลื่อนที่")]
    [Tooltip("เอนไปข้างหน้ากี่องศาต่อความเร็ว 1 หน่วยตัว/วินาที")]
    public float leanPerSpeed = 2f;
    public float maxLean = 7f;
    [Tooltip("เอียงเข้าโค้งกี่องศาต่อความเร็วเลี้ยว 1 องศา/วินาที")]
    public float turnLean = .03f;
    public float maxTurnLean = 8f;
    [Tooltip("หันหัวนำไปทางที่เลี้ยว")]
    public float headTurn = .06f;
    public float maxHeadTurn = 18f;
    [Tooltip("สปริงของการเอน: แข็งน้อย = ตามช้าและเด้งเลยนิด ๆ ตอนออกตัว/หยุด")]
    public float stiffness = 60f;
    public float damping = 11f;

    private float weight;
    private Vector3 previousPosition;
    private float previousYaw;
    private Vector2 lean, leanVelocity;   // x = เอนหน้า, y = เอียงข้าง
    private float headYaw;
    private float phase;
    private float noiseSeed;
    private Quaternion spineOutput, spineBase, headOutput, headBase;
    private bool hasOutput;

    private void OnEnable()
    {
        if (axes == null) axes = transform;
        previousPosition = axes.position;
        previousYaw = axes.eulerAngles.y;
        phase = Random.value * 10f;
        noiseSeed = Random.value * 100f;
        hasOutput = false;
    }

    private void LateUpdate()
    {
        if (axes == null || spine == null) return;
        float dt = Time.deltaTime;

        // ถ้า Animator ไม่ได้เขียนท่าใหม่เฟรมนี้ (เวลาหยุด/ถูก Cull) ต้องเริ่มจากท่าเดิม ไม่อย่างนั้นมุมจะสะสม
        Quaternion spineAnimated = hasOutput && spine.localRotation == spineOutput ? spineBase : spine.localRotation;
        Quaternion headAnimated = head == null ? Quaternion.identity : hasOutput && head.localRotation == headOutput ? headBase : head.localRotation;
        spine.localRotation = spineAnimated;
        if (head != null) head.localRotation = headAnimated;

        if (dt > 0f)
        {
            float scale = Mathf.Max(.01f, Mathf.Abs(axes.lossyScale.y));
            Vector3 velocity = (axes.position - previousPosition) / (dt * scale);
            float yawRate = Mathf.DeltaAngle(previousYaw, axes.eulerAngles.y) / dt;
            previousPosition = axes.position;
            previousYaw = axes.eulerAngles.y;
            if (velocity.sqrMagnitude > 15f * 15f) velocity = Vector3.zero; // ย้ายตำแหน่งทันที ไม่ใช่การเดิน
            if (Mathf.Abs(yawRate) > 720f) yawRate = 0f;

            Vector2 target = new Vector2(
                Mathf.Clamp(Vector3.Dot(velocity, axes.forward) * leanPerSpeed, -maxLean, maxLean),
                Mathf.Clamp(-yawRate * turnLean, -maxTurnLean, maxTurnLean));
            float step = Mathf.Min(dt, .05f);
            leanVelocity += ((target - lean) * stiffness - leanVelocity * damping) * step;
            lean += leanVelocity * step;

            headYaw = Mathf.Lerp(headYaw, Mathf.Clamp(yawRate * headTurn, -maxHeadTurn, maxHeadTurn), 1f - Mathf.Exp(-6f * dt));
            weight = Mathf.MoveTowards(weight, targetWeight, dt * 4f);
            phase += dt;
        }

        float breath = Mathf.Sin(phase * Mathf.PI * 2f * breathsPerSecond) * breathAngle;
        float sway = Mathf.Sin(phase * Mathf.PI * breathsPerSecond + 1.3f) * swayAngle;

        // Noise ต่อเนื่อง (Perlin) คนละเมล็ดต่อตัว: เอน/เอียง/บิดช้า ๆ ไม่เป็นจังหวะตายตัว
        float n = phase * noiseSpeed;
        float noisePitch = (Mathf.PerlinNoise(noiseSeed, n) - .5f) * 2f * noiseAngle;
        float noiseRoll = (Mathf.PerlinNoise(noiseSeed + 31.7f, n) - .5f) * 2f * noiseAngle;
        float noiseTwist = (Mathf.PerlinNoise(noiseSeed + 67.3f, n * .8f) - .5f) * 2f * noiseAngle;
        float noiseHead = (Mathf.PerlinNoise(noiseSeed + 113.1f, n * .6f) - .5f) * 2f * headNoiseAngle;

        spine.rotation = Quaternion.AngleAxis((lean.x + breath + noisePitch) * weight, axes.right)
            * Quaternion.AngleAxis((lean.y + sway + noiseRoll) * weight, axes.forward)
            * Quaternion.AngleAxis(noiseTwist * weight, axes.up)
            * spine.rotation;
        if (head != null)
        {
            // หัวหันนำ และเงยสวนการเอนของลำตัว สายตาจึงยังมองไปข้างหน้า
            head.rotation = Quaternion.AngleAxis((headYaw + noiseHead) * weight, axes.up)
                * Quaternion.AngleAxis(-(lean.x * .6f + breath * .5f) * weight, axes.right)
                * head.rotation;
            headOutput = head.localRotation;
            headBase = headAnimated;
        }
        spineOutput = spine.localRotation;
        spineBase = spineAnimated;
        hasOutput = true;
    }
}
