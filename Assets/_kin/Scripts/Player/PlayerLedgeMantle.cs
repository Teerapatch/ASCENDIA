using System.Collections;
using UnityEngine;

// ปีนถึงพื้นเรียบ: หาจุดยืนบนหิน ใช้มือจับขอบดึงตัวขึ้น ก้าวเท้าขึ้นไปยืน แล้วเปลี่ยนเป็นเดิน
// ยังไม่มีคลิป "ปีนขึ้นขอบ" จากอาร์ต จึงสร้างท่าจากคลิปปีน + IK มือ/เท้า + การเคลื่อนตัวตามเส้นโค้ง
[RequireComponent(typeof(PlayerStateManager))]
public class PlayerLedgeMantle : MonoBehaviour
{
    [Header("หาพื้นเรียบ (หน่วยอาร์ต)")]
    [Range(.5f, 1f), Tooltip("พื้นผิวที่ normal.y ถึงค่านี้ถือว่าเรียบพอให้ยืน (0.82 ≈ เอียงไม่เกิน 35°)")]
    public float minFlatNormalY = .82f;
    [Tooltip("ขอบต้องอยู่สูงกว่าจุดกลางตัวอย่างน้อยกี่หน่วย")]
    public float minLedgeAboveCenter = -1f;
    [Tooltip("ขอบอยู่สูงกว่าจุดกลางตัวได้มากสุดกี่หน่วย (เอื้อมถึง)")]
    public float maxLedgeAboveCenter = 3f;
    [Tooltip("จุดยืนอยู่ลึกจากแนวหน้าผาเข้าไปกี่หน่วย")]
    public float standForward = 2.6f;
    [Tooltip("รัศมีตรวจว่าพื้นกว้างพอยืน")]
    public float landingRadius = 1.2f;
    [Tooltip("ความสูงพื้นรอบจุดยืนต่างกันได้เท่าไร")]
    public float landingHeightTolerance = .8f;
    [Tooltip("รัศมีตรวจที่ว่างเหนือจุดยืน")]
    public float clearanceRadius = 1f;

    [Header("ท่าขึ้นขอบ")]
    public float pullDuration = .75f;
    public float stepDuration = .6f;
    public float settleDuration = .2f;
    [Tooltip("ความเร็วคลิปปีนตอนดึงตัวขึ้น")]
    public float pullClimbRate = 1.3f;
    [Tooltip("ระยะมือจากกึ่งกลาง (หน่วยอาร์ต)")]
    public float gripHalfWidth = .85f;
    [Tooltip("ปีนลงจากขอบ: ลงไปต่ำกว่าเท้ากี่หน่วย")]
    public float climbDownDrop = 2.5f;
    public float climbDownDuration = .5f;

    private PlayerStateManager state;
    private PlayerClimbController climb;
    private PlayerWalkController walker;
    private PlayerCameraController cameraController;
    private CharacterController capsule;
    private ClimbingContactIK contactIK;
    private ClimbingHandRig handRig;
    private bool busy;
    private float cooldownUntil;

    private float HalfHeight => capsule != null ? capsule.height * .5f * Mathf.Abs(transform.lossyScale.y) : 1f;

    private void Awake()
    {
        state = GetComponent<PlayerStateManager>();
        climb = GetComponent<PlayerClimbController>();
        walker = GetComponent<PlayerWalkController>();
        cameraController = GetComponent<PlayerCameraController>();
        capsule = GetComponent<CharacterController>();
    }

    private void Start()
    {
        contactIK = GetComponentInChildren<ClimbingContactIK>();
        handRig = GetComponentInChildren<ClimbingHandRig>();
    }

    // เรียกตอนกำลังปีนขึ้น: เจอพื้นเรียบที่ยืนได้ก็เริ่มท่าขึ้นขอบ
    public bool TryStartMantle(Vector3 facing, ClimbableWall wall)
    {
        if (busy || wall == null || climb == null || Time.time < cooldownUntil) return false;
        if (!FindLedge(facing, wall, out Vector3 grip, out Vector3 stand)) return false;
        StartCoroutine(RunMantle(grip, stand, facing));
        return true;
    }

