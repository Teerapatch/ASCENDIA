using UnityEngine;

public enum ElementType { Fire, Water, Wind, Earth, None }

// รวมข้อมูลธาตุที่เคยกระจายอยู่หลายไฟล์ (สุ่มธาตุ / สีของธาตุ) ไว้ที่เดียว
public static class ElementTypes
{
    public static readonly ElementType[] Playable = { ElementType.Fire, ElementType.Water, ElementType.Wind, ElementType.Earth };

    public static ElementType RandomPlayable()
    {
        return Playable[Random.Range(0, Playable.Length)];
    }

    public static string GetColorHex(ElementType type)
    {
        switch (type)
        {
            case ElementType.Fire: return "#FF4444";   // แดง (ไฟ)
            case ElementType.Water: return "#4488FF";  // น้ำเงิน (น้ำ)
            case ElementType.Wind: return "#44FF44";   // เขียว (ลม)
            case ElementType.Earth: return "#FFDD44";  // เหลือง (ดิน)
            default: return "#FFFFFF";
        }
    }

    public static Color GetColor(ElementType type)
    {
        return ColorUtility.TryParseHtmlString(GetColorHex(type), out Color color) ? color : Color.white;
    }
}
