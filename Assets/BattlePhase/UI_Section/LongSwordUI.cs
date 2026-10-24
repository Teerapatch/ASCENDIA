// LongSword ยังไม่มีข้อมูล Passive ให้แสดง ใช้แค่ Fade เข้า/ออกจากคลาสแม่
public class LongSwordUI : WeaponPassiveUI
{
    protected override WeaponType TargetWeapon => WeaponType.LongSword;
}
