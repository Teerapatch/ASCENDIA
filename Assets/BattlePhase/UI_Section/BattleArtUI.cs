using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// ผูก UI ของอาร์ต (BattleUI และ SkillsUI คัดมาจาก ENVI_Battle2) เข้ากับระบบต่อสู้เดิม
// ต้องรันก่อน PlayerUIManager และตัวจัดการอื่น จึงตั้ง Execution Order ต่ำ
[DefaultExecutionOrder(-500)]
public class BattleArtUI : MonoBehaviour
{
    private class PassiveInfo
    {
        public string title;
        public string description;
    }

    private static readonly Dictionary<WeaponType, PassiveInfo> Passives = new Dictionary<WeaponType, PassiveInfo>
    {
        { WeaponType.Dagger, new PassiveInfo { title = "Dagger Combo", description = "Stack Combos are gained through normal attacks or parrying with a DAGGER." } },
        { WeaponType.Gun, new PassiveInfo { title = "Counter Shot", description = "Every AP that over the cap. Gun will shot automatically" } },
        { WeaponType.LongSword, new PassiveInfo { title = "Heavy Blade", description = "Has high posture damage and do high damage to STAGGER enemy." } },
        { WeaponType.Spell, new PassiveInfo { title = "Elemental Sigil", description = "Spell have different Sigil to keep and use in the right moment" } },
    };

    private PlayerCombatController player;
    private Image passiveIcon;
    private TextMeshProUGUI passiveTitle, passiveDescription, passiveCount;
    private WeaponData shownWeapon;
    private int shownTurn = -1;
    private CanvasGroup hudGroup;
    private Transform hud;

    private class ActionEntry
    {
        public RectTransform frame;
        public Image frameImage;
        public CanvasGroup labelGroup;
        public RectTransform label;
        public Vector2 framePosition, labelPosition;
    }

    private readonly List<ActionEntry> actionEntries = new List<ActionEntry>();
    private CanvasGroup cloakGroup;
    private RectTransform passiveRect;
    private CanvasGroup passiveGroup;
    private Image passiveBrush;
    private Vector2 passivePosition;
    private Vector3 passiveScale;
    private Coroutine actionIntro, passiveIntro;

    private void Awake()
    {
        player = FindAnyObjectByType<PlayerCombatController>(FindObjectsInactive.Include);
        PlayerUIManager menus = FindAnyObjectByType<PlayerUIManager>(FindObjectsInactive.Include);
        if (player == null || menus == null) return;

        // Turn Order, Passive และช่องอาวุธอยู่บน Canvas ปกติ (BattleHUD) แยกจาก BattleUI ที่เป็น World Space
        GameObject hudObject = GameObject.Find("BattleHUD");
        hud = hudObject != null ? hudObject.transform : transform;

        BindActions(menus);
        BindAP();
        BindSkills(menus);
        BindWeapons();
        BindTurnOrder();
        BindPassive();

        hudGroup = gameObject.AddComponent<CanvasGroup>();
    }

    private void BindActions(PlayerUIManager menus)
    {
        Transform frame = transform.Find("Actions_Frame");

        // AP อยู่ใน Actions_Frame ของอาร์ต แต่ต้องแสดงตลอด แม้เมนูจะซ่อน
        frame.Find("AP_Frame").SetParent(transform, true);

        if (menus.actionMenuPanel != null) menus.actionMenuPanel.gameObject.SetActive(false);
        menus.actionMenuPanel = frame.gameObject.AddComponent<CanvasGroup>();

        AddAction(frame.Find("Actions_AttackFrame"), new Vector4(202f, 140f, 208f, 150f), player.Command_BasicAttack);
        AddAction(frame.Find("Actions_SkillsFrame"), new Vector4(310f, 208f, 174f, 162f), player.Command_Skill);
        AddAction(frame.Find("Actiions_SwitchFrame"), new Vector4(186f, 126f, 208f, 214f), player.Command_SwitchWeapon);

        cloakGroup = frame.Find("Actions_Cloak").gameObject.AddComponent<CanvasGroup>();
        menus.ActionMenuShown += PlayActionIntro;
    }

