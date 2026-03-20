using BepInEx.Configuration;
using Newtonsoft.Json.Linq;
using Steamworks;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using TMPro;
using UnityEngine;

namespace Multi_bloob_adventure_idle
{
    public enum ChatTab
    {
        Global,
        Private,
        System
    }

    public enum ChatMessageKind
    {
        Global,
        Private,
        SystemRegular,
        SystemImportant,
        SystemCritical,
        Error
    }

    public class ChatUiMessage
    {
        public ChatMessageKind Kind;
        public string TimestampUtc;
        public string DisplayName;
        public string OtherPartyName;
        public string FromSteamId;
        public string ToSteamId;
        public string Message;
        public string ActiveTitle;
        public string OtherPartyTitle;
        public bool IsIncomingPrivate;
        public bool IsOutgoingPrivate;

        public string LocalTimeString
        {
            get
            {
                if (DateTime.TryParse(TimestampUtc, null, DateTimeStyles.RoundtripKind, out var dt))
                    return dt.ToLocalTime().ToString("HH:mm");
                return "--:--";
            }
        }
    }

    public class ChatBubble : MonoBehaviour
    {
        private TextMeshPro _text;
        private float _until;
        private Camera _cam;

        public static ChatBubble Attach(GameObject target)
        {
            var existing = target.GetComponentInChildren<ChatBubble>(true);
            if (existing != null)
                return existing;

            var go = new GameObject("ChatBubble");
            go.transform.SetParent(target.transform, false);
            go.transform.localPosition = new Vector3(0f, 1.55f, 0f);

            var bubble = go.AddComponent<ChatBubble>();
            bubble.Build();
            return bubble;
        }

        private void Build()
        {
            _cam = Camera.main;

            _text = gameObject.AddComponent<TextMeshPro>();
            _text.text = "";
            _text.fontSize = 4.2f;
            _text.alignment = TextAlignmentOptions.Center;
            _text.color = Color.white;
            _text.enableWordWrapping = false;

            var mr = _text.GetComponent<MeshRenderer>();
            mr.sortingLayerName = "UI";
            gameObject.SetActive(false);
        }

        public void Show(string message, float duration = 4f)
        {
            if (_text == null)
                Build();

            _text.text = ClampBubbleText(message);
            _until = Time.unscaledTime + duration;
            gameObject.SetActive(true);
        }

        private string ClampBubbleText(string text, int max = 60)
        {
            if (string.IsNullOrWhiteSpace(text))
                return "";

            text = text.Trim();
            return text.Length <= max ? text : text.Substring(0, max) + "...";
        }

        private void LateUpdate()
        {
            if (_cam == null)
                _cam = Camera.main;

            if (_cam != null)
            {
                transform.rotation = Quaternion.LookRotation(transform.position - _cam.transform.position);
            }

            if (Time.unscaledTime > _until && gameObject.activeSelf)
            {
                gameObject.SetActive(false);
            }
        }
    }

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
        public static bool IsChatCapturingInput => Instance != null && Instance._isInputActive;

        public static bool ShouldBlockGameInput
        {
            get
            {
                if (Instance == null)
                    return false;

                return Instance.IsPointerOverChatWindow();
            }
        }

        public static bool ShouldBlockKeyboardInput
        {
            get
            {
                if (Instance == null)
                    return false;

                return Instance._isInputActive &&
                       ChatInputOverlay.Instance != null &&
                       ChatInputOverlay.Instance.HasFocus();
            }
        }

        private readonly List<ChatUiMessage> _globalMessages = new();
        private readonly List<ChatUiMessage> _systemMessages = new();
        private readonly Dictionary<string, List<ChatUiMessage>> _privateMessagesBySteamId = new();
        private readonly List<(string id, string label)> _unlockedTitles = new();

        private ChatTab _lastRenderedTab = ChatTab.Global;
        private string _lastRenderedPrivateSteamId = "";
        private bool _forceScrollToBottom;

        private Rect _windowRect = new Rect(20f, 330f, 760f, 310f);
        private Vector2 _scroll;
        private string _input = "";
        private string _statusLine = "";
        private float _statusUntil;
        private bool _isVisible = true;
        private bool _isInputActive;
        private bool _isPinned;
        private bool _isResizing;
        private Vector2 _resizeStartMouse;
        private Rect _resizeStartRect;
        private ChatTab _activeTab = ChatTab.Global;
        private int _unreadGlobalCount;
        private int _unreadSystemCount;
        private readonly HashSet<string> _unreadPrivateSteamIds = new();

        private string _selectedPrivateSteamId = "";
        private string _selectedPrivateName = "";
        private string _activeTitleId = "";

        private ConfigEntry<string> _blockedSteamIdsConfig;
        private ConfigEntry<string> _preferredTitleIdConfig;
        private ConfigEntry<float> _windowPosXConfig;
        private ConfigEntry<float> _windowPosYConfig;
        private ConfigEntry<float> _windowWidthConfig;
        private ConfigEntry<float> _windowHeightConfig;
        private ConfigEntry<bool> _windowPinnedConfig;
        private ConfigEntry<bool> _windowVisibleConfig;

        private HashSet<string> _blockedSteamIds = [];

