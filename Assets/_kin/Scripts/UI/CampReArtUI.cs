using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// ผูก UI Camp ชุดใหม่ (ReArt v2) กับระบบ Camp: BaseCamp_RE_UI (เมนูหลัก), Loadout_RE_UI (แท็บ Loadout / Skills), Forge_RE_UI
// ใช้ข้อมูลจริงจาก PlayerData / WeaponData และกติกาคราฟต์-อัปเกรดของ ForgeManager.ForgeWeapon
// หน้าเหล่านี้เป็น Canvas Overlay แยกกัน (หาโดยชื่อ) ถ้าไม่มีในฉาก Component นี้ไม่ทำอะไร UI ชุดเก่าทำงานตามเดิม
[DefaultExecutionOrder(-600)]
public class CampReArtUI : MonoBehaviour
{
    public static bool IsSubPageOpen { get; private set; }

    public float pageFadeTime = 0.18f;
    public Color lockedTint = new Color(1f, 1f, 1f, 0.3f);
    public Color missingColor = new Color(1f, 0.35f, 0.35f, 1f);

    private CampManager camp;
    private ForgeManager forge;
    private PlayerData data;

    private GameObject mainPage, loadoutPage, forgePage, currentPage;
    private Coroutine pageRoutine;

    private class SlotView
    {
        public RectTransform root;
        public Image icon;
        public TMP_Text label;
        public GameObject select, add, locked, labelFrame;
        public UIHoverPop pop;
        public List<CellView> cells = new List<CellView>();
    }

    private class CellView
    {
        public RectTransform root;
        public Image icon;
        public GameObject glow, equipped;
        public TMP_Text equippedText;
        public UIHoverPop pop;
    }

    // ---------- Loadout
    private GameObject loadoutTab, skillsTab;
    private TMP_Text loadoutTabLabel, skillsTabLabel, slotCountText, inventoryHint;
    private readonly List<SlotView> loadoutSlots = new List<SlotView>();
    private RectTransform inventoryGrid;
    private GameObject inventoryTemplate, lockTemplate;
    private int selectedLoadoutSlot;

    // ---------- Skills
    private readonly List<SlotView> skillColumns = new List<SlotView>();
    private TMP_Text infoCost, infoTag, infoName, infoDescription, equipLabel;
    private UIHoverPop equipPop;
    private WeaponData skillWeapon;
    private WeaponSkill selectedSkill;

    // ---------- Forge
    private readonly List<SlotView> forgeSlots = new List<SlotView>();
    private WeaponData forgeWeapon;
    private TMP_Text currentLevelText, nextLevelText, upgradeLabel, newSkillText;
    private UIHoverPop upgradePop;
    private TMP_Text gemCount, monsterCount;
    private GameObject thirdRequirement;
    private RectTransform statsGrid;
    private GameObject statTemplate;
    private readonly List<GameObject> statRows = new List<GameObject>();

    private void Awake()
    {
        foreach (GameObject root in gameObject.scene.GetRootGameObjects())
        {
            if (root.name == "BaseCamp_RE_UI") mainPage = root;
            else if (root.name == "Loadout_RE_UI") loadoutPage = root;
            else if (root.name == "Forge_RE_UI") forgePage = root;
        }
        if (mainPage == null) { enabled = false; return; }

        camp = FindAnyObjectByType<CampManager>(FindObjectsInactive.Include);
        forge = FindAnyObjectByType<ForgeManager>(FindObjectsInactive.Include);

        // อาร์ตเปิด Raycast Target ทุกภาพ (กรอบเต็มจอจะกินทุกคลิก) ปิดหมดก่อน แล้วเปิดเฉพาะจุดที่กดได้ตอนผูก
        foreach (GameObject page in new[] { mainPage, loadoutPage, forgePage })
            if (page != null)
                foreach (Graphic graphic in page.GetComponentsInChildren<Graphic>(true)) graphic.raycastTarget = false;

        // ข้อความตัวอย่างที่อาร์ตลืมไว้ ("New Text") ไม่ต้องแสดง
        foreach (GameObject page in new[] { mainPage, loadoutPage, forgePage })
            if (page != null)
                foreach (TMP_Text text in page.GetComponentsInChildren<TMP_Text>(true))
                    if (text.text == "New Text") text.gameObject.SetActive(false);

        HideOldUI();
        BindMain();
        if (loadoutPage != null) BindLoadout();
        if (forgePage != null) BindForge();

        if (loadoutPage != null) loadoutPage.SetActive(false);
        if (forgePage != null) forgePage.SetActive(false);
        mainPage.SetActive(true);
        currentPage = mainPage;
        IsSubPageOpen = false;
    }

    private void Start()
    {
        data = camp != null && camp.playerData != null ? camp.playerData
             : GameManager.Instance != null ? GameManager.Instance.playerData
             : forge != null ? forge.playerData : null;
    }

    private void OnDestroy()
    {
        IsSubPageOpen = false;
    }

