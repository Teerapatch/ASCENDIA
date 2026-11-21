using UnityEngine;

public class PlayerStateManager : MonoBehaviour
{
    // เปลี่ยนสถานะสุดท้ายเป็น AutoClimbing
    public enum State { Walking, Climbing, Penalty, NodeSelection, AutoClimbing, Mantling }
    public State currentState = State.Walking;

    // ล็อกการบังคับชั่วคราว (เช่น ระหว่างเปลี่ยนหน้าผาหลังเลือกเส้นทาง: ผู้เล่นเกาะผนังแล้วแต่ยังไม่ให้ขยับจนกว่า UI จะปิด)
    public static bool InputLocked;

    public void ChangeState(State newState)
    {
        currentState = newState;
    }
}