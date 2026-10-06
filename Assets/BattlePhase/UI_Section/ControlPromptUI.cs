using UnityEngine;
using TMPro;

[RequireComponent(typeof(CanvasGroup))]
public class ControlPromptUI : MonoBehaviour
{
    public static ControlPromptUI Instance;

    public TextMeshProUGUI promptText;
    public float fadeSpeed = 12f;

    private CanvasGroup canvasGroup;
    private float targetAlpha = 0f;

    private void Awake()
    {
        Instance = this;
        canvasGroup = GetComponent<CanvasGroup>();
        canvasGroup.alpha = 0f;
    }

    private void Update()
    {
        if (Mathf.Abs(canvasGroup.alpha - targetAlpha) > 0.01f)
        {
            canvasGroup.alpha = Mathf.Lerp(canvasGroup.alpha, targetAlpha, Time.deltaTime * fadeSpeed);
        }
        else
        {
            canvasGroup.alpha = targetAlpha;
        }
    }

    public void ShowPrompt(string message)
    {
        promptText.text = message;
        targetAlpha = 1f;
    }

    public void HidePrompt()
    {
        targetAlpha = 0f;
    }
}