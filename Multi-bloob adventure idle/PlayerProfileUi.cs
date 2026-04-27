using System;
using System.Collections.Generic;
using System.Linq;
using Steamworks;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Multi_bloob_adventure_idle
{
    public class PlayerProfileUi : MonoBehaviour
    {
        public static PlayerProfileUi Instance { get; private set; }

        private Canvas _canvas;
        private RectTransform _panel;
        private Image _panelImage;
        private Outline _panelOutline;
        private Image _scrollBackgroundImage;
        private TextMeshProUGUI _headerText;
        private TextMeshProUGUI _bodyText;
        private TextMeshProUGUI _footerText;
        private Button _viewClanButton;
        private TextMeshProUGUI _viewClanButtonText;
        private Button _closeButton;
        private TextMeshProUGUI _closeButtonText;
        private ScrollRect _scrollRect;
        private RectTransform _content;
        private string _currentSteamId;
        private ChatThemeSettings _theme;

        public bool IsOpen => _panel != null && _panel.gameObject.activeSelf;

        public static PlayerProfileUi Create()
        {
            if (Instance != null)
                return Instance;

            var go = new GameObject("PlayerProfileUi");
            DontDestroyOnLoad(go);
            return go.AddComponent<PlayerProfileUi>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            EnsureEventSystem();
            _theme = UiThemeUtility.GetSharedTheme();
            BuildUi();
            ApplyTheme();
            Hide();
        }

        private void Update()
        {
            if (IsOpen && Input.GetKeyDown(KeyCode.Escape))
                Hide();

            if (IsOpen && !string.IsNullOrWhiteSpace(_currentSteamId))
                RefreshVisibleProfile();
        }

        public void ShowProfile(string steamId)
        {
            if (string.IsNullOrWhiteSpace(steamId))
                return;

            _currentSteamId = steamId;
            RefreshVisibleProfile();
            _panel.gameObject.SetActive(true);
            ForceRebuildAndScrollToTop();
        }

        public void Hide()
        {
            if (_panel != null)
                _panel.gameObject.SetActive(false);
        }

        private void RefreshVisibleProfile()
        {
            var data = ResolvePlayerData(_currentSteamId);
            if (data == null)
                return;

            _theme = UiThemeUtility.GetSharedTheme();
            ApplyTheme();

            var displayName = MultiplayerPatchPlugin.GetPlayerNameFromSteamId(_currentSteamId, data.name ?? "Unknown Player");
            _headerText.text = displayName;
            _bodyText.text = BuildProfileBody(data);
            _footerText.text = BuildFooterText(data);

            bool hasClan = !string.IsNullOrWhiteSpace(data.clanId);
            _viewClanButton.gameObject.SetActive(hasClan);
            _viewClanButton.onClick.RemoveAllListeners();
            if (hasClan)
                _viewClanButton.onClick.AddListener(() => ChatSystem.Instance?.ShowClanProfile(data.clanId));
        }

        private PlayerData ResolvePlayerData(string steamId)
        {
            if (MultiplayerPatchPlugin.Players.TryGetValue(steamId, out var remoteData) && remoteData != null)
                return remoteData;

            if (SteamClient.IsValid && SteamClient.SteamId.ToString() == steamId)
                return MultiplayerPatchPlugin.instance?.BuildLocalPlayerProfileSnapshot();

            return null;
        }

        private string BuildProfileBody(PlayerData data)
        {
            var lines = new List<string>
            {
                $"SteamID: {data.steamId}",
                $"Title: {BuildTitleLabel(data)}",
                $"Clan: {BuildClanLabel(data)}",
                $"Save Type: {(data.isTurboSave ? "Turbo" : "Standard")}",
                $"Total Level: {ComputeTotalLevel(data)}",
                $"Total Prestige: {ComputeTotalPrestige(data)}",
                string.Empty,
                "Skills:"
            };

            foreach (var kvp in (data.skillData ?? [])
                .OrderByDescending(x => x.Value.prestige)
                .ThenByDescending(x => x.Value.level)
                .ThenBy(x => x.Key, StringComparer.OrdinalIgnoreCase))
            {
                string entry = kvp.Value.prestige > 0
                    ? $" - {kvp.Key}: Level {kvp.Value.level} (Prestige {kvp.Value.prestige})"
                    : $" - {kvp.Key}: Level {kvp.Value.level}";

                if (data.skillExperienceData != null && data.skillExperienceData.TryGetValue(kvp.Key, out var xpValue))
                    entry += $" | XP {Math.Round(xpValue, 2)}";

                lines.Add(entry);
            }

            if (data.bossKillData != null && data.bossKillData.Count > 0)
            {
                lines.Add(string.Empty);
                lines.Add("Boss Kills:");
                foreach (var boss in data.bossKillData.OrderByDescending(x => x.Value).ThenBy(x => x.Key, StringComparer.OrdinalIgnoreCase))
                    lines.Add($" - {boss.Key}: {boss.Value}");
            }

            var clanState = ChatSystem.Instance?.GetCurrentClanState();
            if (clanState != null && !string.IsNullOrWhiteSpace(data.clanId) && clanState.clanId == data.clanId)
            {
                if (clanState.contributionsByMember != null && clanState.contributionsByMember.TryGetValue(data.steamId, out var contribution) && contribution != null)
                {
                    lines.Add(string.Empty);
                    lines.Add("Clan Contribution:");
                    lines.Add($" - Total XP Contributed: {Math.Round(contribution.totalExperience, 2)}");
                    lines.Add($" - Total Levels Contributed: {contribution.totalLevelsGained}");
                    lines.Add($" - Total Prestige Contributed: {contribution.totalPrestigeGained}");
                    lines.Add($" - Total Boss Kills Contributed: {contribution.totalBossKills}");

                    foreach (var boss in (contribution.bossKills ?? new Dictionary<string, long>())
                        .OrderByDescending(x => x.Value)
                        .ThenBy(x => x.Key, StringComparer.OrdinalIgnoreCase))
                    {
                        lines.Add($"   - {boss.Key}: {boss.Value}");
                    }
                }
            }

            return string.Join("\n", lines);
        }

        private string BuildFooterText(PlayerData data)
        {
            if (!string.IsNullOrWhiteSpace(data.clanTag) && !string.IsNullOrWhiteSpace(data.clanName))
                return $"[{data.clanTag}] {data.clanName}";

            return data.isTurboSave ? "Turbo save active" : "Standard save";
        }

        private static string BuildTitleLabel(PlayerData data)
        {
            if (SteamClient.IsValid && data.steamId == SteamClient.SteamId.ToString())
            {
                var chat = ChatSystem.Instance;
                var current = chat?.ActiveTitleId;
                if (!string.IsNullOrWhiteSpace(current) && chat != null)
                {
                    var title = chat.GetUnlockedTitles().FirstOrDefault(x => x.id == current);
                    if (!string.IsNullOrWhiteSpace(title.id))
                        return title.label;
                }
            }

            return "None";
        }

        private static string BuildClanLabel(PlayerData data)
        {
            if (!string.IsNullOrWhiteSpace(data.clanTag) && !string.IsNullOrWhiteSpace(data.clanName))
                return $"[{data.clanTag}] {data.clanName}";
            if (!string.IsNullOrWhiteSpace(data.clanName))
                return data.clanName;
            return "None";
        }

        private static int ComputeTotalLevel(PlayerData data)
        {
            return (data.skillData ?? []).Values.Where(x => x.level > 0).Sum(x => x.level);
        }

        private static int ComputeTotalPrestige(PlayerData data)
        {
            return (data.skillData ?? []).Values.Where(x => x.prestige > 0).Sum(x => x.prestige);
        }

        private void BuildUi()
        {
            _canvas = gameObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 32010;
            gameObject.AddComponent<GraphicRaycaster>();

            _panel = UiThemeUtility.CreateRect("ProfilePanel", transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f));
            _panel.sizeDelta = new Vector2(520f, 560f);
            _panel.anchoredPosition = new Vector2(40f, -80f);
            _panelImage = _panel.gameObject.AddComponent<Image>();
            _panelOutline = _panel.gameObject.AddComponent<Outline>();

            var vertical = _panel.gameObject.AddComponent<VerticalLayoutGroup>();
            vertical.padding = new RectOffset(16, 16, 14, 14);
            vertical.spacing = 6f;
            vertical.childForceExpandWidth = true;
            vertical.childForceExpandHeight = false;

            var dragHandle = _panel.gameObject.AddComponent<PlayerProfileDragHandle>();
            dragHandle.Initialize(this);

            var headerRow = UiThemeUtility.CreateRect("HeaderRow", _panel);
            var headerLayout = headerRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            headerLayout.spacing = 6f;
            headerLayout.childForceExpandWidth = false;
            headerLayout.childForceExpandHeight = true;
            var headerLE = headerRow.gameObject.AddComponent<LayoutElement>();
            headerLE.preferredHeight = 36f;
            headerLE.minHeight = 36f;

            var headerTextRoot = UiThemeUtility.CreateRect("HeaderTextRoot", headerRow);
            var headerTextLayout = headerTextRoot.gameObject.AddComponent<LayoutElement>();
            headerTextLayout.flexibleWidth = 1f;
            _headerText = headerTextRoot.gameObject.AddComponent<TextMeshProUGUI>();
            _headerText.alignment = TextAlignmentOptions.MidlineLeft;
            _headerText.raycastTarget = false;
            _headerText.margin = new Vector4(12f, 0f, 12f, 0f);
            _headerText.text = "Player Profile";

            _viewClanButton = UiThemeUtility.CreateButton("ViewClanButton", headerRow, out _viewClanButtonText, "View Clan", 110f, 32f);
            _closeButton = UiThemeUtility.CreateButton("CloseButton", headerRow, out _closeButtonText, "Close", 88f, 32f);
            _closeButton.onClick.AddListener(Hide);

            var scrollRoot = UiThemeUtility.CreateRect("ScrollRoot", _panel);
            var scrollLE = scrollRoot.gameObject.AddComponent<LayoutElement>();
            scrollLE.flexibleHeight = 1f;
            scrollLE.minHeight = 420f;
            _scrollBackgroundImage = scrollRoot.gameObject.AddComponent<Image>();
            _scrollRect = scrollRoot.gameObject.AddComponent<ScrollRect>();
            _scrollRect.horizontal = false;
            _scrollRect.vertical = true;
            _scrollRect.movementType = ScrollRect.MovementType.Clamped;
            _scrollRect.scrollSensitivity = 28f;

            var viewport = UiThemeUtility.CreateRect("Viewport", scrollRoot);
            UiThemeUtility.Stretch(viewport);
            viewport.gameObject.AddComponent<Image>().color = new Color(1f, 1f, 1f, 0.01f);
            viewport.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            _scrollRect.viewport = viewport;

            _content = UiThemeUtility.CreateRect("Content", viewport, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f));
            var contentLayout = _content.gameObject.AddComponent<VerticalLayoutGroup>();
            contentLayout.padding = new RectOffset(18, 18, 12, 12);
            contentLayout.spacing = 0f;
            contentLayout.childForceExpandHeight = false;
            contentLayout.childForceExpandWidth = true;

            var contentFitter = _content.gameObject.AddComponent<ContentSizeFitter>();
            contentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            _bodyText = _content.gameObject.AddComponent<TextMeshProUGUI>();
            _bodyText.alignment = TextAlignmentOptions.TopLeft;
            _bodyText.enableWordWrapping = true;
            _bodyText.overflowMode = TextOverflowModes.Overflow;
            _bodyText.raycastTarget = false;
            _bodyText.margin = new Vector4(12f, 8f, 12f, 8f);
            _bodyText.text = string.Empty;

            _scrollRect.content = _content;

            var footerRow = UiThemeUtility.CreateRect("FooterRow", _panel);
            var footerLE = footerRow.gameObject.AddComponent<LayoutElement>();
            footerLE.preferredHeight = 28f;
            footerLE.minHeight = 28f;
            _footerText = footerRow.gameObject.AddComponent<TextMeshProUGUI>();
            _footerText.alignment = TextAlignmentOptions.MidlineLeft;
            _footerText.raycastTarget = false;
            _footerText.margin = new Vector4(12f, 0f, 12f, 0f);
            _footerText.text = string.Empty;
        }

        private void ApplyTheme()
        {
            UiThemeUtility.ApplyPanelStyle(_panelImage, _panelOutline, _theme);
            UiThemeUtility.ApplyScrollViewportStyle(_scrollBackgroundImage, _theme);
            UiThemeUtility.ApplyButtonStyle(_viewClanButton, _viewClanButtonText, _theme);
            UiThemeUtility.ApplyButtonStyle(_closeButton, _closeButtonText, _theme);

            _panel.localScale = Vector3.one * Mathf.Clamp(_theme?.UiScale.Value ?? 1f, 0.75f, 2f);

            _headerText.fontSize = UiThemeUtility.GetScaledFont(_theme, 22f);
            _headerText.color = _theme != null ? _theme.GetHeaderTextColor() : Color.white;

            _bodyText.fontSize = UiThemeUtility.GetScaledFont(_theme, 17f);
            _bodyText.color = _theme != null ? _theme.GetBodyTextColor() : Color.white;

            _footerText.fontSize = UiThemeUtility.GetScaledFont(_theme, 14f);
            _footerText.color = _theme != null ? _theme.GetStatusTextColor() : new Color(0.72f, 0.72f, 0.72f, 1f);
        }

        public void MoveWindow(Vector2 delta)
        {
            if (_panel == null)
                return;

            float scale = Mathf.Clamp(_panel.localScale.x, 0.75f, 2f);
            _panel.anchoredPosition += delta / scale;
            ClampToScreen();
        }

        private void ForceRebuildAndScrollToTop()
        {
            if (_panel == null || _scrollRect == null)
                return;

            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(_panel);
            if (_scrollRect.content != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(_scrollRect.content);
            Canvas.ForceUpdateCanvases();

            if (_scrollRect.content != null && _scrollRect.viewport != null)
            {
                _scrollRect.StopMovement();
                _scrollRect.verticalNormalizedPosition = 1f;
            }
        }

        private void ClampToScreen()
        {
            if (_panel == null)
                return;

            float width = _panel.sizeDelta.x * _panel.localScale.x;
            float height = _panel.sizeDelta.y * _panel.localScale.y;
            float maxX = Mathf.Max(0f, Screen.width - width);
            float minY = -Mathf.Max(0f, Screen.height - height);

            _panel.anchoredPosition = new Vector2(
                Mathf.Clamp(_panel.anchoredPosition.x, 0f, maxX),
                Mathf.Clamp(_panel.anchoredPosition.y, minY, 0f));
        }

        private void EnsureEventSystem()
        {
            if (FindObjectOfType<EventSystem>() != null)
                return;

            var go = new GameObject("PlayerProfileEventSystem");
            DontDestroyOnLoad(go);
            go.AddComponent<EventSystem>();
            go.AddComponent<StandaloneInputModule>();
        }
    }

    public sealed class PlayerProfileDragHandle : MonoBehaviour, IBeginDragHandler, IDragHandler
    {
        private PlayerProfileUi _ui;
        public void Initialize(PlayerProfileUi ui) { _ui = ui; }
        public void OnBeginDrag(PointerEventData eventData) { }
        public void OnDrag(PointerEventData eventData) { _ui?.MoveWindow(eventData.delta); }
    }
}
