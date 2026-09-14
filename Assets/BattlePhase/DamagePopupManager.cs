using UnityEngine;

public class DamagePopupManager : MonoBehaviour
{
    public static DamagePopupManager Instance;

    [Tooltip("ใส่ Prefab ที่มีสคริปต์ DamagePopup เข้ามาที่นี่")]
    public GameObject damagePopupPrefab;

    private void Awake()
    {
        Instance = this;
    }

    public void CreatePopup(Vector3 position, int damageAmount, bool isCritical, Color color)
    {
        if (damagePopupPrefab == null) return;

        // สุ่มขยับจุดเกิดนิดหน่อย ป้องกันตัวเลขซ้อนทับกันเป๊ะๆ เวลาโดนตีหลายฮิต
        Vector3 randomOffset = new Vector3(Random.Range(-0.5f, 0.5f), Random.Range(-0.5f, 0.5f), 0);

        GameObject popupTransform = Instantiate(damagePopupPrefab, position + randomOffset, Quaternion.identity);
        DamagePopup popup = popupTransform.GetComponent<DamagePopup>();

        popup.Setup(damageAmount, isCritical, color);
    }
}