    // UI ชุดเก่า: ปิดเมนู CampUI_New และหน้าเก่าทั้งหมด ข้อความระบบย้ายขึ้น Overlay ของตัวเองให้อยู่บนหน้าใหม่
    private void HideOldUI()
    {
        foreach (GameObject root in gameObject.scene.GetRootGameObjects())
        {
            if (root.name == "CampUI_New") root.SetActive(false); // ปิดก่อน Awake ของ CampArtUI
        }
        if (camp == null) return;
        foreach (GameObject old in new[] { camp.mainMenuPanel, camp.loadoutPanel, camp.forgePanel })
        {
            if (old != null) old.SetActive(false);
        }
        camp.mainMenuPanel = null;
        camp.loadoutPanel = null;
        camp.forgePanel = null;

        if (camp.systemMessageText != null)
        {
            GameObject canvasObject = new GameObject("CampMessageCanvas", typeof(Canvas), typeof(CanvasScaler));
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            RectTransform text = camp.systemMessageText.rectTransform;
            text.SetParent(canvasObject.transform, false);
            text.anchorMin = text.anchorMax = new Vector2(0.5f, 0f);
            text.pivot = new Vector2(0.5f, 0f);
            text.anchoredPosition = new Vector2(0f, 60f);
            text.sizeDelta = new Vector2(1400f, 80f);
            text.localScale = Vector3.one;
            camp.systemMessageText.raycastTarget = false;
        }
    }

    // ---------------------------------------------------------------- หน้าหลัก
    private void BindMain()
    {
        Transform grid = mainPage.transform.Find("Grid");
        if (grid == null) return;
        BindMenuEntry(grid.Find("Rest"), () => { if (camp != null) camp.OnClickRest(); });
        BindMenuEntry(grid.Find("Loadout"), () => ShowPage(loadoutPage));
        BindMenuEntry(grid.Find("Forge"), () => ShowPage(forgePage));
    }

    private static void BindMenuEntry(Transform entry, UnityEngine.Events.UnityAction click)
    {
        if (entry == null) return;
        // ภาพพู่กันของแต่ละแถวใหญ่กว่าแถวและซ้อนกัน ใช้กรอบโปร่งใสขนาดแถว (Group 600x200) รับคลิกแทน
        Transform group = entry.Find("Group");
        Transform host = group != null ? group : entry;
        Image hit = host.GetComponent<Image>();
        if (hit == null)
        {
            hit = host.gameObject.AddComponent<Image>();
            hit.color = new Color(1f, 1f, 1f, 0f);
        }
        UIHoverPop pop = UIHoverPop.Add(hit.gameObject, click, host);
        pop.hoverScale = 1.08f;
    }

    private void ShowPage(GameObject page)
    {
        if (page == null || page == currentPage) return;
        if (pageRoutine != null) StopCoroutine(pageRoutine);
        pageRoutine = StartCoroutine(SwapPage(page));
    }

    private IEnumerator SwapPage(GameObject page)
    {
        GameObject from = currentPage;
        currentPage = page;
        IsSubPageOpen = page != mainPage;

        if (page == loadoutPage) { SetTab(false); RefreshLoadout(); }
        if (page == forgePage) RefreshForge();

        CanvasGroup fromGroup = from != null ? Group(from) : null;
        for (float t = 0f; fromGroup != null && t < pageFadeTime; t += Time.unscaledDeltaTime)
        {
            fromGroup.alpha = 1f - t / pageFadeTime;
            yield return null;
        }
        if (from != null) from.SetActive(false);

        page.SetActive(true);
        CanvasGroup toGroup = Group(page);
        for (float t = 0f; t < pageFadeTime; t += Time.unscaledDeltaTime)
        {
            toGroup.alpha = t / pageFadeTime;
            yield return null;
        }
        toGroup.alpha = 1f;
        pageRoutine = null;
    }

    // ลบทิ้ง (ในโหมด Edit ใช้ DestroyImmediate ได้อย่างเดียว ใช้ตอนเรนเดอร์ตรวจหน้าตา)
    private static void Kill(GameObject target)
    {
        if (Application.isPlaying) Destroy(target);
        else DestroyImmediate(target);
    }

    private static List<Transform> Children(Transform parent)
    {
        var list = new List<Transform>();
        foreach (Transform child in parent) list.Add(child);
        return list;
    }

    private static CanvasGroup Group(GameObject page)
    {
        CanvasGroup group = page.GetComponent<CanvasGroup>();
        return group != null ? group : page.AddComponent<CanvasGroup>();
    }

    private void Update()
    {
        // Esc ในหน้าย่อย = กลับหน้าหลัก (GameExitMenu ไม่เปิดหน้าต่างออกเกมระหว่างนี้)
        if (IsSubPageOpen && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) ShowPage(mainPage);
    }

    // ---------------------------------------------------------------- ตัวช่วยจัดช่อง
    private static Transform FindPrefix(Transform parent, string prefix)
    {
        if (parent == null) return null;
        foreach (Transform child in parent)
            if (child.name.StartsWith(prefix)) return child;
        return null;
    }

