using BepInEx.Configuration;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Multi_bloob_adventure_idle;

public class HoverUIManager : MonoBehaviour
{
    //–– Config entries ––
    private static ConfigEntry<int> _panelWidth;
    private static ConfigEntry<int> _panelHeight;
    private static ConfigEntry<int> _panelPosY;
    private static ConfigEntry<float> _panelAlpha;
    private static ConfigEntry<int> _panelColorR;
    private static ConfigEntry<int> _panelColorG;
    private static ConfigEntry<int> _panelColorB;

    private static ConfigEntry<bool> _shadowEnabled;
    private static ConfigEntry<int> _shadowColorR;
    private static ConfigEntry<int> _shadowColorG;
    private static ConfigEntry<int> _shadowColorB;
    private static ConfigEntry<float> _shadowAlpha;
    private static ConfigEntry<int> _shadowOffsetX;
    private static ConfigEntry<int> _shadowOffsetY;

    private static ConfigEntry<int> _fontSize;
    private static ConfigEntry<int> _textColorR;
    private static ConfigEntry<int> _textColorG;
    private static ConfigEntry<int> _textColorB;
    private static ConfigEntry<float> _textAlpha;
    private static ConfigEntry<int> _marginLeft;
    private static ConfigEntry<int> _marginTop;
    private static ConfigEntry<int> _marginRight;
    private static ConfigEntry<int> _marginBottom;

    //–– UI fields ––
    GameObject _canvasGO;
    GameObject _panel;
    RectTransform _panelRT;
    Image _panelImage;
    Shadow _panelShadow;
    TextMeshProUGUI _infoText;
    RectTransform _textRT;

    public static HoverUIManager Instance;

    void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        var cfg = MultiplayerPatchPlugin.instance.Config;

        // Panel dimensions & position
        _panelWidth = cfg.Bind("UI", "Panel Width", 300, "Hover panel width");
        _panelHeight = cfg.Bind("UI", "Panel Height", 150, "Hover panel height");
        _panelPosY = cfg.Bind("UI", "Panel Pos Y", -100, "Hover panel vertical offset");

        // Panel background color & alpha
        _panelColorR = cfg.Bind("UI", "Panel Color R", 30, "Background R 0–255");
        _panelColorG = cfg.Bind("UI", "Panel Color G", 30, "Background G 0–255");
        _panelColorB = cfg.Bind("UI", "Panel Color B", 30, "Background B 0–255");
        _panelAlpha = cfg.Bind("UI", "Panel Alpha", 0.7f, "Background opacity 0–1");

        // Shadow
        _shadowEnabled = cfg.Bind("UI", "Shadow Enabled", true, "Enable panel drop shadow");
        _shadowColorR = cfg.Bind("UI", "Shadow Color R", 0, "Shadow color R 0–255");
        _shadowColorG = cfg.Bind("UI", "Shadow Color G", 0, "Shadow color G 0–255");
        _shadowColorB = cfg.Bind("UI", "Shadow Color B", 0, "Shadow color B 0–255");
        _shadowAlpha = cfg.Bind("UI", "Shadow Alpha", 0.5f, "Shadow opacity 0–1");
        _shadowOffsetX = cfg.Bind("UI", "Shadow Offset X", 4, "Shadow horizontal offset");
        _shadowOffsetY = cfg.Bind("UI", "Shadow Offset Y", -4, "Shadow vertical offset");

        // Text styling
        _fontSize = cfg.Bind("UI", "Font Size", 14, "Hover text font size");
        _textColorR = cfg.Bind("UI", "Text Color R", 240, "Text color R 0–255");
        _textColorG = cfg.Bind("UI", "Text Color G", 240, "Text color G 0–255");
        _textColorB = cfg.Bind("UI", "Text Color B", 240, "Text color B 0–255");
        _textAlpha = cfg.Bind("UI", "Text Alpha", 1.0f, "Text opacity 0–1");
        _marginLeft = cfg.Bind("UI", "Margin Left", 8, "Text margin left");
        _marginTop = cfg.Bind("UI", "Margin Top", 8, "Text margin top");
        _marginRight = cfg.Bind("UI", "Margin Right", 8, "Text margin right");
        _marginBottom = cfg.Bind("UI", "Margin Bottom", 8, "Text margin bottom");

        HandleConfigurationSubscriptions();

