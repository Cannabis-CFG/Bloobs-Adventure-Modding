using BepInEx.Configuration;
using UnityEngine;

namespace Multi_bloob_adventure_idle
{
    public sealed class ChatThemeSettings(ConfigFile config)
    {
        public readonly ConfigEntry<float> WindowPosX = config.Bind("Chat Window", "Position X", 20f, "Saved chat window X position.");
        public readonly ConfigEntry<float> WindowPosY = config.Bind("Chat Window", "Position Y", 330f, "Saved chat window Y position.");
        public readonly ConfigEntry<float> WindowWidth = config.Bind("Chat Window", "Width", 760f, "Saved chat window width.");
        public readonly ConfigEntry<float> WindowHeight = config.Bind("Chat Window", "Height", 340f, "Saved chat window height.");
        public readonly ConfigEntry<bool> WindowVisible = config.Bind("Chat Window", "Visible", true, "If false, the chat window is hidden.");
        public readonly ConfigEntry<bool> WindowPinned = config.Bind("Chat Window", "Pinned", false, "If true, the chat window cannot be dragged.");

        public readonly ConfigEntry<float> BackgroundOpacity = config.Bind("Chat Theme", "Background Opacity", 0.94f, "Main chat window opacity.");
        public readonly ConfigEntry<string> BackgroundColor = config.Bind("Chat Theme", "Background Color", "#161616", "Main chat window background color.");
        public readonly ConfigEntry<string> AccentColor = config.Bind("Chat Theme", "Accent Color", "#2D7DFF", "Accent color.");
        public readonly ConfigEntry<string> BorderColor = config.Bind("Chat Theme", "Border Color", "#4A4A4A", "Border color.");

        public readonly ConfigEntry<string> TabColor = config.Bind("Chat Theme", "Tab Color", "#2E2E2E", "Unselected tab/button background color.");
        public readonly ConfigEntry<string> SelectedTabColor = config.Bind("Chat Theme", "Selected Tab Color", "#2D7DFF", "Selected tab/button background color.");
        public readonly ConfigEntry<string> TabTextColor = config.Bind("Chat Theme", "Tab Text Color", "#FFFFFF", "Unselected tab/button text color.");
        public readonly ConfigEntry<string> SelectedTabTextColor = config.Bind("Chat Theme", "Selected Tab Text Color", "#FFFFFF", "Selected tab/button text color.");

        public readonly ConfigEntry<string> InputBackgroundColor = config.Bind("Chat Theme", "Input Background Color", "#222222", "Input field background color.");
        public readonly ConfigEntry<float> InputBackgroundOpacity = config.Bind("Chat Theme", "Input Background Opacity", 1f, "Input field background opacity.");
        public readonly ConfigEntry<string> InputTextColor = config.Bind("Chat Theme", "Input Text Color", "#FFFFFF", "Input field text color.");
        public readonly ConfigEntry<string> InputPlaceholderColor = config.Bind("Chat Theme", "Input Placeholder Color", "#FFFFFF", "Input placeholder text color.");
        public readonly ConfigEntry<string> InputOutlineColor = config.Bind("Chat Theme", "Input Outline Color", "#000000", "Input outline color.");
        public readonly ConfigEntry<string> InputOutlineMaxColor = config.Bind("Chat Theme", "Input Outline Max Color", "#FF3333", "Input outline color when character limit is reached.");

        public readonly ConfigEntry<string> TimestampTextColor = config.Bind("Chat Theme", "Timestamp Color", "#B0B0B0", "Timestamp text color.");
        public readonly ConfigEntry<string> StatusTextColor = config.Bind("Chat Theme", "Status Text Color", "#B0B0B0", "Status row text color.");
        public readonly ConfigEntry<string> HeaderTextColor = config.Bind("Chat Theme", "Header Text Color", "#FFFFFF", "Header text color.");
        public readonly ConfigEntry<string> BodyTextColor = config.Bind("Chat Theme", "Body Text Color", "#FFFFFF", "Message body text color.");
        public readonly ConfigEntry<string> CharCounterColor = config.Bind("Chat Theme", "Character Counter Color", "#B0B0B0", "Character counter text color.");
        public readonly ConfigEntry<string> CharCounterMaxColor = config.Bind("Chat Theme", "Character Counter Max Color", "#FF5959", "Character counter text color when limit is reached.");