        private List<PlayerResolutionCandidate> _pendingCandidates = new();
        private PendingSelectionMode _pendingSelectionMode = PendingSelectionMode.None;
        private string _pendingWhisperMessage = "";

        private const int MaxMessagesPerBucket = 250;
        private const string ChatControlName = "MultiBloobGlobalChatInput";
        private const float MinWindowWidth = 420f;
        private const float MinWindowHeight = 300f;
        private const float ResizeGripSize = 18f;
        private const float HeaderSectionHeight = 74f;
        private const float ComposerSectionHeight = 82f;
        private const float FooterSectionHeight = 28f;

        public static ChatSystem Create(ConfigFile config)
        {
            if (Instance != null)
                return Instance;

            var go = new GameObject("BloobsMultiplayer_ChatSystem");
            DontDestroyOnLoad(go);
            var chat = go.AddComponent<ChatSystem>();
            chat.Initialize(config);
            Instance = chat;
            ChatInputOverlay.Create();
            return chat;
        }

        private void Initialize(ConfigFile config)
        {
            _blockedSteamIdsConfig = config.Bind(
                "Chat",
                "Blocked SteamIDs",
                "",
                "Comma-separated SteamIDs that are blocked locally from chat display."
            );

            _preferredTitleIdConfig = config.Bind(
                "Chat",
                "Preferred Title ID",
                "",
                "Preferred chat title ID."
            );

            _windowPosXConfig = config.Bind("Chat Window", "Position X", 20f, "Saved chat window X position.");
            _windowPosYConfig = config.Bind("Chat Window", "Position Y", 330f, "Saved chat window Y position.");
            _windowWidthConfig = config.Bind("Chat Window", "Width", 760f, "Saved chat window width.");
            _windowHeightConfig = config.Bind("Chat Window", "Height", 310f, "Saved chat window height.");
            _windowPinnedConfig = config.Bind("Chat Window", "Pinned", false, "If true, the chat window cannot be dragged.");
            _windowVisibleConfig = config.Bind("Chat Window", "Visible", true, "If false, the chat window is hidden.");

            _windowRect = new Rect(
                _windowPosXConfig.Value,
                _windowPosYConfig.Value,
                Mathf.Max(MinWindowWidth, _windowWidthConfig.Value),
                Mathf.Max(MinWindowHeight, _windowHeightConfig.Value)
            );

            _isPinned = _windowPinnedConfig.Value;
            _isVisible = _windowVisibleConfig.Value;

            ChatInputOverlay.Create();
            ChatInputOverlay.Instance.OnSubmit = submittedText =>
            {
                _input = submittedText ?? "";
                SubmitChat();
                CloseChat(clearInput: true);
            };

            ReloadBlockedList();
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
            if (Instance == this)
                Instance = null;
        }