    private bool FindLedge(Vector3 facing, ClimbableWall wall, out Vector3 grip, out Vector3 stand)
    {
        grip = stand = Vector3.zero;
        Vector3 center = transform.position;
        float probeTop = maxLedgeAboveCenter + 2f;
        float probeLength = probeTop - minLedgeAboveCenter + .5f;

        for (int i = 1; i <= 2; i++)
        {
            Vector3 origin = center + facing * (climb.surfaceOffset + .8f * i) + Vector3.up * probeTop;
            if (!ClimbableWall.Raycast(origin, Vector3.down, probeLength, out RaycastHit top)) continue;
            float above = top.point.y - center.y;
            if (top.normal.y < minFlatNormalY || above < minLedgeAboveCenter || above > maxLedgeAboveCenter) continue;

            Vector3 candidate = center + facing * (climb.surfaceOffset + standForward);
            candidate.y = top.point.y;
            if (!ValidateLanding(candidate, wall, out float groundY)) continue;

            stand = new Vector3(candidate.x, groundY, candidate.z);
            // ขอบจริงคือจุดที่หน้าผาตัดกับพื้นด้านบน (ยิงตรงเข้าหน้าผาใต้พื้นเล็กน้อย)
            Vector3 faceOrigin = new Vector3(center.x, top.point.y - .4f, center.z);
            grip = ClimbableWall.Raycast(faceOrigin, facing, climb.surfaceOffset + 6f, out RaycastHit face)
                ? new Vector3(face.point.x, top.point.y + .05f, face.point.z) + facing * .25f
                : top.point;
            return true;
        }
        return false;
    }

    private bool ValidateLanding(Vector3 point, ClimbableWall wall, out float groundY)
    {
        groundY = point.y;
        // ต่ำกว่าโซน Node เท่านั้น และอยู่ในความกว้างของเลน
        if (point.y + HalfHeight > wall.MaxClimbY - 1f) return false;
        if ((wall.ClampPosition(point) - point).sqrMagnitude > .01f) return false;

        Vector3[] offsets = { Vector3.zero, Vector3.right, Vector3.left, Vector3.forward, Vector3.back };
        int good = 0;
        float centerY = point.y;
        for (int i = 0; i < offsets.Length; i++)
        {
            Vector3 origin = point + offsets[i] * landingRadius + Vector3.up * 2f;
            if (!ClimbableWall.Raycast(origin, Vector3.down, 4f, out RaycastHit hit) || hit.normal.y < minFlatNormalY) continue;
            if (i == 0) centerY = hit.point.y;
            if (Mathf.Abs(hit.point.y - point.y) <= landingHeightTolerance) good++;
        }
        if (good < 4) return false;
        groundY = centerY;

        // เหนือจุดยืนต้องไม่มีหินบัง
        Vector3 bottom = new Vector3(point.x, groundY + clearanceRadius + .3f, point.z);
        Vector3 top = bottom + Vector3.up * Mathf.Max(0f, HalfHeight * 2f - clearanceRadius * 2f - .3f);
        foreach (Collider c in Physics.OverlapCapsule(bottom, top, clearanceRadius, ~0, QueryTriggerInteraction.Ignore))
        {
            if (c.CompareTag(ClimbableWall.WallTag)) return false;
        }
        return true;
    }

    private IEnumerator RunMantle(Vector3 grip, Vector3 stand, Vector3 facing)
    {
        busy = true;
        state.ChangeState(PlayerStateManager.State.Mantling);

        Vector3 right = Vector3.Cross(Vector3.up, facing);
        float half = HalfHeight;
        float visualScale = contactIK != null ? Mathf.Abs(contactIK.transform.lossyScale.y) : 3f;
        Vector3 gripLeft = grip - right * gripHalfWidth;
        Vector3 gripRight = grip + right * gripHalfWidth;
        Quaternion startRotation = transform.rotation;
        Quaternion endRotation = Quaternion.LookRotation(facing, Vector3.up);
        Vector3 start = transform.position;
        Vector3 end = stand + Vector3.up * half;
        // ดึงตัวจนสะโพกพ้นขอบ แล้วจึงก้าวเข้าไปยืน
        Vector3 top = start + facing * .3f;
        top.y = stand.y + half * .7f;

        // 1) มือจับขอบ ดึงตัวขึ้น
        if (contactIK != null) contactIK.SetMantleAnimation(true, pullClimbRate);
        for (float t = 0f; t < pullDuration; t += Time.deltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / pullDuration);
            transform.position = Vector3.Lerp(start, top, k);
            transform.rotation = Quaternion.Slerp(startRotation, endRotation, k);
            if (handRig != null) handRig.SetMantleGrip(gripLeft, gripRight, Mathf.Clamp01(t / (pullDuration * .3f)));
            yield return null;
        }

