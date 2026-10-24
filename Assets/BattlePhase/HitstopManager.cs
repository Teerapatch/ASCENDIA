using System.Collections;
using UnityEngine;

public class HitstopManager : MonoBehaviour
{
    public static HitstopManager Instance;

    private Coroutine hitstopCoroutine;

    private void Awake()
    {
        Instance = this;
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
        Time.timeScale = 0f;

        // 2. รอเวลาจริง (Realtime) ตามระยะเวลาที่กำหนด
        yield return new WaitForSecondsRealtime(duration);

        // 3. คืนค่าเวลาเป็นปกติ
        Time.timeScale = 1f;
        hitstopCoroutine = null;
    }

    // ถ้าออกจากฉากต่อสู้ระหว่าง Hitstop ห้ามปล่อยให้ timeScale ค้างที่ 0 ไปถึงฉากอื่น
    private void OnDestroy()
    {
        if (hitstopCoroutine != null) Time.timeScale = 1f;
    }
}