using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

// หน้าต่างกำหนดจังหวะและทิศ Parry ของบอส: เลื่อนดูท่าจากมุมกล้องฉากต่อสู้ แล้วกดเลือกทิศได้เลย
public class BossParryEditor : EditorWindow
{
    const string SetPath = "Assets/Resources/GuardianBossSet.asset";
    const float PanelWidth = 240f;
    const float TimelineHeight = 54f;
    const float MarkerRadius = 8f;

    static readonly Color WeaponColor = new Color(1f, 0.35f, 0.3f);
    static readonly Color SwipeColor = new Color(0.35f, 0.95f, 0.45f);

    BossAnimationSet set;
    PreviewRenderUtility preview;
    GameObject model;
    BladeProbe probe;
    Dictionary<string, AnimationClip> clips;

    int moveIndex;
    int selected = -1;
    float time;
    bool playing;
    double lastTick;
    bool draggingStrike;

    [MenuItem("Tools/ASCENDIA/Boss Parry Editor")]
    static void Open()
    {
        GetWindow<BossParryEditor>("Boss Parry");
    }

    void OnEnable()
    {
        minSize = new Vector2(720f, 420f);
        set = AssetDatabase.LoadAssetAtPath<BossAnimationSet>(SetPath);
        if (set == null || set.model == null) return;

        clips = AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(set.model)).OfType<AnimationClip>()
            .Where(c => !c.name.StartsWith("__preview")).ToDictionary(c => c.name);

        preview = new PreviewRenderUtility();
        preview.camera.clearFlags = CameraClearFlags.SolidColor;
        preview.camera.backgroundColor = new Color(0.17f, 0.19f, 0.23f);
        preview.camera.nearClipPlane = 0.1f;
        preview.camera.farClipPlane = 200f;
        preview.camera.fieldOfView = 38f;
        preview.lights[0].intensity = 1.2f;
        preview.lights[0].transform.rotation = Quaternion.Euler(40f, 20f, 0f);
        preview.lights[1].intensity = 0.7f;
        preview.ambientColor = new Color(0.45f, 0.45f, 0.5f);

        model = preview.InstantiatePrefabInScene(set.model);
        probe = new BladeProbe(model);

