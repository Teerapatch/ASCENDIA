using UnityEngine;
using System.Collections;

[RequireComponent(typeof(LineRenderer))]
public class CurvedSlashEffect : MonoBehaviour
{
    private LineRenderer lineRenderer;

    private void Awake()
    {
        lineRenderer = GetComponent<LineRenderer>();

        // --- [สำคัญ] บังคับให้ Line Renderer ใช้พิกัด World Space เสมอ ---
        // เพื่อให้เส้นวาดตรงตามพิกัดหน้าจอ/โลกที่ผู้เล่นลากเป๊ะๆ 100% ไม่มีการเพี้ยน
        lineRenderer.useWorldSpace = true;
    }

    public void Initialize(Vector3 startPos, Vector3 endPos, float duration = 0.5f)
    {
        StartCoroutine(AnimateSlash(startPos, endPos, duration));
    }

    private IEnumerator AnimateSlash(Vector3 startPos, Vector3 endPos, float duration)
    {
        int resolution = 20;
        lineRenderer.positionCount = resolution;

        // 1. หาจุดกึ่งกลางและทิศทางเพื่อดัดให้เส้นโค้ง (Arc)
        Vector3 midPoint = (startPos + endPos) * 0.5f;
        Vector3 direction = (endPos - startPos);

        // หาแกนตั้งฉากเพื่อให้เส้นโค้งรับกับมุมมองกล้อง
        Vector3 camForward = Camera.main != null ? Camera.main.transform.forward : Vector3.forward;
        Vector3 bendDirection = Vector3.Cross(direction.normalized, camForward);
        if (bendDirection == Vector3.zero) bendDirection = Vector3.up;

        // ระยะความโค้ง (ยิ่งลากไกล เส้นยิ่งโค้งสวยขึ้น)
        float bendAmount = direction.magnitude * 0.25f;
        Vector3 controlPoint = midPoint + bendDirection * bendAmount;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float progress = elapsed / duration;

            // 2. คำนวณพิกัดเส้นโค้งด้วย Quadratic Bezier Curve
            // สูตรนี้จะบังคับให้จุดแรก (t=0) อยู่ที่ startPos และจุดสุดท้าย (t=1) อยู่ที่ endPos เป๊ะๆ
            for (int i = 0; i < resolution; i++)
            {
                float t = i / (float)(resolution - 1);
                Vector3 point = Mathf.Pow(1 - t, 2) * startPos + 2 * (1 - t) * t * controlPoint + Mathf.Pow(t, 2) * endPos;
                lineRenderer.SetPosition(i, point);
            }

            // 3. ทำการ Fade Alpha ของเส้นให้จางหายไปตามเวลา
            Color c1 = lineRenderer.startColor;
            Color c2 = lineRenderer.endColor;
            c1.a = Mathf.Lerp(1f, 0f, progress);
            c2.a = Mathf.Lerp(1f, 0f, progress);
            lineRenderer.startColor = c1;
            lineRenderer.endColor = c2;

            yield return null;
        }

        Destroy(gameObject);
    }
}