    private void AddAction(Transform frame, Vector4 padding, UnityEngine.Events.UnityAction onClick)
    {
        Image frameImage = frame.GetComponent<Image>();
        Image label = frame.Find("Image (2)").GetComponent<Image>();
        ArtButtonHover.Make(frame, padding, onClick, label);

        // ลายพู่กันค่อย ๆ ปาดออกมาตอนเมนูขึ้น
        frameImage.type = Image.Type.Filled;
        frameImage.fillMethod = Image.FillMethod.Horizontal;
        frameImage.fillOrigin = (int)Image.OriginHorizontal.Left;

        actionEntries.Add(new ActionEntry
        {
            frame = (RectTransform)frame,
            frameImage = frameImage,
            labelGroup = label.gameObject.AddComponent<CanvasGroup>(),
            label = (RectTransform)label.transform,
            framePosition = ((RectTransform)frame).anchoredPosition,
            labelPosition = ((RectTransform)label.transform).anchoredPosition,
        });
    }

    private void BindAP()
    {
        Transform ap = transform.Find("AP_Frame");
        player.textAP = ap.Find("Text (TMP)").GetComponent<TextMeshProUGUI>();
        player.apFillImage = ap.Find("Image (2)").GetComponent<Image>();
        player.showMaxAP = false;
    }

    private void BindSkills(PlayerUIManager menus)
    {
        Transform canvasRoot = player.transform.Find("SkillsUI");
        Transform pose = player.transform.Find("POSE_ATTACK");

        // อาร์ตวางไว้ใต้ POSE_ATTACK ซึ่งถูกซ่อนตอนเล่น จึงย้ายมาอยู่ตำแหน่งเดียวกันใต้ Player_Holder
        if (pose != null)
        {
            canvasRoot.SetPositionAndRotation(pose.TransformPoint(canvasRoot.localPosition), pose.rotation * canvasRoot.localRotation);
        }
        canvasRoot.GetComponent<Canvas>().worldCamera = Camera.main;

        Transform grid = canvasRoot.Find("Skills_Grid");
        Transform template = grid.Find("Image (1)");
        template.SetParent(canvasRoot, false);
        template.gameObject.SetActive(false);
        foreach (Transform old in grid) Destroy(old.gameObject);

        Transform card = template.GetChild(0);
        card.Find("Image (1)/Text (TMP) (2)").name = "Cost";
        card.Find("Text (TMP) (4)").name = "Name";
        card.Find("Text (TMP) (5)").name = "Description";
        Image frame = card.GetComponent<Image>();
        frame.raycastTarget = true;
        Button button = template.gameObject.AddComponent<Button>();
        button.transition = Selectable.Transition.None;
        button.targetGraphic = frame;
        card.gameObject.AddComponent<ArtButtonHover>();

        if (menus.skillMenuPanel != null) menus.skillMenuPanel.gameObject.SetActive(false);
        // PlayerUIManager ย่อ/ขยาย Panel ที่ 0.8-1 จึงต้องใส่บน Skills_Grid ไม่ใช่ตัว Canvas ที่สเกลเล็กมาก
        menus.skillMenuPanel = grid.gameObject.AddComponent<CanvasGroup>();
        menus.skillListContainer = grid;
        menus.skillButtonPrefab = template.gameObject;
    }

    private void BindWeapons()
    {
        WeaponUIManager manager = FindAnyObjectByType<WeaponUIManager>(FindObjectsInactive.Include);
        if (manager == null) return;

        Transform grid = hud.Find("Weapons_Grid");
        Transform slot = grid.Find("Slot1");
        slot.SetParent(hud, false);
        slot.gameObject.SetActive(false);

        WeaponSlotUI ui = slot.gameObject.AddComponent<WeaponSlotUI>();
        ui.weaponImage = slot.GetComponent<Image>();
        ui.durabilityText = slot.Find("Text (TMP) (2)").GetComponent<TextMeshProUGUI>();
        ui.maxDurabilityText = slot.Find("Text (TMP) (3)").GetComponent<TextMeshProUGUI>();
        ui.durabilityFillImage = slot.Find("Image (6)/Image (7)").GetComponent<Image>();
        ui.gaugeColor = Color.white;
        ui.activeScale = 1.08f;
        ui.activeYOffset = 60f;

        manager.weaponSlotPrefab = slot.gameObject;
        manager.container = grid;
    }