    // อาร์ตทำช่องแต่ละช่องไม่เหมือนกัน (ช่องว่างไม่มีไอคอน/ป้าย) ใช้ช่องแรกเป็นแม่แบบ วางแทนทุกช่องตามตำแหน่งเดิม
    private static List<RectTransform> Normalize(Transform grid, int count)
    {
        var originals = new List<RectTransform>();
        foreach (Transform child in grid) originals.Add((RectTransform)child);
        var result = new List<RectTransform>();
        if (originals.Count == 0) return result;
        RectTransform template = originals[0];
        result.Add(template);
        for (int i = 1; i < count; i++)
        {
            RectTransform clone = Instantiate(template, grid);
            clone.name = template.name.TrimEnd('1', ' ', '(', ')') + (i + 1);
            if (i < originals.Count)
            {
                RectTransform spot = originals[i];
                clone.anchorMin = spot.anchorMin;
                clone.anchorMax = spot.anchorMax;
                clone.pivot = spot.pivot;
                clone.anchoredPosition = spot.anchoredPosition;
                clone.SetSiblingIndex(spot.GetSiblingIndex());
            }
            result.Add(clone);
        }
        for (int i = 1; i < originals.Count; i++) DestroyImmediate(originals[i].gameObject);
        return result;
    }

    private static GameObject CloneInto(GameObject template, Transform parent)
    {
        if (template == null) return null;
        GameObject clone = Instantiate(template, parent);
        clone.name = template.name;
        RectTransform rect = (RectTransform)clone.transform;
        rect.anchoredPosition = Vector2.zero;
        clone.SetActive(false);
        return clone;
    }

    private static SlotView MakeSlot(RectTransform root, GameObject addTemplate, GameObject lockTemplate)
    {
        var slot = new SlotView { root = root };
        Transform select = FindPrefix(root, "SelectFrame");
        slot.select = select != null ? select.gameObject : null;
        Transform icon = FindPrefix(root, "WeaponIcon");
        slot.icon = icon != null ? icon.GetComponent<Image>() : null;
        Transform frame = root.Find("Frame");
        slot.labelFrame = frame != null ? frame.gameObject : null;
        slot.label = frame != null ? frame.GetComponentInChildren<TMP_Text>(true) : null;
        foreach (Transform child in root)
            if (child.name.StartsWith("Add") || child.name.StartsWith("Locked")) child.gameObject.SetActive(false);
        slot.add = CloneInto(addTemplate, root);
        slot.locked = CloneInto(lockTemplate, root);
        if (slot.locked != null) slot.locked.transform.SetAsLastSibling();
        return slot;
    }

    private static void SetText(TMP_Text text, string value)
    {
        if (text != null) text.text = value;
    }

    private List<WeaponData> AllWeapons => forge != null ? forge.allWeaponsInGame : new List<WeaponData>();

    private void Message(string text)
    {
        if (camp != null && !string.IsNullOrEmpty(text)) camp.ShowSystemMessage(text);
    }

    // ---------------------------------------------------------------- Loadout
    private void BindLoadout()
    {
        Transform page = loadoutPage.transform;
        loadoutTab = page.Find("Loadout_Tab")?.gameObject;
        skillsTab = page.Find("Skills_Tab")?.gameObject;

        Transform tabs = page.Find("Tab_Grid");
        if (tabs != null)
        {
            Transform loadoutButton = tabs.Find("Loadout"), skillsButton = tabs.Find("Skills");
            loadoutTabLabel = loadoutButton != null ? loadoutButton.GetComponentInChildren<TMP_Text>(true) : null;
            skillsTabLabel = skillsButton != null ? skillsButton.GetComponentInChildren<TMP_Text>(true) : null;
            if (loadoutButton != null) UIHoverPop.Add(loadoutButton.gameObject, () => SetTab(false));
            if (skillsButton != null) UIHoverPop.Add(skillsButton.gameObject, () => SetTab(true));
        }

        if (loadoutTab != null)
        {
            Transform tab = loadoutTab.transform;
            BindBack(tab.Find("BackButon"));
            slotCountText = tab.Find("Loadout_Tepo")?.GetComponent<TMP_Text>();

            // แม่แบบ: เครื่องหมาย + (ช่องว่าง) จากช่อง 3, กุญแจจากหน้า Skills ช่อง 3
            Transform grid = tab.Find("Loadout_Grid");
            GameObject addTemplate = FindPrefix(grid.childCount > 2 ? grid.GetChild(2) : null, "Add")?.gameObject;
            Transform skillGrid = skillsTab != null ? skillsTab.transform.Find("Loadout_Grid") : null;
            lockTemplate = FindPrefix(skillGrid != null && skillGrid.childCount > 2 ? skillGrid.GetChild(2) : null, "Locked")?.gameObject;
            if (addTemplate != null) addTemplate = Instantiate(addTemplate, page);
            if (lockTemplate != null) lockTemplate = Instantiate(lockTemplate, page);
            if (addTemplate != null) addTemplate.SetActive(false);
            if (lockTemplate != null) lockTemplate.SetActive(false);

            List<RectTransform> slots = Normalize(grid, 4);
            for (int i = 0; i < slots.Count; i++)
            {
                SlotView slot = MakeSlot(slots[i], addTemplate, lockTemplate);
                int index = i;
                Image hit = slots[i].GetComponent<Image>();
                slot.pop = UIHoverPop.Add(hit != null ? hit.gameObject : slots[i].gameObject, () => SelectLoadoutSlot(index), slots[i]);
                loadoutSlots.Add(slot);
            }

            Transform inventory = tab.Find("Inv_Grid");
            if (inventory != null)
            {
                inventoryHint = inventory.Find("Text (TMP) (2)")?.GetComponent<TMP_Text>();
                inventoryGrid = inventory.Find("Grid") as RectTransform;
                if (inventoryGrid != null && inventoryGrid.childCount > 0)
                {
                    inventoryTemplate = inventoryGrid.GetChild(0).gameObject;
                    inventoryTemplate.transform.SetParent(inventory, false);
                    inventoryTemplate.SetActive(false);
                    foreach (Transform child in Children(inventoryGrid)) Kill(child.gameObject);
                }
            }
        }

        if (skillsTab != null) BindSkills(skillsTab.transform);
    }