        CreateUI();
    }

    private void CreateUI()
    {
        // Canvas
        _canvasGO = new GameObject("HoverCanvas");
        var canvas = _canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvasGO.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        _canvasGO.AddComponent<GraphicRaycaster>();

        // Panel
        _panel = new GameObject("HoverPanel");
        _panel.transform.SetParent(_canvasGO.transform, false);

        _panelImage = _panel.AddComponent<Image>();
        _panelRT = _panel.GetComponent<RectTransform>();

        _panelShadow = _panel.AddComponent<Shadow>();

        // Text
        var textGO = new GameObject("HoverText");
        textGO.transform.SetParent(_panel.transform, false);

        _infoText = textGO.AddComponent<TextMeshProUGUI>();
        _textRT = _infoText.GetComponent<RectTransform>();

        ApplyAllStyles();
        _panel.SetActive(false);
    }

    private void ApplyAllStyles()
    {
        // Panel size & pos
        _panelRT.sizeDelta = new Vector2(_panelWidth.Value, _panelHeight.Value);
        _panelRT.anchoredPosition = new Vector2(0, _panelPosY.Value);

        // Panel color
        _panelImage.color = new Color32(
            (byte)_panelColorR.Value,
            (byte)_panelColorG.Value,
            (byte)_panelColorB.Value,
            (byte)(_panelAlpha.Value * 255)
        );

        // Shadow
        _panelShadow.enabled = _shadowEnabled.Value;
        _panelShadow.effectColor = new Color32(
            (byte)_shadowColorR.Value,
            (byte)_shadowColorG.Value,
            (byte)_shadowColorB.Value,
            (byte)(_shadowAlpha.Value * 255)
        );
        _panelShadow.effectDistance = new Vector2(_shadowOffsetX.Value, _shadowOffsetY.Value);

        // Text styling
        _infoText.fontSize = _fontSize.Value;
        _infoText.color = new Color32(
            (byte)_textColorR.Value,
            (byte)_textColorG.Value,
            (byte)_textColorB.Value,
            (byte)(_textAlpha.Value * 255)
        );
        _infoText.margin = new Vector4(
            _marginLeft.Value,
            _marginTop.Value,
            _marginRight.Value,
            _marginBottom.Value
        );
        _infoText.enableWordWrapping = true;
        _infoText.alignment = TextAlignmentOptions.Center;
    }

    public void ShowInfo(string info, Vector3 screenPosition)
    {
        _infoText.text = info;
        _panelRT.position = screenPosition;
        _panel.SetActive(true);
    }

    public void HideInfo() => _panel.SetActive(false);

    public void HandleConfigurationSubscriptions()
    {
        _panelWidth.SettingChanged += (_, __) => ApplyAllStyles();
        _panelHeight.SettingChanged += (_, __) => ApplyAllStyles();
        _panelPosY.SettingChanged += (_, __) => ApplyAllStyles();

        _panelColorR.SettingChanged += (_, __) => ApplyAllStyles();
        _panelColorG.SettingChanged += (_, __) => ApplyAllStyles();
        _panelColorB.SettingChanged += (_, __) => ApplyAllStyles();
        _panelAlpha.SettingChanged += (_, __) => ApplyAllStyles();

        _shadowEnabled.SettingChanged += (_, __) => ApplyAllStyles();
        _shadowColorR.SettingChanged += (_, __) => ApplyAllStyles();
        _shadowColorG.SettingChanged += (_, __) => ApplyAllStyles();
        _shadowColorB.SettingChanged += (_, __) => ApplyAllStyles();
        _shadowAlpha.SettingChanged += (_, __) => ApplyAllStyles();
        _shadowOffsetX.SettingChanged += (_, __) => ApplyAllStyles();
        _shadowOffsetY.SettingChanged += (_, __) => ApplyAllStyles();

        _fontSize.SettingChanged += (_, __) => ApplyAllStyles();
        _textColorR.SettingChanged += (_, __) => ApplyAllStyles();
        _textColorG.SettingChanged += (_, __) => ApplyAllStyles();
        _textColorB.SettingChanged += (_, __) => ApplyAllStyles();
        _textAlpha.SettingChanged += (_, __) => ApplyAllStyles();

        _marginLeft.SettingChanged += (_, __) => ApplyAllStyles();
        _marginTop.SettingChanged += (_, __) => ApplyAllStyles();
        _marginRight.SettingChanged += (_, __) => ApplyAllStyles();
        _marginBottom.SettingChanged += (_, __) => ApplyAllStyles();
    }
}
