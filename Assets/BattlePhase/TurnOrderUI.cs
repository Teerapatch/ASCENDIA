using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class TurnOrderUI : MonoBehaviour
{
    [Header("UI Setup")]
    public TextMeshProUGUI turnTextPrefab;
    public Transform layoutGroup;

    // เปลี่ยน Parameter มารับค่า PredictedTurn
    public void UpdateUI(List<CombatManager.PredictedTurn> queue)
    {
        // 1. ล้าง UI เก่าทิ้ง
        foreach (Transform child in layoutGroup)
        {
            Destroy(child.gameObject);
        }

        // 2. สร้างรายชื่อเทิร์นล่วงหน้า
        for (int i = 0; i < queue.Count; i++)
        {
            TextMeshProUGUI newText = Instantiate(turnTextPrefab, layoutGroup);

            // i == 0 คือคนที่จะได้เล่นตอนนี้
            if (i == 0)
            {
                newText.text = $"> {queue[i].Name} (NOW)";
                newText.fontStyle = FontStyles.Bold; // ทำตัวหนาให้เทิร์นปัจจุบัน
            }
            else
            {
                // แสดงชื่อและระยะห่างของเทิร์น (AV)
                newText.text = $"{i + 1}. {queue[i].Name} (+{Mathf.RoundToInt(queue[i].SimulatedAV)})";
                newText.fontStyle = FontStyles.Normal;
            }

            // แยกสีฝั่งศัตรูกับผู้เล่น
            if (queue[i].IsEnemy)
                newText.color = new Color(1f, 0.4f, 0.4f); // สีแดงหม่น
            else
                newText.color = new Color(0.4f, 0.8f, 1f); // สีฟ้าสว่าง
        }
    }
}