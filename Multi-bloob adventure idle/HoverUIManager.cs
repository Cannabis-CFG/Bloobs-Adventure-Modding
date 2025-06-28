using UnityEngine;
using UnityEngine.UI;

namespace Multi_bloob_adventure_idle;

public class HoverUIManager : MonoBehaviour
{
    public static HoverUIManager Instance;

    private GameObject panel;
    private Text infoText;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            CreateUI();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void CreateUI()
    {
        // Canvas
        var canvasGO = new GameObject("HoverCanvas");
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasGO.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasGO.AddComponent<GraphicRaycaster>();

        // Panel
        panel = new GameObject("HoverPanel");
        panel.transform.SetParent(canvasGO.transform, false);
        var img = panel.AddComponent<Image>();
        img.color = new Color(0, 0, 0, 0.7f);

        var rt = panel.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(300, 150);
        rt.anchoredPosition = new Vector2(0, -100);

        // Text
        var textGO = new GameObject("HoverText");
        textGO.transform.SetParent(panel.transform, false);
        infoText = textGO.AddComponent<Text>();
        infoText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        infoText.fontSize = 14;
        infoText.alignment = TextAnchor.MiddleCenter;
        infoText.color = Color.white;

        var textRT = textGO.GetComponent<RectTransform>();
        textRT.sizeDelta = new Vector2(280, 130);
        textRT.anchoredPosition = Vector2.zero;

        panel.SetActive(false);
    }

    public void ShowInfo(string info, Vector3 screenPosition)
    {
        panel.SetActive(true);
        infoText.text = info;

        RectTransform rt = panel.GetComponent<RectTransform>();
        rt.position = screenPosition;
    }

    public void HideInfo()
    {
        panel.SetActive(false);
    }
}