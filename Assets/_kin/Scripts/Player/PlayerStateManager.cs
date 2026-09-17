using UnityEngine;

public class PlayerStateManager : MonoBehaviour
{
    public enum State { Walking, Climbing, Penalty }
    public State currentState = State.Walking;

    // การเปลี่ยนสถานะ
    public void ChangeState(State newState)
    {
        currentState = newState;
    }
}