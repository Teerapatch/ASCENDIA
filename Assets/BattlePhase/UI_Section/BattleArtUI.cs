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
    private SigilSlotsUI sigilSlots; // ช่องธาตุ 5 ช่องของ Wand แทนตัวเลข
    [Tooltip("Prefab ช่องธาตุของ Wand (Weapon/Spell/SigilSlots.prefab) ว่าง = Resources/SigilSlots หรือสร้างจากโค้ด")]
    public GameObject sigilSlotsPrefab;
    [Tooltip("ใช้ตำแหน่งใน RectTransform ของ Prefab SigilSlots (จัดเองใน Prefab) ปิด = วางตรงตัวเลข Passive")]
    public bool sigilUsePrefabPosition = true;
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

        // อาร์ต ReArt v2 เปิด Raycast Target ทุกภาพ ภาพใหญ่จะบังปุ่ม ปิดหมดก่อน ปุ่มที่ผูกจะเปิดของตัวเอง
        foreach (Graphic graphic in GetComponentsInChildren<Graphic>(true)) graphic.raycastTarget = false;

        // Turn Order, Passive และช่องอาวุธอยู่บน Canvas ปกติ (BattleHUD) แยกจาก BattleUI ที่เป็น World Space
        GameObject hudObject = GameObject.Find("BattleHUD");
        hud = hudObject != null ? hudObject.transform : transform;

        BindActions(menus);
        BindAP();
        BindSkills(menus);
        BindWeapons();
        BindTurnOrder();
        BindPassive();

        // ชิ้นใหม่ของ UI ชุด ReArt v2 (ถ้ามี)
        BindQTE();
        BindAnnouncement();
        BindCrosshair();
        BindEnemyHUD();

        // UI ชุดเก่าเป็น World Space วางหน้ากล้องภาพรวม จึงต้องจางออกเมื่อกล้องย้าย
        // ชุดใหม่ (ReArt v2) เป็น Overlay รวม HUD ไว้ด้วยกัน ต้องแสดงตลอด (เมนูคำสั่งซ่อนเองผ่าน PlayerUIManager)
        if (GetComponent<Canvas>().renderMode == RenderMode.WorldSpace) hudGroup = gameObject.AddComponent<CanvasGroup>();
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
        // ชุด v2: การ์ดสกิล Skills_Ex อยู่บน BattleUI (Overlay) ใช้แทน SkillsUI แบบ World Space ชุดเก่า
        Transform example = transform.Find("Skills_Ex");
        if (example != null)
        {
            BindSkillCards(menus, example);
            return;
        }

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

    [Header("การ์ดสกิล (ReArt v2)")]
    [Tooltip("ตำแหน่งกองการ์ดบนจอ (อ้างอิง 1920x1080 จากมุมซ้ายล่าง)")]
    public Vector2 skillCardsPosition = new Vector2(430f, 640f);
    [Tooltip("ระยะห่างระหว่างการ์ดแต่ละใบ (เลื่อนลงและเยื้องขวาเล็กน้อยแบบพู่กัน)")]
    public Vector2 skillCardStep = new Vector2(40f, -210f);
    public float skillCardScale = 0.42f;

    private void BindSkillCards(PlayerUIManager menus, Transform example)
    {
        // กองการ์ดสกิล: ใช้ Skills_List ที่วางไว้ใน Hierarchy ถ้ามี (Tools/ASCENDIA/Bake Runtime UI) ไม่มีค่อยสร้างเอง
        RectTransform list = transform.Find("Skills_List") as RectTransform;
        if (list == null) list = CreateSkillList(transform, skillCardsPosition, skillCardStep);
        if (list.GetComponent<SkillCardStack>() == null) list.gameObject.AddComponent<SkillCardStack>().step = skillCardStep;

        Transform template = example;
        template.SetParent(transform, false);
        template.gameObject.SetActive(false);
        RectTransform templateRect = (RectTransform)template;
        templateRect.anchorMin = templateRect.anchorMax = new Vector2(0.5f, 0.5f);
        templateRect.anchoredPosition = Vector2.zero;
        templateRect.localScale = Vector3.one * skillCardScale;
        Rename(template, "Image (1)/Text (TMP) (2)", "Cost");
        Rename(template, "Text (TMP) (4)", "Name");
        Rename(template, "Text (TMP) (5)", "Description");

        Image frame = template.GetComponent<Image>();
        if (frame == null) frame = template.GetComponentInChildren<Image>(true);
        frame.raycastTarget = true;
        Button button = template.gameObject.AddComponent<Button>();
        button.transition = Selectable.Transition.None;
        button.targetGraphic = frame;
        template.gameObject.AddComponent<ArtButtonHover>();

        if (menus.skillMenuPanel != null) menus.skillMenuPanel.gameObject.SetActive(false);
        Transform oldSkills = player.transform.Find("SkillsUI");
        if (oldSkills != null) oldSkills.gameObject.SetActive(false);

        menus.skillMenuPanel = GetOrAdd<CanvasGroup>(list.gameObject);
        menus.skillListContainer = list;
        menus.skillButtonPrefab = template.gameObject;

        BindWeaponCards(menus, template);
    }

    private static T GetOrAdd<T>(GameObject target) where T : Component
    {
        T component = target.GetComponent<T>();
        return component != null ? component : target.AddComponent<T>();
    }

    // กองการ์ดสกิล (การ์ดอาร์ตมี Rect ใหญ่กว่าภาพ ใช้ LayoutGroup ไม่ได้ SkillCardStack วางตำแหน่งแต่ละใบเอง)
    public static RectTransform CreateSkillList(Transform canvas, Vector2 position, Vector2 step)
    {
        RectTransform list = new GameObject("Skills_List", typeof(RectTransform)).GetComponent<RectTransform>();
        list.SetParent(canvas, false);
        list.anchorMin = list.anchorMax = Vector2.zero;
        list.pivot = new Vector2(0.5f, 0.5f);
        list.anchoredPosition = position;
        list.sizeDelta = Vector2.zero;
        list.gameObject.AddComponent<SkillCardStack>().step = step;
        return list;
    }

    [Header("เปลี่ยนอาวุธ (Switch): ไอคอนล้อมรอบตัวผู้เล่น")]
    [Tooltip("Prefab ของปุ่มอาวุธหนึ่งอัน (Back/Icon/Name) ใช้เมื่อในฉากไม่มี WeaponOption_Template แต่งหน้าตาที่ Prefab ได้เลย")]
    public GameObject weaponOptionPrefab;
    [Tooltip("ขนาดไอคอนอาวุธ (อ้างอิง 1920x1080) ใช้เฉพาะตอนสร้างแม่แบบเอง ถ้าแม่แบบอยู่ใน Hierarchy แล้วให้แก้ที่ตัวมันเลย")]
    public float weaponRingIconSize = 120f;
    [Tooltip("ขนาดตัวอักษรชื่ออาวุธใต้ไอคอน (ใช้เฉพาะตอนสร้างแม่แบบเอง)")]
    public float weaponRingNameSize = 26f;
    [Tooltip("สีพื้นมืดหลังไอคอน (ใช้เฉพาะตอนสร้างแม่แบบเอง)")]
    public Color weaponRingBackColor = new Color(0.02f, 0.03f, 0.1f, 0.85f);

    // หน้าเปลี่ยนอาวุธ: ไอคอนเส้นของอาร์ต (WeaponData.battleIcon) + ชื่ออาวุธ วางเป็นวงรอบตัวผู้เล่นบนจอ (WeaponRingLayout)
    // ไม่มีปุ่ม Back (กด Esc เพื่อกลับ) อาวุธที่ถืออยู่/พังแล้วจางลงและกดไม่ได้
    // Weapons_Ring และ WeaponOption_Template ที่วางไว้ใน Hierarchy (Bake Runtime UI) ใช้ตามนั้นเลย จัดหน้าตาที่ตัวมันได้
    private void BindWeaponCards(PlayerUIManager menus, Transform skillTemplate)
    {
        RectTransform list = transform.Find("Weapons_Ring") as RectTransform;
        if (list == null) list = CreateWeaponRing(transform);
        if (list.GetComponent<WeaponRingLayout>() == null) list.gameObject.AddComponent<WeaponRingLayout>();

        // ลำดับที่ใช้: แม่แบบในฉาก (Instance ของ Prefab) > Prefab ที่ผูกไว้ > สร้างจากโค้ด
        Transform found = transform.Find("WeaponOption_Template");
        GameObject option;
        if (found != null) option = found.gameObject;
        else if (weaponOptionPrefab != null)
        {
            option = Instantiate(weaponOptionPrefab, transform);
            option.name = "WeaponOption_Template";
        }
        else option = CreateWeaponOptionTemplate(transform, skillTemplate.Find("Name") != null ? skillTemplate.Find("Name").GetComponent<TMP_Text>() : null,
                GlowSprite(transform), weaponRingIconSize, weaponRingNameSize, weaponRingBackColor);
        // Awake ปิด Raycast ทุกภาพบน Canvas นี้ไปแล้ว พื้นรับคลิกของปุ่มต้องเปิดคืน
        Button optionButton = option.GetComponent<Button>();
        if (optionButton != null && optionButton.targetGraphic != null) optionButton.targetGraphic.raycastTarget = true;
        option.SetActive(false);

        // รายการเปลี่ยนอาวุธชุดเก่า (World Space ใต้ Player_Holder/Canvas) ไม่ใช้แล้ว
        if (menus.weaponMenuPanel != null) menus.weaponMenuPanel.gameObject.SetActive(false);
        menus.weaponMenuPanel = GetOrAdd<CanvasGroup>(list.gameObject);
        menus.weaponListContainer = list;
        menus.weaponOptionTemplate = option;
        menus.weaponBackCard = false;
        menus.dimUnavailableWeapons = true;
    }

    public static Sprite GlowSprite(Transform canvas)
    {
        Transform glow = canvas.Find("QTE_Frame/Glow");
        Image image = glow != null ? glow.GetComponent<Image>() : null;
        return image != null ? image.sprite : null;
    }

    public static RectTransform CreateWeaponRing(Transform canvas)
    {
        RectTransform list = new GameObject("Weapons_Ring", typeof(RectTransform)).GetComponent<RectTransform>();
        list.SetParent(canvas, false);
        list.anchorMin = Vector2.zero;
        list.anchorMax = Vector2.one;
        list.pivot = new Vector2(0.5f, 0.5f);
        list.sizeDelta = Vector2.zero;
        list.anchoredPosition = Vector2.zero;
        list.gameObject.AddComponent<WeaponRingLayout>();
        return list;
    }

    // ปุ่มหนึ่งอัน = พื้นรับคลิกโปร่งใส + พื้นมืดนุ่ม (Glow ของอาร์ต) + ไอคอน + ชื่อ (ฟอนต์เดียวกับชื่อการ์ดสกิล)
    public static GameObject CreateWeaponOptionTemplate(Transform canvas, TMP_Text styleSource, Sprite glow, float iconSize, float nameSize, Color backColor)
    {
        GameObject option = new GameObject("WeaponOption_Template", typeof(RectTransform), typeof(Image), typeof(Button));
        RectTransform root = (RectTransform)option.transform;
        root.SetParent(canvas, false);
        root.anchorMin = root.anchorMax = root.pivot = new Vector2(0.5f, 0.5f);
        root.sizeDelta = new Vector2(iconSize * 1.5f, iconSize * 1.5f);
        Image hit = option.GetComponent<Image>();
        hit.color = new Color(1f, 1f, 1f, 0f);
        hit.raycastTarget = true;
        Button button = option.GetComponent<Button>();
        button.transition = Selectable.Transition.None;
        button.targetGraphic = hit;

        if (glow != null)
        {
            GameObject backObject = new GameObject("Back", typeof(RectTransform), typeof(Image));
            RectTransform back = (RectTransform)backObject.transform;
            back.SetParent(root, false);
            back.anchorMin = back.anchorMax = back.pivot = new Vector2(0.5f, 0.5f);
            back.anchoredPosition = new Vector2(0f, iconSize * 0.05f);
            back.sizeDelta = Vector2.one * iconSize * 2.1f;
            Image backImage = backObject.GetComponent<Image>();
            backImage.sprite = glow;
            backImage.color = backColor;
            backImage.raycastTarget = false;
        }

        GameObject iconObject = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        RectTransform icon = (RectTransform)iconObject.transform;
        icon.SetParent(root, false);
        icon.anchorMin = icon.anchorMax = icon.pivot = new Vector2(0.5f, 0.5f);
        icon.anchoredPosition = new Vector2(0f, iconSize * 0.12f);
        icon.sizeDelta = Vector2.one * iconSize;
        Image iconImage = iconObject.GetComponent<Image>();
        iconImage.preserveAspect = true;
        iconImage.raycastTarget = false;

        GameObject nameObject = new GameObject("Name", typeof(RectTransform), typeof(TextMeshProUGUI));
        RectTransform nameRect = (RectTransform)nameObject.transform;
        nameRect.SetParent(root, false);
        nameRect.anchorMin = nameRect.anchorMax = nameRect.pivot = new Vector2(0.5f, 0.5f);
        nameRect.anchoredPosition = new Vector2(0f, -iconSize * 0.62f);
        nameRect.sizeDelta = new Vector2(iconSize * 2.4f, nameSize * 1.6f);
        TextMeshProUGUI nameText = nameObject.GetComponent<TextMeshProUGUI>();
        if (styleSource != null)
        {
            nameText.font = styleSource.font;
            nameText.fontSharedMaterial = styleSource.fontSharedMaterial;
            nameText.color = styleSource.color;
        }
        nameText.text = "Weapon";
        nameText.fontSize = nameSize;
        nameText.alignment = TextAlignmentOptions.Center;
        nameText.textWrappingMode = TextWrappingModes.NoWrap;
        nameText.raycastTarget = false;

        UIHoverPop pop = option.AddComponent<UIHoverPop>();
        pop.target = icon;
        pop.hoverScale = 1.15f;
        pop.pressScale = 0.92f;
        option.SetActive(false);
        return option;
    }

    private static void Rename(Transform root, string path, string name)
    {
        Transform target = root.Find(path);
        if (target != null) target.name = name;
    }

    // QTE วงแหวนชุดใหม่: Bar = ส่วนโค้งที่ต้องกด (Radial), SlideCheck = เข็มที่หมุน
    private void BindQTE()
    {
        Transform frame = transform.Find("QTE_Frame");
        SkillQTEManager qte = FindAnyObjectByType<SkillQTEManager>(FindObjectsInactive.Include);
        if (frame == null || qte == null) return;

        // วงแหวนเก่าเป็นลูกของตัว Manager เอง (ตัว Manager ต้องเปิดอยู่ให้ Update ทำงาน) ซ่อนแค่ภาพเดิม
        if (qte.qteCanvasPanel == qte.gameObject)
            foreach (Transform child in qte.transform) child.gameObject.SetActive(false);
        else if (qte.qteCanvasPanel != null) qte.qteCanvasPanel.SetActive(false);

        qte.mainCanvas = GetComponent<Canvas>();
        qte.qteCanvasPanel = frame.gameObject;
        qte.indicatorUI = (RectTransform)frame.Find("SlideCheck");
        qte.indicatorTip = qte.indicatorUI.childCount > 0 ? qte.indicatorUI.GetChild(0) : qte.indicatorUI;
        qte.hitZoneUI = (RectTransform)frame.Find("Bar");
        qte.Bg_Circle = (RectTransform)frame.Find("Back");
        qte.Bg_Diamond = (RectTransform)frame.Find("Glow");
        qte.successArc = frame.Find("Bar").GetComponent<Image>();
        frame.gameObject.SetActive(false);

        // ตัว Manager ต้องเปิดอยู่ (Awake ตั้ง Instance, Update หมุนเข็ม) ถ้าฉากบันทึก QTE_Panel ปิดไว้
        // SkillQTEManager.Instance จะเป็น null และทุกสกิลข้าม QTE ไปเงียบ ๆ: ซ่อนวงแหวนเก่าทั้งหมดแล้วเปิดตัว Manager
        if (!qte.gameObject.activeSelf)
        {
            foreach (Transform child in qte.transform) child.gameObject.SetActive(false);
            qte.gameObject.SetActive(true);
        }
    }

    // ป้ายแจ้งเหตุการณ์ (เทิร์นศัตรู, เวฟใหม่, บอสเรียกลูกน้อง) ใช้กรอบ LogAction ด้านบนแทนป้ายดำเดิม
    private void BindAnnouncement()
    {
        Transform log = transform.Find("LogAction");
        if (log == null) return;

        TurnAnnouncementUI old = FindAnyObjectByType<TurnAnnouncementUI>(FindObjectsInactive.Include);
        if (old != null) old.gameObject.SetActive(false); // ปิดก่อน Awake ของมันจะทำงาน Instance จึงเป็นตัวใหม่

        TextMeshProUGUI text = log.GetComponentInChildren<TextMeshProUGUI>(true);
        TurnAnnouncementUI announcement = log.gameObject.AddComponent<TurnAnnouncementUI>();
        announcement.announcementText = text;
        if (old != null)
        {
            announcement.fadeSpeed = old.fadeSpeed;
            announcement.displayDuration = old.displayDuration;
        }
        if (text != null)
        {
            // ข้อความประกาศมี 2 บรรทัด (เช่น ชื่อศัตรู + กำลังโจมตี) ขยายกล่องและย่อตัวอักษรอัตโนมัติ
            text.rectTransform.sizeDelta = new Vector2(420f, 60f);
            text.enableAutoSizing = true;
            text.fontSizeMin = 8f;
            text.fontSizeMax = 18f;
            text.alignment = TextAlignmentOptions.Center;
        }
    }

    private void BindCrosshair()
    {
        Transform aim = transform.Find("Aim");
        if (aim == null) return;
        if (player.crosshairUI != null && player.crosshairUI != aim.gameObject) player.crosshairUI.SetActive(false);
        player.crosshairUI = aim.gameObject;
        aim.gameObject.SetActive(false);
        foreach (Graphic graphic in aim.GetComponentsInChildren<Graphic>(true)) graphic.raycastTarget = false;
    }

    // หลอดเลือด/Stagger ของศัตรูแบบใหม่ ลอยเหนือศัตรูแต่ละตัวบน Overlay
    private void BindEnemyHUD()
    {
        Transform template = transform.Find("EnemyHUD");
        if (template == null) return;
        EnemyHUDOverlay overlay = gameObject.AddComponent<EnemyHUDOverlay>();
        overlay.template = (RectTransform)template;
        template.gameObject.SetActive(false);
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

        // Wand: เพชร 5 ช่องแสดงธาตุที่เก็บไว้ วางตรงกลางของตัวเลข Passive (ตัวเลขซ่อนตอนถือ Wand)
        RectTransform countRect = passiveCount.rectTransform;
        GameObject sigilSource = sigilSlotsPrefab != null ? sigilSlotsPrefab : Resources.Load<GameObject>("SigilSlots");
        if (sigilSource != null)
        {
            GameObject made = Instantiate(sigilSource, countRect.parent, false);
            made.name = "SigilSlots";
            sigilSlots = made.GetComponent<SigilSlotsUI>();
            if (sigilSlots == null) sigilSlots = made.AddComponent<SigilSlotsUI>();
        }
        else sigilSlots = SigilSlotsUI.Create((RectTransform)countRect.parent, Vector2.zero);
        // มาจาก Prefab: ใช้ตำแหน่ง/สเกลใน RectTransform ของ Prefab ตามที่จัดไว้ (เทียบกับ Passive_Frame) ไม่ทับ
        // สร้างจากโค้ด (หรือปิด sigilUsePrefabPosition): วางตรงกลางตัวเลขของ Passive
        if (sigilSource == null || !sigilUsePrefabPosition)
        {
            RectTransform sigilRect = (RectTransform)sigilSlots.transform;
            sigilRect.position = countRect.TransformPoint(countRect.rect.center);
        }
        sigilSlots.gameObject.SetActive(false);

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

        bool spell = weapon.weaponType == WeaponType.Spell;
        if (sigilSlots != null)
        {
            if (sigilSlots.gameObject.activeSelf != spell) sigilSlots.gameObject.SetActive(spell);
            if (spell) sigilSlots.Show(player.elementSlots);
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
                passiveCount.text = "";
                break;
            default:
                passiveCount.text = "";
                break;
        }
    }
}
