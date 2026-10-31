using System.Collections.Generic;
using UnityEngine;

public enum SpellSkillMode
{
    ChaosCatalyst,          // ปล่อยธาตุทั้งหมดใส่เป้าเดี่ยว ดาเมจตามจำนวนธาตุ
    ElementalDischarge,     // ดาเมจมหาศาลตามจำนวนธาตุที่ซ้ำกัน
    ElementalTransmutation, // แปลงธาตุทั้งหมดให้เป็นธาตุเดียวกัน (ธาตุที่มีมากที่สุด)
    AuraFusion,             // ธาตุซ้ำ 3 อัน = บัฟ Attack / ไม่งั้นคืน AP
    SpellBurn,              // ใช้ธาตุทั้งหมดเป็น DOT ระยะเวลาตามจำนวนธาตุ
    SpellBreak              // ธาตุซ้ำกันทั้งหมด = Stagger ทุกตัวทันที / ไม่งั้น Shatter ทุกตัว (เสียธาตุตามจำนวนศัตรู)
}

// สกิลของ Spell ที่ทำงานกับ Elemental Sigil (PlayerCombatController.elementSlots)
// Unity ต้องการ 1 คลาสต่อ 1 ไฟล์สำหรับ ScriptableObject จึงรวมทั้ง 6 สกิลไว้ในคลาสเดียวแยกด้วย mode
[CreateAssetMenu(fileName = "New Spell Skill", menuName = "Ascendia/Skills/Spell Skill")]
public class SpellSkill : WeaponSkill
{
    public SpellSkillMode mode = SpellSkillMode.ChaosCatalyst;

    [Header("Damage (Chaos Catalyst / Elemental Discharge)")]
    [Tooltip("ดาเมจ = ดาเมจอาวุธ x ค่านี้ x จำนวนธาตุ (Discharge ใช้จำนวนธาตุที่ซ้ำมากที่สุด)")]
    public float damagePerElement = 0.8f;
    [Tooltip("ดาเมจเพิ่มต่อ 1 QTE ที่สำเร็จ (0.25 = +25%)")]
    public float bonusPerQTE = 0.25f;
    [Tooltip("ดาเมจ Posture = ดาเมจที่ทำ x ค่านี้ (Aura Fusion ใช้ x ดาเมจอาวุธ)")]
    public float postureRatio = 0f;

    [Header("Aura Fusion")]
    public int fusionRequiredDuplicates = 3;
    public float fusionAttackBuff = 0.5f;
    public int fusionBuffTurns = 3;
    public int fusionFallbackAP = 3;

    [Header("Spell Burn")]
    [Tooltip("ดาเมจต่อเทิร์น = ดาเมจอาวุธ x ค่านี้ (จำนวนเทิร์น = จำนวนธาตุที่ใช้)")]
    public float burnDamageRatio = 0.6f;

    [Header("Spell Break")]
    public float shatterValue = 0.5f;

    public override bool CanExecute(PlayerCombatController player, out string reason)
    {
        // Aura Fusion ใช้ได้เสมอ (ถ้าธาตุไม่ครบจะคืน AP แทน)
        if (mode != SpellSkillMode.AuraFusion && player.elementSlots.Count == 0)
        {
            reason = "No Elements";
            return false;
        }
        reason = null;
        return true;
    }

