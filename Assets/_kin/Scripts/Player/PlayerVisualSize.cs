using UnityEngine;

/// <summary>Scales only the rendered character around its original ground anchor.</summary>
[ExecuteAlways, DisallowMultipleComponent]
public class PlayerVisualSize : MonoBehaviour
{
    public Transform visual;
    [Range(0.25f, 3f), Tooltip("1 = original size. Changes visuals only, not gameplay colliders or cameras.")]
    public float visualScale = 1f;
    [SerializeField, HideInInspector] private Vector3 originalScale;
    [SerializeField, HideInInspector] private Vector3 originalPosition;
    [SerializeField, HideInInspector] private Vector3 groundAnchor;
    [SerializeField, HideInInspector] private Transform capturedVisual;
    private bool pending = true;
    private float lastScale = -1f;

    public bool Initialize(Transform target)
    {
        if (!target || target.parent != transform) return false;
        visual = target;
        if (capturedVisual == target) return true;
        capturedVisual = target;
        originalScale = target.localScale;
        originalPosition = target.localPosition;
        var controller = GetComponent<CharacterController>();
        groundAnchor = new Vector3(originalPosition.x,
            controller ? controller.center.y - controller.height * 0.5f : originalPosition.y,
            originalPosition.z);
        visualScale = 1f;
        ApplySize();
        return true;
    }
    private void OnEnable() { pending = true; }
    private void OnValidate() { pending = true; }
    private void Update()
    {
        if (pending || !Mathf.Approximately(lastScale, visualScale)) ApplySize();
    }
    public void ApplySize()
    {
        pending = false;
        if (!visual || capturedVisual != visual || visual.parent != transform) return;
        if (float.IsNaN(visualScale) || float.IsInfinity(visualScale)) visualScale = 1f;
        visualScale = Mathf.Clamp(visualScale, 0.25f, 3f);
        visual.localScale = originalScale * visualScale;
        visual.localPosition = groundAnchor + (originalPosition - groundAnchor) * visualScale;
        lastScale = visualScale;
    }
}
