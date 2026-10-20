using UnityEngine;

public class VFXManager : MonoBehaviour
{
    public static VFXManager Instance;

    [Header("Parry Effects")]
    public GameObject parrySlashVFXPrefab; // Prefab ที่มี Component 'CurvedSlashEffect' และ 'LineRenderer'
    public GameObject parrySparkVFXPrefab; // Prefab ประกายไฟ

    private void Awake()
    {
        Instance = this;
    }

    /// <summary>
    /// เสกเอฟเฟกต์รอยฟันแบบเส้นโค้งตามการลาก
    /// </summary>
    public void SpawnParrySlash(Vector3 startPosition, Vector3 endPosition)
    {
        if (parrySlashVFXPrefab != null)
        {
            // สร้าง GameObject ของรอยฟัน
            GameObject vfxObj = Instantiate(parrySlashVFXPrefab, startPosition, Quaternion.identity);
            CurvedSlashEffect slashEffect = vfxObj.GetComponent<CurvedSlashEffect>();

            if (slashEffect != null)
            {
                slashEffect.Initialize(startPosition, endPosition, 0.5f);
            }
            else
            {
                Destroy(vfxObj, 0.5f);
            }
        }

        // เสกประกายไฟที่จุดปะทะปลายทาง (End Position)
        if (parrySparkVFXPrefab != null)
        {
            GameObject sparkObj = Instantiate(parrySparkVFXPrefab, endPosition, Quaternion.identity);
            Destroy(sparkObj, 0.5f);
        }

        Debug.Log($"Spawned Curved Parry Slash from {startPosition} to {endPosition}");
    }
}