    private void BindTurnOrder()
    {
        TurnOrderUI order = FindAnyObjectByType<TurnOrderUI>(FindObjectsInactive.Include);
        if (order == null) return;

        Transform frame = hud.Find("Turnorder");
        RectTransform container = (RectTransform)frame.GetChild(1);
        Transform template = container.GetChild(0);

        // ชื่อศัตรูใช้ตัวหนังสือจากช่องตัวอย่างของอาร์ต ส่วนผู้เล่นใช้รูปอาวุธ
        Transform sampleName = container.GetChild(1).GetChild(0);
        TextMeshProUGUI name = Instantiate(sampleName.gameObject, template).GetComponent<TextMeshProUGUI>();

        template.SetParent(frame, false);
        template.gameObject.SetActive(false);
        DestroyImmediate(container.GetComponent<GridLayoutGroup>());
        foreach (Transform old in container) Destroy(old.gameObject);

        RectTransform rect = (RectTransform)template;
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(600f, 250f);

        template.gameObject.AddComponent<CanvasGroup>();
        TurnPortraitUI portrait = template.gameObject.AddComponent<TurnPortraitUI>();
        portrait.nameText = name;
        portrait.background = template.GetComponent<Image>();
        portrait.iconImage = template.GetChild(0).GetComponent<Image>();
        portrait.activeScale = 1.15f;

        order.portraitPrefab = template.gameObject;
        order.container = container;
        order.xOffsetPerItem = 415f;
        order.ySpacing = 0f;
        order.startOffset = new Vector2(300f, 0f);
        order.spawnOffset = new Vector2(600f, 0f);
        order.exitOffset = new Vector2(-600f, 0f);

        Transform oldCanvas = order.transform.parent;
        Image oldImage = order.GetComponent<Image>();
        if (oldImage != null) oldImage.enabled = false;
        Mask oldMask = order.GetComponent<Mask>();
        if (oldMask != null) oldMask.enabled = false;
        foreach (Transform child in order.transform) child.gameObject.SetActive(false);

        foreach (string oldName in new[] { "AP", "TurnOrder", "DaggerPassive_Panel", "GunPassive_Panel", "LongSwordPassive_Panel", "SpellPassive_Panel" })
        {
            Transform old = oldCanvas.Find(oldName);
            if (old != null) old.gameObject.SetActive(false);
        }

        // Canvas เดิมอยู่บนสุด ส่วนที่ไม่ใช่ปุ่มต้องไม่บังการคลิก UI ใหม่
        foreach (Graphic graphic in oldCanvas.GetComponentsInChildren<Graphic>(true))
        {
            if (graphic.GetComponentInParent<Selectable>(true) == null) graphic.raycastTarget = false;
        }
    }

    private void BindPassive()
    {
        Transform frame = hud.Find("Passive_Frame");
        passiveIcon = frame.GetComponent<Image>();
        passiveTitle = frame.GetChild(2).GetComponent<TextMeshProUGUI>();
        passiveDescription = frame.GetChild(3).GetComponent<TextMeshProUGUI>();
        passiveCount = frame.GetChild(6).GetComponent<TextMeshProUGUI>();

        passiveRect = (RectTransform)frame;
        passivePosition = passiveRect.anchoredPosition;
        passiveScale = passiveRect.localScale;
        passiveGroup = frame.gameObject.AddComponent<CanvasGroup>();
        passiveBrush = frame.GetChild(0).GetComponent<Image>();
        passiveBrush.type = Image.Type.Filled;
        passiveBrush.fillMethod = Image.FillMethod.Horizontal;
        passiveBrush.fillOrigin = (int)Image.OriginHorizontal.Right;
    }

    private void PlayActionIntro()
    {
        if (actionIntro != null) StopCoroutine(actionIntro);
        actionIntro = StartCoroutine(ActionIntroRoutine());
    }

