using System;
using System.Collections.Generic;
using UnityEngine;

// จุดปล่อยกระสุนที่จัดเอง (Tools/ASCENDIA/Slash VFX Editor > ผู้เล่น > จุดปล่อยกระสุน) เก็บใน Resources/ShotOriginSettings.asset
// จุด = ตำแหน่งกระดูก + การหมุนของกระดูก * (offset * สเกลของโมเดล) จึงตามอาวุธ/มือไปตามท่า และไม่ขึ้นกับสเกลแปลก ๆ ของกระดูกจาก FBX
// ไม่มีรายการ = PlayerBattleAnimator หาเองจากจุดยอดของ Mesh อาวุธที่ยื่นไปทางเป้ามากที่สุด
[Serializable]
public class ShotOriginEntry
{
    public WeaponType weapon;
    [Tooltip("ชื่อ State ของท่า เว้นว่าง = ทุกท่าของอาวุธนี้ที่ไม่มีค่าของตัวเอง")]
    public string state;
    [Tooltip("กระดูกที่จุดนี้ติดอยู่ (เช่นกระดูกปืน หรือ DEF-hand.R)")]
    public string bone = "DEF-hand.R";
    [Tooltip("ระยะจากกระดูก ในทิศของกระดูก หน่วยเท่าโมเดลสเกล 1")]
    public Vector3 offset;
}

public class ShotOriginSettings : ScriptableObject
{
    public const string ResourcePath = "ShotOriginSettings";
    public const string AssetPath = "Assets/Resources/ShotOriginSettings.asset";

    public List<ShotOriginEntry> entries = new List<ShotOriginEntry>();

    private static ShotOriginSettings cached;
    public static ShotOriginSettings Instance
    {
        get
        {
            if (cached == null) cached = Resources.Load<ShotOriginSettings>(ResourcePath);
            return cached;
        }
    }

    // ค่าของท่านี้ก่อน ไม่มีค่อยใช้ค่าของทั้งอาวุธ
    public ShotOriginEntry Find(WeaponType weapon, string state)
    {
        ShotOriginEntry fallback = null;
        foreach (ShotOriginEntry entry in entries)
        {
            if (entry.weapon != weapon) continue;
            if (!string.IsNullOrEmpty(state) && entry.state == state) return entry;
            if (string.IsNullOrEmpty(entry.state)) fallback = entry;
        }
        return fallback;
    }

    public ShotOriginEntry FindExact(WeaponType weapon, string state)
    {
        state = state ?? "";
        return entries.Find(e => e.weapon == weapon && (e.state ?? "") == state);
    }

    // จุดในโลกจากรายการ (bone = กระดูกในโมเดล, modelScale = lossyScale.x ของรากโมเดล)
    public static Vector3 Resolve(ShotOriginEntry entry, Transform bone, float modelScale)
    {
        return bone.position + bone.rotation * (entry.offset * modelScale);
    }

    public static Vector3 ToOffset(Vector3 world, Transform bone, float modelScale)
    {
        return Quaternion.Inverse(bone.rotation) * (world - bone.position) / Mathf.Max(0.0001f, modelScale);
    }
}
