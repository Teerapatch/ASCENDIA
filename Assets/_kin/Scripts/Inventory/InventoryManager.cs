using System;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class InventorySlot
{
    public ItemData item;
    public int amount;
    public InventorySlot(ItemData item, int amount) { this.item = item; this.amount = amount; }
}

public class InventoryManager : MonoBehaviour
{
    public static InventoryManager Instance;

    [Header("Linked Data (ลาก PlayerData มาใส่)")]
    public PlayerData playerData; 

    [Header("Content")]
    public List<InventorySlot> slots = new List<InventorySlot>();
    public event Action OnInventoryChanged;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public bool AddItem(ItemData itemToAdd, int amount)
    {
        if (itemToAdd == null || playerData == null) return false;

        // 🌟 1. ใช้ลิมิตน้ำหนัก (maxWeight) จาก PlayerData ในการเช็ค
        if (playerData.weight + (itemToAdd.weightPerItem * amount) > playerData.maxWeight)
        {
            Debug.LogWarning("⚠️ กระเป๋าหนักเกินไป! ไม่สามารถเก็บเพิ่มได้");
            return false;
        }

        // 🌟 2. ลอจิกยัดของลงกระเป๋า (ซ้อนทับกันถ้าเป็นของชิ้นเดียวกัน)
        bool added = false;
        foreach (InventorySlot slot in slots)
        {
            if (slot.item == itemToAdd && slot.amount < itemToAdd.maxStack)
            {
                int spaceLeft = itemToAdd.maxStack - slot.amount;
                if (amount <= spaceLeft) { slot.amount += amount; amount = 0; added = true; break; }
                else { slot.amount += spaceLeft; amount -= spaceLeft; }
            }
        }
        if (amount > 0) { slots.Add(new InventorySlot(itemToAdd, amount)); added = true; }

        // 🌟 3. สั่งอัปเดตข้อมูลกลับไปที่ PlayerData
        if (added) UpdatePlayerData();
        return added;
    }

    private void UpdatePlayerData()
    {
        if (playerData == null) return;
        
        playerData.weight = 0;
        playerData.gemStone = 0; // รีเซ็ตเพื่อนับใหม่จากของในกระเป๋า

        foreach (InventorySlot slot in slots)
        {
            // คำนวณน้ำหนักรวม
            playerData.weight += slot.item.weightPerItem * slot.amount;
            
            // ถ้าไอเทมนี้เป็นแร่ ให้บวกเข้าตัวแปร ore ของ PlayerData ด้วย
            // (ใช้เช็คจากชื่อ ถ้าตั้งชื่อไอเทมว่ามีคำว่า "Ore" หรือ "แร่")
            if (slot.item.itemName.Contains("Ore") || slot.item.itemName.Contains("แร่"))
            {
                playerData.gemStone += slot.amount;
            }
        }
        OnInventoryChanged?.Invoke(); 
    }
}