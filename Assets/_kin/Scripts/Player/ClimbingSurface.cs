using UnityEngine;
public class ClimbingSurface : MonoBehaviour
{
    public Transform[] holds = new Transform[0];
    public Vector3 NearestHold(Vector3 point, float radius)
    {
        float best = radius * radius;
        Vector3 result = point;
        foreach (var hold in holds)
        {
            if (!hold) continue;
            float distance = (hold.position - point).sqrMagnitude;
            if (distance < best) { best = distance; result = hold.position; }
        }
        return result;
    }
}