    public override void ExecuteSkill(PlayerCombatController player, EnemyController target, int qteSuccesses)
    {
        List<ElementType> slots = player.elementSlots;
        float weaponDamage = WeaponDamage(player);
        float qteMultiplier = 1f + bonusPerQTE * qteSuccesses;
        ElementType mostCommon = GetMostCommon(slots, out int duplicates);

        switch (mode)
        {
            case SpellSkillMode.ChaosCatalyst:
            {
                float damage = weaponDamage * damagePerElement * slots.Count * qteMultiplier;
                player.DealDamage(target, damage, damage * postureRatio);
                Log($"ปล่อยธาตุ {slots.Count} อัน ใส่ {target.enemyName}");
                slots.Clear();
                break;
            }
            case SpellSkillMode.ElementalDischarge:
            {
                float damage = weaponDamage * damagePerElement * duplicates * qteMultiplier;
                player.DealDamage(target, damage, damage * postureRatio);
                Log($"ธาตุ {mostCommon} ซ้ำ {duplicates} อัน -> ดาเมจ {Mathf.RoundToInt(damage)}");
                slots.Clear();
                break;
            }
            case SpellSkillMode.ElementalTransmutation:
            {
                for (int i = 0; i < slots.Count; i++) slots[i] = mostCommon;
                player.ShowPopup($"ALL {mostCommon.ToString().ToUpper()}", ElementTypes.GetColor(mostCommon));
                Log($"แปลงธาตุทั้งหมดเป็น {mostCommon}");
                break;
            }
            case SpellSkillMode.AuraFusion:
            {
                if (duplicates >= fusionRequiredDuplicates)
                {
                    RemoveElements(slots, mostCommon, fusionRequiredDuplicates);
                    player.ApplySelfStatus(CombatStatusType.AttackUp, fusionAttackBuff, fusionBuffTurns, "ATK UP");
                    Log($"ธาตุ {mostCommon} ซ้ำ {fusionRequiredDuplicates} -> ATK +{fusionAttackBuff * 100}% {fusionBuffTurns} เทิร์น");
                }
                else
                {
                    player.RestoreAP(fusionFallbackAP);
                    player.ShowPopup($"+{fusionFallbackAP} AP", Color.cyan);
                    Log($"ธาตุซ้ำไม่ถึง {fusionRequiredDuplicates} -> คืน AP {fusionFallbackAP}");
                }
                if (postureRatio > 0f) player.DealDamage(target, 0f, weaponDamage * postureRatio);
                break;
            }
            case SpellSkillMode.SpellBurn:
            {
                int turns = slots.Count;
                float burnPerTurn = weaponDamage * burnDamageRatio * qteMultiplier;
                target.Statuses.Apply(CombatStatusType.Burn, burnPerTurn, turns);
                PopupOn(target, $"BURN x{turns}", new Color(1f, 0.5f, 0.1f));
                Log($"{target.enemyName} ติด Burn {Mathf.RoundToInt(burnPerTurn)}/เทิร์น นาน {turns} เทิร์น");
                slots.Clear();
                break;
            }
            case SpellSkillMode.SpellBreak:
                ExecuteSpellBreak(slots, target, duplicates);
                break;
        }
    }

    private void ExecuteSpellBreak(List<ElementType> slots, EnemyController target, int duplicates)
    {
        List<EnemyController> enemies = new List<EnemyController>(CombatManager.Instance.enemies);

        // ธาตุซ้ำกันทั้งหมด (อย่างน้อย 2 อัน) -> Stagger ทุกตัวทันที
        if (slots.Count >= 2 && duplicates == slots.Count)
        {
            foreach (EnemyController enemy in enemies)
            {
                enemy.BreakPosture();
                PopupOn(enemy, "BREAK!", Color.yellow);
            }
            Log($"ธาตุซ้ำกันทั้งหมด {slots.Count} อัน -> Stagger ศัตรูทุกตัว");
            slots.Clear();
            return;
        }

        // ไม่งั้นใส่ Shatter ให้ศัตรู 1 ตัวต่อ 1 ธาตุ (เริ่มจากเป้าที่เลือก)
        enemies.Remove(target);
        enemies.Insert(0, target);

        int count = Mathf.Min(enemies.Count, slots.Count);
        for (int i = 0; i < count; i++)
        {
            enemies[i].Statuses.Apply(CombatStatusType.Shatter, shatterValue, -1);
            PopupOn(enemies[i], "SHATTER", new Color(0.8f, 0.5f, 1f));
        }
        slots.RemoveRange(0, count);
        Log($"ใส่ Shatter ให้ศัตรู {count} ตัว (ใช้ธาตุไป {count} อัน)");
    }

    // ธาตุที่มีมากที่สุดใน Slot (เสมอกันเอาตัวที่เจอก่อน)
    private static ElementType GetMostCommon(List<ElementType> slots, out int count)
    {
        ElementType best = ElementType.None;
        count = 0;
        foreach (ElementType element in slots)
        {
            int c = slots.FindAll(e => e == element).Count;
            if (c > count)
            {
                count = c;
                best = element;
            }
        }
        return best;
    }

    private static void RemoveElements(List<ElementType> slots, ElementType element, int amount)
    {
        for (int i = 0; i < amount; i++) slots.Remove(element);
    }

    private static void PopupOn(EnemyController enemy, string text, Color color)
    {
        if (DamagePopupManager.Instance != null)
            DamagePopupManager.Instance.CreateTextPopup(enemy.transform.position + Vector3.up, text, color);
    }

    private void Log(string message)
    {
        Debug.Log($"<color=magenta>🔮 [{skillName}] {message}</color>");
    }
}
