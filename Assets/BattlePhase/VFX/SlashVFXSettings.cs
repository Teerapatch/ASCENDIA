using System;
using System.Collections.Generic;
using UnityEngine;

// SlashVFX ที่จัดเองทีละท่า (Tools/ASCENDIA/Slash VFX Editor) เก็บใน Resources/SlashVFXSettings.asset
// ตำแหน่ง/มุม/ขนาดอยู่ในพื้นที่ของโมเดลผู้ตี (รากของโมเดลอาวุธผู้เล่น / Model ของศัตรู) จึงตามตัวไปทุกที่ที่ท่านั้นเล่น
// ท่าที่ไม่มีรายการ ใช้รอยฟันอัตโนมัติจากทิศอาวุธ (CombatVFX.Slash) เฉพาะท่าเข้าไปตี
[Serializable]
public class SlashVFXEntry
{
    [Tooltip("Player/<อาวุธ>/<State> หรือ Enemy/<ชุดอนิเมชัน>/<State>")]
    public string key;
    [Tooltip("จังหวะโดนที่รายการนี้แทน (ฮิตนั้นจะไม่มีรอยฟันอัตโนมัติ) -1 = รอยเสริม")]
    public int hit = -1;
    [Tooltip("ปิด = ไม่มีรอยฟันในจังหวะนี้")]
    public bool enabled = true;
    [Range(0f, 1f), Tooltip("จังหวะในคลิปที่รอยฟันเกิด (สัดส่วนของคลิป)")]
    public float time;
    [Tooltip("ตำแหน่งรากของ VFX_Slash ในพื้นที่ของโมเดล")]
    public Vector3 position;
    [Tooltip("มุมของรากของ VFX_Slash เทียบกับโมเดล")]
    public Vector3 euler;
    [Tooltip("ขนาดเทียบกับ Prefab (คูณสเกลของโมเดลอีกที)")]
    public float scale = 1f;
}

public class SlashVFXSettings : ScriptableObject
{
    public const string ResourcePath = "SlashVFXSettings";
    public const string AssetPath = "Assets/Resources/SlashVFXSettings.asset";

    public List<SlashVFXEntry> entries = new List<SlashVFXEntry>();

    private static SlashVFXSettings cached;
    public static SlashVFXSettings Instance
    {
        get
        {
            if (cached == null) cached = Resources.Load<SlashVFXSettings>(ResourcePath);
            return cached;
        }
    }

    public static string PlayerKey(WeaponType weapon, string state) => "Player/" + weapon + "/" + state;
    public static string EnemyKey(EnemyAnimationSet set, string state) => "Enemy/" + (set != null ? set.name : "") + "/" + state;

    public List<SlashVFXEntry> For(string key)
    {
        List<SlashVFXEntry> found = new List<SlashVFXEntry>();
        foreach (SlashVFXEntry entry in entries) if (entry.key == key) found.Add(entry);
        found.Sort((a, b) => a.time.CompareTo(b.time));
        return found;
    }

    // จังหวะนี้จัดเองแล้ว (เปิดหรือปิดก็ตาม) ไม่ต้องมีรอยฟันอัตโนมัติ
    public bool Covers(string key, int hit)
    {
        foreach (SlashVFXEntry entry in entries) if (entry.key == key && entry.hit == hit) return true;
        return false;
    }
}