        public readonly ConfigEntry<string> MessagesViewportColor = config.Bind("Chat Theme", "Messages Viewport Color", "#000000", "Messages viewport color.");
        public readonly ConfigEntry<float> MessagesViewportOpacity = config.Bind("Chat Theme", "Messages Viewport Opacity", 0.12f, "Messages viewport opacity.");
        public readonly ConfigEntry<string> PrivateTabsBackgroundColor = config.Bind("Chat Theme", "Private Tabs Background Color", "#000000", "Private tabs strip color.");
        public readonly ConfigEntry<float> PrivateTabsBackgroundOpacity = config.Bind("Chat Theme", "Private Tabs Background Opacity", 0.15f, "Private tabs strip opacity.");

        public readonly ConfigEntry<string> MessageGlobalBackgroundColor = config.Bind("Chat Theme", "Global Message Background Color", "#FFFFFF", "Global message row color.");
        public readonly ConfigEntry<float> MessageGlobalBackgroundOpacity = config.Bind("Chat Theme", "Global Message Background Opacity", 0.04f, "Global message row opacity.");
        public readonly ConfigEntry<string> MessagePrivateBackgroundColor = config.Bind("Chat Theme", "Private Message Background Color", "#142452", "Private message row color.");
        public readonly ConfigEntry<float> MessagePrivateBackgroundOpacity = config.Bind("Chat Theme", "Private Message Background Opacity", 0.38f, "Private message row opacity.");
        public readonly ConfigEntry<string> MessageImportantBackgroundColor = config.Bind("Chat Theme", "Important Message Background Color", "#614703", "Important message row color.");
        public readonly ConfigEntry<float> MessageImportantBackgroundOpacity = config.Bind("Chat Theme", "Important Message Background Opacity", 0.55f, "Important message row opacity.");
        public readonly ConfigEntry<string> MessageCriticalBackgroundColor = config.Bind("Chat Theme", "Critical Message Background Color", "#6B1414", "Critical message row color.");
        public readonly ConfigEntry<float> MessageCriticalBackgroundOpacity = config.Bind("Chat Theme", "Critical Message Background Opacity", 0.62f, "Critical message row opacity.");
        public readonly ConfigEntry<string> MessageErrorBackgroundColor = config.Bind("Chat Theme", "Error Message Background Color", "#591919", "Error message row color.");
        public readonly ConfigEntry<float> MessageErrorBackgroundOpacity = config.Bind("Chat Theme", "Error Message Background Opacity", 0.62f, "Error message row opacity.");

        public readonly ConfigEntry<string> TitleMenuBackgroundColor = config.Bind("Chat Theme", "Title Menu Background Color", "#1F1F1F", "Title selection menu background color.");
        public readonly ConfigEntry<float> TitleMenuBackgroundOpacity = config.Bind("Chat Theme", "Title Menu Background Opacity", 0.98f, "Title selection menu background opacity.");
        public readonly ConfigEntry<string> TitleMenuOutlineColor = config.Bind("Chat Theme", "Title Menu Outline Color", "#000000", "Title selection menu outline color.");

        public readonly ConfigEntry<string> ResizeHandleColor = config.Bind("Chat Theme", "Resize Handle Color", "#FFFFFF", "Resize handle color.");
        public readonly ConfigEntry<float> ResizeHandleOpacity = config.Bind("Chat Theme", "Resize Handle Opacity", 0.08f, "Resize handle opacity.");

        public readonly ConfigEntry<float> UiScale = config.Bind("Chat Theme", "UI Scale", 1f, "Overall chat UI scale.");
        public readonly ConfigEntry<float> BaseFontSize = config.Bind("Chat Theme", "Base Font Size", 18f, "Base TMP font size.");

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

