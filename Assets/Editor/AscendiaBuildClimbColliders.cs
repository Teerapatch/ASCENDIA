using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

// สร้าง Collider ผิวปีนแบบกำหนดเองของ MountainTile แต่ละแบบ: ถอดความลึกของหินอาร์ตเป็นตาราง เกลี่ยให้เรียบ
// แล้วเก็บเป็น Mesh ใน Assets/_kin/Prefab/ClimbColliders และใส่เป็นลูก "ClimbCollider" ใน Prefab ของไทล์
// แก้หินอาร์ตแล้วสั่งเมนูนี้ใหม่ หรือจะแทน/เพิ่ม Collider เองใต้ ClimbCollider ก็ได้ (ต้องไม่ใช่ Trigger)
public static class AscendiaBuildClimbColliders
{
    const string Folder = "Assets/_kin/Prefab/ClimbColliders";
    const string ChildName = "ClimbCollider";
    const float Cell = 0.5f;          // ขนาดช่องตาราง (หน่วยอาร์ต)
    const float SideMargin = 2f;      // เผื่อซ้าย-ขวานอกขอบ ClimbBounds
    const float BelowMargin = 5f;     // เผื่อใต้จุดต่ำสุด
    const float AboveMargin = 8f;     // เผื่อเหนือจุดเลือก Node
    const float ProbeBack = 40f;
    const float Smoothing = 0.5f;     // 0 = ตามหินเป๊ะ, 1 = เฉลี่ยกับช่องรอบ ๆ ทั้งหมด

    static readonly string[] Tiles =
    {
        "Assets/_kin/Prefab/MountainTile_Normal.prefab",
        "Assets/_kin/Prefab/MountainTile_Normal 1.prefab",
        "Assets/_kin/Prefab/MountainTile_Normal 2.prefab",
    };

    [MenuItem("Tools/ASCENDIA/Build Climb Colliders")]
    public static void BuildFromMenu() { Debug.Log(Build()); }

    public static string Build()
    {
        var report = new StringBuilder();
        if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder(Path.GetDirectoryName(Folder).Replace('\\', '/'), Path.GetFileName(Folder));

        foreach (string path in Tiles)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) { report.AppendLine($"{path}: not found"); continue; }

            Mesh mesh = Sample(prefab, report);
            if (mesh == null) continue;

            string meshPath = $"{Folder}/{Path.GetFileNameWithoutExtension(path)}_ClimbCollider.asset";
            Mesh asset = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            if (asset == null)
            {
                AssetDatabase.CreateAsset(mesh, meshPath);
                asset = mesh;
            }
            else
            {
                EditorUtility.CopySerialized(mesh, asset);
                EditorUtility.SetDirty(asset);
                Object.DestroyImmediate(mesh);
            }

            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                Transform child = root.transform.Find(ChildName);
                if (child == null)
                {
                    child = new GameObject(ChildName).transform;
                    child.SetParent(root.transform, false);
                }
                child.localPosition = Vector3.zero;
                child.localRotation = Quaternion.identity;
                child.localScale = Vector3.one;
                child.gameObject.tag = ClimbableWall.WallTag;

                MeshCollider collider = child.GetComponent<MeshCollider>();
                if (collider == null) collider = child.gameObject.AddComponent<MeshCollider>();
                collider.sharedMesh = asset;

