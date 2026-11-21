using UnityEngine;

public class NodeSelectionTrigger : MonoBehaviour
{
    private bool hasTriggered = false;

    private void OnTriggerEnter(Collider other)
    {
        // ตอนนี้เปิดการ์ดเลือกเส้นทางตามความสูง (ครบทุก 100 m ที่ยอดหน้าผา) Trigger บนผานี้ใช้เฉพาะเมื่อปิดโหมดนั้น
        if (NodeSelectionManager.Instance != null && NodeSelectionManager.Instance.selectByHeight) return;
        if (other.CompareTag("Player") && !hasTriggered)
        {
            hasTriggered = true;
            // เรียกใช้ Manager ที่อยู่ข้างนอกห้อง
            if (NodeSelectionManager.Instance != null)
            {
                NodeSelectionManager.Instance.StartSelection(other.transform);
            }
        }
    }
}