    // เมนูหลัก: ผ้าคลุมจางเข้า แล้วแต่ละแถวปาดพู่กันเข้ามาเรียงกัน ตัวหนังสือพุ่งตามหลัง
    private IEnumerator ActionIntroRoutine()
    {
        const float stagger = 0.08f, brushTime = 0.24f, labelTime = 0.2f;
        float total = stagger * (actionEntries.Count - 1) + brushTime + labelTime * 0.6f;

        for (float t = 0f; t < total; t += Time.unscaledDeltaTime)
        {
            cloakGroup.alpha = Mathf.Clamp01(t / 0.2f);
            for (int i = 0; i < actionEntries.Count; i++) ApplyActionEntry(actionEntries[i], t - stagger * i, brushTime, labelTime);
            yield return null;
        }

        cloakGroup.alpha = 1f;
        foreach (ActionEntry entry in actionEntries) ApplyActionEntry(entry, 10f, brushTime, labelTime);
        actionIntro = null;
    }

    private static void ApplyActionEntry(ActionEntry entry, float time, float brushTime, float labelTime)
    {
        float brush = EaseOut(time / brushTime);
        entry.frameImage.fillAmount = brush;
        entry.frame.anchoredPosition = entry.framePosition + new Vector2((brush - 1f) * 140f, 0f);

        float label = EaseOut((time - brushTime * 0.45f) / labelTime);
        entry.labelGroup.alpha = label;
        entry.label.anchoredPosition = entry.labelPosition + new Vector2((label - 1f) * 220f, 0f);
    }

    private static float EaseOut(float t)
    {
        t = Mathf.Clamp01(t);
        return 1f - (1f - t) * (1f - t) * (1f - t);
    }

    private void PlayPassiveIntro()
    {
        if (passiveIntro != null) StopCoroutine(passiveIntro);
        passiveIntro = StartCoroutine(PassiveIntroRoutine());
    }

    // แผงอาวุธด้านขวา: เลื่อนเข้าจากขอบจอ ปาดพู่กัน แล้วเด้งเข้าที่
    private IEnumerator PassiveIntroRoutine()
    {
        const float duration = 0.34f;
        for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
        {
            float k = EaseOut(t / duration);
            passiveGroup.alpha = Mathf.Clamp01(t / 0.12f);
            passiveBrush.fillAmount = k;
            passiveRect.anchoredPosition = passivePosition + new Vector2((1f - k) * 180f, 0f);
            passiveRect.localScale = passiveScale * Mathf.Lerp(1.12f, 1f, k);
            yield return null;
        }

        passiveGroup.alpha = 1f;
        passiveBrush.fillAmount = 1f;
        passiveRect.anchoredPosition = passivePosition;
        passiveRect.localScale = passiveScale;
        passiveIntro = null;
    }

    private void Update()
    {
        // UI เป็น World Space วางหน้ากล้องภาพรวม พอกล้องซูมไปจุดอื่นจึงจางออก ไม่ให้บังภาพ
        if (hudGroup != null && BattleCameraController.Instance != null)
        {
            bool visible = BattleCameraController.Instance.CurrentState == CameraState.Overview;
            hudGroup.alpha = Mathf.MoveTowards(hudGroup.alpha, visible ? 1f : 0f, Time.unscaledDeltaTime * 8f);
            hudGroup.blocksRaycasts = visible;
        }

        if (player == null || player.ActiveWeapon == null || passiveTitle == null) return;

        WeaponData weapon = player.ActiveWeapon;
        Passives.TryGetValue(weapon.weaponType, out PassiveInfo info);
        // เล่น Effect เมื่ออาวุธเปลี่ยน หรือเริ่มเทิร์นใหม่ของผู้เล่น
        int turn = CombatManager.Instance != null && CombatManager.Instance.currentState == CombatState.PlayerTurn ? CombatManager.Instance.TurnNumber : shownTurn;
        if (weapon != shownWeapon || turn != shownTurn)
        {
            shownTurn = turn;
            PlayPassiveIntro();
        }

        if (weapon != shownWeapon)
        {
            shownWeapon = weapon;
            passiveIcon.sprite = weapon.weaponIcon;
            passiveTitle.text = info != null ? info.title : weapon.weaponName;
            passiveDescription.text = info != null ? info.description : "";
        }

        switch (weapon.weaponType)
        {
            case WeaponType.Dagger:
                passiveCount.text = player.currentComboStacks.ToString();
                break;
            case WeaponType.Gun:
                passiveCount.text = player.totalGunCounterAttacks.ToString();
                break;
            case WeaponType.Spell:
                passiveCount.text = player.elementSlots.Count.ToString();
                break;
            default:
                passiveCount.text = "";
                break;
        }
    }
}
