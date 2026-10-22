using UnityEngine;

public class Billboard : MonoBehaviour
{
    private Camera mainCamera;
    
    [Header("Position Settings")]
    [Tooltip("ระยะห่างจากไอเทมหลัก")]
    public Vector3 offset = new Vector3(0, 1.5f, 0); 
    
    private Transform targetItem; // เป้าหมายที่มันจะบินตาม

    private void Start()
    {
        mainCamera = Camera.main; 
        
        // 🌟 1. จำเอาไว้ว่าตัวเองต้องบินตามใคร (จำตัวพ่อแม่เดิมไว้)
        if (transform.parent != null)
        {
            targetItem = transform.parent;
            
            // 🌟 2. ตัดขาดการเป็นลูก! (Unparent) เพื่อไม่ให้โดนยืดสเกล
            transform.SetParent(null);
            
            // 🌟 3. รีเซ็ตสเกลของตัวเองให้เป็น 1,1,1 ปกติ
            transform.localScale = Vector3.one;
        }
    }

    private void LateUpdate()
    {
        // 🌟 4. ถ้าก้อนแร่ (เป้าหมาย) โดนเก็บจนพังไปแล้ว ก็ให้ข้อความนี้ทำลายตัวเองตามไปด้วย
        if (targetItem == null)
        {
            Destroy(gameObject);
            return;
        }

        if (mainCamera != null)
        {
            // บินตามก้อนแร่
            transform.position = targetItem.position + offset;

            // หันหน้าหากล้อง
            transform.LookAt(mainCamera.transform);
            
            // หมุนกลับ 180 องศาให้อ่านออก
            transform.Rotate(0, 180, 0);
        }
    }
}