    private void BindBack(Transform back)
    {
        if (back == null) return;
        Image hit = back.GetComponent<Image>();
        UIHoverPop.Add(hit != null ? hit.gameObject : back.gameObject, () => ShowPage(mainPage));
    }

    private void SetTab(bool skills)
    {
        if (loadoutTab != null) loadoutTab.SetActive(!skills);
        if (skillsTab != null) skillsTab.SetActive(skills);
        if (loadoutTabLabel != null) loadoutTabLabel.alpha = skills ? 0.45f : 1f;
        if (skillsTabLabel != null) skillsTabLabel.alpha = skills ? 1f : 0.45f;
        if (skills) RefreshSkills();
        else RefreshLoadout();
    }

    private void SelectLoadoutSlot(int index)
    {
        if (data == null) return;
        if (!data.IsLoadoutSlotUnlocked(index))
        {
            Message("This slot is still locked.");
            return;
        }
        selectedLoadoutSlot = index;
        RefreshLoadout();
    }

    private void RefreshLoadout()
    {
        if (data == null) return;
        data.RemoveUncraftedFromLoadout(); // อาวุธที่ยังไม่ได้คราฟต์ใส่ช่องไม่ได้
        int equipped = 0;
        for (int i = 0; i < loadoutSlots.Count; i++)
        {
            SlotView slot = loadoutSlots[i];
            WeaponData weapon = i < data.activeLoadout.Length ? data.activeLoadout[i] : null;
            bool unlocked = data.IsLoadoutSlotUnlocked(i);
            if (weapon != null) equipped++;
            if (slot.icon != null)
            {
                slot.icon.gameObject.SetActive(weapon != null);
                if (weapon != null) slot.icon.sprite = weapon.weaponIcon;
            }
            if (slot.labelFrame != null) slot.labelFrame.SetActive(weapon != null);
            SetText(slot.label, weapon != null ? weapon.weaponName.ToUpper() : "");
            if (slot.add != null) slot.add.SetActive(weapon == null && unlocked);
            if (slot.locked != null) slot.locked.SetActive(!unlocked);
            if (slot.select != null) slot.select.SetActive(i == selectedLoadoutSlot);
            if (slot.pop != null) slot.pop.Selected = i == selectedLoadoutSlot;
        }
        SetText(slotCountText, $"SLOT ({equipped}/{data.UnlockedLoadoutSlots})");
        WeaponData current = selectedLoadoutSlot < data.activeLoadout.Length ? data.activeLoadout[selectedLoadoutSlot] : null;
        SetText(inventoryHint, current != null
            ? $"SLOT {selectedLoadoutSlot + 1}: {current.weaponName.ToUpper()}  -  click it again to remove"
            : $"SLOT {selectedLoadoutSlot + 1}: EMPTY  -  pick a weapon");
        BuildInventory();
    }

    private void BuildInventory()
    {
        if (inventoryGrid == null || inventoryTemplate == null) return;
        foreach (Transform child in Children(inventoryGrid)) Kill(child.gameObject);
        foreach (WeaponData weapon in AllWeapons)
        {
            if (weapon == null) continue;
            GameObject item = Instantiate(inventoryTemplate, inventoryGrid);
            item.SetActive(true);
            Image icon = item.transform.Find("Icon")?.GetComponent<Image>();
            if (icon != null)
            {
                icon.sprite = weapon.weaponIcon;
                icon.color = weapon.isUnlocked ? Color.white : lockedTint;
            }
            bool inLoadout = System.Array.IndexOf(data.activeLoadout, weapon) >= 0;
            CanvasGroup group = item.AddComponent<CanvasGroup>();
            group.alpha = inLoadout ? 0.55f : 1f;
            // ยังไม่ได้คราฟต์ = ล็อก: กุญแจกลางไอคอน ใส่ลงช่องไม่ได้ (กดแล้วบอกให้ไปคราฟต์ที่ Forge)
            if (!weapon.isUnlocked) AddLockMark(icon != null ? icon.transform : item.transform);
            Image hit = item.GetComponent<Image>();
            WeaponData chosen = weapon;
            UIHoverPop pop = UIHoverPop.Add(hit != null ? hit.gameObject : item, () => AssignWeapon(chosen), item.transform);
            if (!weapon.isUnlocked) pop.hoverScale = pop.pressScale = 1f;
        }
    }

    private void AddLockMark(Transform parent)
    {
        if (lockTemplate == null || parent == null) return;
        GameObject lockMark = Instantiate(lockTemplate, parent);
        lockMark.name = "Locked";
        lockMark.SetActive(true);
        RectTransform rect = (RectTransform)lockMark.transform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.SetAsLastSibling();
    }

