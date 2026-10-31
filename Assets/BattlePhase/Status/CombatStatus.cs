using System.Collections.Generic;

// บัฟ/ดีบัฟในการต่อสู้
// ค่าเลขของ enum ถูกเซฟลงไฟล์สกิล (StatusSkill.status) ห้ามสลับลำดับหรือแทรกกลาง (เพิ่มต่อท้ายได้)
public enum CombatStatusType
{
    // --- ผู้เล่น (บัฟ) ---
    Evasion,        // หลบการโจมตีครั้งถัดไป นับเป็น Parry สำเร็จ (value = จำนวนครั้ง)
    Berserk,        // ดาเมจที่ทำ +value (0.5 = +50%)
    AttackUp,       // ดาเมจที่ทำ +value (จาก Aura Fusion)

    // --- ศัตรู (ดีบัฟ) ---
    ExposeWeakness, // รับดาเมจจาก Gun / LongSword +value
    Shatter,        // รับดาเมจ Posture +value จนกว่าจะ Stagger
    Slow,           // ความเร็วลด value (0.5 = ช้าลง 50%)
    Burn            // เสียเลือด value ทุกครั้งที่เริ่มเทิร์นของตัวเอง
}

public class CombatStatus
{
    public CombatStatusType type;
    public float value;

    // จำนวนเทิร์นของ "ตัวที่ติดสถานะ" ที่เหลือ (-1 = อยู่จนกว่าจะถูกใช้/ถูกลบ)
    public int turnsLeft;

    // ข้ามการ Tick ครั้งถัดไป: ใช้กับบัฟที่ใส่ให้ตัวเองระหว่างเทิร์นของตัวเอง
    // (ไม่งั้นจบเทิร์นปุ๊บจะโดนลดไป 1 ทันที ทำให้ "2 เทิร์น" เหลือใช้ได้จริงแค่ 1)
    public bool skipNextTick;

    public string ShortLabel
    {
        get
        {
            string code;
            switch (type)
            {
                case CombatStatusType.Evasion: code = "EVA"; break;
                case CombatStatusType.Berserk: code = "BSK"; break;
                case CombatStatusType.AttackUp: code = "ATK"; break;
                case CombatStatusType.ExposeWeakness: code = "EXP"; break;
                case CombatStatusType.Shatter: code = "SHT"; break;
                case CombatStatusType.Slow: code = "SLW"; break;
                case CombatStatusType.Burn: code = "BRN"; break;
                default: code = type.ToString(); break;
            }
            return turnsLeft > 0 ? $"{code}{turnsLeft}" : code;
        }
    }
}

// ชุดสถานะของตัวละคร 1 ตัว (ผู้เล่นหรือศัตรู)
public class CombatStatusSet
{
    private readonly List<CombatStatus> statuses = new List<CombatStatus>();

    public event System.Action OnChanged;

    public IReadOnlyList<CombatStatus> All => statuses;

    // ใส่สถานะ ถ้ามีอยู่แล้วให้รีเฟรช (ใช้ค่าที่มากกว่า)
    public void Apply(CombatStatusType type, float value, int turns, bool appliedDuringOwnTurn = false)
    {
        CombatStatus existing = Get(type);
        if (existing != null)
        {
            existing.value = UnityEngine.Mathf.Max(existing.value, value);
            existing.turnsLeft = (existing.turnsLeft < 0 || turns < 0) ? -1 : UnityEngine.Mathf.Max(existing.turnsLeft, turns);
            existing.skipNextTick = appliedDuringOwnTurn;
        }
        else
        {
            statuses.Add(new CombatStatus { type = type, value = value, turnsLeft = turns, skipNextTick = appliedDuringOwnTurn });
        }
        OnChanged?.Invoke();
    }

    public CombatStatus Get(CombatStatusType type)
    {
        return statuses.Find(s => s.type == type);
    }

    public bool Has(CombatStatusType type) => Get(type) != null;

    public float GetValue(CombatStatusType type)
    {
        CombatStatus status = Get(type);
        return status != null ? status.value : 0f;
    }

    public bool Remove(CombatStatusType type)
    {
        bool removed = statuses.RemoveAll(s => s.type == type) > 0;
        if (removed) OnChanged?.Invoke();
        return removed;
    }

    // ใช้สถานะแบบนับครั้ง (เช่น Evasion) คืน true ถ้าใช้ได้
    public bool ConsumeCharge(CombatStatusType type)
    {
        CombatStatus status = Get(type);
        if (status == null) return false;

        status.value -= 1f;
        if (status.value <= 0f) statuses.Remove(status);
        OnChanged?.Invoke();
        return true;
    }

    // ลดจำนวนเทิร์นลง 1 (เรียกตอนตัวที่ติดสถานะจบเทิร์นของตัวเอง)
    public void TickTurn()
    {
        bool changed = false;
        for (int i = statuses.Count - 1; i >= 0; i--)
        {
            CombatStatus status = statuses[i];
            if (status.skipNextTick)
            {
                status.skipNextTick = false;
                continue;
            }
            if (status.turnsLeft < 0) continue;

            status.turnsLeft--;
            changed = true;
            if (status.turnsLeft <= 0) statuses.RemoveAt(i);
        }
        if (changed) OnChanged?.Invoke();
    }

    public void Clear()
    {
        if (statuses.Count == 0) return;
        statuses.Clear();
        OnChanged?.Invoke();
    }
}
