using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    public PlayerData playerData = new PlayerData();

    public int currentNodeID = 0;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void TakeDamage(int damage)
    {
        playerData.hp -= damage;

        if (playerData.hp < 0)
            playerData.hp = 0;
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