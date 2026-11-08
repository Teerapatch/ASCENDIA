using UnityEngine;
[DisallowMultipleComponent]
public class PlayerAnimationBridge : MonoBehaviour
{
    public Animator animator;
    public float damping = 0.12f;
    private PlayerStateManager state;
    private Vector3 previous;
    private static readonly int Speed = Animator.StringToHash("Speed");
    void Awake() { state = GetComponent<PlayerStateManager>(); }
    void OnEnable() { previous = transform.position; }
    void LateUpdate()
    {
        Vector3 delta = transform.position - previous; previous = transform.position;
        if (!animator || Time.deltaTime <= 0) return;
        float speed = state && state.currentState == PlayerStateManager.State.Walking ? new Vector2(delta.x, delta.z).magnitude / Time.deltaTime : 0f;
        if (speed > 10f) speed = 0f; // Ignore teleports.
        animator.SetFloat(Speed, Mathf.Clamp(speed, 0f, 3.5f), damping, Time.deltaTime);
    }
    void OnDisable() { if (animator && animator.isInitialized) animator.SetFloat(Speed, 0); }
}
