using UnityEngine;

// คำสั่งนี้จะทำให้เราคลิกขวาในโฟลเดอร์เพื่อสร้างไอเทมใหม่ได้เลย!
[CreateAssetMenu(fileName = "NewItemData", menuName = "Game Data1/Item Data")]
public class ItemData : ScriptableObject
{
    [Header("Item Info")]
    public string itemName;            // ชื่อไอเทม เช่น "แร่เงิน", "แร่ทอง"
    public Sprite icon;                // รูปภาพที่จะโชว์ในกระเป๋า
    [TextArea(3, 5)] 
    public string description;         // คำอธิบาย

    [Header("Inventory Settings")]
    public float weightPerItem = 0.5f; // น้ำหนักต่อ 1 ชิ้น (เช่น แร่ 1 ก้อนหนัก 0.5)
    public int maxStack = 99;          // 1 ช่องกระเป๋า ทับซ้อนกันได้กี่ชิ้น
    public bool isConsumable = false;  // กิน/ใช้ได้ไหม
}