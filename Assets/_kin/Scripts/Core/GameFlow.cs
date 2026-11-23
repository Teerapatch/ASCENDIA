using UnityEngine;

// ลูปเกมตั้งแต่ต้นจนจบ (ทุกการเปลี่ยนฉากผ่าน SceneTransition จึงมีจอดำคั่นเสมอ)
//   Title --PLAY--> ClimbScene (รันใหม่: รีเซ็ต PlayerData, ให้อาวุธเริ่มต้น)
//   ClimbScene: ปีนทีละชั้น เลือกเส้นทาง (Normal / Battle / MiniBoss / Ore / Rest) ครบ maxFloorBeforeCamp ชั้น -> โหนด Camp
//   CampScene : Forge / Loadout / Rest -> ช่วงปีนถัดไป (currentCampLevel + 1)
//   ช่วงสุดท้าย (currentCampLevel >= finalCampLevel) ชั้นสุดท้ายเป็นโหนด Boss -> ชนะ = หน้าจบ (THE SUMMIT) -> Title
//   แพ้: หมด Piton ตอนหมดแรง หรือแพ้การต่อสู้ตอนไม่มี Piton เหลือ = Game Over -> Title / ปิดเกม
public static class GameFlow
{
    public const string TitleScene = "TitleScene";
    public const string ClimbScene = "ClimbScene";
    public const string CampScene = "CampScene";

    public static PlayerData Data => GameManager.Instance != null ? GameManager.Instance.playerData : null;

    // เริ่มรันใหม่จากหน้า Title (GameManager อยู่ข้ามฉาก กลับมา Title แล้วเล่นใหม่ต้องรีเซ็ตเอง ครั้งแรกสุด GameManager รีเซ็ตตอนเกิด)
    public static void StartNewRun(string climbScene = ClimbScene)
    {
        SceneTransition.Load(string.IsNullOrEmpty(climbScene) ? ClimbScene : climbScene, () =>
        {
            ClearTransientState();
            if (Data != null) Data.ResetData();
        });
    }

    public static void ReturnToTitle()
    {
        SceneTransition.Load(TitleScene, ClearTransientState);
    }

    // ค่าที่ค้างข้ามฉาก (static / หน้าต่างที่อยู่ข้ามฉาก) ต้องล้างก่อนเริ่มใหม่
    private static void ClearTransientState()
    {
        BattleSession.Abort();
        PlayerStateManager.InputLocked = false;
        Time.timeScale = 1f;
        if (GameExitMenu.Instance != null) GameExitMenu.Instance.ResetForNewRun();
    }

    // ชนะบอสยอดเขา: หน้าจบพร้อมสรุปรัน
    public static void ShowEnding()
    {
        PlayerData data = Data;
        string message = data != null
            ? $"You reached the summit at {Mathf.RoundToInt(data.heightMeters)} m.\nFloors climbed: {data.floorsClimbed}    Battles won: {data.battlesWon}"
            : "You reached the summit.";
        SoundManager.Play(Sfx.Victory);
        SoundManager.Play(Sfx.PlayerWin);
        if (GameExitMenu.Instance != null) GameExitMenu.Instance.ShowGameOver("THE SUMMIT", message);
    }

    // จบรันแบบแพ้
    public static void ShowGameOver(string title, string reason)
    {
        PlayerData data = Data;
        string message = data != null
            ? $"{reason}\nHeight reached: {Mathf.RoundToInt(data.heightMeters)} m    Floors climbed: {data.floorsClimbed}"
            : reason;
        SoundManager.Play(Sfx.Defeat);
        if (GameExitMenu.Instance != null) GameExitMenu.Instance.ShowGameOver(title, message);
    }
}
