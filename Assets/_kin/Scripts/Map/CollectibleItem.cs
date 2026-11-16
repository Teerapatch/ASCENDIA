using UnityEngine;
using UnityEngine.InputSystem; // ใช้สำหรับรับค่าปุ่ม E

public class CollectibleItem : MonoBehaviour
{
    [Header("Item Settings")]
    public ItemData itemData; 
    public int amount = 1;

    [Header("Behavior (พฤติกรรม)")]
    [Tooltip("ติ๊กถูกถ้าเป็นสมุนไพร (เก็บปุ๊บใช้งานทันทีไม่ต้องเข้ากระเป๋า)")]
    public bool useImmediately = false; 
    [Tooltip("สมุนไพรลดความเหนื่อยเท่าไร (คืน Stamina สูงสุด)")]
    public float fatigueCure = 8f;
    public float staminaRestoreAmount = 10f; // ถ้าเป็นสมุนไพร จะให้ฟื้นฟูเท่าไหร่

    [Header("UI Prompt")]
    [Tooltip("ลากข้อความ 3D (Press E) มาใส่ตรงนี้")]
    public GameObject promptUI; 

    [Header("Effects")]
    public GameObject collectEffect;
    [Tooltip("ประกายอ่อน ๆ รอบของ บอกผู้เล่นว่าเก็บได้")]
    public bool showSparkle = true;
    public Color gemSparkleColor = new Color(1f, .95f, .7f);
    public Color herbSparkleColor = new Color(.65f, 1f, .7f);

    private bool isPlayerNearby = false;

    private void Start()
    {
        // เริ่มเกมมา ให้ซ่อนปุ่ม E ไว้ก่อน
        if (promptUI != null) promptUI.SetActive(false);

        if (showSparkle)
        {
            // วางตรงกลางโมเดลที่มองเห็น (โมเดลของอาร์ตอาจอยู่เยื้องจากจุดกดเก็บ)
            bool found = false;
            Bounds bounds = default;
            foreach (Renderer renderer in GetComponentsInChildren<Renderer>())
            {
                if (!renderer.enabled || renderer is ParticleSystemRenderer) continue;
                if (!found) { bounds = renderer.bounds; found = true; }
                else bounds.Encapsulate(renderer.bounds);
            }
            Vector3 center = found ? bounds.center : transform.position;
            float radius = found ? Mathf.Clamp(bounds.extents.magnitude * .6f, .5f, 1.6f) : .8f;
            SoftGlow.Sparkle(transform, center, useImmediately ? herbSparkleColor : gemSparkleColor, radius);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        // ถ้าผู้เล่นเดินเข้ามาในรัศมี
        if (other.CompareTag("Player"))
        {
            isPlayerNearby = true;
            if (promptUI != null) promptUI.SetActive(true); // โชว์ข้อความ "Press E"
        }
    }

    private void OnTriggerExit(Collider other)
    {
        // ถ้าผู้เล่นเดินออกนอกรัศมี
        if (other.CompareTag("Player"))
        {
            isPlayerNearby = false;
            if (promptUI != null) promptUI.SetActive(false); // ซ่อนข้อความ
        }
    }

    private void Update()
    {
        // ถ้าผู้เล่นอยู่ใกล้ๆ และมีการกดปุ่ม E บนคีย์บอร์ด
        if (!GameExitMenu.IsOpen && isPlayerNearby && Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
        {
            InteractWithItem();
        }
    }

    private void InteractWithItem()
    {
        bool success = false;

        if (useImmediately)
        {
            // 🌿 กรณี: ใช้ทันที (เช่น สมุนไพร)
            if (GameManager.Instance != null && GameManager.Instance.playerData != null)
            {
                PlayerData data = GameManager.Instance.playerData;
                // บวก Stamina และบังคับไม่ให้เกิน Max
                data.AddFatigue(-fatigueCure);
                data.stamina = Mathf.Min(data.EffectiveMaxStamina, data.stamina + staminaRestoreAmount);
                
                Debug.Log($"🌿 ใช้งาน {itemData.itemName} ทันที! (ฟื้นฟู Stamina +{staminaRestoreAmount})");
                success = true; // ถือว่าใช้งานสำเร็จ
            }
        }
        else
        {
            // 💎 กรณี: เก็บเข้ากระเป๋า (เช่น แร่, Gem Stone)
            if (InventoryManager.Instance != null)
            {
                success = InventoryManager.Instance.AddItem(itemData, amount);
                if (success) Debug.Log($"เก็บ {itemData.itemName} เข้ากระเป๋าสำเร็จ!");
                else Debug.LogWarning("⚠️ เก็บไม่ได้! กระเป๋าเต็ม หรือน้ำหนักเกิน");
            }
        }

        // ถ้าเก็บหรือใช้งานสำเร็จ ให้ทำลายของทิ้ง
        if (success)
        {
            if (collectEffect != null) Instantiate(collectEffect, transform.position, Quaternion.identity);
            Destroy(gameObject);
        }
    }
}