        private void Update()
        {
            if (!MultiplayerPatchPlugin.isReady || !SteamClient.IsValid)
                return;

            if (!_isInputActive)
            {
                if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) && _isVisible)
                {
                    OpenChat(seedSlash: false);
                }
                else if (Input.GetKeyDown(KeyCode.Slash) && _isVisible)
                {
                    OpenChat(seedSlash: true);
                }
                else if (Input.GetKeyDown(KeyCode.BackQuote))
                {
                    ToggleVisibilityOnly();
                }
            }
            else
            {
                if (Input.GetKeyDown(KeyCode.Escape))
                {
                    CloseChat(clearInput: false);
                }
            }

            if (!string.IsNullOrEmpty(_statusLine) && Time.unscaledTime > _statusUntil)
            {
                _statusLine = "";
            }
        }

        private void OnGUI()
        {
            if (!MultiplayerPatchPlugin.isReady || !_isVisible)
            {
                ChatInputOverlay.Instance?.SetVisible(false);
                return;
            }

            Rect previousRect = _windowRect;
            _windowRect = GUI.Window(781245, _windowRect, DrawChatWindow, "Bloobs Online Chat");
            ClampWindowToScreen();

            if (_windowRect != previousRect)
                SaveWindowLayout();

            if (_isInputActive)
            {
                ChatInputOverlay.Instance?.SetVisible(true);
                ChatInputOverlay.Instance?.SetScreenRect(_windowRect);
            }
            else
            {
                ChatInputOverlay.Instance?.SetVisible(false);
            }
        }

        private void DrawChatWindow(int id)
        {
            Rect pinButtonRect = new Rect(_windowRect.width - 30f, 3f, 24f, 18f);
            string pinLabel = _isPinned ? "P" : "U";
            if (GUI.Button(pinButtonRect, pinLabel))
            {
                _isPinned = !_isPinned;
                SaveWindowLayout();
            }

            GUILayout.BeginVertical();

            DrawTopSection();

            if (_activeTab == ChatTab.Private)
            {
                DrawPrivateTabHeader();
            }

            float privateHeaderHeight = _activeTab == ChatTab.Private ? 34f : 0f;
            float statusHeight = string.IsNullOrEmpty(_statusLine) ? 0f : FooterSectionHeight;
            float bottomSectionHeight = _isInputActive ? ComposerSectionHeight : 52f;

            float messageAreaHeight = Mathf.Max(
                70f,
                _windowRect.height - HeaderSectionHeight - privateHeaderHeight - bottomSectionHeight - statusHeight - 18f
            );

            _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.Height(messageAreaHeight));

            foreach (var line in GetCurrentTabMessages())
            {
                GUILayout.Label(FormatMessage(line));
            }

            GUILayout.EndScrollView();

            string currentPrivateKey = _activeTab == ChatTab.Private ? _selectedPrivateSteamId ?? "" : "";

            if (_lastRenderedTab != _activeTab || _lastRenderedPrivateSteamId != currentPrivateKey)
            {
                _lastRenderedTab = _activeTab;
                _lastRenderedPrivateSteamId = currentPrivateKey;
                _forceScrollToBottom = true;
            }

            if (_forceScrollToBottom)
            {
                _scroll.y = float.MaxValue;
                _forceScrollToBottom = false;
            }

            DrawBottomSection();

            //if (!string.IsNullOrEmpty(_statusLine))
            //{
            //    GUILayout.Space(2f);
            //    GUILayout.Label(_statusLine, GUILayout.Height(FooterSectionHeight - 4f));
            //}

            GUILayout.EndVertical();

            HandleResizeGripInsideWindow();

            if (!_isPinned)
            {
                GUI.DragWindow(new Rect(0, 0, _windowRect.width - 36f, 22));
            }
        }

        private void HandleResizeGripInsideWindow()
        {
            if (!_isVisible || Event.current == null)
                return;

            Rect gripRect = new Rect(
                _windowRect.width - ResizeGripSize - 4f,
                _windowRect.height - ResizeGripSize - 4f,
                ResizeGripSize,
                ResizeGripSize
            );

            GUI.Box(gripRect, "↘");

            Vector2 mouse = Event.current.mousePosition;

            switch (Event.current.type)
            {
                case EventType.MouseDown:
                    if (gripRect.Contains(mouse))
                    {
                        _isResizing = true;
                        _resizeStartMouse = mouse;
                        _resizeStartRect = _windowRect;
                        Event.current.Use();
                    }
                    break;

                case EventType.MouseDrag:
                    if (_isResizing)
                    {
                        Vector2 delta = mouse - _resizeStartMouse;
                        _windowRect.width = Mathf.Max(MinWindowWidth, _resizeStartRect.width + delta.x);
                        _windowRect.height = Mathf.Max(MinWindowHeight, _resizeStartRect.height + delta.y);
                        Event.current.Use();
                    }
                    break;

                case EventType.MouseUp:
                    if (_isResizing)
                    {
                        _isResizing = false;
                        SaveWindowLayout();
                        Event.current.Use();
                    }
                    break;
            }
        }

        private void DrawTopSection()
        {
            GUILayout.BeginVertical(GUILayout.Height(HeaderSectionHeight));

            GUILayout.BeginHorizontal();

            if (GUILayout.Toggle(_activeTab == ChatTab.Global, BuildTabLabel(ChatTab.Global), GUI.skin.button, GUILayout.Width(100)))
            {
                if (_activeTab != ChatTab.Global)
                    ScrollToBottomNextFrame();

                _activeTab = ChatTab.Global;
                ClearUnreadForActiveTab();
            }

            if (GUILayout.Toggle(_activeTab == ChatTab.Private, BuildTabLabel(ChatTab.Private), GUI.skin.button, GUILayout.Width(100)))
            {
                if (_activeTab != ChatTab.Private)
                    ScrollToBottomNextFrame();

                _activeTab = ChatTab.Private;
                ClearUnreadForActiveTab();
            }

            if (GUILayout.Toggle(_activeTab == ChatTab.System, BuildTabLabel(ChatTab.System), GUI.skin.button, GUILayout.Width(100)))
            {
                if (_activeTab != ChatTab.System)
                    ScrollToBottomNextFrame();

                _activeTab = ChatTab.System;
                ClearUnreadForActiveTab();
            }

            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();

            string currentTitleLabel = _unlockedTitles.FirstOrDefault(t => t.id == _activeTitleId).label ?? "None";
            GUILayout.Label($"Title: {currentTitleLabel}", GUILayout.Width(220));

            if (GUILayout.Button("Next Title", GUILayout.Width(100)))
            {
                CycleToNextUnlockedTitle();
            }

            if (GUILayout.Button("Clear Title", GUILayout.Width(100)))
            {
                ClearActiveTitle();
            }

            GUILayout.EndHorizontal();

            GUILayout.EndVertical();
        }

        private void DrawBottomSection()
        {
            GUILayout.BeginVertical(GUILayout.Height(_isInputActive ? ComposerSectionHeight : 52f));

            if (_isInputActive)
            {
                _input = ChatInputOverlay.Instance?.GetText() ?? _input;

                GUILayout.Space(28f);
                GUILayout.Space(6f);

                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Send", GUILayout.Width(90), GUILayout.Height(26)))
                {
                    _input = ChatInputOverlay.Instance?.GetText() ?? _input;
                    SubmitChat();
                }

                if (GUILayout.Button("Close", GUILayout.Width(90), GUILayout.Height(26)))
                {
                    CloseChat(clearInput: false);
                }
                GUILayout.EndHorizontal();
            }
            else
            {
                GUILayout.Space(8f);
                GUILayout.Label("Press Enter or / to chat. Press ` to hide/show the window.");
            }

            GUILayout.EndVertical();
        }

        private void DrawPrivateTabHeader()
        {
            GUILayout.BeginHorizontal(GUILayout.Height(30f));

            var privateTargets = _privateMessagesBySteamId
                .Select(kvp =>
                {
                    var first = kvp.Value.LastOrDefault();
                    return new
                    {
                        SteamId = kvp.Key,
                        Name = first?.OtherPartyName ?? "Unknown",
                        HasUnread = _unreadPrivateSteamIds.Contains(kvp.Key)
                    };
                })
                .OrderBy(x => x.Name)
                .ToList();

            if (privateTargets.Count == 0)
            {
                GUILayout.Label("No active private conversations.");
            }
            else
            {
                foreach (var target in privateTargets)
                {
                    bool selected = _selectedPrivateSteamId == target.SteamId;
                    string privateLabel = target.HasUnread ? $"{target.Name} (*)" : target.Name;

                    if (GUILayout.Toggle(selected, privateLabel, GUI.skin.button, GUILayout.Width(130)))
                    {
                        if (_selectedPrivateSteamId != target.SteamId)
                            ScrollToBottomNextFrame();

                        _selectedPrivateSteamId = target.SteamId;
                        _selectedPrivateName = target.Name;
                        _unreadPrivateSteamIds.Remove(target.SteamId);
                    }
                }
            }

            GUILayout.FlexibleSpace();

            GUI.enabled = !string.IsNullOrWhiteSpace(_selectedPrivateSteamId);

            if (GUILayout.Button("Clear", GUILayout.Width(70)))
            {
                ClearSelectedPrivateConversation(removeTabCompletely: false);
            }

            if (GUILayout.Button("Close Tab", GUILayout.Width(90)))
            {
                ClearSelectedPrivateConversation(removeTabCompletely: true);
            }

            GUI.enabled = true;

            GUILayout.EndHorizontal();
        }

        private IEnumerable<ChatUiMessage> GetCurrentTabMessages()
        {
            return _activeTab switch
            {
                ChatTab.Global => _globalMessages,
                ChatTab.System => _systemMessages,
                ChatTab.Private => GetSelectedPrivateMessages(),
                _ => _globalMessages
            };
        }

        private IEnumerable<ChatUiMessage> GetSelectedPrivateMessages()
        {
            if (string.IsNullOrEmpty(_selectedPrivateSteamId))
                return Array.Empty<ChatUiMessage>();

            if (_privateMessagesBySteamId.TryGetValue(_selectedPrivateSteamId, out var list))
                return list;

            return Array.Empty<ChatUiMessage>();
        }

        private string FormatMessage(ChatUiMessage msg)
        {
            string prefix = $"[{msg.LocalTimeString}] ";

            string displayNameWithTitle = FormatNameWithTitle(msg.ActiveTitle, MultiplayerPatchPlugin.GetPlayerNameFromSteamId(msg.FromSteamId, msg.DisplayName));
            string otherPartyWithTitle = FormatNameWithTitle(msg.OtherPartyTitle, MultiplayerPatchPlugin.GetPlayerNameFromSteamId(msg.ToSteamId, msg.OtherPartyName));

            return msg.Kind switch
            {
                ChatMessageKind.Global => $"{prefix}{displayNameWithTitle}: {msg.Message}",
                ChatMessageKind.Private when msg.IsOutgoingPrivate => $"{prefix}[To {otherPartyWithTitle}] {msg.Message}",
                ChatMessageKind.Private when msg.IsIncomingPrivate => $"{prefix}[From {displayNameWithTitle}] {msg.Message}",
                ChatMessageKind.SystemRegular => $"{prefix}[SERVER] {msg.Message}",
                ChatMessageKind.SystemImportant => $"{prefix}[IMPORTANT] {msg.Message}",
                ChatMessageKind.SystemCritical => $"{prefix}[CRITICAL] {msg.Message}",
                ChatMessageKind.Error => $"{prefix}[ERROR] {msg.Message}",
                _ => $"{prefix}{displayNameWithTitle}: {msg.Message}"
            };
        }

        private string FormatNameWithTitle(string title, string name)
        {
            if (string.IsNullOrWhiteSpace(title))
                return name;

            return $"[{title}] {name}";
        }

        private void CycleToNextUnlockedTitle()
        {
            if (_unlockedTitles.Count == 0)
            {
                AddSystemLine("You do not currently have any unlocked chat titles.", ChatMessageKind.Error);
                return;
            }

            int currentIndex = _unlockedTitles.FindIndex(t => t.id == _activeTitleId);
            int nextIndex = currentIndex + 1;

            if (currentIndex < 0 || nextIndex >= _unlockedTitles.Count)
                nextIndex = 0;

            string nextTitleId = _unlockedTitles[nextIndex].id;
            _preferredTitleIdConfig.Value = nextTitleId;
            MultiplayerPatchPlugin.instance?.SendSetTitlePacket(nextTitleId);
        }

        private void ClearActiveTitle()
        {
            _preferredTitleIdConfig.Value = "";
            MultiplayerPatchPlugin.instance?.SendSetTitlePacket("");
        }

        private void ScrollToBottomNextFrame()
        {
            _forceScrollToBottom = true;
        }

        private void ClearSelectedPrivateConversation(bool removeTabCompletely)
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

                    var next = _privateMessagesBySteamId.Keys.OrderBy(x => x).FirstOrDefault();
                    _selectedPrivateSteamId = next ?? "";
                    _selectedPrivateName = string.IsNullOrWhiteSpace(next)
                        ? ""
                        : MultiplayerPatchPlugin.GetPlayerNameFromSteamId(next, "Unknown");
                }
                else
                {
                    bucket.Clear();
                }

                ScrollToBottomNextFrame();
            }
        }

        private void ClearPrivateConversationBySteamId(string steamId, bool removeTabCompletely)
        {
            if (string.IsNullOrWhiteSpace(steamId))
                return;

            if (_privateMessagesBySteamId.ContainsKey(steamId))
            {
                if (removeTabCompletely)
                    _privateMessagesBySteamId.Remove(steamId);
                else
                    _privateMessagesBySteamId[steamId].Clear();
            }

            if (_selectedPrivateSteamId == steamId)
            {
                if (removeTabCompletely)
                {
                    var next = _privateMessagesBySteamId.Keys.OrderBy(x => x).FirstOrDefault();
                    _selectedPrivateSteamId = next ?? "";
                    _selectedPrivateName = string.IsNullOrWhiteSpace(next)
                        ? ""
                        : MultiplayerPatchPlugin.GetPlayerNameFromSteamId(next, "Unknown");
                }

                ScrollToBottomNextFrame();
            }
        }

        public void OpenChat(bool seedSlash)
        {
            _isVisible = true;
            _isInputActive = true;
            SaveWindowLayout();

            if (seedSlash && string.IsNullOrWhiteSpace(_input))
                _input = "/";

            ChatInputOverlay.Instance?.SetVisible(true);
            ChatInputOverlay.Instance?.SetText(_input);
            ChatInputOverlay.Instance?.Focus();
        }

        public void CloseChat(bool clearInput)
        {
            _isInputActive = false;

            if (clearInput)
                _input = "";

            ChatInputOverlay.Instance?.SetVisible(false);
            ChatInputOverlay.Instance?.Unfocus();
            GUI.FocusControl(null);
        }

        public void ToggleVisibilityOnly()
        {
            _isVisible = !_isVisible;
            if (!_isVisible)
            {
                _isInputActive = false;
                GUI.FocusControl(null);
            }
            SaveWindowLayout();
        }

        public void SubmitChat()
        {
            string raw = (_input ?? "").Trim();
            if (string.IsNullOrWhiteSpace(raw))
            {
                CloseChat(clearInput: true);
                return;
            }

            if (TryHandleLocalOnlyCommand(raw))
            {
                _input = "";
                CloseChat(clearInput: true);
                return;
            }

            MultiplayerPatchPlugin.instance?.SendChatPacket(raw);

            _input = "";
            CloseChat(clearInput: true);
        }

        private void SaveWindowLayout()
        {
            _windowPosXConfig.Value = _windowRect.x;
            _windowPosYConfig.Value = _windowRect.y;
            _windowWidthConfig.Value = _windowRect.width;
            _windowHeightConfig.Value = _windowRect.height;
            _windowPinnedConfig.Value = _isPinned;
            _windowVisibleConfig.Value = _isVisible;
        }

        private void ClampWindowToScreen()
        {
            _windowRect.width = Mathf.Max(MinWindowWidth, _windowRect.width);
            _windowRect.height = Mathf.Max(MinWindowHeight, _windowRect.height);

            float maxX = Mathf.Max(0f, Screen.width - _windowRect.width);
            float maxY = Mathf.Max(0f, Screen.height - _windowRect.height);

            _windowRect.x = Mathf.Clamp(_windowRect.x, 0f, maxX);
            _windowRect.y = Mathf.Clamp(_windowRect.y, 0f, maxY);
        }

        public void ReceiveTitleState(JObject msg)
        {
            _unlockedTitles.Clear();

            var unlocked = msg["unlockedTitles"] as JArray;
            if (unlocked != null)
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
            {
                _activeTitleId = activeObj["id"]?.ToString() ?? "";
            }
            else if (activeTitleToken is JValue activeValue)
            {
                _activeTitleId = activeValue.ToString() ?? "";
            }

            string preferred = _preferredTitleIdConfig.Value ?? "";
            if (!string.IsNullOrWhiteSpace(preferred) &&
                _unlockedTitles.Any(t => t.id == preferred) &&
                preferred != _activeTitleId)
            {
                MultiplayerPatchPlugin.instance?.SendSetTitlePacket(preferred);
            }
        }

        private bool TryHandleLocalOnlyCommand(string raw)
        {
            if (!raw.StartsWith("/"))
                return false;

            string[] split = raw.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (split.Length == 0)
                return false;

            string cmd = split[0].ToLowerInvariant();

            switch (cmd)
            {
                case "/help":
                    AddSystemLine("Available commands:", ChatMessageKind.SystemRegular);
                    AddSystemLine("/w <name|partial|steamId> <message> - Whisper a player", ChatMessageKind.SystemRegular);
                    AddSystemLine("/wselect <number> - Finish a pending whisper selection", ChatMessageKind.SystemRegular);
                    AddSystemLine("/r <message> - Reply to your last whisper target", ChatMessageKind.SystemRegular);
                    AddSystemLine("/unblock <steamId> - Unblock a player locally by SteamID", ChatMessageKind.SystemRegular);
                    AddSystemLine("/block <partialName|steamId> - Block by partial username or SteamID", ChatMessageKind.SystemRegular);
                    AddSystemLine("/blockselect <number> - Finish a pending /block selection", ChatMessageKind.SystemRegular);
                    AddSystemLine("/unblock <partialName|steamId> - Unblock by partial username or SteamID", ChatMessageKind.SystemRegular);
                    AddSystemLine("/pmclear - Clear the selected private conversation", ChatMessageKind.SystemRegular);
                    AddSystemLine("/pmclose - Close the selected private conversation tab", ChatMessageKind.SystemRegular);
                    AddSystemLine("/title - Cycle to the next unlocked title", ChatMessageKind.SystemRegular);
                    AddSystemLine("/cleartitle - Clear your active title", ChatMessageKind.SystemRegular);
                    return true;

                case "/block":
                    if (split.Length < 2)
                    {
                        AddSystemLine("Usage: /block <playerName|steamId>", ChatMessageKind.Error);
                        return true;
                    }

                    BeginBlockResolution(string.Join(" ", split.Skip(1)));
                    return true;

                case "/blockselect":
                    if (split.Length < 2 || !int.TryParse(split[1], out int selectedIndex))
                    {
                        AddSystemLine("Usage: /blockselect <number>", ChatMessageKind.Error);
                        return true;
                    }

                    CompletePendingSelection(PendingSelectionMode.Block, selectedIndex);
                    return true;

                case "/unblock":
                    if (split.Length < 2)
                    {
                        AddSystemLine("Usage: /unblock <playerName|steamId>", ChatMessageKind.Error);
                        return true;
                    }

                    BeginUnblockResolution(string.Join(" ", split.Skip(1)));
                    return true;

                case "/w":
                case "/whisper":
                    if (split.Length < 3)
                    {
                        AddSystemLine("Usage: /w <playerName|partial|steamId> <message>", ChatMessageKind.Error);
                        return true;
                    }

                    BeginWhisperResolution(split[1], string.Join(" ", split.Skip(2)));
                    return true;

                case "/wselect":
                    if (split.Length < 2 || !int.TryParse(split[1], out int whisperSelection))
                    {
                        AddSystemLine("Usage: /wselect <number>", ChatMessageKind.Error);
                        return true;
                    }

                    CompletePendingSelection(PendingSelectionMode.Whisper, whisperSelection);
                    return true;

                case "/pmclear":
                    ClearSelectedPrivateConversation(removeTabCompletely: false);
                    AddSystemLine("Cleared selected private conversation.", ChatMessageKind.SystemRegular);
                    return true;

                case "/pmclose":
                    ClearSelectedPrivateConversation(removeTabCompletely: true);
                    AddSystemLine("Closed selected private conversation tab.", ChatMessageKind.SystemRegular);
                    return true;

                case "/title":
                    CycleToNextUnlockedTitle();
                    return true;

                case "/cleartitle":
                    ClearActiveTitle();
                    return true;
            }

            return false;
        }

        private void BeginBlockResolution(string input)
        {
            var matches = ResolvePlayersFromInput(input, onlyBlocked: false);

            if (matches.Count == 0)
            {
                AddSystemLine($"No players found matching '{input}'.", ChatMessageKind.Error);
                return;
            }

            if (matches.Count == 1)
            {
                var target = matches[0];
                BlockSteamId(target.steamId);
                AddSystemLine($"Blocked {target.name} ({target.steamId}) locally.", ChatMessageKind.SystemRegular);
                return;
            }

            BeginPendingSelection(
                PendingSelectionMode.Block,
                matches,
                $"Found multiple players matching '{input}'. Select one with /blockselect <number>:"
            );
        }

        private void BeginUnblockResolution(string input)
        {
            var matches = ResolvePlayersFromInput(input, onlyBlocked: true);

            if (matches.Count == 0)
            {
                AddSystemLine($"No blocked players found matching '{input}'.", ChatMessageKind.Error);
                return;
            }

            if (matches.Count == 1)
            {
                var target = matches[0];
                UnblockSteamId(target.steamId);
                AddSystemLine($"Unblocked {target.name} ({target.steamId}) locally.", ChatMessageKind.SystemRegular);
                return;
            }

            BeginPendingSelection(
                PendingSelectionMode.Unblock,
                matches,
                $"Found multiple blocked players matching '{input}'. Select one with /blockselect <number>:"
            );
        }

        private void BeginWhisperResolution(string input, string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                AddSystemLine("Whisper message was empty.", ChatMessageKind.Error);
                return;
            }

            var matches = ResolvePlayersFromInput(input, onlyBlocked: false)
                .Where(p => p != null && p.steamId != SteamClient.SteamId.ToString())
                .ToList();

            if (matches.Count == 0)
            {
                AddSystemLine($"No players found matching '{input}'.", ChatMessageKind.Error);
                return;
            }

            if (matches.Count == 1)
            {
                var target = matches[0];
                MultiplayerPatchPlugin.instance?.SendChatPacket($"/w {target.steamId} {message}");
                //AddSystemLine($"Resolved whisper target to {target.name} ({target.steamId}).", ChatMessageKind.SystemRegular);
                return;
            }

            _pendingWhisperMessage = message;

            BeginPendingSelection(
                PendingSelectionMode.Whisper,
                matches,
                $"Found multiple whisper targets matching '{input}'. Select one with /wselect <number>:"
            );
        }

        private void BeginPendingSelection(PendingSelectionMode mode, List<PlayerData> matches, string header)
        {
            _pendingCandidates = matches
                .Where(p => p != null && !string.IsNullOrWhiteSpace(p.steamId))
                .GroupBy(p => p.steamId)
                .Select(g => g.First())
                .Select(p => new PlayerResolutionCandidate
                {
                    Name = p.name,
                    SteamId = p.steamId,
                    LastMessagePreview = GetLastKnownMessagePreviewForSteamId(p.steamId)
                })
                .ToList();

            _pendingSelectionMode = mode;

            AddSystemLine(header, ChatMessageKind.SystemRegular);

            for (int i = 0; i < _pendingCandidates.Count; i++)
            {
                var c = _pendingCandidates[i];
                AddSystemLine($"{i + 1}: {c.Name} ({c.SteamId}) - {c.LastMessagePreview}", ChatMessageKind.SystemRegular);
            }
        }

        private void CompletePendingSelection(PendingSelectionMode expectedMode, int selectedIndex)
        {
            if (_pendingSelectionMode != expectedMode || _pendingCandidates.Count == 0)
            {
                AddSystemLine("There is no matching pending selection.", ChatMessageKind.Error);
                return;
            }

            int zeroBased = selectedIndex - 1;
            if (zeroBased < 0 || zeroBased >= _pendingCandidates.Count)
            {
                AddSystemLine("Invalid selection number.", ChatMessageKind.Error);
                return;
            }

            var chosen = _pendingCandidates[zeroBased];

            switch (expectedMode)
            {
                case PendingSelectionMode.Block:
                    BlockSteamId(chosen.SteamId);
                    AddSystemLine($"Blocked {chosen.Name} ({chosen.SteamId}) locally.", ChatMessageKind.SystemRegular);
                    break;

                case PendingSelectionMode.Unblock:
                    UnblockSteamId(chosen.SteamId);
                    AddSystemLine($"Unblocked {chosen.Name} ({chosen.SteamId}) locally.", ChatMessageKind.SystemRegular);
                    break;

                case PendingSelectionMode.Whisper:
                    MultiplayerPatchPlugin.instance?.SendChatPacket($"/w {chosen.SteamId} {_pendingWhisperMessage}");
                    //AddSystemLine($"Resolved whisper target to {chosen.Name} ({chosen.SteamId}).", ChatMessageKind.SystemRegular);
                    break;
            }

            _pendingCandidates.Clear();
            _pendingSelectionMode = PendingSelectionMode.None;
            _pendingWhisperMessage = "";
        }

        private List<PlayerData> ResolvePlayersFromInput(string input, bool onlyBlocked)
        {
            var results = new List<PlayerData>();
            if (string.IsNullOrWhiteSpace(input))
                return results;

            string trimmed = input.Trim();

            lock (MultiplayerPatchPlugin.Players)
            {
                if (LooksLikeSteamId(trimmed))
                {
                    if (MultiplayerPatchPlugin.Players.TryGetValue(trimmed, out var bySteamId) && bySteamId != null)
                    {
                        if (!onlyBlocked || IsBlocked(bySteamId.steamId))
                            results.Add(bySteamId);

                        return results.GroupBy(p => p.steamId).Select(g => g.First()).ToList();
                    }
                }

                var exactMatches = MultiplayerPatchPlugin.Players.Values
                    .Where(p => p != null && !string.IsNullOrWhiteSpace(p.name) && p.name.Equals(trimmed, StringComparison.OrdinalIgnoreCase))
                    .Where(p => !onlyBlocked || IsBlocked(p.steamId))
                    .GroupBy(p => p.steamId)
                    .Select(g => g.First())
                    .ToList();

                if (exactMatches.Count > 0)
                    return exactMatches;

                return MultiplayerPatchPlugin.Players.Values
                    .Where(p => p != null && !string.IsNullOrWhiteSpace(p.name) && p.name.IndexOf(trimmed, StringComparison.OrdinalIgnoreCase) >= 0)
                    .Where(p => !onlyBlocked || IsBlocked(p.steamId))
                    .GroupBy(p => p.steamId)
                    .Select(g => g.First())
                    .OrderBy(p => p.name.Length)
                    .ThenBy(p => p.name, StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }
        }

        private bool LooksLikeSteamId(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return false;

            if (input.Length < 10)
                return false;

            return input.All(char.IsDigit);
        }

        private string GetLastKnownMessagePreviewForSteamId(string steamId)
        {
            if (string.IsNullOrWhiteSpace(steamId))
                return "No recent messages";

            var global = _globalMessages.LastOrDefault(m => m.FromSteamId == steamId);
            if (global != null && !string.IsNullOrWhiteSpace(global.Message))
                return TruncatePreview(global.Message);

            if (_privateMessagesBySteamId.TryGetValue(steamId, out var priv) && priv.Count > 0)
            {
                var last = priv[priv.Count - 1];
                if (!string.IsNullOrWhiteSpace(last.Message))
                    return TruncatePreview(last.Message);
            }

            return "No recent messages";
        }

        private string TruncatePreview(string text, int max = 45)
        {
            if (string.IsNullOrWhiteSpace(text))
                return "No recent messages";

            text = text.Trim();
            if (text.Length <= max)
                return text;

            return text.Substring(0, max) + "...";
        }

        public void ReceiveHistory(JArray messages)
        {
            if (messages == null)
                return;

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
                }
            }
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
        }

        private void MarkUnreadForIncomingMessage(ChatTab tab, string privateSteamId = null)
        {
            switch (tab)
            {
                case ChatTab.Global:
                    if (_activeTab != ChatTab.Global)
                        _unreadGlobalCount++;
                    break;

                case ChatTab.System:
                    if (_activeTab != ChatTab.System)
                        _unreadSystemCount++;
                    break;

                case ChatTab.Private:
                    if (string.IsNullOrWhiteSpace(privateSteamId))
                        return;

                    if (!(_activeTab == ChatTab.Private && _selectedPrivateSteamId == privateSteamId))
                        _unreadPrivateSteamIds.Add(privateSteamId);
                    break;
            }
        }

        private void ClearUnreadForActiveTab()
        {
            switch (_activeTab)
            {
                case ChatTab.Global:
                    _unreadGlobalCount = 0;
                    break;

                case ChatTab.System:
                    _unreadSystemCount = 0;
                    break;

                case ChatTab.Private:
                    if (!string.IsNullOrWhiteSpace(_selectedPrivateSteamId))
                        _unreadPrivateSteamIds.Remove(_selectedPrivateSteamId);
                    break;
            }
        }

        private string BuildTabLabel(ChatTab tab)
        {
            return tab switch
            {
                ChatTab.Global => _unreadGlobalCount > 0 ? $"Global ({_unreadGlobalCount})" : "Global",
                ChatTab.System => _unreadSystemCount > 0 ? $"System ({_unreadSystemCount})" : "System",
                ChatTab.Private => _unreadPrivateSteamIds.Count > 0 ? $"Private ({_unreadPrivateSteamIds.Count})" : "Private",
                _ => tab.ToString()
            };
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
            {
                ShowBubbleForPlayer(fromSteamId, fromName, text);
            }
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
                bucket = new List<ChatUiMessage>();
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

            _selectedPrivateSteamId = otherSteamId;
            _selectedPrivateName = otherName;
            if (_activeTab == ChatTab.Private && _selectedPrivateSteamId == otherSteamId)
                _unreadPrivateSteamIds.Remove(otherSteamId);
            ScrollToBottomNextFrame();
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

            _forceScrollToBottom = true;
        }

        private void SetStatus(string text, float duration = 3f)
        {
            _statusLine = text ?? "";
            _statusUntil = Time.unscaledTime + duration;
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

            if (target == null && !string.IsNullOrWhiteSpace(steamId))
            {
                var cloneMarkers = GameObject.FindObjectsOfType<IsMultiplayerClone>(true);
                foreach (var marker in cloneMarkers)
                {
                    if (marker != null && marker.gameObject != null && !string.IsNullOrWhiteSpace(marker.steamId) && marker.steamId == steamId)
                    {
                        target = marker.gameObject;
                        break;
                    }
                }
            }

            if (target == null && !string.IsNullOrWhiteSpace(playerName) && SteamClient.IsValid && playerName == SteamClient.Name)
                target = GameObject.Find("BloobCharacter");

            if (target == null && !string.IsNullOrWhiteSpace(playerName))
            {
                var cloneMarkers = GameObject.FindObjectsOfType<IsMultiplayerClone>(true);
                foreach (var marker in cloneMarkers)
                {
                    if (marker != null && marker.gameObject != null && !string.IsNullOrWhiteSpace(marker.displayName) && marker.displayName.Equals(playerName, StringComparison.OrdinalIgnoreCase))
                    {
                        target = marker.gameObject;
                        break;
                    }
                }
            }

            if (target == null)
                return;

            var bubble = ChatBubble.Attach(target);
            bubble.Show(text);
        }

        public bool IsPointerOverChatWindow()
        {
            if (!_isVisible)
                return false;

            Vector2 mouse = new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y);
            return _windowRect.Contains(mouse);
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
            _blockedSteamIds = new HashSet<string>(
                (_blockedSteamIdsConfig.Value ?? "").Split(',').Select(x => x.Trim()).Where(x => !string.IsNullOrWhiteSpace(x))
            );
        }

        private void SaveBlockedList()
        {
            _blockedSteamIdsConfig.Value = string.Join(",", _blockedSteamIds.OrderBy(x => x));
        }
    }
}