                root.GetComponent<ClimbableWall>().colliderRoot = child;
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        AssetDatabase.SaveAssets();
        return report.ToString();
    }

    // วาง Prefab ชั่วคราวไกล ๆ ในฉาก ใส่ MeshCollider ให้หินอาร์ต แล้วยิงเข้าหาหน้าผาเป็นตาราง
    static Mesh Sample(GameObject prefab, StringBuilder report)
    {
        GameObject instance = Object.Instantiate(prefab, new Vector3(5000f, 0f, 5000f), Quaternion.identity);
        instance.hideFlags = HideFlags.HideAndDontSave;
        try
        {
            ClimbableWall wall = instance.GetComponent<ClimbableWall>();
            if (wall == null || wall.bounds == null) { report.AppendLine($"{prefab.name}: no ClimbableWall/bounds"); return null; }

            Transform old = instance.transform.Find(ChildName);
            if (old != null) Object.DestroyImmediate(old.gameObject);

            var rocks = new HashSet<Collider>();
            Transform surface = wall.surfaceRoot != null ? wall.surfaceRoot : instance.transform;
            foreach (MeshFilter filter in surface.GetComponentsInChildren<MeshFilter>())
            {
                MeshRenderer renderer = filter.GetComponent<MeshRenderer>();
                if (filter.sharedMesh == null || renderer == null || !renderer.enabled) continue;
                MeshCollider rock = filter.gameObject.AddComponent<MeshCollider>();
                rock.sharedMesh = filter.sharedMesh;
                rocks.Add(rock);
            }
            Physics.SyncTransforms();

            Vector3 facing = wall.Facing;
            Vector3 right = Vector3.Cross(Vector3.up, facing).normalized;
            Vector3 center = wall.bounds.transform.TransformPoint(wall.bounds.center);
            float half = wall.bounds.size.x * 0.5f * Mathf.Abs(wall.bounds.transform.lossyScale.x) + SideMargin;
            float yMin = wall.MinClimbY - BelowMargin, yMax = wall.MaxClimbY + AboveMargin;
            int nx = Mathf.CeilToInt(2f * half / Cell) + 1, ny = Mathf.CeilToInt((yMax - yMin) / Cell) + 1;

            var depth = new float[nx, ny];
            var known = new bool[nx, ny];
            int hits = 0;
            for (int ix = 0; ix < nx; ix++)
            {
                for (int iy = 0; iy < ny; iy++)
                {
                    Vector3 origin = GridPoint(center, right, half, yMin, ix, iy) - facing * ProbeBack;
                    float best = float.MaxValue;
                    foreach (RaycastHit hit in Physics.RaycastAll(origin, facing, ProbeBack * 3f, ~0, QueryTriggerInteraction.Ignore))
                    {
                        if (hit.distance < best && rocks.Contains(hit.collider)) best = hit.distance;
                    }
                    if (best == float.MaxValue) continue;
                    depth[ix, iy] = best - ProbeBack;
                    known[ix, iy] = true;
                    hits++;
                }
            }
            if (hits == 0) { report.AppendLine($"{prefab.name}: no rock hit"); return null; }

            FillGaps(depth, known, nx, ny);
            Smooth(depth, nx, ny);

            var vertices = new Vector3[nx * ny];
            float minDepth = float.MaxValue, maxDepth = float.MinValue;
            for (int ix = 0; ix < nx; ix++)
            {
                for (int iy = 0; iy < ny; iy++)
                {
                    Vector3 world = GridPoint(center, right, half, yMin, ix, iy) + facing * depth[ix, iy];
                    vertices[ix * ny + iy] = instance.transform.InverseTransformPoint(world);
                    minDepth = Mathf.Min(minDepth, depth[ix, iy]);
                    maxDepth = Mathf.Max(maxDepth, depth[ix, iy]);
                }
            }

            // หน้าสามเหลี่ยมหันออกจากหน้าผา (สวนทิศ facing) เพราะ MeshCollider รับรังสีด้านเดียว
            var triangles = new int[(nx - 1) * (ny - 1) * 6];
            int t = 0;
            for (int ix = 0; ix < nx - 1; ix++)
            {
                for (int iy = 0; iy < ny - 1; iy++)
                {
                    int a = ix * ny + iy, b = (ix + 1) * ny + iy, c = ix * ny + iy + 1, d = (ix + 1) * ny + iy + 1;
                    triangles[t++] = a; triangles[t++] = c; triangles[t++] = b;
                    triangles[t++] = b; triangles[t++] = c; triangles[t++] = d;
                }
            }

            var mesh = new Mesh { name = prefab.name + "_ClimbCollider" };
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            float facingDot = Vector3.Dot(instance.transform.TransformDirection(mesh.normals[(nx / 2) * ny + ny / 2]), facing);
            report.AppendLine($"{prefab.name}: grid {nx}x{ny} rock hits {hits}/{nx * ny} depth {minDepth:0.0}..{maxDepth:0.0} normal·facing {facingDot:0.00}");
            return mesh;
        }
        finally
        {
            Object.DestroyImmediate(instance);
        }
    }

    static Vector3 GridPoint(Vector3 center, Vector3 right, float half, float yMin, int ix, int iy)
    {
        Vector3 point = center + right * (-half + ix * Cell);
        point.y = yMin + iy * Cell;
        return point;
    }

    // ช่องที่ยิงไม่โดนหิน (ช่องว่างระหว่างก้อน) เติมจากช่องข้างเคียง ผิวจึงต่อเนื่องไม่มีรู
    static void FillGaps(float[,] depth, bool[,] known, int nx, int ny)
    {
        for (int pass = 0; pass < nx + ny; pass++)
        {
            var filled = new List<(int x, int y, float value)>();
            for (int ix = 0; ix < nx; ix++)
            {
                for (int iy = 0; iy < ny; iy++)
                {
                    if (known[ix, iy]) continue;
                    float sum = 0f;
                    int count = 0;
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        for (int dy = -1; dy <= 1; dy++)
                        {
                            int x = ix + dx, y = iy + dy;
                            if (x < 0 || y < 0 || x >= nx || y >= ny || !known[x, y]) continue;
                            sum += depth[x, y];
                            count++;
                        }
                    }
                    if (count > 0) filled.Add((ix, iy, sum / count));
                }
            }
            if (filled.Count == 0) return;
            foreach (var cell in filled)
            {
                depth[cell.x, cell.y] = cell.value;
                known[cell.x, cell.y] = true;
            }
        }
    }

    static void Smooth(float[,] depth, int nx, int ny)
    {
        var source = (float[,])depth.Clone();
        for (int ix = 0; ix < nx; ix++)
        {
            for (int iy = 0; iy < ny; iy++)
            {
                float sum = 0f;
                int count = 0;
                // เกลี่ยเฉพาะแนวซ้าย-ขวา: ถ้าเกลี่ยแนวตั้งด้วย ชะง่อนพื้นเรียบจะกลายเป็นทางลาดและยืนไม่ได้
                for (int dx = -1; dx <= 1; dx++)
                {
                    int x = ix + dx;
                    if (x < 0 || x >= nx) continue;
                    sum += source[x, iy];
                    count++;
                }
                depth[ix, iy] = Mathf.Lerp(source[ix, iy], sum / count, Smoothing);
            }
        }
    }
}
