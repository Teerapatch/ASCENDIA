using UnityEngine;
public class ClimbingHold : MonoBehaviour
{
    public float grabDistance = 1.5f;

    private void Update()
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    {
        if (GameManager.Instance == null)
            return;

        GameObject player =
            GameObject.FindGameObjectWithTag("Player");

        if (player == null)
            return;

        float distance =
            Vector3.Distance(
                player.transform.position,
                transform.position
            );

        if (distance <= grabDistance)
        {
            if (Input.GetKeyDown(KeyCode.Space))
            {
                GrabHold(player);
            }
        }
        
    }

    private void GrabHold(GameObject player)
    // Update is called once per frame
    {
        player.transform.position =
            transform.position +
            new Vector3(0, 0, -0.6f);

        Debug.Log("Grabbed Hold!");
        
    }
} 