    // ใส่อาวุธลงช่องที่เลือก: อยู่อีกช่องอยู่แล้วก็สลับกัน, กดอาวุธเดิมของช่องนั้นซ้ำ = ถอดออก (ต้องเหลืออย่างน้อย 1 ชิ้น)
    private void AssignWeapon(WeaponData weapon)
    {
        if (data == null || weapon == null) return;
        if (!weapon.isUnlocked)
        {
            Message($"{weapon.weaponName} is not crafted yet. Craft it at the Forge.");
            return;
        }
        WeaponData[] loadout = data.activeLoadout;
        int slot = selectedLoadoutSlot;
        if (loadout[slot] == weapon)
        {
            int count = 0;
            foreach (WeaponData w in loadout) if (w != null) count++;
            if (count <= 1) { Message("You need at least one weapon."); return; }
            loadout[slot] = null;
        }
        else
        {
            int existing = System.Array.IndexOf(loadout, weapon);
            if (existing >= 0) loadout[existing] = loadout[slot];
            loadout[slot] = weapon;
        }
        RefreshLoadout();
    }

    // ---------------------------------------------------------------- Skills
    private void BindSkills(Transform tab)
    {
        BindBack(tab.Find("BackButon"));
        Transform grid = tab.Find("Loadout_Grid");
        if (grid == null) return;

        // แม่แบบช่องสกิล: ช่องแรกของคอลัมน์แรก (มีไฮไลต์ ไอคอน เลขโรมัน และเครื่องหมาย Equipped)
        List<RectTransform> columns = Normalize(grid, 4);
        foreach (RectTransform column in columns)
        {
            SlotView view = MakeSlot(column, null, lockTemplate);
            Transform cellsRoot = FindPrefix(column, "Skills_Grid");
            if (cellsRoot != null)
            {
                foreach (RectTransform cell in Normalize(cellsRoot, 6))
                {
                    var cellView = new CellView { root = cell };
                    cellView.icon = cell.Find("Image")?.GetComponent<Image>();
                    Transform glow = cell.Find("Glow_Unlocked");
                    if (glow == null) glow = cell.Find("Image (1)");
                    cellView.glow = glow != null ? glow.gameObject : null;
                    Transform mark = FindPrefix(cell, "Equipped");
                    cellView.equipped = mark != null ? mark.gameObject : null;
                    cellView.equippedText = mark != null ? mark.GetComponentInChildren<TMP_Text>(true) : null;
                    Image hit = cell.GetComponent<Image>();
                    if (hit == null) hit = cellView.icon;
                    cellView.pop = UIHoverPop.Add(hit != null ? hit.gameObject : cell.gameObject, null, cell);
                    cellView.pop.selectedScale = 1.18f;
                    view.cells.Add(cellView);
                }
                // เลขโรมันบนช่อง (I..VI) ตามลำดับ
                string[] numerals = { "I", "II", "III", "IV", "V", "VI" };
                for (int i = 0; i < view.cells.Count; i++)
                {
                    TMP_Text numeral = view.cells[i].root.Find("Text (TMP) (2)")?.GetComponent<TMP_Text>();
                    SetText(numeral, numerals[i]);
                }
            }
            skillColumns.Add(view);
        }

        Transform info = tab.Find("Skills_Info");
        if (info != null)
        {
            infoCost = info.Find("Image (1)/Text (TMP) (2)")?.GetComponent<TMP_Text>();
            infoTag = info.Find("Text (TMP) (3)")?.GetComponent<TMP_Text>();
            infoName = info.Find("Text (TMP) (4)")?.GetComponent<TMP_Text>();
            infoDescription = info.Find("Text (TMP) (5)")?.GetComponent<TMP_Text>();
            if (infoDescription != null)
            {
                infoDescription.textWrappingMode = TextWrappingModes.Normal;
                infoDescription.rectTransform.sizeDelta = new Vector2(300f, 70f);
                infoDescription.enableAutoSizing = true;
                infoDescription.fontSizeMin = 8f;
                infoDescription.fontSizeMax = 18f;
            }
        }

        Transform equip = tab.Find("EquipButton");
        Transform equipInner = equip != null ? (equip.Find("EquipButton") ?? equip) : null;
        if (equipInner != null)
        {
            equipLabel = equipInner.GetComponentInChildren<TMP_Text>(true);
            Image hit = equipInner.GetComponent<Image>();
            equipPop = UIHoverPop.Add(hit != null ? hit.gameObject : equipInner.gameObject, ToggleEquip, equipInner);
        }
    }

