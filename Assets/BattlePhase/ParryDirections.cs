using UnityEngine;

// จุดเดียวที่นิยามทิศ Parry ทั้ง 8 ทิศ (ทิศที่ผู้เล่นต้องลากบนจอ)
// ค่าเป็น int ที่ serialize อยู่ใน Prefab และ Asset แล้ว จึงห้ามสลับลำดับ
public static class ParryDirections
{
    public const int Auto = -1;
    public const int Right = 0;
    public const int Left = 1;
    public const int Down = 2;
    public const int Up = 3;
    public const int UpRight = 4;
    public const int UpLeft = 5;
    public const int DownRight = 6;
    public const int DownLeft = 7;

    private static readonly Vector2[] Vectors =
    {
        Vector2.right,
        Vector2.left,
        Vector2.down,
        Vector2.up,
        new Vector2(1f, 1f).normalized,
        new Vector2(-1f, 1f).normalized,
        new Vector2(1f, -1f).normalized,
        new Vector2(-1f, -1f).normalized
    };

    private static readonly string[] Names = { "ขวา", "ซ้าย", "ลง", "ขึ้น", "ขวาบน", "ซ้ายบน", "ขวาล่าง", "ซ้ายล่าง" };

    public static Vector2 ToVector(int index)
    {
        return index >= 0 && index < Vectors.Length ? Vectors[index] : Vector2.right;
    }

    public static string Name(int index)
    {
        return index >= 0 && index < Names.Length ? Names[index] : "อัตโนมัติ";
    }

    // ทิศที่ใกล้ที่สุดใน 8 ทิศ
    public static int FromVector(Vector2 direction)
    {
        int best = Right;
        float bestDot = float.MinValue;
        for (int i = 0; i < Vectors.Length; i++)
        {
            float dot = Vector2.Dot(direction, Vectors[i]);
            if (dot > bestDot)
            {
                bestDot = dot;
                best = i;
            }
        }
        return best;
    }

    // ทิศอาวุธที่เห็นบนจอ
    public static Vector2 ToScreen(Vector3 worldDirection, Transform view)
    {
        return new Vector2(Vector3.Dot(worldDirection, view.right), Vector3.Dot(worldDirection, view.up));
    }

    // ผู้เล่นต้องลากสวนทิศอาวุธ: อาวุธฟาดลง ผู้เล่นลากขึ้น
    public static int FromWeaponSwing(Vector3 worldSwing, Transform view)
    {
        return FromVector(-ToScreen(worldSwing, view));
    }
}

// แสดงช่อง int เป็นปุ่มลูกศร 8 ทิศใน Inspector ค่าที่เก็บยังเป็น int เดิม
public class ParryDirectionPadAttribute : PropertyAttribute
{
    public readonly bool AllowAuto;

    public ParryDirectionPadAttribute(bool allowAuto = false)
    {
        AllowAuto = allowAuto;
    }
}
