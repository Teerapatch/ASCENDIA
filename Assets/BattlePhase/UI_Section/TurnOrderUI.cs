using System.Collections.Generic;
using UnityEngine;

public class TurnOrderUI : MonoBehaviour
{
    public GameObject portraitPrefab;
    public RectTransform container;

    [Header("Staircase Layout (ขั้นบันได)")]
    public float ySpacing = 90f;
    public float xOffsetPerItem = 25f;

    [Header("Animation Settings")]
    public Vector2 spawnOffset = new Vector2(300f, 0f);
    public Vector2 exitOffset = new Vector2(-300f, 0f);

    private Dictionary<string, TurnPortraitUI> activePortraits = new Dictionary<string, TurnPortraitUI>();

    public void UpdateUI(List<CombatManager.PredictedTurn> predictions)
    {
        HashSet<string> incomingIDs = new HashSet<string>();

        for (int i = 0; i < predictions.Count; i++)
        {
            var pred = predictions[i];
            incomingIDs.Add(pred.UniqueID);

            Vector2 targetPos = new Vector2(i * xOffsetPerItem, -i * ySpacing);

            if (activePortraits.ContainsKey(pred.UniqueID))
            {
                activePortraits[pred.UniqueID].Setup(pred);
                // สั่งเปลี่ยนอันดับและตำแหน่ง
                activePortraits[pred.UniqueID].UpdateRankAndPosition(targetPos, i);
            }
            else
            {
                GameObject go = Instantiate(portraitPrefab, container);
                TurnPortraitUI newPortrait = go.GetComponent<TurnPortraitUI>();

                newPortrait.Setup(pred);

                // แฟล็กบอกว่านี่คือตัวใหม่ ให้เตรียมสไลด์
                newPortrait.isNewlySpawned = true;
                newPortrait.GetComponent<RectTransform>().anchoredPosition = targetPos + spawnOffset;

                // กำหนดเป้าหมาย และส่งอันดับคิว
                newPortrait.UpdateRankAndPosition(targetPos, i);

                activePortraits.Add(pred.UniqueID, newPortrait);
            }
        }

        List<string> keysToRemove = new List<string>();
        foreach (var kvp in activePortraits)
        {
            if (!incomingIDs.Contains(kvp.Key))
            {
                kvp.Value.SlideOutAndDestroy(exitOffset);
                keysToRemove.Add(kvp.Key);
            }
        }

        foreach (var key in keysToRemove)
        {
            activePortraits.Remove(key);
        }
    }
}