        public Color GetTabColor() => ParseColor(TabColor.Value, new Color(0.18f, 0.18f, 0.18f), 1f);
        public Color GetSelectedTabColor() => ParseColor(SelectedTabColor.Value, new Color(0.18f, 0.49f, 1f), 1f);
        public Color GetTabTextColor() => ParseColor(TabTextColor.Value, Color.white, 1f);
        public Color GetSelectedTabTextColor() => ParseColor(SelectedTabTextColor.Value, Color.white, 1f);

        public Color GetInputBackgroundColor() => ParseColor(InputBackgroundColor.Value, new Color(0.13f, 0.13f, 0.13f), Mathf.Clamp01(InputBackgroundOpacity.Value));
        public Color GetInputTextColor() => ParseColor(InputTextColor.Value, Color.white, 1f);
        public Color GetInputPlaceholderColor() => ParseColor(InputPlaceholderColor.Value, Color.white, 0.45f);
        public Color GetInputOutlineColor() => ParseColor(InputOutlineColor.Value, Color.black, 0.55f);
        public Color GetInputOutlineMaxColor() => ParseColor(InputOutlineMaxColor.Value, new Color(1f, 0.2f, 0.2f), 0.95f);

        public Color GetTimestampColor() => ParseColor(TimestampTextColor.Value, new Color(0.69f, 0.69f, 0.69f), 1f);
        public Color GetStatusTextColor() => ParseColor(StatusTextColor.Value, new Color(0.69f, 0.69f, 0.69f), 1f);
        public Color GetHeaderTextColor() => ParseColor(HeaderTextColor.Value, Color.white, 1f);
        public Color GetBodyTextColor() => ParseColor(BodyTextColor.Value, Color.white, 1f);
        public Color GetCharCounterColor() => ParseColor(CharCounterColor.Value, new Color(0.69f, 0.69f, 0.69f), 1f);
        public Color GetCharCounterMaxColor() => ParseColor(CharCounterMaxColor.Value, new Color(1f, 0.35f, 0.35f), 1f);

        public Color GetMessagesViewportColor() => ParseColor(MessagesViewportColor.Value, Color.black, Mathf.Clamp01(MessagesViewportOpacity.Value));
        public Color GetPrivateTabsBackgroundColor() => ParseColor(PrivateTabsBackgroundColor.Value, Color.black, Mathf.Clamp01(PrivateTabsBackgroundOpacity.Value));

        public Color GetMessageGlobalBackgroundColor() => ParseColor(MessageGlobalBackgroundColor.Value, Color.white, Mathf.Clamp01(MessageGlobalBackgroundOpacity.Value));
        public Color GetMessagePrivateBackgroundColor() => ParseColor(MessagePrivateBackgroundColor.Value, new Color(0.08f, 0.14f, 0.32f), Mathf.Clamp01(MessagePrivateBackgroundOpacity.Value));
        public Color GetMessageImportantBackgroundColor() => ParseColor(MessageImportantBackgroundColor.Value, new Color(0.38f, 0.28f, 0.02f), Mathf.Clamp01(MessageImportantBackgroundOpacity.Value));
        public Color GetMessageCriticalBackgroundColor() => ParseColor(MessageCriticalBackgroundColor.Value, new Color(0.42f, 0.08f, 0.08f), Mathf.Clamp01(MessageCriticalBackgroundOpacity.Value));
        public Color GetMessageErrorBackgroundColor() => ParseColor(MessageErrorBackgroundColor.Value, new Color(0.35f, 0.1f, 0.1f), Mathf.Clamp01(MessageErrorBackgroundOpacity.Value));

        public Color GetTitleMenuBackgroundColor() => ParseColor(TitleMenuBackgroundColor.Value, new Color(0.12f, 0.12f, 0.12f), Mathf.Clamp01(TitleMenuBackgroundOpacity.Value));
        public Color GetTitleMenuOutlineColor() => ParseColor(TitleMenuOutlineColor.Value, Color.black, 0.5f);

        public Color GetResizeHandleColor() => ParseColor(ResizeHandleColor.Value, Color.white, Mathf.Clamp01(ResizeHandleOpacity.Value));

        public float GetScaledFontSize() => Mathf.Max(12f, BaseFontSize.Value) * Mathf.Clamp(UiScale.Value, 0.75f, 2f);
    }
}
