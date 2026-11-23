using UnityEngine;

// ไฟส่องศัตรูที่ถึงเทิร์น: ใช้ไฟ "Point_Light_to_Focus" ในฉาก (อาร์ตวางไว้ข้างจุดเกิด Pos_1)
// จำระยะเยื้องจากจุดเกิดที่ไฟนั้นอยู่ แล้วย้ายไปอยู่ตำแหน่งเดียวกันของศัตรูที่กำลังเล่นเทิร์น ค่อย ๆ สว่างขึ้น/ดับลง (เวลาจริง)
// CombatManager.NextTurn เรียก Focus (เทิร์นศัตรู) / Release (เทิร์นผู้เล่น, จบการต่อสู้) สร้างตัวเองครั้งแรกที่ใช้ วางในฉากเพื่อปรับค่า
public class EnemyTurnLight : MonoBehaviour
{
    private static EnemyTurnLight instance;

    [Tooltip("ไฟที่ใช้ เว้นว่าง = หาในฉากจากชื่อ lightName")]
    public Light focusLight;
    public string lightName = "Point_Light_to_Focus";
    [Tooltip("ความสว่างตอนส่อง (ติดลบ = ใช้ค่าที่ตั้งไว้ในไฟ)")]
    public float intensity = -1f;
    public float fadeInTime = 0.3f;
    public float fadeOutTime = 0.4f;

    private Vector3 offset;
    private float fullIntensity;
    private float level;
    private EnemyController following;
    private bool ready;

    private static EnemyTurnLight Get()
    {
        if (instance == null) instance = FindAnyObjectByType<EnemyTurnLight>();
        if (instance == null) instance = new GameObject("EnemyTurnLight").AddComponent<EnemyTurnLight>();
        instance.Setup();
        return instance;
    }

    public static void Focus(EnemyController enemy)
    {
        EnemyTurnLight light = Get();
        light.following = enemy;
    }

    // เทิร์นผู้เล่น/จบการต่อสู้: ดับไฟ (ครั้งแรกจะเก็บไฟจากฉากแล้วปิดไว้ก่อน ไม่ให้ค้างสว่างที่ Pos_1)
    public static void Release()
    {
        Get().following = null;
    }

    private void Awake()
    {
        if (instance == null) instance = this;
        Setup();
    }

    private void Setup()
    {
        if (ready) return;
        if (focusLight == null)
        {
            foreach (Light candidate in FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (candidate.name == lightName) { focusLight = candidate; break; }
        }
        if (focusLight == null) return;
        ready = true;

        // ระยะเยื้องจากจุดเกิดที่ไฟถูกวางไว้ (พ่อของไฟ) = ระยะจากรากของศัตรู
        Transform anchor = focusLight.transform.parent;
        offset = anchor != null ? focusLight.transform.position - anchor.position : Vector3.up * 2f;
        fullIntensity = intensity >= 0f ? intensity : focusLight.intensity;
        focusLight.transform.SetParent(null, true);
        focusLight.gameObject.SetActive(true);
        focusLight.intensity = 0f;
        focusLight.enabled = false;
        level = 0f;
    }

    private void LateUpdate()
    {
        if (!ready || focusLight == null) return;
        bool on = following != null && !following.IsDead && CombatManager.Instance != null && !CombatManager.Instance.IsBattleOver;
        float target = on ? 1f : 0f;
        float time = on ? fadeInTime : fadeOutTime;
        level = Mathf.MoveTowards(level, target, time > 0f ? Time.unscaledDeltaTime / time : 1f);

        if (following != null) focusLight.transform.position = following.transform.position + offset;
        focusLight.intensity = fullIntensity * level * level * (3f - 2f * level);
        focusLight.enabled = level > 0.001f;
        if (!on && level <= 0f) following = null;
    }
}
