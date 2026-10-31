using UnityEngine;
using Unity.Cinemachine;

// Keep the authored view direction. Only correct framing when the body leaves the safe frame.
[DisallowMultipleComponent]
public class PlayerCameraFraming : CinemachineExtension
{
    public Transform player;
    [Range(0f, .3f)] public float screenMargin = .1f;
    private Renderer[] renderers;
    private PathNode[] visibleNodes;
    private float nextNodeScan;
    [Range(.2f, .6f)] public float nodePlayerScreenHeight = .32f;

    protected override void PostPipelineStageCallback(CinemachineVirtualCameraBase vcam,
        CinemachineCore.Stage stage, ref Unity.Cinemachine.CameraState state, float deltaTime)
    {
        if (stage != CinemachineCore.Stage.Finalize || !player) return;
        if (renderers == null) renderers = player.GetComponentsInChildren<Renderer>(true);
        Bounds bounds = new Bounds(); bool found = false;
        foreach (var r in renderers)
        {
            if (!r || !r.enabled || !r.gameObject.activeInHierarchy || !(r is SkinnedMeshRenderer)) continue;
            // Fluttering cloth must not continuously push the camera backwards.
            if (r.name.IndexOf("Cape", System.StringComparison.OrdinalIgnoreCase) >= 0) continue;
            if (!found) { bounds = r.bounds; found = true; } else bounds.Encapsulate(r.bounds);
        }
        if (!found) return;
        bool nodeView = vcam.name == "NodeCam";
        float playerHeight = bounds.size.y;
        if (nodeView)
        {
            if (visibleNodes == null || Time.unscaledTime >= nextNodeScan)
            {
                visibleNodes = FindObjectsByType<PathNode>(FindObjectsSortMode.None);
                nextNodeScan = Time.unscaledTime + .5f;
            }
            foreach (var node in visibleNodes)
            {
                // Match the nodes shown by the selection UI, not every room in the level.
                if (!node || !node.uiNodeTransform || !node.uiNodeTransform.gameObject.activeInHierarchy) continue;
                bounds.Encapsulate(node.transform.position);
                if (node.nodeText) bounds.Encapsulate(node.nodeText.bounds);
            }
        }
        Quaternion rotation = state.GetFinalOrientation();
        Quaternion inverse = Quaternion.Inverse(rotation);
        Vector3 position = state.GetFinalPosition();
        Vector3 min = Vector3.one * float.PositiveInfinity;
        Vector3 max = Vector3.one * float.NegativeInfinity;
        for (int i = 0; i < 8; i++)
        {
            Vector3 p = bounds.center + Vector3.Scale(bounds.extents,
                new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
            p = inverse * (p - position); min = Vector3.Min(min, p); max = Vector3.Max(max, p);
        }
        float aspect = Mathf.Max(.1f, state.Lens.Aspect);
        float safe = 1f - 2f * (nodeView ? Mathf.Max(screenMargin, .18f) : screenMargin);
        float halfY, halfX, retreat;
        if (state.Lens.Orthographic)
        {
            var lens = state.Lens;
            lens.OrthographicSize = Mathf.Max(lens.OrthographicSize,
                Mathf.Max((max.y-min.y)*.5f, (max.x-min.x)*.5f/aspect)/safe);
            if (nodeView) lens.OrthographicSize = Mathf.Max(lens.OrthographicSize, playerHeight / (2f * nodePlayerScreenHeight));
            state.Lens = lens;
            halfY = lens.OrthographicSize * safe; halfX = halfY * aspect;
            retreat = Mathf.Max(0, lens.NearClipPlane + .1f - min.z);
        }
        else
        {
            float slope = Mathf.Tan(state.Lens.FieldOfView * Mathf.Deg2Rad * .5f) * safe;
            float depth = Mathf.Max((max.y-min.y)*.5f/slope, (max.x-min.x)*.5f/(slope*aspect));
            if (nodeView)
                depth = Mathf.Max(depth, playerHeight / (2f * Mathf.Tan(state.Lens.FieldOfView * Mathf.Deg2Rad * .5f) * nodePlayerScreenHeight));
            depth = Mathf.Max(depth, state.Lens.NearClipPlane + .1f);
            retreat = Mathf.Max(0, depth-min.z);
            halfY = (min.z+retreat)*slope; halfX = halfY*aspect;
        }
        float x = Mathf.Clamp(0f, max.x-halfX, min.x+halfX);
        float y = Mathf.Clamp(0f, max.y-halfY, min.y+halfY);
        state.PositionCorrection += rotation * new Vector3(x,y,-retreat);
    }
}
