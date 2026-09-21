using UnityEngine;

public class NodeSelectionTrigger : MonoBehaviour
{
    private bool hasTriggered = false;

    private void OnTriggerEnter(Collider other)
    {
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