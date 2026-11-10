using UnityEditor;
using UnityEngine;

// ปุ่มลูกศร 8 ทิศ ใช้ทั้งใน Inspector และ Boss Parry Editor
public static class ParryDirectionPad
{
    static readonly int[,] Grid =
    {
        { ParryDirections.UpLeft, ParryDirections.Up, ParryDirections.UpRight },
        { ParryDirections.Left, ParryDirections.Auto, ParryDirections.Right },
        { ParryDirections.DownLeft, ParryDirections.Down, ParryDirections.DownRight }
    };

    static readonly Color SelectedColor = new Color(0.35f, 0.9f, 0.45f);
    static readonly Color ImpliedColor = new Color(0.95f, 0.8f, 0.3f);

    // autoResult: ทิศที่ระบบคำนวณได้เมื่อเลือกอัตโนมัติ จะแสดงเป็นสีเหลือง
    public static int Draw(Rect rect, int value, bool allowAuto, int autoResult = ParryDirections.Auto)
    {
        float cellWidth = rect.width / 3f;
        float cellHeight = rect.height / 3f;

        for (int row = 0; row < 3; row++)
        {
            for (int column = 0; column < 3; column++)
            {
                int index = Grid[row, column];
                bool center = index == ParryDirections.Auto;
                if (center && !allowAuto) continue;

                Rect cell = new Rect(rect.x + column * cellWidth, rect.y + row * cellHeight, cellWidth - 2f, cellHeight - 2f);
                bool implied = value == ParryDirections.Auto && !center && autoResult == index;

                Color previous = GUI.backgroundColor;
                if (value == index) GUI.backgroundColor = SelectedColor;
                else if (implied) GUI.backgroundColor = ImpliedColor;
                if (GUI.Button(cell, center ? "A" : string.Empty)) value = index;
                GUI.backgroundColor = previous;

                if (!center && Event.current.type == EventType.Repaint)
                {
                    DrawArrow(cell.center, ParryDirections.ToVector(index), Mathf.Min(cellWidth, cellHeight) * 0.55f, Color.white, 2f);
                }
            }
        }
        return value;
    }

    // direction เป็นทิศบนจอ (y ขึ้น) วาดใน GUI (y ลง)
    public static void DrawArrow(Vector2 center, Vector2 direction, float length, Color color, float width)
    {
        Vector2 forward = new Vector2(direction.x, -direction.y).normalized;
        Vector2 side = new Vector2(-forward.y, forward.x);
        Vector2 tail = center - forward * (length * 0.5f);
        Vector2 tip = center + forward * (length * 0.5f);
        float head = length * 0.35f;

        Handles.color = color;
        Handles.DrawAAPolyLine(width, tail, tip);
        Handles.DrawAAPolyLine(width, tip - forward * head + side * (head * 0.65f), tip, tip - forward * head - side * (head * 0.65f));
    }
}

[CustomPropertyDrawer(typeof(ParryDirectionPadAttribute))]
public class ParryDirectionPadDrawer : PropertyDrawer
{
    const float PadSize = 66f;

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        return PadSize + 2f;
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        if (property.propertyType != SerializedPropertyType.Integer)
        {
            EditorGUI.PropertyField(position, property, label);
            return;
        }

        bool allowAuto = ((ParryDirectionPadAttribute)attribute).AllowAuto;
        EditorGUI.BeginProperty(position, label, property);
        Rect field = EditorGUI.PrefixLabel(position, label);
        Rect pad = new Rect(field.x, field.y + 1f, PadSize, PadSize);
        int value = ParryDirectionPad.Draw(pad, property.intValue, allowAuto);
        if (value != property.intValue) property.intValue = value;

        Rect caption = new Rect(pad.xMax + 6f, field.y, Mathf.Max(0f, field.width - PadSize - 6f), EditorGUIUtility.singleLineHeight);
        EditorGUI.LabelField(caption, ParryDirections.Name(property.intValue), EditorStyles.miniLabel);
        EditorGUI.EndProperty();
    }
}
