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
        public static bool IsChatCapturingInput => Instance != null && Instance._isChatOpen;

        private readonly List<ChatUiMessage> _globalMessages = new();
        private readonly List<ChatUiMessage> _systemMessages = new();
        private readonly Dictionary<string, List<ChatUiMessage>> _privateMessagesBySteamId = new();

        private Rect _windowRect = new Rect(20f, 330f, 760f, 310f);
        private Vector2 _scroll;
        private string _input = "";
        private string _statusLine = "";
        private float _statusUntil;
        private bool _isChatOpen;
        private bool _focusInputNextFrame;
        private ChatTab _activeTab = ChatTab.Global;

        private string _selectedPrivateSteamId = "";
        private string _selectedPrivateName = "";

        private ConfigEntry<string> _blockedSteamIdsConfig;
        private HashSet<string> _blockedSteamIds = new();

        private List<PlayerResolutionCandidate> _pendingCandidates = new();
        private PendingSelectionMode _pendingSelectionMode = PendingSelectionMode.None;
        private string _pendingWhisperMessage = "";

        private const int MaxMessagesPerBucket = 250;
        private const string ChatControlName = "MultiBloobGlobalChatInput";

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
            _blockedSteamIdsConfig = config.Bind(
                "Chat",
                "Blocked SteamIDs",
                "",
                "Comma-separated SteamIDs that are blocked locally from chat display."
            );

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

            if (!_isChatOpen)
            {
                if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
                {
                    OpenChat(seedSlash: false);
                }
                else if (Input.GetKeyDown(KeyCode.Slash))
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
                else if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
                {
                    SubmitChat();
                }
            }

            if (!string.IsNullOrEmpty(_statusLine) && Time.unscaledTime > _statusUntil)
            {
                _statusLine = "";
            }
        }

        private void OnGUI()
        {
            if (!MultiplayerPatchPlugin.isReady)
                return;

            _windowRect = GUI.Window(781245, _windowRect, DrawChatWindow, "Multiplayer Chat");
        }

        private void DrawChatWindow(int id)
        {
            GUILayout.BeginVertical();

            GUILayout.BeginHorizontal();
            if (GUILayout.Toggle(_activeTab == ChatTab.Global, "Global", GUI.skin.button, GUILayout.Width(100)))
                _activeTab = ChatTab.Global;

            if (GUILayout.Toggle(_activeTab == ChatTab.Private, "Private", GUI.skin.button, GUILayout.Width(100)))
                _activeTab = ChatTab.Private;

            if (GUILayout.Toggle(_activeTab == ChatTab.System, "System", GUI.skin.button, GUILayout.Width(100)))
                _activeTab = ChatTab.System;
            GUILayout.EndHorizontal();

            if (_activeTab == ChatTab.Private)
            {
                DrawPrivateTabHeader();
            }

            _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.Height(_isChatOpen ? 190f : 230f));

            foreach (var line in GetCurrentTabMessages())
            {
                GUILayout.Label(FormatMessage(line));
            }

            GUILayout.EndScrollView();

            if (_isChatOpen)
            {
                GUI.SetNextControlName(ChatControlName);
                _input = GUILayout.TextField(_input, GUILayout.Height(28f));

                if (_focusInputNextFrame)
                {
                    GUI.FocusControl(ChatControlName);
                    _focusInputNextFrame = false;
                }

                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Send", GUILayout.Width(90), GUILayout.Height(26)))
                {
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
                GUILayout.Label("Press Enter or / to chat. Press ` to hide/show the window.");
            }

            if (!string.IsNullOrEmpty(_statusLine))
            {
                GUILayout.Space(4f);
                GUILayout.Label(_statusLine);
            }

            GUILayout.EndVertical();
            GUI.DragWindow(new Rect(0, 0, 9999, 22));
        }

        private void DrawPrivateTabHeader()
        {
            GUILayout.BeginHorizontal();

            var privateTargets = _privateMessagesBySteamId
                .Select(kvp =>
                {
                    var first = kvp.Value.LastOrDefault();
                    return new
                    {
                        SteamId = kvp.Key,
                        Name = first?.OtherPartyName ?? "Unknown"
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
                    if (GUILayout.Toggle(selected, target.Name, GUI.skin.button, GUILayout.Width(130)))
                    {
                        _selectedPrivateSteamId = target.SteamId;
                        _selectedPrivateName = target.Name;
                    }
                }
            }

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

            return msg.Kind switch
            {
                ChatMessageKind.Global => $"{prefix}{msg.DisplayName}: {msg.Message}",
                ChatMessageKind.Private when msg.IsOutgoingPrivate => $"{prefix}[To {msg.OtherPartyName}] {msg.Message}",
                ChatMessageKind.Private when msg.IsIncomingPrivate => $"{prefix}[From {msg.DisplayName}] {msg.Message}",
                ChatMessageKind.SystemRegular => $"{prefix}[SERVER] {msg.Message}",
                ChatMessageKind.SystemImportant => $"{prefix}[IMPORTANT] {msg.Message}",
                ChatMessageKind.SystemCritical => $"{prefix}[CRITICAL] {msg.Message}",
                ChatMessageKind.Error => $"{prefix}[ERROR] {msg.Message}",
                _ => $"{prefix}{msg.DisplayName}: {msg.Message}"
            };
        }

        public void OpenChat(bool seedSlash)
        {
            _isChatOpen = true;
            _focusInputNextFrame = true;

            if (seedSlash && string.IsNullOrWhiteSpace(_input))
            {
                _input = "/";
            }
        }

        public void CloseChat(bool clearInput)
        {
            _isChatOpen = false;

            if (clearInput)
                _input = "";

            GUI.FocusControl(null);
        }

        public void ToggleVisibilityOnly()
        {
            _windowRect.x = (_windowRect.x < -700f) ? 20f : -900f;
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

        private bool TryHandleLocalOnlyCommand(string raw)
        {
            if (!raw.StartsWith("/"))
                return false;

            string[] split = raw.Split(' ', (char)StringSplitOptions.RemoveEmptyEntries);
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
                    AddSystemLine("/unblockname <partialName|steamId> - Unblock by partial username or SteamID", ChatMessageKind.SystemRegular);
                    return true;

                case "/unblock":
                    if (split.Length < 2)
                    {
                        AddSystemLine("Usage: /unblock <steamId>", ChatMessageKind.Error);
                        return true;
                    }

                    UnblockSteamId(split[1]);
                    AddSystemLine($"Unblocked SteamID {split[1]} locally.", ChatMessageKind.SystemRegular);
                    return true;

                case "/block":
                    if (split.Length < 2)
                    {
                        AddSystemLine("Usage: /blockname <playerName|steamId>", ChatMessageKind.Error);
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

                case "/unblockname":
                    if (split.Length < 2)
                    {
                        AddSystemLine("Usage: /unblockname <playerName|steamId>", ChatMessageKind.Error);
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
                AddSystemLine($"Resolved whisper target to {target.name} ({target.steamId}).", ChatMessageKind.SystemRegular);
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
                    AddSystemLine($"Resolved whisper target to {chosen.Name} ({chosen.SteamId}).", ChatMessageKind.SystemRegular);
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
                // SteamID direct fallback first
                if (LooksLikeSteamId(trimmed))
                {
                    if (MultiplayerPatchPlugin.Players.TryGetValue(trimmed, out var bySteamId) && bySteamId != null)
                    {
                        if (!onlyBlocked || IsBlocked(bySteamId.steamId))
                            results.Add(bySteamId);

                        return results
                            .GroupBy(p => p.steamId)
                            .Select(g => g.First())
                            .ToList();
                    }
                }

                // Exact name first
                var exactMatches = MultiplayerPatchPlugin.Players.Values
                    .Where(p =>
                        p != null &&
                        !string.IsNullOrWhiteSpace(p.name) &&
                        p.name.Equals(trimmed, StringComparison.OrdinalIgnoreCase))
                    .Where(p => !onlyBlocked || IsBlocked(p.steamId))
                    .GroupBy(p => p.steamId)
                    .Select(g => g.First())
                    .ToList();

                if (exactMatches.Count > 0)
                    return exactMatches;

                // Then partial name match
                var partialMatches = MultiplayerPatchPlugin.Players.Values
                    .Where(p =>
                        p != null &&
                        !string.IsNullOrWhiteSpace(p.name) &&
                        p.name.IndexOf(trimmed, StringComparison.OrdinalIgnoreCase) >= 0)
                    .Where(p => !onlyBlocked || IsBlocked(p.steamId))
                    .GroupBy(p => p.steamId)
                    .Select(g => g.First())
                    .OrderBy(p => p.name.Length)
                    .ThenBy(p => p.name, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                return partialMatches;
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
                    msg["toName"]?.ToString(),
                    msg["toSteamId"]?.ToString(),
                    msg["message"]?.ToString(),
                    msg["timestampUtc"]?.ToString()
                );
            }
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

        private void ReceiveGlobalMessage(string fromName, string fromSteamId, string text, string timestampUtc, bool showBubble)
        {
            if (IsBlocked(fromSteamId))
                return;

            var msg = new ChatUiMessage
            {
                Kind = ChatMessageKind.Global,
                TimestampUtc = timestampUtc,
                DisplayName = fromName,
                FromSteamId = fromSteamId,
                Message = text
            };

            AddMessage(_globalMessages, msg);

            if (showBubble)
            {
                ShowBubbleForPlayer(fromSteamId, fromName, text);
            }
        }

        private void ReceivePrivateMessage(string fromName, string fromSteamId, string toName, string toSteamId, string text, string timestampUtc)
        {
            string mySteamId = SteamClient.SteamId.ToString();

            string otherSteamId;
            string otherName;
            bool incoming;

            if (fromSteamId == mySteamId)
            {
                otherSteamId = toSteamId;
                otherName = toName;
                incoming = false;
            }
            else
            {
                otherSteamId = fromSteamId;
                otherName = fromName;
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
                IsIncomingPrivate = incoming,
                IsOutgoingPrivate = !incoming
            };

            AddMessage(bucket, msg);

            _selectedPrivateSteamId = otherSteamId;
            _selectedPrivateName = otherName;
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
            SetStatus(text);
        }

        private void AddMessage(List<ChatUiMessage> list, ChatUiMessage msg)
        {
            list.Add(msg);
            while (list.Count > MaxMessagesPerBucket)
                list.RemoveAt(0);

            _scroll.y = float.MaxValue;
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

            if (steamId == SteamClient.SteamId.ToString())
            {
                target = GameObject.Find("BloobCharacter");
            }
            else if (!string.IsNullOrWhiteSpace(steamId))
            {
                target = GameObject.Find($"BloobClone_{steamId}");
            }

            if (target == null && !string.IsNullOrWhiteSpace(playerName) && playerName == SteamClient.Name)
            {
                target = GameObject.Find("BloobCharacter");
            }

            if (target == null)
                return;

            var bubble = ChatBubble.Attach(target);
            bubble.Show(text);
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
                (_blockedSteamIdsConfig.Value ?? "")
                    .Split(',')
                    .Select(x => x.Trim())
                    .Where(x => !string.IsNullOrWhiteSpace(x))
            );
        }

        private void SaveBlockedList()
        {
            _blockedSteamIdsConfig.Value = string.Join(",", _blockedSteamIds.OrderBy(x => x));
        }
    }
}
