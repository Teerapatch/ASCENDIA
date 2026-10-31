using UnityEngine;
using UnityEngine.InputSystem;

// เครื่องมือเทส: ปุ่ม WIN BATTLE มุมขวาบน + ปุ่มลัด (F9)
// CombatManager เพิ่ม Component นี้ให้เองเฉพาะใน Editor / Development Build (ปิดได้ที่ CombatManager.enableDebugWinButton)
public class BattleDebugTools : MonoBehaviour
{
    public Key winHotkey = Key.F9;

    private GUIStyle buttonStyle;

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current[winHotkey].wasPressedThisFrame) ForceWin();
    }

    private void ForceWin()
    {
        CombatManager combat = CombatManager.Instance;
        if (combat == null || combat.IsBattleOver) return;

        Debug.Log("<color=yellow>[Debug] Force Win Battle</color>");
        combat.DebugForceVictory();
    }

    private void OnGUI()
    {
        CombatManager combat = CombatManager.Instance;
        if (combat == null || combat.IsBattleOver) return;

        if (buttonStyle == null)
        {
            buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = 16, fontStyle = FontStyle.Bold };
        }

        if (GUI.Button(new Rect(Screen.width - 200f, 10f, 190f, 40f), $"WIN BATTLE [{winHotkey}]", buttonStyle))
        {
            ForceWin();
        }
    }
}
