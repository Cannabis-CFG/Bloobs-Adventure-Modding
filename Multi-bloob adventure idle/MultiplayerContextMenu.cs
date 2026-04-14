using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Multi_bloob_adventure_idle
{
    public class MultiplayerContextMenu : MonoBehaviour
    {
        private sealed class PlayerMenuTarget
        {
            public string SteamId;
            public string DisplayName;
        }

        private sealed class ButtonEntry
        {
            public Button Button;
            public TextMeshProUGUI Text;
        }

        public Canvas uiCanvas;
        public GameObject buttonPrefab;

        private GameObject menuGo;
        private RectTransform menuRT;
        private TextMeshProUGUI titleText;
        private const float BtnHeight = 30f;
        private const float Padding = 4f;
        private const float DefaultWidth = 190f;
        private readonly List<PlayerMenuTarget> currentTargets = [];
        private readonly List<ButtonEntry> currentButtons = [];
        private ChatThemeSettings _theme;

        public static MultiplayerContextMenu Instance { get; private set; }
        public static bool IsContextMenuOpen { get; private set; }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            if (FindObjectOfType<EventSystem>() == null)
                new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

            _theme = UiThemeUtility.GetSharedTheme();
            CreateCanvas();
        }

        void Update()
        {
            if (Input.GetMouseButtonDown(1) && MultiplayerPatchPlugin.enableContextMenu.Value && !ChatSystem.ShouldBlockGameInput)
                TryShowMenu();

            if (!IsContextMenuOpen)
                return;

            if (Input.GetMouseButtonDown(0) && !IsPointerOverMenu())
                CloseMenu();

            if (Input.GetKeyDown(KeyCode.Escape))
                CloseMenu();
        }

        public void ShowPlayerActions(string steamId, string displayName, Vector2 screenPosition)
        {
            if (string.IsNullOrWhiteSpace(steamId))
                return;

            currentTargets.Clear();
            currentTargets.Add(new PlayerMenuTarget
            {
                SteamId = steamId,
                DisplayName = string.IsNullOrWhiteSpace(displayName)
                    ? MultiplayerPatchPlugin.GetPlayerNameFromSteamId(steamId, steamId)
                    : displayName
            });

            BuildPlayerActionMenu(currentTargets[0], screenPosition);
        }

        private void CreateCanvas()
        {
            if (uiCanvas != null)
                return;

            var canvasGo = new GameObject("MultiplayerContextCanvas");
            canvasGo.transform.SetParent(transform, false);
            uiCanvas = canvasGo.AddComponent<Canvas>();
            uiCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            uiCanvas.sortingOrder = 32005;
            canvasGo.AddComponent<GraphicRaycaster>();
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        }

        private void TryShowMenu()
        {
            var hits = MultiplayerHoverDetector.GetPlayersAtScreenPosition(Input.mousePosition)
                .Select(pd => new PlayerMenuTarget
                {
                    SteamId = pd.steamId,
                    DisplayName = MultiplayerPatchPlugin.GetPlayerNameFromSteamId(pd.steamId, pd.name)
                })
                .GroupBy(x => x.SteamId)
                .Select(x => x.First())
                .ToList();

            if (hits.Count == 0)
            {
                CloseMenu();
                return;
            }

            currentTargets.Clear();
            currentTargets.AddRange(hits);

            if (hits.Count == 1)
            {
                BuildPlayerActionMenu(hits[0], Input.mousePosition);
                return;
            }

            BuildPlayerSelectionMenu(Input.mousePosition);
        }

        private void BuildPlayerSelectionMenu(Vector2 screenPosition)
        {
            CreateMenuRoot("CloneContextMenu", screenPosition, DefaultWidth, currentTargets.Count * (BtnHeight + Padding) + Padding + 6f);

            for (int i = 0; i < currentTargets.Count; i++)
            {
                var target = currentTargets[i];
                var entry = CreateButtonRow(target.DisplayName, i);
                var trigger = entry.Button.gameObject.AddComponent<EventTrigger>();

                var enter = new EventTrigger.Entry
                {
                    eventID = EventTriggerType.PointerEnter
                };
                enter.callback.AddListener(_ => ShowCloneInfo(target.SteamId));
                trigger.triggers.Add(enter);

                var exit = new EventTrigger.Entry
                {
                    eventID = EventTriggerType.PointerExit
                };
                exit.callback.AddListener(_ => HoverUIManager.Instance.HideInfo());
                trigger.triggers.Add(exit);

                entry.Button.onClick.AddListener(() => BuildPlayerActionMenu(target, Input.mousePosition));
            }
        }

        private void BuildPlayerActionMenu(PlayerMenuTarget target, Vector2 screenPosition)
        {
            var labels = new List<(string label, System.Action action)>
            {
                ($"Whisper {target.DisplayName}", () => ChatSystem.Instance?.StartWhisperToSteamId(target.SteamId)),
                (ChatSystem.Instance != null && ChatSystem.Instance.IsBlockedSteamId(target.SteamId) ? $"Unblock {target.DisplayName}" : $"Block {target.DisplayName}", () => ChatSystem.Instance?.ToggleBlockedSteamId(target.SteamId)),
                ($"View Profile {target.DisplayName}", () => ChatSystem.Instance?.ShowProfileForSteamId(target.SteamId)),
                ($"Copy SteamID {target.DisplayName}", () => ChatSystem.Instance?.CopySteamIdToClipboard(target.SteamId))
            };

            CreateMenuRoot("PlayerContextMenu", screenPosition, DefaultWidth, labels.Count * (BtnHeight + Padding) + Padding + 20f);
            titleText.text = target.DisplayName;

            for (int i = 0; i < labels.Count; i++)
            {
                var item = labels[i];
                var entry = CreateButtonRow(item.label, i, topOffset: 24f);
                entry.Button.onClick.AddListener(() =>
                {
                    item.action?.Invoke();
                    CloseMenu();
                });
            }
        }

        private void CreateMenuRoot(string name, Vector2 screenPosition, float width, float height)
        {
            _theme = UiThemeUtility.GetSharedTheme();
            CreateCanvas();
            CloseMenu();
            currentButtons.Clear();

            menuGo = new GameObject(name, typeof(RectTransform), typeof(Image));
            menuGo.transform.SetParent(uiCanvas.transform, false);
            menuRT = menuGo.GetComponent<RectTransform>();

            var bg = menuGo.GetComponent<Image>();
            var outline = menuGo.AddComponent<Outline>();
            UiThemeUtility.ApplyPanelStyle(bg, outline, _theme);

            menuRT.anchorMin = new Vector2(0f, 0f);
            menuRT.anchorMax = new Vector2(0f, 0f);
            menuRT.sizeDelta = new Vector2(width, height);
            menuRT.pivot = new Vector2(0f, 1f);

            var scaled = screenPosition / uiCanvas.scaleFactor;
            var maxX = Mathf.Max(0f, Screen.width / uiCanvas.scaleFactor - width - 8f);
            var maxY = Mathf.Max(height + 8f, Screen.height / uiCanvas.scaleFactor);
            menuRT.anchoredPosition = new Vector2(
                Mathf.Clamp(scaled.x, 8f, maxX),
                Mathf.Clamp(scaled.y, height + 8f, maxY - 8f));

            var title = new GameObject("Title", typeof(RectTransform));
            title.transform.SetParent(menuGo.transform, false);
            var titleRt = title.GetComponent<RectTransform>();
            titleRt.anchorMin = new Vector2(0f, 1f);
            titleRt.anchorMax = new Vector2(1f, 1f);
            titleRt.pivot = new Vector2(0f, 1f);
            titleRt.sizeDelta = new Vector2(-2f * Padding, 20f);
            titleRt.anchoredPosition = new Vector2(Padding, -Padding);

            titleText = title.AddComponent<TextMeshProUGUI>();
            titleText.fontSize = UiThemeUtility.GetScaledFont(_theme, 16f);
            titleText.alignment = TextAlignmentOptions.Center;
            titleText.color = _theme != null ? _theme.GetHeaderTextColor() : Color.white;
            titleText.enableWordWrapping = false;
            titleText.overflowMode = TextOverflowModes.Ellipsis;
            titleText.raycastTarget = false;
            titleText.text = string.Empty;

            IsContextMenuOpen = true;
        }

        private ButtonEntry CreateButtonRow(string label, int index, float topOffset = 0f)
        {
            var button = UiThemeUtility.CreateButton("Btn_" + label, menuGo.transform, out var text, label, DefaultWidth - 2f * Padding, BtnHeight);
            var btnRT = button.GetComponent<RectTransform>();
            btnRT.anchorMin = new Vector2(0f, 1f);
            btnRT.anchorMax = new Vector2(1f, 1f);
            btnRT.pivot = new Vector2(0f, 1f);
            btnRT.sizeDelta = new Vector2(-2f * Padding, BtnHeight);
            btnRT.anchoredPosition = new Vector2(Padding, -Padding - topOffset - index * (BtnHeight + Padding));

            UiThemeUtility.ApplyButtonStyle(button, text, _theme);
            var entry = new ButtonEntry { Button = button, Text = text };
            currentButtons.Add(entry);
            return entry;
        }

        private void ShowCloneInfo(string steamId)
        {
            if (MultiplayerPatchPlugin.Players.TryGetValue(steamId, out var pd))
            {
                string info = MultiplayerHoverDetector.BuildHoverInfo(MultiplayerPatchPlugin.GetPlayerNameFromSteamId(steamId), pd);
                HoverUIManager.Instance.ShowInfo(info, Input.mousePosition);
            }
        }

        private bool IsPointerOverMenu()
        {
            if (menuRT == null || !menuRT.gameObject.activeInHierarchy)
                return false;

            return RectTransformUtility.RectangleContainsScreenPoint(menuRT, Input.mousePosition, null);
        }

        public void CloseMenu()
        {
            if (menuGo != null)
                Destroy(menuGo);

            menuGo = null;
            menuRT = null;
            titleText = null;
            HoverUIManager.Instance?.HideInfo();
            IsContextMenuOpen = false;
        }
    }
}