    private void RefreshSkills()
    {
        if (data == null) return;
        for (int c = 0; c < skillColumns.Count; c++)
        {
            SlotView column = skillColumns[c];
            WeaponData weapon = c < data.activeLoadout.Length ? data.activeLoadout[c] : null;
            if (column.icon != null)
            {
                column.icon.gameObject.SetActive(true);
                column.icon.sprite = weapon != null ? weapon.weaponIcon : column.icon.sprite;
                column.icon.color = weapon != null ? Color.white : lockedTint;
            }
            if (column.labelFrame != null) column.labelFrame.SetActive(weapon != null);
            SetText(column.label, weapon != null ? $"LEVEL {weapon.currentLevel}" : "");
            if (column.locked != null) column.locked.SetActive(weapon == null);
            if (column.select != null) column.select.SetActive(weapon != null && weapon == skillWeapon);

            List<WeaponSkill> equipped = weapon != null ? data.GetEquippedSkills(weapon) : new List<WeaponSkill>();
            for (int i = 0; i < column.cells.Count; i++)
            {
                CellView cell = column.cells[i];
                WeaponSkill skill = weapon != null && i < weapon.skillPool.Count ? weapon.skillPool[i] : null;
                cell.root.gameObject.SetActive(weapon != null);
                if (skill == null) { if (weapon != null) cell.root.gameObject.SetActive(false); continue; }

                bool unlocked = weapon.IsSkillUnlocked(skill);
                int order = equipped.IndexOf(skill);
                if (cell.glow != null) cell.glow.SetActive(unlocked);
                // ยังไม่ปลดล็อก: จางทั้งช่อง (ไม่แต้มสีไอคอน ภาพพื้นช่องของอาร์ตเป็นสีขาว จะกลบเลขโรมัน)
                CanvasGroup cellGroup = cell.root.GetComponent<CanvasGroup>();
                if (cellGroup == null) cellGroup = cell.root.gameObject.AddComponent<CanvasGroup>();
                cellGroup.alpha = unlocked ? 1f : 0.35f;
                if (cell.equipped != null) cell.equipped.SetActive(order >= 0);
                SetText(cell.equippedText, "E");
                cell.pop.Selected = skill == selectedSkill;
                WeaponData owner = weapon;
                WeaponSkill picked = skill;
                cell.pop.onClick.RemoveAllListeners();
                cell.pop.onClick.AddListener(() => SelectSkill(owner, picked));
            }
        }

        if (selectedSkill == null || skillWeapon == null)
        {
            foreach (WeaponData weapon in data.activeLoadout)
            {
                if (weapon == null) continue;
                WeaponSkill first = weapon.skillPool.Find(s => s != null);
                if (first != null) { SelectSkill(weapon, first); return; }
            }
        }
        RefreshSkillInfo();
    }

    private void SelectSkill(WeaponData weapon, WeaponSkill skill)
    {
        skillWeapon = weapon;
        selectedSkill = skill;
        RefreshSkills();
    }

    private void RefreshSkillInfo()
    {
        if (selectedSkill == null || skillWeapon == null)
        {
            SetText(infoName, "");
            SetText(infoDescription, "");
            if (equipPop != null) equipPop.interactable = false;
            return;
        }
        bool unlocked = skillWeapon.IsSkillUnlocked(selectedSkill);
        bool equipped = data.GetEquippedSkills(skillWeapon).Contains(selectedSkill);
        SetText(infoCost, selectedSkill.apCost.ToString());
        SetText(infoTag, unlocked ? skillWeapon.weaponName.ToUpper() : $"LOCKED  LV.{skillWeapon.SkillUnlockLevel(selectedSkill)}");
        SetText(infoName, selectedSkill.skillName);
        SetText(infoDescription, selectedSkill.description);
        SetText(equipLabel, !unlocked ? "LOCKED" : equipped ? "UNEQUIP" : "EQUIP");
        if (equipPop != null) equipPop.interactable = unlocked;
    }

    // ใส่/ถอดสกิล (สูงสุด WeaponData.MaxEquippedSkills ต่ออาวุธ ต้องเหลืออย่างน้อย 1) เรียงตามลำดับใน skillPool
    private void ToggleEquip()
    {
        if (data == null || skillWeapon == null || selectedSkill == null) return;
        if (!skillWeapon.IsSkillUnlocked(selectedSkill)) return;
        List<WeaponSkill> equipped = data.GetEquippedSkills(skillWeapon);
        if (equipped.Contains(selectedSkill))
        {
            if (equipped.Count <= 1) { Message("A weapon needs at least one skill."); return; }
            equipped.Remove(selectedSkill);
        }
        else
        {
            if (equipped.Count >= WeaponData.MaxEquippedSkills)
            {
                Message($"Up to {WeaponData.MaxEquippedSkills} skills per weapon. Unequip one first.");
                return;
            }
            equipped.Add(selectedSkill);
            List<WeaponSkill> pool = skillWeapon.skillPool;
            equipped.Sort((x, y) => pool.IndexOf(x).CompareTo(pool.IndexOf(y)));
        }
        data.SetEquippedSkills(skillWeapon, equipped);
        RefreshSkills();
    }

