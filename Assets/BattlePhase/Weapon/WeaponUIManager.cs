using System.Collections.Generic;
using UnityEngine;

public class WeaponUIManager : MonoBehaviour
{
    public static WeaponUIManager Instance;

    public GameObject weaponSlotPrefab;
    public Transform container;

    private List<WeaponSlotUI> spawnedSlots = new List<WeaponSlotUI>();

    private void Awake()
    {
        Instance = this;
    }

    public void UpdateWeaponUI(List<WeaponData> inventory, WeaponData activeWeapon)
    {
        if (spawnedSlots.Count != inventory.Count)
        {
            foreach (Transform child in container) Destroy(child.gameObject);
            spawnedSlots.Clear();

            foreach (var weapon in inventory)
            {
                GameObject go = Instantiate(weaponSlotPrefab, container);
                spawnedSlots.Add(go.GetComponent<WeaponSlotUI>());
            }
        }

        bool isPlayerTurn = false;
        if (CombatManager.Instance != null)
        {
            isPlayerTurn = (CombatManager.Instance.currentState == CombatState.PlayerTurn);
        }

        for (int i = 0; i < inventory.Count; i++)
        {
            bool isActive = isPlayerTurn && (inventory[i] == activeWeapon);
            spawnedSlots[i].Setup(inventory[i], isActive);
        }
    }
}