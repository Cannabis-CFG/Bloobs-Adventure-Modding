using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Multi_bloob_adventure_idle
{
    public static class UiThemeUtility
    {
        public static ChatThemeSettings GetSharedTheme()
        {
            if (ChatSystem.Instance != null)
                return ChatSystem.Instance.Theme;

            if (MultiplayerPatchPlugin.instance != null)
                return new ChatThemeSettings(MultiplayerPatchPlugin.instance.Config);

            return null;
        }

        public static RectTransform CreateRect(string name, Transform parent, Vector2? anchorMin = null, Vector2? anchorMax = null, Vector2? pivot = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin ?? new Vector2(0f, 0f);
            rt.anchorMax = anchorMax ?? new Vector2(1f, 1f);
            rt.pivot = pivot ?? new Vector2(0.5f, 0.5f);
            return rt;
        }

        public static void Stretch(RectTransform rt, Vector2? offsetMin = null, Vector2? offsetMax = null)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = offsetMin ?? Vector2.zero;
            rt.offsetMax = offsetMax ?? Vector2.zero;
        }

        public static TextMeshProUGUI CreateText(string name, Transform parent, string text, float fontSize, TextAlignmentOptions alignment)
        {
            var rt = CreateRect(name, parent, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f));
            var tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.alignment = alignment;
            tmp.color = Color.white;
            tmp.raycastTarget = false;
            tmp.enableAutoSizing = false;
            return tmp;
        }

        public static Button CreateButton(string name, Transform parent, out TextMeshProUGUI text, string label, float width, float height)
        {
            var rt = CreateRect(name, parent);
            rt.sizeDelta = new Vector2(width, height);

            rt.gameObject.AddComponent<Image>();
            var button = rt.gameObject.AddComponent<Button>();

            var layout = rt.gameObject.AddComponent<LayoutElement>();
            layout.preferredWidth = width;
            layout.minWidth = width;
            layout.preferredHeight = height;
            layout.minHeight = height;

            text = CreateText("Text", rt, label, 16f, TextAlignmentOptions.Center);
            text.enableWordWrapping = false;
            text.overflowMode = TextOverflowModes.Ellipsis;
            Stretch(text.rectTransform, new Vector2(6f, 2f), new Vector2(-6f, -2f));

            return button;
        }

        public static Color WithAlpha(Color color, float alphaMultiplier)
        {
            color.a *= Mathf.Clamp01(alphaMultiplier);
            return color;
        }

        public static Color GetSurfaceColor(ChatThemeSettings theme, float alphaMultiplier = 1f)
        {
            if (theme == null)
                return new Color(0.1f, 0.1f, 0.1f, 0.96f);

            return WithAlpha(theme.GetBackgroundColor(), alphaMultiplier);
        }

        public static Color GetSubtleSurfaceColor(ChatThemeSettings theme, float alphaMultiplier = 1f)
        {
            if (theme == null)
                return new Color(0.16f, 0.16f, 0.16f, 0.95f);

            return WithAlpha(theme.GetTabColor(), alphaMultiplier);
        }

        public static void ApplyPanelStyle(Image panelImage, Outline outline, ChatThemeSettings theme)
        {
            if (panelImage != null)
                panelImage.color = GetSurfaceColor(theme, 1f);

            if (outline != null)
                outline.effectColor = theme != null ? theme.GetBorderColor() : new Color(0f, 0f, 0f, 0.45f);
        }

        public static void ApplyScrollViewportStyle(Image viewportImage, ChatThemeSettings theme)
        {
            if (viewportImage != null)
                viewportImage.color = theme != null ? theme.GetMessagesViewportColor() : new Color(0f, 0f, 0f, 0.15f);
        }

        public static void ApplyButtonStyle(Button button, TextMeshProUGUI text, ChatThemeSettings theme, bool selected = false)
        {
            if (button == null)
                return;

            var image = button.GetComponent<Image>();
            if (image != null)
                image.color = selected && theme != null ? theme.GetSelectedTabColor() : GetSubtleSurfaceColor(theme, 1f);

            if (text != null)
            {
                text.color = selected && theme != null
                    ? theme.GetSelectedTabTextColor()
                    : theme != null ? theme.GetTabTextColor() : Color.white;
                text.fontSize = GetScaledFont(theme, 16f);
            }
        }

        public static float GetScaledFont(ChatThemeSettings theme, float fallback)
        {
            if (theme == null)
                return fallback;

            return Mathf.Max(12f, fallback) * Mathf.Clamp(theme.UiScale.Value, 0.75f, 2f);
        }
    }
}
