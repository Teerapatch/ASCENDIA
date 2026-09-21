using UnityEngine;

public class RoomManager : MonoBehaviour
{
    public static RoomManager Instance;

    [Header("Room Prefabs (แพ็คฉาก)")]
    public GameObject[] climbRoomPrefabs; 
    public GameObject[] battleRoomPrefabs; 

    [Header("Environment")]
    public Transform roomContainer; 
    
    private GameObject currentActiveRoom; 

    private void Awake() { Instance = this; }

    private void Start()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null) LoadNewRoom(PathNode.RoomType.Climb, player.transform);
    }

    public void LoadNewRoom(PathNode.RoomType type, Transform playerTransform)
    {
        if (currentActiveRoom != null) Destroy(currentActiveRoom); 

        GameObject roomToSpawn = null;
        if (type == PathNode.RoomType.Climb && climbRoomPrefabs.Length > 0)
            roomToSpawn = climbRoomPrefabs[Random.Range(0, climbRoomPrefabs.Length)];
        else if (type == PathNode.RoomType.Battle && battleRoomPrefabs.Length > 0)
            roomToSpawn = battleRoomPrefabs[Random.Range(0, battleRoomPrefabs.Length)];

        if (roomToSpawn != null)
        {
            Transform parentContainer = (roomContainer != null) ? roomContainer : null;
            currentActiveRoom = Instantiate(roomToSpawn, Vector3.zero, Quaternion.identity, parentContainer);

            int currentFloor = GameManager.Instance != null ? GameManager.Instance.playerData.currentFloor : 1;
            
            Transform spawnPoint = null;
            
            // 🌟 ถ้าเป็นห้องปีนเขา ด่าน 2 ขึ้นไป
            if (currentFloor > 1 && type == PathNode.RoomType.Climb)
            {
                // ใช้จุดเกิดแบบเกาะกำแพง
                spawnPoint = currentActiveRoom.transform.Find("WallSpawnPoint");
                
                // 🌟 สั่งปิดการมองเห็นของพื้นดินทิ้งไปเลย!
                Transform startFloor = currentActiveRoom.transform.Find("StartFloor");
                if (startFloor != null)
                {
                    startFloor.gameObject.SetActive(false); 
                }
            }
            
            // ถ้าเป็นด่าน 1 หรือห้องต่อสู้ ให้เกิดที่พื้นปกติ
            if (spawnPoint == null)
            {
                spawnPoint = currentActiveRoom.transform.Find("PlayerSpawnPoint");
            }

            if (spawnPoint != null)
            {
                CharacterController cc = playerTransform.GetComponent<CharacterController>();
                if (cc != null) cc.enabled = false;

                playerTransform.position = spawnPoint.position;
                playerTransform.rotation = spawnPoint.rotation;

                if (cc != null) cc.enabled = true;
            }
        }
    }
}