using BepInEx.Configuration;
using Newtonsoft.Json.Linq;
using Steamworks;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Multi_bloob_adventure_idle
{
    public class ChatSystem : MonoBehaviour
    {
        private sealed class PlayerResolutionCandidate
        {
            public string Name;
            public string SteamId;
            public string LastMessagePreview;
        }

        private enum PendingSelectionMode
        {
            None,
            Block,
            Unblock,
            Whisper
        }

        public static ChatSystem Instance { get; private set; }
        public static bool IsChatCapturingInput => Instance != null && Instance._ui != null && Instance._ui.IsInputFocused;

        public static bool ShouldBlockGameInput
        {
            get
            {
                if (Instance == null || Instance._ui == null)
                    return false;

                return Instance._ui.IsPointerOverChatWindow() || Instance._ui.IsInputFocused;
            }
        }

        public static bool ShouldBlockKeyboardInput
        {
            get
            {
                if (Instance == null || Instance._ui == null)
                    return false;

                return Instance._ui.IsInputFocused;
            }
        }

        private readonly List<ChatUiMessage> _globalMessages = [];
        private readonly List<ChatUiMessage> _clanMessages = [];
        private readonly List<ChatUiMessage> _systemMessages = [];
        private readonly Dictionary<string, List<ChatUiMessage>> _privateMessagesBySteamId = [];
        private readonly HashSet<string> _blockedSteamIds = [];
        private readonly HashSet<string> _unreadPrivateSteamIds = [];
        private readonly List<(string id, string label)> _unlockedTitles = [];
        private readonly List<PlayerResolutionCandidate> _pendingCandidates = [];
        private readonly Dictionary<string, ClanStateDto> _clanProfilesById = [];

        private ConfigEntry<string> _blockedSteamIdsConfig;
        private ConfigEntry<string> _preferredTitleIdConfig;

        private ChatThemeSettings _theme;
        private ChatWindowUi _ui;

        private PendingSelectionMode _pendingSelectionMode = PendingSelectionMode.None;
        private string _pendingWhisperMessage = "";
        private string _activeTitleId = "";
        private string _selectedPrivateSteamId = "";
        private string _selectedPrivateName = "";
        private string _statusLine = "";
        private float _statusUntil;
        private int _unreadGlobalCount;
        private int _unreadClanCount;
        private int _unreadSystemCount;
        private ClanStateDto _currentClanState;
        private bool _uiDirty = true;

        private const int MaxMessagesPerBucket = 250;

        public static ChatSystem Create(ConfigFile config)
        {
            if (Instance != null)
                return Instance;

            var go = new GameObject("BloobsMultiplayer_ChatSystem");
            DontDestroyOnLoad(go);
            var chat = go.AddComponent<ChatSystem>();
            chat.Initialize(config);
            Instance = chat;
            return chat;
        }

        private void Initialize(ConfigFile config)
        {
            _blockedSteamIdsConfig = config.Bind("Chat", "Blocked SteamIDs", "", "Comma-separated SteamIDs that are blocked locally from chat display.");
            _preferredTitleIdConfig = config.Bind("Chat", "Preferred Title ID", "", "Preferred chat title ID.");
            _theme = new ChatThemeSettings(config);
            ReloadBlockedList();

            var uiGo = new GameObject("BloobsMultiplayer_ChatWindowUi");
            DontDestroyOnLoad(uiGo);
            _ui = uiGo.AddComponent<ChatWindowUi>();
            _ui.Initialize(this, _theme);
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
        }

        private void OnDestroy()
        {
            if (_ui != null)
                Destroy(_ui.gameObject);

            if (Instance == this)
                Instance = null;
        }

        private void Update()
        {
            if (!MultiplayerPatchPlugin.isReady || !SteamClient.IsValid)
                return;

            if (!_ui.IsInputFocused)
            {
                if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
                    FocusInput(false);
                else if (Input.GetKeyDown(KeyCode.Slash))
                    FocusInput(true);
                else if (Input.GetKeyDown(KeyCode.BackQuote))
                    ToggleVisibilityOnly();
            }
            else if (Input.GetKeyDown(KeyCode.Escape))
            {
                BlurInput();
            }

            if (!string.IsNullOrEmpty(_statusLine) && Time.unscaledTime > _statusUntil)
            {
                _statusLine = "";
                MarkUiDirty();
            }

            if (_uiDirty)
            {
                _ui.Refresh();
                _uiDirty = false;
            }
        }

        public void FocusInput(bool seedSlash)
        {
            _theme.WindowVisible.Value = true;
            if (seedSlash && string.IsNullOrWhiteSpace(_ui.CurrentInputText))
                _ui.CurrentInputText = "/";

            ClearUnreadForActiveTab();
            _ui.SetVisible(true);
            _ui.FocusInput();
            MarkUiDirty();
        }

        public void BlurInput()
        {
            _ui.BlurInput();
            MarkUiDirty();
        }

        public void ToggleVisibilityOnly()
        {
            _theme.WindowVisible.Value = !_theme.WindowVisible.Value;
            _ui.SetVisible(_theme.WindowVisible.Value);
            if (!_theme.WindowVisible.Value)
                _ui.BlurInput();
            MarkUiDirty();
        }

        public void SubmitChat(string raw)
        {
            raw = (raw ?? "").Trim();
            if (string.IsNullOrWhiteSpace(raw))
            {
                _ui.CurrentInputText = "";
                _ui.FocusInput();
                return;
            }

            if (TryHandleLocalOnlyCommand(raw))
            {
                _ui.CurrentInputText = "";
                _ui.FocusInput();
                MarkUiDirty();
                return;
            }

            MultiplayerPatchPlugin.instance?.SendChatPacket(raw);
            _ui.CurrentInputText = "";
            _ui.FocusInput();
        }

        public void SetActiveTab(ChatTab tab)
        {
            if (_ui.ActiveTab == tab)
                return;

            _ui.ActiveTab = tab;
            ClearUnreadForActiveTab();
            _ui.ForceEnableAutoScroll();
            MarkUiDirty();
        }

        public void SelectPrivateTab(string steamId)
        {
            if (string.IsNullOrWhiteSpace(steamId))
                return;

            _selectedPrivateSteamId = steamId;
            _selectedPrivateName = MultiplayerPatchPlugin.GetPlayerNameFromSteamId(steamId, "Unknown");
            _unreadPrivateSteamIds.Remove(steamId);
            _ui.ForceEnableAutoScroll();
            MarkUiDirty();
        }

        public void SelectTitle(string titleId)
        {
            _preferredTitleIdConfig.Value = titleId ?? "";
            MultiplayerPatchPlugin.instance?.SendSetTitlePacket(titleId ?? "");
            MarkUiDirty();
        }

        public void ClearActiveTitle()
        {
            SelectTitle("");
        }

        public ChatThemeSettings Theme => _theme;
        public ChatTab CurrentTab => _ui.ActiveTab;
        public string SelectedPrivateSteamId => _selectedPrivateSteamId;
        public string StatusLine => _statusLine;
        public string ActiveTitleId => _activeTitleId;
        public bool IsVisible => _theme.WindowVisible.Value;
        public bool IsPinned => _theme.WindowPinned.Value;

        public void SetPinned(bool value)
        {
            _theme.WindowPinned.Value = value;
            MarkUiDirty();
        }

        public Rect GetWindowRect() => _theme.GetWindowRect();

        public void SetWindowRect(Rect rect)
        {
            _theme.SaveWindowRect(rect);
            MarkUiDirty();
        }

        public IReadOnlyList<ChatUiMessage> GetCurrentTabMessages()
        {
            return _ui.ActiveTab switch
            {
                ChatTab.Global => _globalMessages,
                ChatTab.Clan => _clanMessages,
                ChatTab.System => _systemMessages,
                ChatTab.Private => GetSelectedPrivateMessages(),
                _ => _globalMessages
            };
        }

        public IReadOnlyList<PrivateTabInfo> GetPrivateTabs()
        {
            return [.. _privateMessagesBySteamId
                .Select(kvp =>
                {
                    var first = kvp.Value.LastOrDefault();
                    return new PrivateTabInfo
                    {
                        SteamId = kvp.Key,
                        Name = first?.OtherPartyName ?? MultiplayerPatchPlugin.GetPlayerNameFromSteamId(kvp.Key, "Unknown"),
                        HasUnread = _unreadPrivateSteamIds.Contains(kvp.Key)
                    };
                })
                .OrderBy(x => x.Name)];
        }

        public IReadOnlyList<(string id, string label)> GetUnlockedTitles() => _unlockedTitles;
        public ClanStateDto GetCurrentClanState() => _currentClanState;
        public ClanStateDto GetClanProfile(string clanId)
        {
            if (string.IsNullOrWhiteSpace(clanId))
                return null;

            if (_currentClanState != null && _currentClanState.clanId == clanId)
                return _currentClanState;

            _clanProfilesById.TryGetValue(clanId, out var profile);
            return profile;
        }

        public bool IsBlockedSteamId(string steamId) => IsBlocked(steamId);

        public void ToggleBlockedSteamId(string steamId)
        {
            if (IsBlocked(steamId))
            {
                UnblockSteamId(steamId);
                AddSystemLine($"Unblocked {MultiplayerPatchPlugin.GetPlayerNameFromSteamId(steamId, steamId)} locally.", ChatMessageKind.SystemRegular);
            }
            else
            {
                BlockSteamId(steamId);
                AddSystemLine($"Blocked {MultiplayerPatchPlugin.GetPlayerNameFromSteamId(steamId, steamId)} locally.", ChatMessageKind.SystemRegular);
            }

            MarkUiDirty();
        }

        public void StartWhisperToSteamId(string steamId)
        {
            if (string.IsNullOrWhiteSpace(steamId))
                return;

            SelectPrivateTab(steamId);
            FocusInput(false);
            _ui.CurrentInputText = $"/w {steamId} ";
            _ui.FocusInput();
            MarkUiDirty();
        }

        public void ShowProfileForSteamId(string steamId)
        {
            if (string.IsNullOrWhiteSpace(steamId))
                return;

            PlayerProfileUi.Create().ShowProfile(steamId);
        }

        public void ShowClanProfile(string clanId)
        {
            if (string.IsNullOrWhiteSpace(clanId))
                return;

            ClanProfileUi.Create().ShowClan(clanId);
        }

        public void CopySteamIdToClipboard(string steamId)
        {
            if (string.IsNullOrWhiteSpace(steamId))
                return;

            GUIUtility.systemCopyBuffer = steamId;
            AddSystemLine($"Copied SteamID for {MultiplayerPatchPlugin.GetPlayerNameFromSteamId(steamId, steamId)}.", ChatMessageKind.SystemRegular);
        }

        public int GetUnreadCount(ChatTab tab)
        {
            return tab switch
            {
                ChatTab.Global => _unreadGlobalCount,
                ChatTab.Clan => _unreadClanCount,
                ChatTab.System => _unreadSystemCount,
                ChatTab.Private => _unreadPrivateSteamIds.Count,
                _ => 0
            };
        }

        public string BuildTabLabel(ChatTab tab)
        {
            int unread = GetUnreadCount(tab);
            string label = tab == ChatTab.Clan ? "Clan" : tab.ToString();
            return unread > 0 ? $"{label} ({unread})" : label;
        }

        public void ReceiveTitleState(JObject msg)
        {
            _unlockedTitles.Clear();

            if (msg["unlockedTitles"] is JArray unlocked)
            {
                foreach (var token in unlocked)
                {
                    if (token is JObject obj)
                    {
                        string id = obj["id"]?.ToString();
                        string label = obj["label"]?.ToString();
                        if (!string.IsNullOrWhiteSpace(id))
                            _unlockedTitles.Add((id, string.IsNullOrWhiteSpace(label) ? id : label));
                    }
                    else if (token is JValue valueToken)
                    {
                        string raw = valueToken.ToString();
                        if (!string.IsNullOrWhiteSpace(raw))
                            _unlockedTitles.Add((raw, raw));
                    }
                }
            }

            _activeTitleId = "";
            var activeTitleToken = msg["activeTitle"];
            if (activeTitleToken is JObject activeObj)
                _activeTitleId = activeObj["id"]?.ToString() ?? "";
            else if (activeTitleToken is JValue activeValue)
                _activeTitleId = activeValue.ToString() ?? "";

            string preferred = _preferredTitleIdConfig.Value ?? "";
            if (!string.IsNullOrWhiteSpace(preferred) &&
                _unlockedTitles.Any(t => t.id == preferred) &&
                preferred != _activeTitleId)
            {
                MultiplayerPatchPlugin.instance?.SendSetTitlePacket(preferred);
            }

            MarkUiDirty();
        }

        public void ReceiveClanState(JObject msg)
        {
            if (msg == null)
                return;

            _currentClanState = msg["clan"]?.ToObject<ClanStateDto>();
            if (_currentClanState != null && !string.IsNullOrWhiteSpace(_currentClanState.clanId))
                _clanProfilesById[_currentClanState.clanId] = _currentClanState;
            MarkUiDirty();
        }

        public void ReceiveClanProfile(JObject msg)
        {
            if (msg == null)
                return;

            var profile = msg["clan"]?.ToObject<ClanStateDto>();
            if (profile == null || string.IsNullOrWhiteSpace(profile.clanId))
                return;

            _clanProfilesById[profile.clanId] = profile;
            ClanProfileUi.Instance?.RefreshVisibleClan();
            MarkUiDirty();
        }

        public void ReceiveHistory(JArray messages)
        {
            if (messages == null)
                return;

            _globalMessages.Clear();
            _clanMessages.Clear();
            _systemMessages.Clear();

            foreach (var token in messages)
            {
                string type = token["type"]?.ToString();
                string channel = token["channel"]?.ToString();

                if (type == "chatMessage" && channel == "global")
                {
                    ReceiveGlobalMessage(
                        token["name"]?.ToString(),
                        token["steamId"]?.ToString(),
                        token["activeTitle"]?.ToString(),
                        token["message"]?.ToString(),
                        token["timestampUtc"]?.ToString(),
                        false
                    );
                    continue;
                }

                if (type == "chatMessage" && channel == "clan")
                {
                    ReceiveClanMessage(
                        token["name"]?.ToString(),
                        token["steamId"]?.ToString(),
                        token["activeTitle"]?.ToString(),
                        token["message"]?.ToString(),
                        token["timestampUtc"]?.ToString(),
                        false
                    );
                    continue;
                }

                if (type == "serverBroadcast")
                {
                    ReceiveBroadcast(token as JObject);
                }
            }

            _unreadGlobalCount = 0;
            _unreadClanCount = 0;
            _unreadSystemCount = 0;
            _statusLine = "";
            MarkUiDirty();
        }

        public void ReceiveChatMessage(JObject msg)
        {
            string channel = msg["channel"]?.ToString();

            if (channel == "global")
            {
                ReceiveGlobalMessage(
                    msg["name"]?.ToString(),
                    msg["steamId"]?.ToString(),
                    msg["activeTitle"]?.ToString(),
                    msg["message"]?.ToString(),
                    msg["timestampUtc"]?.ToString(),
                    true
                );
            }
            else if (channel == "private")
            {
                ReceivePrivateMessage(
                    msg["fromName"]?.ToString(),
                    msg["fromSteamId"]?.ToString(),
                    msg["fromActiveTitle"]?.ToString(),
                    msg["toName"]?.ToString(),
                    msg["toSteamId"]?.ToString(),
                    msg["toActiveTitle"]?.ToString(),
                    msg["message"]?.ToString(),
                    msg["timestampUtc"]?.ToString()
                );
            }
            else if (channel == "clan")
            {
                ReceiveClanMessage(
                    msg["name"]?.ToString(),
                    msg["steamId"]?.ToString(),
                    msg["activeTitle"]?.ToString(),
                    msg["message"]?.ToString(),
                    msg["timestampUtc"]?.ToString(),
                    true
                );
            }

            MarkUiDirty();
        }

        public void ReceiveBroadcast(JObject msg)
        {
            string severity = msg["severity"]?.ToString() ?? "regular";
            string text = msg["message"]?.ToString() ?? "";
            string timestampUtc = msg["timestampUtc"]?.ToString();

            ChatMessageKind kind = severity.ToLowerInvariant() switch
            {
                "important" => ChatMessageKind.SystemImportant,
                "critical" => ChatMessageKind.SystemCritical,
                _ => ChatMessageKind.SystemRegular
            };

            AddSystemLine(text, kind, timestampUtc);
        }

        public void ReceiveError(string error)
        {
            AddSystemLine(error, ChatMessageKind.Error);
        }

        public void ClearSelectedPrivateConversation(bool removeTabCompletely)
        {
            if (string.IsNullOrWhiteSpace(_selectedPrivateSteamId))
            {
                AddSystemLine("No private conversation is selected.", ChatMessageKind.Error);
                return;
            }

            if (_privateMessagesBySteamId.TryGetValue(_selectedPrivateSteamId, out var bucket))
            {
                if (removeTabCompletely)
                {
                    _privateMessagesBySteamId.Remove(_selectedPrivateSteamId);
                    _unreadPrivateSteamIds.Remove(_selectedPrivateSteamId);

                    var next = _privateMessagesBySteamId.Keys.OrderBy(x => x).FirstOrDefault();
                    _selectedPrivateSteamId = next ?? "";
                    _selectedPrivateName = string.IsNullOrWhiteSpace(next) ? "" : MultiplayerPatchPlugin.GetPlayerNameFromSteamId(next, "Unknown");
                }
                else
                {
                    bucket.Clear();
                }

                MarkUiDirty();
            }
        }

        public string FormatMessage(ChatUiMessage msg)
        {
            string prefix = $"[{msg.LocalTimeString}] ";
            string displayNameWithTitle = FormatNameWithTitle(msg.ActiveTitle, MultiplayerPatchPlugin.GetPlayerNameFromSteamId(msg.FromSteamId, msg.DisplayName));
            string otherPartyWithTitle = FormatNameWithTitle(msg.OtherPartyTitle, MultiplayerPatchPlugin.GetPlayerNameFromSteamId(msg.ToSteamId, msg.OtherPartyName));

            return msg.Kind switch
            {
                ChatMessageKind.Global => $"{prefix}{displayNameWithTitle}: {msg.Message}",
                ChatMessageKind.Private when msg.IsOutgoingPrivate => $"{prefix}[To {otherPartyWithTitle}] {msg.Message}",
                ChatMessageKind.Private when msg.IsIncomingPrivate => $"{prefix}[From {displayNameWithTitle}] {msg.Message}",
                ChatMessageKind.Clan => $"{prefix}[Clan] {displayNameWithTitle}: {msg.Message}",
                ChatMessageKind.SystemRegular => $"{prefix}[SERVER] {msg.Message}",
                ChatMessageKind.SystemImportant => $"{prefix}[IMPORTANT] {msg.Message}",
                ChatMessageKind.SystemCritical => $"{prefix}[CRITICAL] {msg.Message}",
                ChatMessageKind.Error => $"{prefix}[ERROR] {msg.Message}",
                _ => $"{prefix}{displayNameWithTitle}: {msg.Message}"
            };
        }


        public void MarkUiDirty()
        {
            _uiDirty = true;
        }

        private IReadOnlyList<ChatUiMessage> GetSelectedPrivateMessages()
        {
            if (string.IsNullOrEmpty(_selectedPrivateSteamId))
                return [];

            if (_privateMessagesBySteamId.TryGetValue(_selectedPrivateSteamId, out var list))
                return list;

            return [];
        }

        private static string FormatNameWithTitle(string title, string name)
        {
            if (string.IsNullOrWhiteSpace(title))
                return name;

            return $"[{title}] {name}";
        }

        private bool TryHandleLocalOnlyCommand(string raw)
        {
            if (!raw.StartsWith("/"))
                return false;

            string[] split = raw.Split([' '], StringSplitOptions.RemoveEmptyEntries);
            if (split.Length == 0)
                return false;

            string cmd = split[0].ToLowerInvariant();

            switch (cmd)
            {
                case "/help":
                    AddSystemLine("Available commands:", ChatMessageKind.SystemRegular);
                    AddSystemLine("/w <name|partial|steamId> <message> - Whisper a player", ChatMessageKind.SystemRegular);
                    AddSystemLine("/c <message> - Speak in clan chat", ChatMessageKind.SystemRegular);
                    AddSystemLine("/clan help - Show clan command help on the server", ChatMessageKind.SystemRegular);
                    AddSystemLine("/wselect <number> - Finish a pending whisper selection", ChatMessageKind.SystemRegular);
                    AddSystemLine("/r <message> - Reply to your last whisper target", ChatMessageKind.SystemRegular);
                    AddSystemLine("/unblock <steamId> - Unblock a player locally by SteamID", ChatMessageKind.SystemRegular);
                    AddSystemLine("/block <partialName|steamId> - Block by partial username or SteamID", ChatMessageKind.SystemRegular);
                    AddSystemLine("/blockselect <number> - Finish a pending /block selection", ChatMessageKind.SystemRegular);
                    AddSystemLine("/unblock <partialName|steamId> - Unblock by partial username or SteamID", ChatMessageKind.SystemRegular);
                    AddSystemLine("/pmclear - Clear the selected private conversation", ChatMessageKind.SystemRegular);
                    AddSystemLine("/pmclose - Close the selected private conversation tab", ChatMessageKind.SystemRegular);
                    AddSystemLine("/cleartitle - Clear your active title", ChatMessageKind.SystemRegular);
                    return true;
            }

            return false;
        }

        private void ReceiveGlobalMessage(string fromName, string fromSteamId, string activeTitle, string text, string timestampUtc, bool showBubble)
        {
            if (IsBlocked(fromSteamId))
                return;

            var msg = new ChatUiMessage
            {
                Kind = ChatMessageKind.Global,
                TimestampUtc = timestampUtc,
                DisplayName = fromName,
                FromSteamId = fromSteamId,
                Message = text,
                ActiveTitle = activeTitle
            };

            AddMessage(_globalMessages, msg);

            bool isFromLocalPlayer = !string.IsNullOrWhiteSpace(fromSteamId) && SteamClient.IsValid && fromSteamId == SteamClient.SteamId.ToString();
            if (!isFromLocalPlayer)
                MarkUnreadForIncomingMessage(ChatTab.Global);

            if (showBubble)
                ShowBubbleForPlayer(fromSteamId, fromName, text);
        }

        private void ReceivePrivateMessage(string fromName, string fromSteamId, string fromActiveTitle, string toName, string toSteamId, string toActiveTitle, string text, string timestampUtc)
        {
            string mySteamId = SteamClient.SteamId.ToString();
            string otherSteamId;
            string otherName;
            string otherTitle;
            bool incoming;

            if (fromSteamId == mySteamId)
            {
                otherSteamId = toSteamId;
                otherName = toName;
                otherTitle = toActiveTitle;
                incoming = false;
            }
            else
            {
                otherSteamId = fromSteamId;
                otherName = fromName;
                otherTitle = fromActiveTitle;
                incoming = true;
            }

            if (IsBlocked(otherSteamId))
                return;

            if (!_privateMessagesBySteamId.TryGetValue(otherSteamId, out var bucket))
            {
                bucket = [];
                _privateMessagesBySteamId[otherSteamId] = bucket;
            }

            var msg = new ChatUiMessage
            {
                Kind = ChatMessageKind.Private,
                TimestampUtc = timestampUtc,
                DisplayName = fromName,
                OtherPartyName = otherName,
                FromSteamId = fromSteamId,
                ToSteamId = toSteamId,
                Message = text,
                ActiveTitle = fromActiveTitle,
                OtherPartyTitle = otherTitle,
                IsIncomingPrivate = incoming,
                IsOutgoingPrivate = !incoming
            };

            AddMessage(bucket, msg);

            if (incoming)
                MarkUnreadForIncomingMessage(ChatTab.Private, otherSteamId);

            if (string.IsNullOrEmpty(_selectedPrivateSteamId))
            {
                _selectedPrivateSteamId = otherSteamId;
                _selectedPrivateName = otherName;
            }

            ShowBubbleForPlayer(fromSteamId, fromName, text);
        }

        private void ReceiveClanMessage(string fromName, string fromSteamId, string activeTitle, string text, string timestampUtc, bool showBubble)
        {
            if (IsBlocked(fromSteamId))
                return;

            var msg = new ChatUiMessage
            {
                Kind = ChatMessageKind.Clan,
                TimestampUtc = timestampUtc,
                DisplayName = fromName,
                FromSteamId = fromSteamId,
                Message = text,
                ActiveTitle = activeTitle
            };

            AddMessage(_clanMessages, msg);

            bool isFromLocalPlayer = !string.IsNullOrWhiteSpace(fromSteamId) && SteamClient.IsValid && fromSteamId == SteamClient.SteamId.ToString();
            if (!isFromLocalPlayer)
                MarkUnreadForIncomingMessage(ChatTab.Clan);

            if (showBubble)
                ShowBubbleForPlayer(fromSteamId, fromName, text);
        }

        private void AddSystemLine(string text, ChatMessageKind kind, string timestampUtc = null)
        {
            var msg = new ChatUiMessage
            {
                Kind = kind,
                TimestampUtc = timestampUtc ?? DateTime.UtcNow.ToString("o"),
                DisplayName = "SERVER",
                Message = text
            };

            AddMessage(_systemMessages, msg);
            MarkUnreadForIncomingMessage(ChatTab.System);
            SetStatus(text);
        }

        private void AddMessage(List<ChatUiMessage> list, ChatUiMessage msg)
        {
            list.Add(msg);
            while (list.Count > MaxMessagesPerBucket)
                list.RemoveAt(0);
            MarkUiDirty();
        }

        private void SetStatus(string text, float duration = 3f)
        {
            _statusLine = text ?? "";
            _statusUntil = Time.unscaledTime + duration;
            MarkUiDirty();
        }

        private void MarkUnreadForIncomingMessage(ChatTab tab, string privateSteamId = null)
        {
            switch (tab)
            {
                case ChatTab.Global:
                    if (_ui.ActiveTab != ChatTab.Global)
                        _unreadGlobalCount++;
                    break;
                case ChatTab.Clan:
                    if (_ui.ActiveTab != ChatTab.Clan)
                        _unreadClanCount++;
                    break;
                case ChatTab.System:
                    if (_ui.ActiveTab != ChatTab.System)
                        _unreadSystemCount++;
                    break;
                case ChatTab.Private:
                    if (!string.IsNullOrWhiteSpace(privateSteamId) && !(_ui.ActiveTab == ChatTab.Private && _selectedPrivateSteamId == privateSteamId))
                        _unreadPrivateSteamIds.Add(privateSteamId);
                    break;
            }

            MarkUiDirty();
        }

        private void ClearUnreadForActiveTab()
        {
            switch (_ui.ActiveTab)
            {
                case ChatTab.Global:
                    _unreadGlobalCount = 0;
                    break;
                case ChatTab.Clan:
                    _unreadClanCount = 0;
                    break;
                case ChatTab.System:
                    _unreadSystemCount = 0;
                    break;
                case ChatTab.Private:
                    if (!string.IsNullOrWhiteSpace(_selectedPrivateSteamId))
                        _unreadPrivateSteamIds.Remove(_selectedPrivateSteamId);
                    break;
            }

            MarkUiDirty();
        }

        private bool IsBlocked(string steamId)
        {
            return !string.IsNullOrWhiteSpace(steamId) && _blockedSteamIds.Contains(steamId);
        }

        private void BlockSteamId(string steamId)
        {
            if (string.IsNullOrWhiteSpace(steamId))
                return;

            _blockedSteamIds.Add(steamId);
            SaveBlockedList();
        }

        private void UnblockSteamId(string steamId)
        {
            if (string.IsNullOrWhiteSpace(steamId))
                return;

            _blockedSteamIds.Remove(steamId);
            SaveBlockedList();
        }

        private void ReloadBlockedList()
        {
            _blockedSteamIds.Clear();
            foreach (var part in (_blockedSteamIdsConfig.Value ?? "").Split(',').Select(x => x.Trim()).Where(x => !string.IsNullOrWhiteSpace(x)))
                _blockedSteamIds.Add(part);
        }

        private void SaveBlockedList()
        {
            _blockedSteamIdsConfig.Value = string.Join(",", _blockedSteamIds.OrderBy(x => x));
        }

        private void ShowBubbleForPlayer(string steamId, string playerName, string text)
        {
            if (IsBlocked(steamId))
                return;

            GameObject target = null;

            if (!string.IsNullOrWhiteSpace(steamId) && steamId == SteamClient.SteamId.ToString())
                target = GameObject.Find("BloobCharacter");

            if (target == null && !string.IsNullOrWhiteSpace(steamId))
                target = GameObject.Find($"BloobClone_{steamId}");

            if (target == null && !string.IsNullOrWhiteSpace(playerName) && SteamClient.IsValid && playerName == SteamClient.Name)
                target = GameObject.Find("BloobCharacter");

            if (target == null)
                return;

            var bubble = ChatBubble.Attach(target);
            bubble.Show(text);
        }
    }
}
