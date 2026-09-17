using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    // เปลี่ยนจาก = new PlayerData() เป็นการเปิดช่องว่างไว้รอรับไฟล์จาก Inspector
    public PlayerData playerData; 

    public int currentNodeID = 0;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);

            // รีเซ็ตค่าสเตตัสกลับเป็นค่าเริ่มต้นทุกครั้งที่กด Play เกม
            if (playerData != null)
            {
                playerData.ResetData();
            }
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void AddOre(int amount)
    {
        playerData.ore += amount;
    }

    public void AddWeight(float amount)
    {
        playerData.weight += amount;
    }

    public void UsePiton()
    {
        if (playerData.piton > 0)
        {
            playerData.piton--;
        }
    }
}