    // ---------------------------------------------------------------- Forge
    private void BindForge()
    {
        Transform tab = forgePage.transform.Find("Forge_Tab");
        if (tab == null) return;

        Transform grid = tab.Find("Loadout_Grid");
        GameObject forgeLock = FindPrefix(grid != null && grid.childCount > 2 ? grid.GetChild(2) : null, "Locked")?.gameObject;
        if (forgeLock != null) { forgeLock = Instantiate(forgeLock, forgePage.transform); forgeLock.SetActive(false); }
        if (grid != null)
        {
            List<RectTransform> slots = Normalize(grid, Mathf.Max(4, AllWeapons.Count));
            for (int i = 0; i < slots.Count; i++)
            {
                SlotView slot = MakeSlot(slots[i], null, forgeLock);
                int index = i;
                Image hit = slots[i].GetComponent<Image>();
                slot.pop = UIHoverPop.Add(hit != null ? hit.gameObject : slots[i].gameObject, () => SelectForgeWeapon(index), slots[i]);
                forgeSlots.Add(slot);
            }
        }

        Transform upgrade = tab.Find("UpgradeButton");
        if (upgrade != null)
        {
            upgradeLabel = upgrade.GetComponentInChildren<TMP_Text>(true);
            Image hit = upgrade.GetComponent<Image>();
            upgradePop = UIHoverPop.Add(hit != null ? hit.gameObject : upgrade.gameObject, DoForge, upgrade);

            // อาร์ตไม่มีปุ่มกลับในหน้า Forge: ทำจากปุ่ม Upgrade วางมุมซ้ายล่าง (กด Esc ก็กลับได้)
            RectTransform back = Instantiate((RectTransform)upgrade, tab);
            back.name = "BackButton";
            back.anchorMin = back.anchorMax = new Vector2(0f, 0f);
            back.pivot = new Vector2(0f, 0f);
            back.anchoredPosition = new Vector2(60f, 40f);
            back.localScale = upgrade.localScale * 0.7f;
            SetText(back.GetComponentInChildren<TMP_Text>(true), "BACK");
            Image backHit = back.GetComponent<Image>();
            UIHoverPop.Add(backHit != null ? backHit.gameObject : back.gameObject, () => ShowPage(mainPage));
        }

        Transform info = tab.Find("UpgradeInfo");
        if (info != null)
        {
            currentLevelText = info.Find("CurrentLevel")?.GetComponent<TMP_Text>();
            nextLevelText = info.Find("NextLevel")?.GetComponent<TMP_Text>();
            Transform require = info.Find("Require_Grid");
            if (require != null)
            {
                gemCount = require.childCount > 0 ? require.GetChild(0).GetComponentsInChildren<TMP_Text>(true).Length > 0 ? require.GetChild(0).GetComponentsInChildren<TMP_Text>(true)[0] : null : null;
                monsterCount = require.childCount > 1 ? (require.GetChild(1).GetComponentsInChildren<TMP_Text>(true).Length > 0 ? require.GetChild(1).GetComponentsInChildren<TMP_Text>(true)[0] : null) : null;
                thirdRequirement = require.childCount > 2 ? require.GetChild(2).gameObject : null;
                if (thirdRequirement != null) thirdRequirement.SetActive(false); // เกมมีวัตถุดิบ 2 ชนิด
            }
            statsGrid = info.Find("StatsUP_Grid") as RectTransform;
            if (statsGrid != null && statsGrid.childCount > 0)
            {
                statTemplate = statsGrid.GetChild(0).gameObject;
                Transform skillRow = statsGrid.childCount > 2 ? statsGrid.GetChild(2) : null;
                newSkillText = skillRow != null ? skillRow.GetComponentInChildren<TMP_Text>(true) : null;
                if (newSkillText != null)
                {
                    // ชื่อสกิลยาวกว่ากรอบที่อาร์ตเผื่อไว้: ขยายกรอบเต็มความกว้างแถว และห้ามตัดข้อความทิ้ง
                    newSkillText.overflowMode = TextOverflowModes.Overflow;
                    RectTransform rect = newSkillText.rectTransform;
                    float scale = rect.localScale.x;
                    float left = rect.anchoredPosition.x - rect.sizeDelta.x * rect.pivot.x * scale; // คงขอบซ้ายเดิม
                    rect.sizeDelta = new Vector2(440f, rect.sizeDelta.y);
                    rect.anchoredPosition = new Vector2(left + rect.sizeDelta.x * rect.pivot.x * scale, rect.anchoredPosition.y);
                    newSkillText.fontSizeMin = Mathf.Max(newSkillText.fontSizeMin, 22f);
                }
                if (skillRow != null) skillRow.SetParent(info, false); // (false: ตอน Awake Canvas Overlay อาจยังสเกล 0 ถ้าคงตำแหน่งโลกแถวจะหดเป็น 0) แถวสกิลใหม่วางเอง ไม่ปนกับแถวสเตตัส
                statTemplate.transform.SetParent(info, false);
                statTemplate.SetActive(false);
                foreach (Transform child in Children(statsGrid)) Kill(child.gameObject);
                if (skillRow != null) skillRow.SetParent(statsGrid, false);
            }
        }
    }

    private void SelectForgeWeapon(int index)
    {
        List<WeaponData> weapons = AllWeapons;
        if (index < 0 || index >= weapons.Count) return;
        forgeWeapon = weapons[index];
        RefreshForge();
    }