        lastTick = EditorApplication.timeSinceStartup;
        EditorApplication.update += Tick;
        Undo.undoRedoPerformed += Repaint;
        SelectStrike(0);
    }

    void OnDisable()
    {
        EditorApplication.update -= Tick;
        Undo.undoRedoPerformed -= Repaint;
        probe?.Dispose();
        preview?.Cleanup();
        if (set != null) AssetDatabase.SaveAssetIfDirty(set);
    }

    void Tick()
    {
        double now = EditorApplication.timeSinceStartup;
        float delta = (float)(now - lastTick);
        lastTick = now;
        if (!playing || CurrentClip == null) return;

        time = Mathf.Repeat(time + delta / CurrentClip.length, 1f);
        Repaint();
    }

    List<BossMove> Moves
    {
        get
        {
            var moves = new List<BossMove>(set.basicAttacks);
            if (set.dance != null && !string.IsNullOrEmpty(set.dance.state)) moves.Add(set.dance);
            return moves;
        }
    }

    BossMove CurrentMove
    {
        get
        {
            List<BossMove> moves = Moves;
            return moves.Count > 0 ? moves[Mathf.Clamp(moveIndex, 0, moves.Count - 1)] : null;
        }
    }

    AnimationClip CurrentClip
    {
        get
        {
            BossMove move = CurrentMove;
            return move != null && clips.TryGetValue(move.state, out AnimationClip clip) ? clip : null;
        }
    }

    BossStrike SelectedStrike
    {
        get
        {
            BossMove move = CurrentMove;
            return move != null && selected >= 0 && selected < move.strikes.Count ? move.strikes[selected] : null;
        }
    }

    void SelectStrike(int index)
    {
        BossMove move = CurrentMove;
        if (move == null || move.strikes.Count == 0)
        {
            selected = -1;
            return;
        }
        selected = Mathf.Clamp(index, 0, move.strikes.Count - 1);
        time = move.strikes[selected].time;
        playing = false;
    }

    void BeginChange(string label)
    {
        Undo.RecordObject(set, label);
        CurrentMove.edited = true;
    }

    void Commit()
    {
        EditorUtility.SetDirty(set);
        AssetDatabase.SaveAssetIfDirty(set);
    }

    int AutoDirection(BossStrike strike)
    {
        return ParryDirections.FromWeaponSwing(model.transform.rotation * strike.swing, preview.camera.transform);
    }

    int FinalDirection(BossStrike strike)
    {
        return strike.direction != ParryDirections.Auto ? strike.direction : AutoDirection(strike);
    }

    void OnGUI()
    {
        if (set == null || model == null)
        {
            EditorGUILayout.HelpBox("ยังไม่มี GuardianBossSet ให้สั่ง Tools/ASCENDIA/Build Boss Animator ก่อน", MessageType.Warning);
            return;
        }

        DrawMoveTabs();
        using (new GUILayout.HorizontalScope())
        {
            using (new GUILayout.VerticalScope())
            {
                DrawPreview(GUILayoutUtility.GetRect(100f, 4000f, 200f, 4000f, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true)));
                DrawTimeline(GUILayoutUtility.GetRect(100f, 4000f, TimelineHeight, TimelineHeight, GUILayout.ExpandWidth(true)));
            }
            using (new GUILayout.VerticalScope(GUILayout.Width(PanelWidth)))
            {
                DrawStrikePanel();
            }
        }
    }

    void DrawMoveTabs()
    {
        string[] names = Moves.Select(m => m.edited ? m.state + " *" : m.state).ToArray();
        int picked = GUILayout.Toolbar(Mathf.Clamp(moveIndex, 0, names.Length - 1), names);
        if (picked == moveIndex) return;
        moveIndex = picked;
        SelectStrike(0);
    }

    void DrawPreview(Rect view)
    {
        if (Event.current.type != EventType.Repaint || CurrentClip == null) return;

        preview.BeginPreview(view, GUIStyle.none);
        RenderPose(view.width / Mathf.Max(1f, view.height));
        GUI.DrawTexture(view, preview.EndPreview(), ScaleMode.StretchToFill, false);
        DrawOverlay(view);
    }

    void RenderPose(float aspect)
    {
        model.transform.SetPositionAndRotation(Vector3.zero, Quaternion.Euler(0f, set.previewBossYaw, 0f));
        CurrentClip.SampleAnimation(model, time * CurrentClip.length);

        Quaternion rotation = Quaternion.Euler(set.previewCameraEuler);
        Vector3 focus = Vector3.up * (set.modelHeight * 0.55f);
        float distance = set.modelHeight * 3.6f;
        preview.camera.transform.SetPositionAndRotation(focus - rotation * Vector3.forward * distance, rotation);
        preview.camera.aspect = aspect;
        preview.Render(true, false);
    }

    // จุดเหลืองคือที่ Hint จะโผล่ ลูกศรแดงคือทิศดาบบนจอ ลูกศรเขียวคือทิศที่ผู้เล่นต้องลาก
    void DrawOverlay(Rect view)
    {
        BossStrike strike = SelectedStrike;
        if (strike == null) return;

        Vector3 bodyCenter = model.transform.TransformPoint(Vector3.up * (set.modelHeight * 0.5f));
        Vector3 viewport = preview.camera.WorldToViewportPoint(bodyCenter);
        Vector2 point = new Vector2(view.x + viewport.x * view.width, view.y + (1f - viewport.y) * view.height);

        Vector2 weapon = ParryDirections.ToScreen(model.transform.rotation * strike.swing, preview.camera.transform);
        ParryDirectionPad.DrawArrow(point + new Vector2(weapon.x, -weapon.y).normalized * -34f, weapon, 44f, WeaponColor, 4f);
        ParryDirectionPad.DrawArrow(point, ParryDirections.ToVector(FinalDirection(strike)), 92f, SwipeColor, 6f);

        Handles.color = Color.yellow;
        Handles.DrawWireDisc(point, Vector3.forward, 12f, 2f);

        GUI.Label(new Rect(view.x + 8f, view.yMax - 24f, 400f, 20f), "จุดเหลือง = Hint   แดง = ทิศดาบ   เขียว = ทิศที่ต้องลาก", EditorStyles.whiteLabel);
    }

    void DrawTimeline(Rect bar)
    {
        BossMove move = CurrentMove;
        if (move == null) return;

        Rect track = new Rect(bar.x + 14f, bar.y + 12f, bar.width - 28f, bar.height - 24f);
        float XOf(float t) => track.x + t * track.width;
        float TimeOf(float x) => Mathf.Clamp01((x - track.x) / track.width);

        int id = GUIUtility.GetControlID(FocusType.Passive);
        Event e = Event.current;

        switch (e.GetTypeForControl(id))
        {
            case EventType.Repaint:
                EditorGUI.DrawRect(bar, new Color(0.13f, 0.14f, 0.17f));
                EditorGUI.DrawRect(new Rect(track.x, track.center.y - 1f, track.width, 2f), new Color(0.4f, 0.42f, 0.48f));
                for (int i = 0; i < move.strikes.Count; i++)
                {
                    Vector2 center = new Vector2(XOf(move.strikes[i].time), track.center.y);
                    EditorGUI.DrawRect(new Rect(center.x - MarkerRadius, center.y - MarkerRadius, MarkerRadius * 2f, MarkerRadius * 2f),
                        i == selected ? Color.yellow : new Color(0.85f, 0.85f, 0.9f));
                    GUI.Label(new Rect(center.x - 10f, center.y - 10f, 20f, 20f), (i + 1).ToString(), CenteredBlackLabel);
                }
                EditorGUI.DrawRect(new Rect(XOf(time) - 1f, bar.y + 3f, 2f, bar.height - 6f), new Color(1f, 0.45f, 0.3f));
                break;

            case EventType.MouseDown:
                if (e.button != 0 || !bar.Contains(e.mousePosition)) break;
                int hit = move.strikes.FindIndex(s => Mathf.Abs(XOf(s.time) - e.mousePosition.x) <= MarkerRadius + 2f);
                if (hit >= 0)
                {
                    SelectStrike(hit);
                    draggingStrike = true;
                }
                else
                {
                    playing = false;
                    draggingStrike = false;
                    time = TimeOf(e.mousePosition.x);
                }
                GUIUtility.hotControl = id;
                e.Use();
                break;

            case EventType.MouseDrag:
                if (GUIUtility.hotControl != id) break;
                if (draggingStrike) MoveSelectedStrike(TimeOf(e.mousePosition.x));
                else time = TimeOf(e.mousePosition.x);
                e.Use();
                break;

            case EventType.MouseUp:
                if (GUIUtility.hotControl != id) break;
                GUIUtility.hotControl = 0;
                if (draggingStrike) Commit();
                draggingStrike = false;
                e.Use();
                break;
        }
    }

    static GUIStyle centeredBlackLabel;
    static GUIStyle CenteredBlackLabel
    {
        get
        {
            if (centeredBlackLabel == null)
            {
                centeredBlackLabel = new GUIStyle(EditorStyles.miniBoldLabel) { alignment = TextAnchor.MiddleCenter };
                centeredBlackLabel.normal.textColor = Color.black;
            }
            return centeredBlackLabel;
        }
    }

    void MoveSelectedStrike(float newTime)
    {
        BossStrike strike = SelectedStrike;
        if (strike == null) return;

        BeginChange("Move Parry Strike");
        BossStrike sampled = probe.MakeStrike(CurrentClip, newTime);
        strike.time = newTime;
        strike.swing = sampled.swing;

        BossMove move = CurrentMove;
        move.strikes.Sort((a, b) => a.time.CompareTo(b.time));
        selected = move.strikes.IndexOf(strike);
        time = newTime;
    }

    void DrawStrikePanel()
    {
        BossMove move = CurrentMove;
        if (move == null) return;

        GUILayout.Label(move.state + $"   {move.length:0.00} วินาที", EditorStyles.boldLabel);
        if (GUILayout.Button(playing ? "หยุด" : "เล่นท่านี้")) playing = !playing;

        using (new GUILayout.HorizontalScope())
        {
            if (GUILayout.Button("เพิ่มจังหวะตรงนี้")) AddStrikeAtCursor();
            using (new EditorGUI.DisabledScope(SelectedStrike == null))
            {
                if (GUILayout.Button("ลบจังหวะนี้")) DeleteSelectedStrike();
            }
        }

        GUILayout.Space(10f);
        BossStrike strike = SelectedStrike;
        if (strike != null)
        {
            using (new GUILayout.HorizontalScope())
            {
                if (GUILayout.Button("<", GUILayout.Width(28f))) SelectStrike(selected - 1);
                GUILayout.Label($"จังหวะที่ {selected + 1} / {move.strikes.Count}", EditorStyles.boldLabel);
                if (GUILayout.Button(">", GUILayout.Width(28f))) SelectStrike(selected + 1);
            }

            EditorGUI.BeginChangeCheck();
            float newTime = EditorGUILayout.Slider(strike.time, 0f, 1f);
            if (EditorGUI.EndChangeCheck())
            {
                MoveSelectedStrike(newTime);
                Commit();
            }

            GUILayout.Space(8f);
            GUILayout.Label("ทิศที่ผู้เล่นต้องลาก (A = อัตโนมัติ)");
            Rect padRect = GUILayoutUtility.GetRect(132f, 132f, GUILayout.Width(132f), GUILayout.Height(132f));
            int auto = AutoDirection(strike);
            int picked = ParryDirectionPad.Draw(padRect, strike.direction, true, auto);
            if (picked != strike.direction)
            {
                BeginChange("Set Parry Direction");
                strike.direction = picked;
                Commit();
                strike = SelectedStrike;
            }

            string source = strike.direction == ParryDirections.Auto ? "อัตโนมัติ" : "กำหนดเอง";
            GUILayout.Label($"ผลลัพธ์: ลาก{ParryDirections.Name(FinalDirection(strike))} ({source})");
        }

        GUILayout.FlexibleSpace();
        if (GUILayout.Button("คืนค่าท่านี้จากอนิเมชัน")) ResetMove();
    }

    void AddStrikeAtCursor()
    {
        BeginChange("Add Parry Strike");
        BossMove move = CurrentMove;
        BossStrike strike = probe.MakeStrike(CurrentClip, time);
        move.strikes.Add(strike);
        move.strikes.Sort((a, b) => a.time.CompareTo(b.time));
        selected = move.strikes.IndexOf(strike);
        Commit();
    }

    void DeleteSelectedStrike()
    {
        BeginChange("Delete Parry Strike");
        CurrentMove.strikes.RemoveAt(selected);
        selected = Mathf.Min(selected, CurrentMove.strikes.Count - 1);
        Commit();
    }

    void ResetMove()
    {
        Undo.RecordObject(set, "Reset Parry Move");
        BossMove move = CurrentMove;
        move.strikes = AscendiaBuildBossAnimator.MakeMove(probe, CurrentClip).strikes;
        move.edited = false;
        SelectStrike(0);
        Commit();
    }
}
