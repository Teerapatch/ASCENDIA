using UnityEngine;

public class PlayerStateManager : MonoBehaviour
{
    // เปลี่ยนสถานะสุดท้ายเป็น AutoClimbing
    public enum State { Walking, Climbing, Penalty, NodeSelection, AutoClimbing }
    public State currentState = State.Walking;

    public void ChangeState(State newState)
    {
        currentState = newState;
    }
}