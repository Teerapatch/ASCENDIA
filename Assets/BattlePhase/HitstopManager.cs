using System.Collections;
using UnityEngine;

public class HitstopManager : MonoBehaviour
{
    public static HitstopManager Instance;

    // ความเร็วเวลาที่จะกลับไปหลัง Hitstop (ParryManager ลดลงระหว่างที่ Hint ทิศทางขึ้น)
    public static float BaseTimeScale { get; private set; } = 1f;
    private static bool isFrozen;
    public static bool IsFrozen => isFrozen;

    private Coroutine hitstopCoroutine;

    private void Awake()
    {
        Instance = this;
        BaseTimeScale = 1f;
        isFrozen = false;
    }

    private void OnDisable()
    {
        isFrozen = false;
    }

    // ระหว่าง Hitstop เวลายังหยุดอยู่ ค่านี้จะมีผลตอน Hitstop จบ
    public static void SetBaseTimeScale(float scale)
    {
        BaseTimeScale = scale;
        if (!isFrozen) Time.timeScale = scale;
    }

    /// <summary>
    /// สั่งหยุดเวลาฉับพลัน
    /// </summary>
    /// <param name="duration">ระยะเวลาที่หยุด (วินาที)</param>

    public void Freeze(float duration)
    {
        if (hitstopCoroutine != null)
            StopCoroutine(hitstopCoroutine);

        hitstopCoroutine = StartCoroutine(DoHitstop(duration));
    }

    private IEnumerator DoHitstop(float duration)
    {
        // 1. หยุดเวลาในเกมเป็น 0
        isFrozen = true;
        Time.timeScale = 0f;

        // 2. รอเวลาจริง (Realtime) ตามระยะเวลาที่กำหนด
        yield return new WaitForSecondsRealtime(duration);

        // 3. คืนค่าเวลาเป็นปกติ
        isFrozen = false;
        Time.timeScale = BaseTimeScale;
    }
}
