using System;
using System.Collections;
using UnityEngine;

// กระสุนของศัตรู: บินโค้งจากมือไปหาผู้เล่น ถึงแล้วค้างรอผล Parry (ปัดออก) หรือระเบิดใส่
public class EnemyProjectile : MonoBehaviour
{
    private Vector3 start;
    private Func<Vector3> target;
    private float flightTime, arc, elapsed;
    private bool finished;
    private GameObject impactPrefab;
    private Vector3 lastPosition;

    public static EnemyProjectile Launch(GameObject prefab, Vector3 from, Func<Vector3> target, float flightTime, float arc, float scale, GameObject impactPrefab)
    {
        GameObject go = prefab != null ? Instantiate(prefab, from, Quaternion.identity) : CreateDefault(from);
        go.transform.localScale = (prefab != null ? prefab.transform.localScale : Vector3.one) * scale;
        EnemyProjectile projectile = go.GetComponent<EnemyProjectile>();
        if (projectile == null) projectile = go.AddComponent<EnemyProjectile>();
        projectile.start = from;
        projectile.lastPosition = from;
        projectile.target = target;
        projectile.flightTime = Mathf.Max(0.05f, flightTime);
        projectile.arc = arc;
        projectile.impactPrefab = impactPrefab;
        Destroy(go, flightTime + 6f);
        return projectile;
    }

    // ไม่มี Prefab: ลูกแก้วม่วงเรืองแสงพร้อมหาง
    private static GameObject CreateDefault(Vector3 at)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = "EnemyProjectile";
        Destroy(go.GetComponent<Collider>());
        go.transform.position = at;
        go.transform.localScale = Vector3.one * 0.8f;

        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        var material = new Material(shader != null ? shader : Shader.Find("Sprites/Default")) { color = new Color(0.75f, 0.45f, 1f) };
        go.GetComponent<Renderer>().sharedMaterial = material;

        TrailRenderer trail = go.AddComponent<TrailRenderer>();
        trail.time = 0.25f;
        trail.startWidth = 0.6f;
        trail.endWidth = 0f;
        trail.sharedMaterial = material;
        trail.startColor = new Color(0.75f, 0.45f, 1f, 0.9f);
        trail.endColor = new Color(0.75f, 0.45f, 1f, 0f);
        return go;
    }

    private void Update()
    {
        if (finished || target == null) return;

        elapsed += Time.deltaTime;
        float t = Mathf.Clamp01(elapsed / flightTime);
        Vector3 end = target();
        Vector3 position = Vector3.Lerp(start, end, t) + Vector3.up * (Mathf.Sin(t * Mathf.PI) * arc);
        Vector3 velocity = position - lastPosition;
        if (velocity.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.LookRotation(velocity);
        transform.position = position;
        lastPosition = position;
    }

    // Parry สำเร็จ: ปัดกระสุนออกไปตามทิศที่ผู้เล่นลาก
    public void Deflect(Vector3 worldDirection)
    {
        if (finished) return;
        finished = true;
        StartCoroutine(FlyAway(worldDirection.sqrMagnitude > 0.001f ? worldDirection.normalized : Vector3.up));
    }

    // โดนตัว: ระเบิดแล้วหาย
    public void Impact()
    {
        if (finished) return;
        finished = true;
        if (impactPrefab != null && VFXManager.Instance != null)
        {
            VFXManager.Instance.PlayTimed(impactPrefab, transform.position, Quaternion.identity, 1f, 0.4f, 2.5f);
        }
        Destroy(gameObject);
    }

    private IEnumerator FlyAway(Vector3 direction)
    {
        Vector3 scale = transform.localScale;
        float time = 0f;
        while (time < 0.35f)
        {
            time += Time.deltaTime;
            transform.position += direction * (28f * Time.deltaTime);
            transform.localScale = scale * Mathf.Max(0f, 1f - time / 0.35f);
            yield return null;
        }
        Destroy(gameObject);
    }
}
