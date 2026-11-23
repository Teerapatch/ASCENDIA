using System;
using UnityEngine;

// กระสุน 1 ลูกของท่ายิงผู้เล่น (สร้างโดย BattleProjectiles.Fire) วิ่งไปหาเป้า (ตามเป้าที่ขยับ) ถึงแล้วเรียก onArrive (ลงดาเมจ) + เอฟเฟกต์กระทบ
// เดินด้วยเวลาเกม (หยุดตาม Hitstop เหมือนตัวละคร) แต่มีเวลาบินสูงสุดเป็นเวลาจริง กันค้างจนเทิร์นไม่จบ
public class PlayerShot : MonoBehaviour
{
    private BattleProjectiles owner;
    private BattleProjectileKind kind;
    private Func<Vector3> target;
    private Action onArrive;
    private Vector3 start, straight, side;
    private float startedAt, travelled, total;
    private bool arrived;

    private ShotVisual visual;
    private Transform knife;      // มีดจริง (สำเนาโมเดลดาบในมือ) หมุนตีลังกาไปตามทาง
    private Vector3 spinAxis;
    private float spin;

    public void Init(BattleProjectiles owner, BattleProjectileKind kind, Vector3 from, Func<Vector3> target, Action onArrive)
    {
        this.owner = owner;
        this.kind = kind;
        this.target = target;
        this.onArrive = onArrive;
        start = straight = from;
        startedAt = Time.unscaledTime;
        Vector3 to = target != null ? target() : from;
        total = Mathf.Max(0.01f, Vector3.Distance(from, to));
        Vector3 direction = (to - from).normalized;
        // ด้านข้างสำหรับการส่ายของลูกพลังงาน: ตั้งฉากกับทิศบินและแกนกล้อง
        Camera cam = Camera.main;
        side = Vector3.Cross(direction, cam != null ? cam.transform.forward : Vector3.up);
        if (side.sqrMagnitude < 0.0001f) side = Vector3.Cross(direction, Vector3.up);
        side.Normalize();
        if (UnityEngine.Random.value < 0.5f) side = -side;
        BuildVisual();
    }

    private void BuildVisual()
    {
        if (kind == BattleProjectileKind.Knife)
        {
            BuildKnife();
            return;
        }
        bool blast = kind == BattleProjectileKind.EnergyBlast;
        visual = ShotVisual.Build(owner, gameObject, blast,
            blast ? owner.blastColor : owner.bulletColor,
            blast ? owner.blastCoreColor : Color.white,
            blast ? owner.blastSize : owner.bulletSize,
            blast ? 0.22f : owner.tracerTime,
            blast ? owner.blastSize * 0.7f : owner.tracerWidth);
    }

    // มีด: สำเนาโมเดลดาบที่ถืออยู่ (ไม่มีก็ใช้หัวกระสุนสีเงิน) + เส้นแสงสีเงินบาง ๆ
    private void BuildKnife()
    {
        visual = ShotVisual.Build(owner, gameObject, false, owner.knifeTrailColor, Color.white, owner.knifeTrailWidth * 1.6f, owner.knifeTrailTime, owner.knifeTrailWidth);
        Vector3 to = target != null ? target() : transform.position + Vector3.forward;
        Vector3 direction = (to - transform.position).sqrMagnitude > 0.0001f ? (to - transform.position).normalized : Vector3.forward;
        spinAxis = Vector3.Cross(Vector3.up, direction);
        if (spinAxis.sqrMagnitude < 0.0001f) spinAxis = side;
        spinAxis.Normalize();

        PlayerBattleAnimator animator = PlayerBattleAnimator.Instance;
        Material[] materials = null;
        Mesh mesh = animator != null ? animator.BakeHeldWeapon(out materials, out _) : null;
        if (mesh == null) return;
        var go = new GameObject("Knife");
        knife = go.transform;
        knife.position = transform.position;
        knife.localScale = Vector3.one * owner.knifeScale;
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = go.AddComponent<MeshRenderer>();
        renderer.sharedMaterials = materials;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        // มีดไม่ได้อยู่ใต้ตัวกระสุน (ตัวกระสุนหมุนตามทิศบิน) จึงหมุนตีลังกาในโลกเองได้
        Destroy(go, owner.maxFlightTime + 1f);
    }

    private void Update()
    {
        if (arrived) return;
        Vector3 to = target != null ? target() : straight;
        bool blast = kind == BattleProjectileKind.EnergyBlast;
        float speed = blast ? owner.blastSpeed : kind == BattleProjectileKind.Knife ? owner.knifeSpeed : owner.bulletSpeed;
        float step = speed * Time.deltaTime;

        Vector3 toTarget = to - straight;
        float remaining = toTarget.magnitude;
        bool timedOut = Time.unscaledTime - startedAt > owner.maxFlightTime;
        if (remaining <= step || timedOut)
        {
            straight = to;
            transform.position = to;
            Arrive(toTarget.sqrMagnitude > 0.0001f ? toTarget.normalized : transform.forward);
            return;
        }

        straight += toTarget / remaining * step;
        travelled += step;
        Vector3 offset = Vector3.zero;
        if (blast)
        {
            // ส่ายเป็นคลื่นเบา ๆ แล้วเข้าตรงเป้าตอนใกล้ถึง
            float progress = Mathf.Clamp01(travelled / total);
            offset = side * Mathf.Sin(progress * Mathf.PI * 2f) * owner.blastWobble * (1f - progress);
            visual?.Pulse(Time.unscaledTime - startedAt);
        }
        transform.position = straight + offset;
        if (toTarget.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.LookRotation(toTarget);
        if (knife != null)
        {
            spin += owner.knifeSpin * Time.deltaTime;
            knife.position = transform.position;
            knife.rotation = Quaternion.AngleAxis(spin, spinAxis);
        }
    }

    private void Arrive(Vector3 direction)
    {
        arrived = true;
        try { onArrive?.Invoke(); }
        catch (Exception e) { Debug.LogException(e); }
        owner.Impact(kind, transform.position, direction);
        if (knife != null) Destroy(knife.gameObject);

        // ซ่อนตัวกระสุน ปล่อยเส้นแสง/ประกายที่ค้างให้จางเองแล้วค่อยลบ
        Destroy(gameObject, visual != null ? visual.Finish() : 0.1f);
    }

    private void OnDestroy()
    {
        if (knife != null) Destroy(knife.gameObject);
        // ถูกลบก่อนถึงเป้า (เช่นออกจากฉาก): ยังต้องลงดาเมจให้เทิร์นจบได้
        if (!arrived)
        {
            arrived = true;
            try { onArrive?.Invoke(); }
            catch (Exception e) { Debug.LogException(e); }
        }
    }
}
