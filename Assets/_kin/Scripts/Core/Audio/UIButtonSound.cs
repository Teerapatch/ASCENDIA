using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// เสียงของปุ่ม UI ทั่วไป: ชี้ = ui.hover, กด = ตามชื่อปุ่ม (Back/Close/Cancel/Quit = ui.back, Confirm/Play/Craft/Upgrade/Continue/Retry = ui.confirm, อื่น ๆ = ui.click)
// SoundManager ใส่ให้ทุกปุ่ม (Button) ที่เปิดอยู่ในฉากเอง ไม่ต้องวาง ปุ่มที่มีระบบเสียงของตัวเอง (UIHoverPop, CampMenuButton, NodeCardButton, NodeMapButton) ข้าม
// อยากได้เสียงเฉพาะ: วางคอมโพเนนต์นี้ที่ปุ่มเองแล้วกรอก clickId / hoverId (ว่าง hoverId = ไม่มีเสียงตอนชี้) หรือใส่ NoUISound เพื่อปิดเสียงปุ่มนั้น
[DisallowMultipleComponent]
public class UIButtonSound : MonoBehaviour, IPointerEnterHandler
{
    [Tooltip("ว่าง = เดาจากชื่อปุ่ม")]
    public string clickId;
    public string hoverId = Sfx.UIHover;

    private Button button;
    private bool hooked;

    private void Awake() => Hook();

    public void Hook()
    {
        if (hooked) return;
        hooked = true;
        button = GetComponent<Button>();
        if (button != null) button.onClick.AddListener(OnClick);
        if (string.IsNullOrEmpty(clickId)) clickId = Guess(gameObject.name);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (string.IsNullOrEmpty(hoverId) || (button != null && !button.IsInteractable())) return;
        SoundManager.Play(hoverId);
    }

    private void OnClick() => SoundManager.Play(clickId);

    public static string Guess(string name)
    {
        string n = name.ToLowerInvariant();
        if (n.Contains("back") || n.Contains("close") || n.Contains("cancel") || n.Contains("quit") || n.Contains("exit")) return Sfx.UIBack;
        if (n.Contains("confirm") || n.Contains("play") || n.Contains("craft") || n.Contains("upgrade") || n.Contains("continue")
            || n.Contains("retry") || n.Contains("start") || n.Contains("equip")) return Sfx.UIConfirm;
        return Sfx.UIClick;
    }

    // ปุ่มที่ SoundManager ไม่ควรใส่เสียงให้ (มีเสียงของตัวเองแล้ว)
    public static bool HasOwnSound(GameObject go) =>
        go.GetComponent<NoUISound>() != null || go.GetComponent<UIHoverPop>() != null || go.GetComponent<CampMenuButton>() != null
        || go.GetComponent<NodeCardButton>() != null || go.GetComponent<NodeMapButton>() != null;
}
