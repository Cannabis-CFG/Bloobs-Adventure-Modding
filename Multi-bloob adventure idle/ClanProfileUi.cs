using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Multi_bloob_adventure_idle
{
    public class ClanProfileUi : MonoBehaviour
    {
        private enum ClanProfileTab
        {
            Overview,
            Members,
            Upgrades,
            Permissions
        }

        private static readonly string[] PermissionOrder =
        [
            "invitePlayers",
            "kickMembers",
            "manageRoles",
            "managePermissions",
            "purchaseUpgrades",
            "toggleUpgrades"
        ];

        private static readonly string[] EditableRoles =
        [
            "Elder",
            "Deputy",
            "Member"
        ];

        public static ClanProfileUi Instance { get; private set; }

        private Canvas _canvas;
        private RectTransform _panel;
        private Image _panelImage;
        private Outline _panelOutline;
        private Image _scrollBackgroundImage;
        private ScrollRect _scrollRect;
        private RectTransform _content;
        private TextMeshProUGUI _headerText;
        private TextMeshProUGUI _bodyText;
        private TextMeshProUGUI _footerText;
        private Button _closeButton;
        private TextMeshProUGUI _closeButtonText;
        private Button _overviewTabButton;
        private Button _membersTabButton;
        private Button _upgradesTabButton;
        private Button _permissionsTabButton;
        private TextMeshProUGUI _overviewTabText;
        private TextMeshProUGUI _membersTabText;
        private TextMeshProUGUI _upgradesTabText;
        private TextMeshProUGUI _permissionsTabText;
        private RectTransform _actionRoot;
        private TMP_InputField _memberInput;
        private ChatThemeSettings _theme;
        private string _currentClanId;
        private string _permissionRole = "Deputy";
        private ClanProfileTab _activeTab = ClanProfileTab.Overview;

        public bool IsOpen => _panel != null && _panel.gameObject.activeSelf;

        public static ClanProfileUi Create()
        {
            if (Instance != null)
                return Instance;

            var go = new GameObject("ClanProfileUi");
            DontDestroyOnLoad(go);
            return go.AddComponent<ClanProfileUi>();
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

            if (IsOpen && !string.IsNullOrWhiteSpace(_currentClanId))
                RefreshVisibleClan();
        }

        public void ShowClan(string clanId)
        {
            if (string.IsNullOrWhiteSpace(clanId))
                return;

            _currentClanId = clanId;
            if (ChatSystem.Instance?.GetCurrentClanState()?.clanId != clanId)
                MultiplayerPatchPlugin.instance?.RequestClanProfile(clanId);

            RefreshVisibleClan();
            _panel.gameObject.SetActive(true);
            _scrollRect.verticalNormalizedPosition = 1f;
        }

        public void RefreshVisibleClan()
        {
            if (string.IsNullOrWhiteSpace(_currentClanId))
                return;

            _theme = UiThemeUtility.GetSharedTheme();
            ApplyTheme();
            var clan = ChatSystem.Instance?.GetClanProfile(_currentClanId);
            if (clan == null)
            {
                _headerText.text = "Clan Profile";
                _bodyText.text = "Loading clan profile...";
                _footerText.text = string.Empty;
                ClearActions();
                return;
            }

            _headerText.text = $"[{clan.tag}] {clan.name}";
            _bodyText.text = BuildBody(clan);
            _footerText.text = BuildFooter(clan);
            RebuildActions(clan);
            RefreshTabStyles(clan);
        }

        public void Hide()
        {
            if (_panel != null)
                _panel.gameObject.SetActive(false);
        }

        private string BuildBody(ClanStateDto clan)
        {
            return _activeTab switch
            {
                ClanProfileTab.Overview => BuildOverviewText(clan),
                ClanProfileTab.Members => BuildMembersText(clan),
                ClanProfileTab.Upgrades => BuildUpgradesText(clan),
                ClanProfileTab.Permissions => BuildPermissionsText(clan),
                _ => string.Empty
            };
        }

        private string BuildOverviewText(ClanStateDto clan)
        {
            var lines = new List<string>
            {
                $"Clan: [{clan.tag}] {clan.name}",
                $"Owner: {MultiplayerPatchPlugin.GetPlayerNameFromSteamId(clan.ownerSteamId, clan.ownerSteamId)}",
                $"Members: {clan.members?.Count ?? 0}",
                $"Public View: {(clan.isPublicProfile ? "Yes" : "No")}",
                string.IsNullOrWhiteSpace(clan.description) ? "Description: None" : $"Description: {clan.description}"
            };

            if (clan.aggregateStats != null)
            {
                lines.Add($"Total Clan XP: {Math.Round(clan.aggregateStats.totalExperience, 2)}");
                lines.Add($"Total Clan Levels: {clan.aggregateStats.totalLevelsGained}");
                lines.Add($"Total Clan Prestige: {clan.aggregateStats.totalPrestigeGained}");
                lines.Add($"Total Clan Boss Kills: {clan.aggregateStats.totalBossKills}");
            }

            lines.Add(string.Empty);
            lines.Add("Clan Skills:");
            foreach (var skill in (clan.skills ?? new Dictionary<string, ClanSkillDto>())
                .OrderByDescending(x => x.Value?.prestige ?? 0)
                .ThenByDescending(x => x.Value?.level ?? 0)
                .ThenBy(x => x.Key, StringComparer.OrdinalIgnoreCase))
            {
                var dto = skill.Value ?? new ClanSkillDto();
                if (dto.prestige > 0)
                    lines.Add($" - {skill.Key}: Level {dto.level} (Prestige {dto.prestige}) | XP {Math.Round(dto.xp, 2)} / {Math.Round(dto.nextLevelRequirement, 2)} | Total XP {Math.Round(dto.totalExperience, 2)} | Next Prestige {dto.nextPrestigeLevel}");
                else
                    lines.Add($" - {skill.Key}: Level {dto.level} | XP {Math.Round(dto.xp, 2)} / {Math.Round(dto.nextLevelRequirement, 2)} | Total XP {Math.Round(dto.totalExperience, 2)} | Next Prestige {dto.nextPrestigeLevel}");
            }

            lines.Add(string.Empty);
            lines.Add("Boss Totals:");
            foreach (var boss in (clan.totalBossKillsByBoss ?? new Dictionary<string, long>())
                .OrderByDescending(x => x.Value)
                .ThenBy(x => x.Key, StringComparer.OrdinalIgnoreCase))
            {
                lines.Add($" - {boss.Key}: {boss.Value}");
            }

            return string.Join("\n", lines);
        }

        private string BuildMembersText(ClanStateDto clan)
        {
            var lines = new List<string>
            {
                "Members:"
            };

            foreach (var member in (clan.members ?? new List<ClanMemberDto>())
                .OrderByDescending(x => RoleWeight(x.role))
                .ThenBy(x => x.name, StringComparer.OrdinalIgnoreCase))
            {
                lines.Add($" - {member.name} [{member.role}] {(member.isOnline ? "(Online)" : "")}".TrimEnd());
                if (clan.viewerIsMember && clan.contributionsByMember != null && clan.contributionsByMember.TryGetValue(member.steamId, out var contribution) && contribution != null)
                {
                    lines.Add($"   XP: {Math.Round(contribution.totalExperience, 2)} | Levels: {contribution.totalLevelsGained} | Prestige: {contribution.totalPrestigeGained} | Boss Kills: {contribution.totalBossKills}");
                }
            }

            if (!clan.viewerIsMember)
            {
                lines.Add(string.Empty);
                lines.Add("Additional management details are only visible to clan members.");
            }

            return string.Join("\n", lines);
        }

        private string BuildUpgradesText(ClanStateDto clan)
        {
            if (!clan.viewerIsMember)
                return "Clan upgrades and bonuses are only visible to members of the clan.";

            var lines = new List<string> { "Clan Upgrades:" };
            foreach (var upgrade in (clan.upgrades ?? new List<ClanUpgradeDto>()).OrderBy(x => x.name, StringComparer.OrdinalIgnoreCase))
            {
                var status = upgrade.currentTier >= upgrade.maxTier
                    ? upgrade.active ? "Max Tier / Active" : "Max Tier / Inactive"
                    : upgrade.purchased
                        ? upgrade.active ? $"Tier {upgrade.currentTier} / Active" : $"Tier {upgrade.currentTier} / Inactive"
                        : upgrade.unlocked ? $"Tier {upgrade.nextTier} Unlocked" : $"Tier {upgrade.nextTier} Locked";
                lines.Add($" - {upgrade.name}: {status}");
                if (!string.IsNullOrWhiteSpace(upgrade.description))
                    lines.Add($"   {upgrade.description}");
                if (!string.IsNullOrWhiteSpace(upgrade.bonusText))
                    lines.Add($"   Current Bonus: {upgrade.bonusText}");
                if (!string.IsNullOrWhiteSpace(upgrade.nextTierBonusText) && upgrade.currentTier < upgrade.maxTier)
                    lines.Add($"   Next Tier Bonus: {upgrade.nextTierBonusText}");
                foreach (var requirement in upgrade.requirementText ?? new List<string>())
                    lines.Add($"   Requirement: {requirement}");
            }

            return string.Join("\n", lines);
        }

        private string BuildPermissionsText(ClanStateDto clan)
        {
            if (!clan.viewerIsMember)
                return "Clan permissions are only visible to members of the clan.";

            if (clan.rolePermissions == null || clan.rolePermissions.Count == 0)
                return "No permission data was sent by the server.";

            var lines = new List<string>
            {
                $"Selected Role: {_permissionRole}"
            };

            if (clan.rolePermissions.TryGetValue(_permissionRole, out var permissions))
            {
                foreach (var permission in PermissionOrder)
                {
                    bool enabled = permissions != null && permissions.TryGetValue(permission, out var value) && value;
                    lines.Add($" - {BeautifyPermission(permission)}: {(enabled ? "Enabled" : "Disabled")}");
                }
            }

            if (!clan.viewerCanManagePermissions)
            {
                lines.Add(string.Empty);
                lines.Add("Only permitted ranks can change clan permissions.");
            }

            return string.Join("\n", lines);
        }

        private string BuildFooter(ClanStateDto clan)
        {
            if (!clan.viewerIsMember)
                return "Viewing public clan profile";

            return string.IsNullOrWhiteSpace(clan.viewerRole)
                ? "Clan member"
                : $"Your Role: {clan.viewerRole}";
        }

        private void RefreshTabStyles(ClanStateDto clan)
        {
            UiThemeUtility.ApplyButtonStyle(_overviewTabButton, _overviewTabText, _theme, _activeTab == ClanProfileTab.Overview);
            UiThemeUtility.ApplyButtonStyle(_membersTabButton, _membersTabText, _theme, _activeTab == ClanProfileTab.Members);
            UiThemeUtility.ApplyButtonStyle(_upgradesTabButton, _upgradesTabText, _theme, _activeTab == ClanProfileTab.Upgrades);
            UiThemeUtility.ApplyButtonStyle(_permissionsTabButton, _permissionsTabText, _theme, _activeTab == ClanProfileTab.Permissions);
            _permissionsTabButton.gameObject.SetActive(clan.viewerIsMember);
        }

        private void RebuildActions(ClanStateDto clan)
        {
            ClearActions();

            switch (_activeTab)
            {
                case ClanProfileTab.Members:
                    BuildMemberActions(clan);
                    break;
                case ClanProfileTab.Upgrades:
                    BuildUpgradeActions(clan);
                    break;
                case ClanProfileTab.Permissions:
                    BuildPermissionActions(clan);
                    break;
            }
        }

        private void BuildMemberActions(ClanStateDto clan)
        {
            if (!clan.viewerIsMember)
                return;

            _memberInput = CreateInputField(_actionRoot, "Player or SteamID");

            var row = CreateActionRow();
            if (clan.viewerCanManageMembers)
            {
                CreateActionButton(row, "Invite", () => SendClanMemberAction("invitePlayer", new { target = SafeInputValue() }));
                CreateActionButton(row, "Kick", () => SendClanMemberAction("kickMember", new { target = SafeInputValue() }));
            }

            var roleRow = CreateActionRow();
            if (clan.viewerCanManageMembers)
            {
                CreateActionButton(roleRow, "Set Elder", () => SendClanMemberAction("setRole", new { target = SafeInputValue(), role = "Elder" }));
                CreateActionButton(roleRow, "Set Deputy", () => SendClanMemberAction("setRole", new { target = SafeInputValue(), role = "Deputy" }));
                CreateActionButton(roleRow, "Set Member", () => SendClanMemberAction("setRole", new { target = SafeInputValue(), role = "Member" }));
            }
        }

        private void BuildUpgradeActions(ClanStateDto clan)
        {
            if (!clan.viewerIsMember)
                return;

            foreach (var upgrade in clan.upgrades ?? new List<ClanUpgradeDto>())
            {
                var row = CreateActionRow();
                if (clan.viewerCanManageUpgrades && upgrade.canPurchaseNextTier)
                    CreateActionButton(row, $"Buy {upgrade.name} T{upgrade.nextTier}", () => SendClanAction("purchaseUpgrade", new { upgradeId = upgrade.id }));
                if (clan.viewerCanManageUpgrades && upgrade.purchased)
                    CreateActionButton(row, upgrade.active ? $"Deactivate {upgrade.name}" : $"Activate {upgrade.name}", () => SendClanAction("toggleUpgradeActive", new { upgradeId = upgrade.id, active = !upgrade.active }));
            }
        }

        private void BuildPermissionActions(ClanStateDto clan)
        {
            if (!clan.viewerIsMember)
                return;

            var headerRow = CreateActionRow();
            CreateActionButton(headerRow, $"Role: {_permissionRole}", () =>
            {
                int idx = Array.IndexOf(EditableRoles, _permissionRole);
                idx = (idx + 1 + EditableRoles.Length) % EditableRoles.Length;
                _permissionRole = EditableRoles[idx];
                RefreshVisibleClan();
            });

            if (!clan.viewerCanManagePermissions)
                return;

            if (!clan.rolePermissions.TryGetValue(_permissionRole, out var permissions))
                permissions = new Dictionary<string, bool>();

            foreach (var permission in PermissionOrder)
            {
                bool enabled = permissions.TryGetValue(permission, out var value) && value;
                var row = CreateActionRow();
                CreateActionButton(row, $"{BeautifyPermission(permission)}: {(enabled ? "On" : "Off")}", () => SendClanAction("togglePermission", new { role = _permissionRole, permission, enabled = !enabled }));
            }
        }

        private void SendClanMemberAction(string action, object payload)
        {
            if (string.IsNullOrWhiteSpace(SafeInputValue()))
                return;

            SendClanAction(action, payload);
        }

        private void SendClanAction(string action, object payload)
        {
            MultiplayerPatchPlugin.instance?.SendClanManagementAction(action, payload);
        }

        private string SafeInputValue()
        {
            return (_memberInput?.text ?? string.Empty).Trim();
        }

        private void ClearActions()
        {
            if (_actionRoot == null)
                return;

            for (int i = _actionRoot.childCount - 1; i >= 0; i--)
                Destroy(_actionRoot.GetChild(i).gameObject);
        }

        private RectTransform CreateActionRow(Transform parent = null)
        {
            parent ??= _actionRoot;
            var row = UiThemeUtility.CreateRect("ActionRow", parent);
            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 6f;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;
            var le = row.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = 34f;
            le.minHeight = 34f;
            return row;
        }

        private void CreateActionButton(Transform parent, string label, Action onClick)
        {
            var button = UiThemeUtility.CreateButton(label.Replace(" ", string.Empty), parent, out var text, label, 160f, 30f);
            UiThemeUtility.ApplyButtonStyle(button, text, _theme);
            button.onClick.AddListener(() => onClick?.Invoke());
        }

        private TMP_InputField CreateInputField(Transform parent, string placeholder)
        {
            var rt = UiThemeUtility.CreateRect("InputField", parent);
            var le = rt.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = 36f;
            le.minHeight = 36f;

            var image = rt.gameObject.AddComponent<Image>();
            image.color = _theme != null ? _theme.GetInputBackgroundColor() : new Color(0.14f, 0.14f, 0.14f, 1f);
            var outline = rt.gameObject.AddComponent<Outline>();
            outline.effectColor = _theme != null ? _theme.GetInputOutlineColor() : Color.black;

            var input = rt.gameObject.AddComponent<TMP_InputField>();
            var textArea = UiThemeUtility.CreateRect("TextArea", rt);
            UiThemeUtility.Stretch(textArea, new Vector2(8f, 4f), new Vector2(-8f, -4f));

            var textComponent = UiThemeUtility.CreateText("Text", textArea, string.Empty, UiThemeUtility.GetScaledFont(_theme, 16f), TextAlignmentOptions.MidlineLeft);
            UiThemeUtility.Stretch(textComponent.rectTransform);
            textComponent.color = _theme != null ? _theme.GetInputTextColor() : Color.white;

            var placeholderComponent = UiThemeUtility.CreateText("Placeholder", textArea, placeholder, UiThemeUtility.GetScaledFont(_theme, 16f), TextAlignmentOptions.MidlineLeft);
            UiThemeUtility.Stretch(placeholderComponent.rectTransform);
            placeholderComponent.color = _theme != null ? _theme.GetInputPlaceholderColor() : new Color(0.75f, 0.75f, 0.75f, 1f);

            input.textViewport = textArea;
            input.textComponent = textComponent;
            input.placeholder = placeholderComponent;
            input.lineType = TMP_InputField.LineType.SingleLine;
            return input;
        }

        private int RoleWeight(string role)
        {
            return (role ?? string.Empty) switch
            {
                "Owner" => 4,
                "Elder" => 3,
                "Deputy" => 2,
                _ => 1
            };
        }

        private static string BeautifyPermission(string permission)
        {
            return permission switch
            {
                "invitePlayers" => "Invite Players",
                "kickMembers" => "Kick Members",
                "manageRoles" => "Manage Roles",
                "managePermissions" => "Manage Permissions",
                "purchaseUpgrades" => "Purchase Upgrades",
                "toggleUpgrades" => "Toggle Upgrades",
                _ => permission
            };
        }

        private void BuildUi()
        {
            _canvas = gameObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 32011;
            gameObject.AddComponent<GraphicRaycaster>();

            _panel = UiThemeUtility.CreateRect("ClanProfilePanel", transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f));
            _panel.sizeDelta = new Vector2(680f, 660f);
            _panel.anchoredPosition = new Vector2(590f, -80f);
            _panelImage = _panel.gameObject.AddComponent<Image>();
            _panelOutline = _panel.gameObject.AddComponent<Outline>();

            var vertical = _panel.gameObject.AddComponent<VerticalLayoutGroup>();
            vertical.padding = new RectOffset(8, 8, 8, 8);
            vertical.spacing = 6f;
            vertical.childForceExpandWidth = true;
            vertical.childForceExpandHeight = false;

            var dragHandle = _panel.gameObject.AddComponent<ClanProfileDragHandle>();
            dragHandle.Initialize(this);

            var headerRow = CreateActionRow(_panel);
            var headerTextRoot = UiThemeUtility.CreateRect("HeaderTextRoot", headerRow);
            var headerLayout = headerTextRoot.gameObject.AddComponent<LayoutElement>();
            headerLayout.flexibleWidth = 1f;
            _headerText = headerTextRoot.gameObject.AddComponent<TextMeshProUGUI>();
            _headerText.alignment = TextAlignmentOptions.MidlineLeft;
            _headerText.raycastTarget = false;
            _headerText.text = "Clan Profile";

            _closeButton = UiThemeUtility.CreateButton("CloseButton", headerRow, out _closeButtonText, "Close", 88f, 30f);
            _closeButton.onClick.AddListener(Hide);

            var tabsRow = CreateActionRow(_panel);
            _overviewTabButton = UiThemeUtility.CreateButton("OverviewTab", tabsRow, out _overviewTabText, "Overview", 110f, 30f);
            _membersTabButton = UiThemeUtility.CreateButton("MembersTab", tabsRow, out _membersTabText, "Members", 110f, 30f);
            _upgradesTabButton = UiThemeUtility.CreateButton("UpgradesTab", tabsRow, out _upgradesTabText, "Upgrades", 110f, 30f);
            _permissionsTabButton = UiThemeUtility.CreateButton("PermissionsTab", tabsRow, out _permissionsTabText, "Permissions", 120f, 30f);
            _overviewTabButton.onClick.AddListener(() => SetTab(ClanProfileTab.Overview));
            _membersTabButton.onClick.AddListener(() => SetTab(ClanProfileTab.Members));
            _upgradesTabButton.onClick.AddListener(() => SetTab(ClanProfileTab.Upgrades));
            _permissionsTabButton.onClick.AddListener(() => SetTab(ClanProfileTab.Permissions));

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
            contentLayout.padding = new RectOffset(0, 0, 0, 0);
            contentLayout.spacing = 8f;
            contentLayout.childForceExpandHeight = false;
            contentLayout.childForceExpandWidth = true;

            var contentFitter = _content.gameObject.AddComponent<ContentSizeFitter>();
            contentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var bodyRoot = UiThemeUtility.CreateRect("BodyRoot", _content);
            var bodyLE = bodyRoot.gameObject.AddComponent<LayoutElement>();
            bodyLE.minHeight = 260f;
            _bodyText = bodyRoot.gameObject.AddComponent<TextMeshProUGUI>();
            _bodyText.alignment = TextAlignmentOptions.TopLeft;
            _bodyText.enableWordWrapping = true;
            _bodyText.overflowMode = TextOverflowModes.Overflow;
            _bodyText.raycastTarget = false;
            _bodyText.margin = new Vector4(6f, 6f, 6f, 6f);

            _actionRoot = UiThemeUtility.CreateRect("ActionRoot", _content);
            var actionLayout = _actionRoot.gameObject.AddComponent<VerticalLayoutGroup>();
            actionLayout.spacing = 6f;
            actionLayout.childForceExpandHeight = false;
            actionLayout.childForceExpandWidth = true;
            var actionLE = _actionRoot.gameObject.AddComponent<LayoutElement>();
            actionLE.minHeight = 120f;

            var footerRow = UiThemeUtility.CreateRect("FooterRow", _panel);
            var footerLE = footerRow.gameObject.AddComponent<LayoutElement>();
            footerLE.preferredHeight = 28f;
            footerLE.minHeight = 28f;
            _footerText = footerRow.gameObject.AddComponent<TextMeshProUGUI>();
            _footerText.alignment = TextAlignmentOptions.MidlineLeft;
            _footerText.raycastTarget = false;
            _footerText.text = string.Empty;
        }

        private void SetTab(ClanProfileTab tab)
        {
            _activeTab = tab;
            RefreshVisibleClan();
        }

        private void ApplyTheme()
        {
            UiThemeUtility.ApplyPanelStyle(_panelImage, _panelOutline, _theme);
            UiThemeUtility.ApplyScrollViewportStyle(_scrollBackgroundImage, _theme);
            UiThemeUtility.ApplyButtonStyle(_closeButton, _closeButtonText, _theme);
            RefreshTabStyles(ChatSystem.Instance?.GetClanProfile(_currentClanId) ?? ChatSystem.Instance?.GetCurrentClanState() ?? new ClanStateDto());

            _panel.localScale = Vector3.one * Mathf.Clamp(_theme?.UiScale.Value ?? 1f, 0.75f, 2f);
            _headerText.fontSize = UiThemeUtility.GetScaledFont(_theme, 22f);
            _headerText.color = _theme != null ? _theme.GetHeaderTextColor() : Color.white;
            _bodyText.fontSize = UiThemeUtility.GetScaledFont(_theme, 16f);
            _bodyText.color = _theme != null ? _theme.GetBodyTextColor() : Color.white;
            _footerText.fontSize = UiThemeUtility.GetScaledFont(_theme, 14f);
            _footerText.color = _theme != null ? _theme.GetStatusTextColor() : Color.white;
        }

        public void MoveWindow(Vector2 delta)
        {
            if (_panel == null)
                return;

            float scale = Mathf.Clamp(_panel.localScale.x, 0.75f, 2f);
            _panel.anchoredPosition += delta / scale;
            ClampToScreen();
        }

        private void ClampToScreen()
        {
            if (_panel == null)
                return;

            float width = _panel.sizeDelta.x * _panel.localScale.x;
            float height = _panel.sizeDelta.y * _panel.localScale.y;
            float minX = width - Screen.width;
            float minY = -height;

            _panel.anchoredPosition = new Vector2(
                Mathf.Clamp(_panel.anchoredPosition.x, minX, 0f),
                Mathf.Clamp(_panel.anchoredPosition.y, 0f, Screen.height));
        }

        private void EnsureEventSystem()
        {
            if (FindObjectOfType<EventSystem>() != null)
                return;

            var go = new GameObject("ClanProfileEventSystem");
            DontDestroyOnLoad(go);
            go.AddComponent<EventSystem>();
            go.AddComponent<StandaloneInputModule>();
        }
    }

    public sealed class ClanProfileDragHandle : MonoBehaviour, IBeginDragHandler, IDragHandler
    {
        private ClanProfileUi _ui;
        public void Initialize(ClanProfileUi ui) { _ui = ui; }
        public void OnBeginDrag(PointerEventData eventData) { }
        public void OnDrag(PointerEventData eventData) { _ui?.MoveWindow(eventData.delta); }
    }
}
