using BepInEx.Configuration;
using UnityEngine;

namespace Multi_bloob_adventure_idle
{
    public sealed class ChatThemeSettings
    {
        public readonly ConfigEntry<float> WindowPosX;
        public readonly ConfigEntry<float> WindowPosY;
        public readonly ConfigEntry<float> WindowWidth;
        public readonly ConfigEntry<float> WindowHeight;
        public readonly ConfigEntry<bool> WindowVisible;
        public readonly ConfigEntry<bool> WindowPinned;

        public readonly ConfigEntry<float> BackgroundOpacity;
        public readonly ConfigEntry<string> BackgroundColor;
        public readonly ConfigEntry<string> AccentColor;
        public readonly ConfigEntry<string> BorderColor;
        public readonly ConfigEntry<string> InputBackgroundColor;
        public readonly ConfigEntry<string> InputTextColor;
        public readonly ConfigEntry<string> TimestampTextColor;
        public readonly ConfigEntry<float> UiScale;
        public readonly ConfigEntry<float> BaseFontSize;

        public ChatThemeSettings(ConfigFile config)
        {
            WindowPosX = config.Bind("Chat Window", "Position X", 20f, "Saved chat window X position.");
            WindowPosY = config.Bind("Chat Window", "Position Y", 330f, "Saved chat window Y position.");
            WindowWidth = config.Bind("Chat Window", "Width", 760f, "Saved chat window width.");
            WindowHeight = config.Bind("Chat Window", "Height", 340f, "Saved chat window height.");
            WindowVisible = config.Bind("Chat Window", "Visible", true, "If false, the chat window is hidden.");
            WindowPinned = config.Bind("Chat Window", "Pinned", false, "If true, the chat window cannot be dragged.");

            BackgroundOpacity = config.Bind("Chat Theme", "Background Opacity", 0.94f, "Main chat window opacity.");
            BackgroundColor = config.Bind("Chat Theme", "Background Color", "#161616", "Main chat window background color.");
            AccentColor = config.Bind("Chat Theme", "Accent Color", "#2D7DFF", "Accent color.");
            BorderColor = config.Bind("Chat Theme", "Border Color", "#4A4A4A", "Border color.");
            InputBackgroundColor = config.Bind("Chat Theme", "Input Background Color", "#222222", "Input field background color.");
            InputTextColor = config.Bind("Chat Theme", "Input Text Color", "#FFFFFF", "Input field text color.");
            TimestampTextColor = config.Bind("Chat Theme", "Timestamp Color", "#B0B0B0", "Timestamp text color.");
            UiScale = config.Bind("Chat Theme", "UI Scale", 1f, "Overall chat UI scale.");
            BaseFontSize = config.Bind("Chat Theme", "Base Font Size", 18f, "Base TMP font size.");
        }

        public Rect GetWindowRect()
        {
            return new Rect(WindowPosX.Value, WindowPosY.Value, WindowWidth.Value, WindowHeight.Value);
        }

        public void SaveWindowRect(Rect rect)
        {
            WindowPosX.Value = rect.x;
            WindowPosY.Value = rect.y;
            WindowWidth.Value = rect.width;
            WindowHeight.Value = rect.height;
        }

        public static Color ParseColor(string raw, Color fallback, float alpha = 1f)
        {
            if (!string.IsNullOrWhiteSpace(raw) && ColorUtility.TryParseHtmlString(raw, out var parsed))
            {
                parsed.a = alpha;
                return parsed;
            }

            fallback.a = alpha;
            return fallback;
        }

        public Color GetBackgroundColor() => ParseColor(BackgroundColor.Value, new Color(0.09f, 0.09f, 0.09f), Mathf.Clamp01(BackgroundOpacity.Value));
        public Color GetAccentColor() => ParseColor(AccentColor.Value, new Color(0.18f, 0.49f, 1f), 1f);
        public Color GetBorderColor() => ParseColor(BorderColor.Value, new Color(0.29f, 0.29f, 0.29f), 1f);
        public Color GetInputBackgroundColor() => ParseColor(InputBackgroundColor.Value, new Color(0.13f, 0.13f, 0.13f), 1f);
        public Color GetInputTextColor() => ParseColor(InputTextColor.Value, Color.white, 1f);
        public Color GetTimestampColor() => ParseColor(TimestampTextColor.Value, new Color(0.69f, 0.69f, 0.69f), 1f);
        public float GetScaledFontSize() => Mathf.Max(12f, BaseFontSize.Value) * Mathf.Clamp(UiScale.Value, 0.75f, 2f);
    }
}
