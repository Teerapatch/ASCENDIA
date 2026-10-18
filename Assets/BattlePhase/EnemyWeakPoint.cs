using UnityEngine;

public class EnemyWeakPoint : MonoBehaviour
{
    [Header("References")]
    public EnemyController mainEnemy; // ลากตัวศัตรูหลักมาใส่

    [Header("Weak Point Settings")]
    public bool isBroken = false;
    public float postureDamageMultiplier = 3.0f; // ยิงโดนจุดอ่อน เกจเหลืองเด้ง 3 เท่า!

    // ฟังก์ชันนี้จะถูกเรียกเมื่อยิงโดน
    public void BreakWeakPoint()
    {
        if (isBroken) return;

        isBroken = true;
        Debug.Log($"<color=orange>💥 WEAK POINT BROKEN on {mainEnemy.enemyName}!</color>");

        // (Optional) ถ้ามี Effect จุดอ่อนแตก ให้สั่ง Instantiate ตรงนี้
        // Instantiate(breakVFX, transform.position, Quaternion.identity);

        // ปิด Collider ทิ้งไปเลย เพื่อไม่ให้ยิงซ้ำได้อีก กระสุนนัดต่อไปจะทะลุไปโดนตัวหลักแทน
        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            col.enabled = false;
        }
    }
}