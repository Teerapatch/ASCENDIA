using UnityEngine;

// จุดวางกล้องของฉากต่อสู้: ลากใน Scene ได้เลย เส้นกรอบบอกภาพที่กล้องจะเห็น
// คลิกขวาที่ Component แล้วเลือก Snap To Scene View เพื่อใช้มุมมองปัจจุบันของ Scene View
public class BattleCameraPoint : MonoBehaviour
{
    [Tooltip("เวลาเคลื่อนกล้องมาที่จุดนี้ (วินาที)")]
    public float transitionDuration = 0.35f;
    public Color gizmoColor = new Color(1f, 0.8f, 0.2f, 1f);

    private void OnDrawGizmos()
    {
        Gizmos.color = gizmoColor;
        Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);
        Gizmos.DrawFrustum(Vector3.zero, 55f, 18f, 0.5f, 16f / 9f);
    }

#if UNITY_EDITOR
    [ContextMenu("Snap To Scene View")]
    private void SnapToSceneView()
    {
        UnityEditor.SceneView view = UnityEditor.SceneView.lastActiveSceneView;
        if (view == null) return;
        UnityEditor.Undo.RecordObject(transform, "Snap Camera Point");
        transform.SetPositionAndRotation(view.camera.transform.position, view.camera.transform.rotation);
    }
#endif
}