        // 2) ก้าวขึ้นไปยืน: เท้าซ้ายขึ้นก่อน เท้าขวาตาม มือค่อยปล่อย
        if (contactIK != null) contactIK.SetMantleAnimation(false, 0f);
        Vector3 ankleOffset = Vector3.up * (.12f * visualScale) + facing * (.15f * visualScale);
        Vector3 landLeft = stand - right * (.17f * visualScale) + ankleOffset;
        Vector3 landRight = stand + right * (.17f * visualScale) + ankleOffset;
        Vector3 footStartLeft = landLeft, footStartRight = landRight;
        if (contactIK != null) contactIK.GetFootPositions(out footStartLeft, out footStartRight);
        Vector3 arc = Vector3.Lerp(top, end, .5f) + Vector3.up * (half * .22f);
        float lift = .35f * visualScale;

        for (float t = 0f; t < stepDuration; t += Time.deltaTime)
        {
            float raw = t / stepDuration;
            float k = Mathf.SmoothStep(0f, 1f, raw);
            transform.position = Vector3.Lerp(Vector3.Lerp(top, arc, k), Vector3.Lerp(arc, end, k), k);
            if (handRig != null) handRig.SetMantleGrip(gripLeft, gripRight, Mathf.Clamp01(1f - raw / .65f));

            float leftK = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0f, .55f, raw));
            float rightK = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.3f, .95f, raw));
            Vector3 left = Vector3.Lerp(footStartLeft, landLeft, leftK) + Vector3.up * (Mathf.Sin(leftK * Mathf.PI) * lift);
            Vector3 right2 = Vector3.Lerp(footStartRight, landRight, rightK) + Vector3.up * (Mathf.Sin(rightK * Mathf.PI) * lift);
            if (contactIK != null) contactIK.SetMantleFeet(left, right2, Mathf.Clamp01(raw * 5f));
            yield return null;
        }

        // 3) ทรงตัว ก่อนส่งให้ท่า Idle
        transform.position = end;
        if (handRig != null) handRig.ClearMantleGrip();
        for (float t = 0f; t < settleDuration; t += Time.deltaTime)
        {
            if (contactIK != null) contactIK.SetMantleFeet(landLeft, landRight, 1f - t / settleDuration);
            yield return null;
        }
        if (contactIK != null) contactIK.EndMantle();

        if (walker != null) walker.DelayWallAttachment(.4f);
        state.ChangeState(PlayerStateManager.State.Walking);
        cooldownUntil = Time.time + .6f;
        busy = false;
    }

    // เดินออกนอกขอบพื้น: ถ้าข้างล่างเป็นหน้าผาให้ปีนลงไปเกาะ
    public bool TryClimbDown(Vector3 direction)
    {
        ClimbableWall wall = MapGenerator.CurrentCliff;
        if (busy || wall == null || climb == null) return false;
        Vector3 facing = wall.Facing;
        if (Vector3.Dot(direction.normalized, -facing) < .3f) return false;

        Vector3 below = transform.position + Vector3.down * (HalfHeight + climbDownDrop);
        if (below.y < wall.MinClimbY) return false;
        if (!ClimbableWall.Raycast(below - facing * 12f, facing, 24f, out RaycastHit hit) || hit.normal.y > .7f) return false;

        Vector3 target = hit.point - facing * climb.surfaceOffset;
        target.y = below.y;
        StartCoroutine(RunClimbDown(target, facing, wall));
        return true;
    }

    private IEnumerator RunClimbDown(Vector3 target, Vector3 facing, ClimbableWall wall)
    {
        busy = true;
        state.ChangeState(PlayerStateManager.State.Mantling);
        if (contactIK != null) contactIK.SetMantleAnimation(true, -pullClimbRate);

        Vector3 start = transform.position;
        Quaternion startRotation = transform.rotation;
        Quaternion endRotation = Quaternion.LookRotation(facing, Vector3.up);
        for (float t = 0f; t < climbDownDuration; t += Time.deltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / climbDownDuration);
            transform.position = Vector3.Lerp(start, target, k);
            transform.rotation = Quaternion.Slerp(startRotation, endRotation, k);
            yield return null;
        }
        transform.position = target;
        transform.rotation = endRotation;

        if (contactIK != null) contactIK.EndMantle();
        state.ChangeState(PlayerStateManager.State.Climbing);
        if (cameraController != null) cameraController.SwitchToClimbCam(Quaternion.LookRotation(wall.ViewFacing).eulerAngles.y);
        cooldownUntil = Time.time + .6f;
        busy = false;
    }
}