    private void RefreshForge()
    {
        if (data == null || forge == null) return;
        List<WeaponData> weapons = AllWeapons;
        if (forgeWeapon == null && weapons.Count > 0) forgeWeapon = weapons[0];

        for (int i = 0; i < forgeSlots.Count; i++)
        {
            SlotView slot = forgeSlots[i];
            WeaponData weapon = i < weapons.Count ? weapons[i] : null;
            slot.root.gameObject.SetActive(weapon != null);
            if (weapon == null) continue;
            if (slot.icon != null)
            {
                slot.icon.gameObject.SetActive(true);
                slot.icon.sprite = weapon.weaponIcon;
                slot.icon.color = weapon.isUnlocked ? Color.white : lockedTint;
            }
            if (slot.labelFrame != null) slot.labelFrame.SetActive(true);
            SetText(slot.label, weapon.isUnlocked ? $"LEVEL {weapon.currentLevel}" : "CRAFT");
            if (slot.locked != null)
            {
                slot.locked.SetActive(!weapon.isUnlocked);
                slot.locked.transform.SetAsLastSibling(); // กุญแจอยู่บนสุด ไม่โดนกรอบเลือกบัง
            }
            if (slot.select != null) slot.select.SetActive(weapon == forgeWeapon);
            if (slot.pop != null) slot.pop.Selected = weapon == forgeWeapon;
        }

        WeaponData w = forgeWeapon;
        if (w == null) return;
        bool max = w.isUnlocked && w.IsMaxLevel;
        int level = w.isUnlocked ? w.currentLevel : 0;
        int next = w.isUnlocked ? w.currentLevel + 1 : 1;
        SetText(currentLevelText, w.isUnlocked ? level.ToString() : "-"); // ยังไม่คราฟต์ = ยังไม่มีอาวุธ
        SetText(nextLevelText, max ? "MAX" : next.ToString());

        ForgeManager.Cost(w, out int gem, out int monster);
        SetRequirement(gemCount, data.gemStone, gem, max);
        SetRequirement(monsterCount, data.monsterPart, monster, max);

        // แถวสเตตัส: ค่าปัจจุบัน -> ค่าหลังอัป (ยังไม่คราฟต์ = ค่าเลเวล 1)
        foreach (GameObject row in statRows) if (row != null) Kill(row);
        statRows.Clear();
        int from = Mathf.Max(1, level);
        int to = max ? from : next;
        AddStat("ATK", w.GetDamageAt(from).ToString("0.#"), w.GetDamageAt(to).ToString("0.#"), max);
        AddStat("SPD", w.GetSpeedAt(from).ToString("0.#"), w.GetSpeedAt(to).ToString("0.#"), max);
        AddStat("DUR", w.GetDurabilityAt(from).ToString("0"), w.GetDurabilityAt(to).ToString("0"), max);
        AddStat("POSTURE", "x" + w.GetPostureAt(from).ToString("0.00"), "x" + w.GetPostureAt(to).ToString("0.00"), max);

        WeaponSkill gained = !max ? w.SkillUnlockedAt(next) : null;
        WeaponSkill upcoming = w.NextSkillToUnlock(out int skillLevel);
        var starting = new List<string>(); // คราฟต์ = ได้สกิลที่ปลดล็อกที่ Lv.1
        if (!w.isUnlocked)
            for (int i = 0; i < w.skillPool.Count; i++)
                if (w.skillPool[i] != null && WeaponData.SkillUnlockLevel(i) <= 1) starting.Add(w.skillPool[i].skillName.ToUpper());
        SetText(newSkillText, starting.Count > 0 ? "- SKILLS: " + string.Join(", ", starting)
            : gained != null ? $"- NEW SKILL: {gained.skillName.ToUpper()}"
            : upcoming != null ? $"- NEXT SKILL AT LV.{skillLevel}: {upcoming.skillName.ToUpper()}" : "- ALL SKILLS UNLOCKED");
        if (newSkillText != null) newSkillText.transform.parent.SetAsLastSibling();

        bool quota = w.isUnlocked || data.CanCraftNewWeapon(forge.CraftedWeaponCount());
        SetText(upgradeLabel, max ? "MAX LEVEL" : !quota ? "CAMP LIMIT" : w.isUnlocked ? "UPGRADE" : "CRAFT");
        if (upgradePop != null) upgradePop.interactable = forge.CanForge(w);
        if (upgradeLabel != null) upgradeLabel.alpha = forge.CanForge(w) ? 1f : 0.45f;
    }

    private void SetRequirement(TMP_Text text, int have, int need, bool max)
    {
        if (text == null) return;
        text.text = max ? "-" : $"{have}/{need}";
        text.color = max || have >= need ? Color.white : missingColor;
    }

    private void AddStat(string stat, string current, string nextValue, bool max)
    {
        if (statTemplate == null || statsGrid == null) return;
        GameObject row = Instantiate(statTemplate, statsGrid);
        row.SetActive(true);
        foreach (TMP_Text text in row.GetComponentsInChildren<TMP_Text>(true))
        {
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Overflow;
        }
        SetText(row.transform.Find("Stats")?.GetComponent<TMP_Text>(), stat);
        SetText(row.transform.Find("Current")?.GetComponent<TMP_Text>(), current);
        SetText(row.transform.Find("NextValue")?.GetComponent<TMP_Text>(), max ? "" : nextValue);
        Transform arrow = row.transform.Find("Arrow");
        if (arrow != null) arrow.gameObject.SetActive(!max);
        statRows.Add(row);
    }

    private void DoForge()
    {
        if (forge == null || forgeWeapon == null) return;
        string message = forge.ForgeWeapon(forgeWeapon);
        if (message == null)
        {
            Message("Not enough materials.");
            return;
        }
        Message(message);
        if (forge.successEffect != null) forge.successEffect.Play();
        RefreshForge();
    }
}
