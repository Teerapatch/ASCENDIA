using UnityEngine;

// สะดุ้งตามทิศที่โดน: เลื่อนและเอียง Pivot ตัวนี้แล้วสปริงกลับ ไม่ยุ่งกับ Animator จึงใช้ซ้อนระหว่างเล่นท่าอื่นได้
// วางบน GameObject ว่างที่คั่นระหว่างรากกับโมเดล (ตำแหน่ง/การหมุนเริ่มต้นต้องเป็นศูนย์)
public class HitReaction : MonoBehaviour
{
    private const float Stiffness = 260f;
    private const float DampingRatio = 0.85f;

    private Vector3 offset, offsetVelocity;
    private float lean, leanVelocity;
    private Vector3 leanAxis = Vector3.right;

    // worldDirection = ทิศที่แรงผลักไป distance = ระยะถอยสูงสุด (หน่วยโลก) leanDegrees = มุมเอียงสูงสุด
    public void Hit(Vector3 worldDirection, float distance, float leanDegrees)
    {
        if (worldDirection.sqrMagnitude < 0.0001f) return;
        Vector3 direction = worldDirection.normalized;
        float omega = Mathf.Sqrt(Stiffness);
        float kick = omega * 2.718f;

        // แรงกระแทกเป็นความเร็วเริ่มต้น สปริงจะพาไปถึงระยะที่ขอแล้วคืนกลับเอง
        Vector3 local = transform.parent != null ? transform.parent.InverseTransformVector(direction * distance) : direction * distance;
        offsetVelocity += local * kick;

        Vector3 flat = new Vector3(direction.x, 0f, direction.z);
        Vector3 axis;
        if (flat.sqrMagnitude > 0.05f) axis = Vector3.Cross(Vector3.up, flat.normalized);
        else axis = (Camera.main != null ? Camera.main.transform.right : Vector3.right) * Mathf.Sign(-direction.y);
        leanAxis = transform.parent != null ? transform.parent.InverseTransformDirection(axis) : axis;
        leanVelocity += leanDegrees * kick;
    }

    private void Update()
    {
        float dt = Mathf.Min(Time.deltaTime, 0.05f);
        if (dt <= 0f) return;

        float damping = 2f * Mathf.Sqrt(Stiffness) * DampingRatio;
        offsetVelocity += (-Stiffness * offset - damping * offsetVelocity) * dt;
        offset += offsetVelocity * dt;
        leanVelocity += (-Stiffness * lean - damping * leanVelocity) * dt;
        lean += leanVelocity * dt;

        transform.localPosition = offset;
        transform.localRotation = Quaternion.AngleAxis(lean, leanAxis);
    }
}
