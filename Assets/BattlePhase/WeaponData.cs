using UnityEngine;

[CreateAssetMenu(fileName = "New Weapon", menuName = "ASCENDIA/Weapon")]
public class WeaponData : ScriptableObject
{
    public string weaponName;
    public int maxDurability; // เปรียบเสมือน Max HP
    public int currentDurability; // ค่า HP ปัจจุบัน
    public float speed;
    public int weight; // อาจจะเอาไว้หักลบกับ Speed พื้นฐานของผู้เล่น
    // สามารถเพิ่ม List ของ Skills เข้าไปที่นี่ได้

    [Header("Combat Stats")]
    public int baseDamage = 10; // ดาเมจโจมตีปกติ
    public float freeAimMultiplier = 1.5f; // ตัวคูณตอน Free Aim
    public float parryMultiplier = 2.0f; // ตัวคูณตอน Parry สวนกลับ (ใช้ลด Posture ศัตรู)
}