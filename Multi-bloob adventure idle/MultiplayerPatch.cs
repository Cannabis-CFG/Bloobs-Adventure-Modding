using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Steamworks;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using WebSocketSharp;
using Image = UnityEngine.UI.Image;

namespace Multi_bloob_adventure_idle
{
    [BepInPlugin("com.cannabis.multibloobidle", "Multibloob Adventure Idle", "1.4.0")]
    public class MultiplayerPatchPlugin : BaseUnityPlugin
    {
        private readonly Queue<string> messageQueue = new();
        private readonly object queueLock = new();

        private readonly LocalPlayerRuntimeCache localPlayerCache = new();
        private readonly SkillDataCache skillDataCache = new();
        private readonly BossKillCache bossKillCache = new();

        private WebSocket ws;
        private bool isConnected;
        private bool isMonitoringConnection;
        private Coroutine connectionMonitorCoroutine;

        private Coroutine positionCoroutine;
        private Coroutine presenceCoroutine;
        private Coroutine ghostPlayerCoroutine;
        private Coroutine skillRefreshCoroutine;

        public static bool isReady;
        private bool nameTagCreated;
        public static ConfigEntry<bool> enableLevelPanel;
        public static ConfigEntry<bool> enableContextMenu;
        private string nameCache;
        private string steamIdCache;
        private int cachedActiveHatIndex = -1;
        private int cachedActiveWingIndex = -1;
        private bool cachedIsTurboSave;
        private bool atMm;
        private JObject lastPayload;
        public static MultiplayerPatchPlugin instance;

        public static readonly Dictionary<string, PlayerData> Players = [];

        private const string WebSocketUrl = "ws://172.93.111.163:42069";
        private const float PositionSendIntervalSeconds = 0.2f;
        private const float PresenceSendIntervalSeconds = 1f;
        private const float GhostRefreshIntervalSeconds = 1f;
        private const float SkillRefreshIntervalSeconds = 5f;

        private void Awake()
        {
            InitializeWebSocket();
            ws.ConnectAsync();

            PatchAll();
            SceneManager.activeSceneChanged += OnActiveSceneChanged;
            HandleConfiguration();
            instance ??= this;
        }

        private void PatchAll()
        {
            Harmony.CreateAndPatchAll(typeof(CharacterMovementUpdatePatch));
            Harmony.CreateAndPatchAll(typeof(CharacterMovementCloseAllShopsPatch));
            Harmony.CreateAndPatchAll(typeof(TeleportScriptTeleportPatch));
            Harmony.CreateAndPatchAll(typeof(BloobColourChangeWingPatch));
            Harmony.CreateAndPatchAll(typeof(BloobColourChangeHatPatch));
            Harmony.CreateAndPatchAll(typeof(CharacterMovementIsPointerOverUiPatch));
            Harmony.CreateAndPatchAll(typeof(MapToggleLateUpdatePatch));
            Harmony.CreateAndPatchAll(typeof(OfflineProgressionLateUpdatePatch));
            Harmony.CreateAndPatchAll(typeof(MenuBarLateUpdatePatch));
            Harmony.CreateAndPatchAll(typeof(CameraMovementLateUpdatePatch));
            Harmony.CreateAndPatchAll(typeof(PrestigeManagerLateUpdatePatch));
            Harmony.CreateAndPatchAll(typeof(BuildingManagerLateUpdatePatch));
            Harmony.CreateAndPatchAll(typeof(CharacterMovementHandleManualInputPatch));
            Harmony.CreateAndPatchAll(typeof(BloobColourChangeHideHatPatch));
            Harmony.CreateAndPatchAll(typeof(BloobColourChangeHideWingsPatch));
        }

        private void InitializeWebSocket()
        {
            ws = new WebSocket(WebSocketUrl);
            HookWebSocketEvents();
        }

        private void HookWebSocketEvents()
        {
            ws.OnOpen += (sender, e) =>
            {
                isConnected = true;
                Debug.Log("WebSocket Connected.");
            };

            ws.OnError += (sender, e) =>
            {
                Debug.Log("WebSocket Error:" + e.Message);
            };

            ws.OnClose += (sender, e) =>
            {
                isConnected = false;
                Debug.Log($"Close Code: {e.Code}");
                Debug.Log($"Reason: {e.Reason}");

                if (e.Code != 1000)
                {
                    Debug.LogWarning("Connection was closed abnormally");
                    BeginConnectionMonitor();
                }
            };

            ws.OnMessage += (sender, e) =>
            {
                lock (queueLock)
                {
                    messageQueue.Enqueue(e.Data);
                }
            };
        }

        private void RebuildWebSocket()
        {
            try
            {
                if (ws != null)
                {
                    try
                    {
                        ws.CloseAsync();
                    }
                    catch
                    {
                        // Ignore close errors during rebuild.
                    }
                }
            }
            catch
            {
                // Ignore cleanup errors during rebuild.
            }

            InitializeWebSocket();
        }

        private void OnApplicationQuit()
        {
            try
            {
                ws?.Close();
            }
            catch
            {
                // Ignore close exceptions on quit.
            }
        }

        private void Update()
        {
            if (!isReady || !SteamClient.IsValid)
                return;

            if (string.IsNullOrEmpty(nameCache))
                nameCache = SteamClient.Name;

            if (string.IsNullOrEmpty(steamIdCache))
                steamIdCache = SteamClient.SteamId.ToString();

            FlushMessageQueue();
        }

        private void FlushMessageQueue()
        {
            lock (queueLock)
            {
                while (messageQueue.Count > 0)
                {
                    var json = messageQueue.Dequeue();
                    JObject msg;

                    try
                    {
                        msg = JObject.Parse(json);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError("Invalid JSON: " + ex);
                        continue;
                    }

                    var type = msg["type"]?.ToString();
                    if (string.IsNullOrEmpty(type))
                        continue;

                    lock (Players)
                    {
                        switch (type)
                        {
                            case "initialState":
                                HandleInitialState(msg["data"] as JArray);
                                break;

                            case "newPlayer":
                                HandleNewPlayer(msg["data"]);
                                break;

                            case "update":
                                HandleRemoteUpdate(msg);
                                break;

                            case "clientRemoved":
                            case "disconnect":
                                HandleClientDisconnect(msg["steamId"]?.ToString(), msg["name"]?.ToString());
                                break;

                            case "chatHistory":
                                ChatSystem.Instance?.ReceiveHistory(msg["messages"] as JArray);
                                break;

                            case "chatMessage":
                                ChatSystem.Instance?.ReceiveChatMessage(msg);
                                break;

                            case "serverBroadcast":
                                ChatSystem.Instance?.ReceiveBroadcast(msg);
                                break;

                            case "chatError":
                                ChatSystem.Instance?.ReceiveError(msg["error"]?.ToString() ?? "Unknown chat error.");
                                break;

                            case "titleState":
                                ChatSystem.Instance?.ReceiveTitleState(msg);
                                break;

                            case "clanState":
                                ChatSystem.Instance?.ReceiveClanState(msg);
                                break;

                            case "clanProfile":
                                ChatSystem.Instance?.ReceiveClanProfile(msg);
                                break;
                        }
                    }
                }
            }
        }

        private void HandleInitialState(JArray dataArray)
        {
            if (dataArray == null)
                return;

            foreach (var token in dataArray)
            {
                var pd = token.ToObject<PlayerData>();
                if (pd == null || string.IsNullOrWhiteSpace(pd.steamId))
                    continue;

                if (pd.steamId == steamIdCache)
                    continue;

                Players[pd.steamId] = pd;
            }
        }

        private void HandleNewPlayer(JToken dataToken)
        {
            if (dataToken == null)
                return;

            var pd = dataToken.ToObject<PlayerData>();
            if (pd == null || string.IsNullOrWhiteSpace(pd.steamId))
                return;

            if (pd.steamId != steamIdCache)
                Players[pd.steamId] = pd;
        }

        private void HandleRemoteUpdate(JObject msg)
        {
            var steamId = msg["steamId"]?.ToString();
            var name = msg["name"]?.ToString();

            PlayerData existing = null;
            var resolvedSteamId = steamId;

            if (!string.IsNullOrWhiteSpace(steamId) && Players.TryGetValue(steamId, out existing))
            {
            }
            else if (!string.IsNullOrWhiteSpace(name))
            {
                existing = Players.Values.FirstOrDefault(p =>
                    p != null &&
                    !string.IsNullOrWhiteSpace(p.name) &&
                    p.name.Equals(name, StringComparison.OrdinalIgnoreCase));

                if (existing != null)
                    resolvedSteamId = existing.steamId;
            }

            if (existing == null || string.IsNullOrWhiteSpace(resolvedSteamId))
                return;

            foreach (var prop in msg.Properties())
            {
                switch (prop.Name)
                {
                    case "currentPosition":
                        existing.currentPosition = prop.Value.ToObject<Vector3Like>();
                        break;
                    case "runSpeed":
                        existing.runSpeed = prop.Value.ToObject<float>();
                        break;
                    case "cloneData":
                        existing.soulData = prop.Value.ToObject<string[]>();
                        break;
                    case "bloobColour":
                        existing.bloobColour = prop.Value.ToObject<ColourLike>();
                        break;
                    case "skillData":
                        existing.skillData = prop.Value.ToObject<Dictionary<string, SkillTupleDto>>()
                            ?.ToDictionary(kvp => kvp.Key, kvp => (kvp.Value.Item1, kvp.Value.Item2))
                            ?? [];
                        break;
                    case "skillExperienceData":
                        existing.skillExperienceData = prop.Value.ToObject<Dictionary<string, double>>() ?? [];
                        break;
                    case "name":
                        existing.name = prop.Value.ToObject<string>();
                        break;
                    case "steamId":
                        existing.steamId = prop.Value.ToObject<string>();
                        break;
                    case "activeHatIndex":
                        existing.activeHatIndex = prop.Value.ToObject<int>();
                        break;
                    case "activeWingIndex":
                        existing.activeWingIndex = prop.Value.ToObject<int>();
                        break;
                    case "clanId":
                        existing.clanId = prop.Value.ToObject<string>();
                        break;
                    case "clanName":
                        existing.clanName = prop.Value.ToObject<string>();
                        break;
                    case "clanTag":
                        existing.clanTag = prop.Value.ToObject<string>();
                        break;
                    case "isTurboSave":
                        existing.isTurboSave = prop.Value.ToObject<bool>();
                        break;
                    case "bossKillData":
                        existing.bossKillData = prop.Value.ToObject<Dictionary<string, long>>() ?? [];
                        break;
                }
            }

            Players[resolvedSteamId] = existing;
        }

        public async void OnActiveSceneChanged(Scene current, Scene next)
        {
            if (next.name != "GameCloud")
                return;

            await Task.Delay(TimeSpan.FromSeconds(7));

            if (!SteamClient.IsValid)
            {
                await Task.Delay(TimeSpan.FromSeconds(5));
                OnActiveSceneChanged(current, next);
                return;
            }

            isReady = true;
            localPlayerCache.Clear();
            CloneCustomizationCache.RefreshFromLocalPlayer();
            ChatSystem.Create(Config);
            new GameObject("HoverUIManager").AddComponent<HoverUIManager>();
            new GameObject("HoverDetector").AddComponent<MultiplayerHoverDetector>();
            new GameObject("MultiplayerContextMenu").AddComponent<MultiplayerContextMenu>();
            PlayerProfileUi.Create();
            ClanProfileUi.Create();
            ModUpdateManager.Create(Config, Info.Metadata.Version.ToString(), Info.Location);

            TryResolveLocalPlayer(forceRefresh: true);
            RefreshSkillSnapshot(forceDirty: true);
            RefreshLocalCustomizationCache();
            RefreshLocalStateFlags();
            ForceSendFullState();

            ghostPlayerCoroutine ??= StartCoroutine(UpdateGhostPlayers());
            skillRefreshCoroutine ??= StartCoroutine(RefreshSkillDataEnumerator());
            positionCoroutine ??= StartCoroutine(GetPositionEnumerator());
            presenceCoroutine ??= StartCoroutine(SendPresenceEnumerator());

            AddsettingOptions();

            if (atMm)
                atMm = false;
        }

        private bool TryResolveLocalPlayer(bool forceRefresh = false)
        {
            return localPlayerCache.TryResolve(forceRefresh);
        }

        private void RefreshSkillSnapshot(bool forceDirty)
        {
            if (!TryResolveLocalPlayer())
                return;

            if (skillDataCache.RefreshFromPlayer(localPlayerCache.PlayerRoot))
                forceDirty = true;

            if (forceDirty)
                skillDataCache.MarkDirty();
        }

        private void RefreshLocalStateFlags()
        {
            cachedIsTurboSave = ResolveTurboSaveFlag();
        }

        private bool ResolveTurboSaveFlag()
        {
            return cachedIsTurboSave;
        }

        public void SetLocalTurboSaveState(bool isTurboSave)
        {
            if (cachedIsTurboSave == isTurboSave)
                return;

            cachedIsTurboSave = isTurboSave;
            SendStateDelta(BuildPresencePayload(), force: true);
        }

        public Dictionary<string, (int level, int prestige)> GetCachedSkillData()
        {
            return skillDataCache.Clone();
        }

        public void HandleClientDisconnect(string steamId, string name = null)
        {
            var resolvedSteamId = steamId;

            lock (Players)
            {
                if (string.IsNullOrWhiteSpace(resolvedSteamId) && !string.IsNullOrWhiteSpace(name))
                {
                    var byName = Players.Values.FirstOrDefault(p =>
                        p != null &&
                        !string.IsNullOrWhiteSpace(p.name) &&
                        p.name.Equals(name, StringComparison.OrdinalIgnoreCase));

                    if (byName != null)
                        resolvedSteamId = byName.steamId;
                }
            }

            if (string.IsNullOrWhiteSpace(resolvedSteamId))
                return;

            CloneManager.RemoveCloneBySteamId(resolvedSteamId);

            lock (Players)
            {
                Players.Remove(resolvedSteamId);
            }
        }

        public static PlayerData FindPlayerByName(string playerName)
        {
            lock (Players)
            {
                return Players.Values.FirstOrDefault(p =>
                    p != null &&
                    !string.IsNullOrWhiteSpace(p.name) &&
                    p.name.Equals(playerName, StringComparison.OrdinalIgnoreCase));
            }
        }

        public static string GetPlayerNameFromSteamId(string steamId, string fallback = null)
        {
            if (string.IsNullOrWhiteSpace(steamId))
                return fallback ?? "Unknown Player";

            if (SteamClient.IsValid && SteamClient.SteamId.ToString() == steamId)
                return SteamClient.Name;

            lock (Players)
            {
                if (Players.TryGetValue(steamId, out var pd) && pd != null && !string.IsNullOrWhiteSpace(pd.name))
                    return pd.name;
            }

            return fallback ?? steamId;
        }

        public static string GetSteamIdFromPlayerName(string playerName)
        {
            if (string.IsNullOrWhiteSpace(playerName))
                return null;

            lock (Players)
            {
                var pd = Players.Values.FirstOrDefault(p =>
                    p != null &&
                    !string.IsNullOrWhiteSpace(p.name) &&
                    p.name.Equals(playerName, StringComparison.OrdinalIgnoreCase));

                return pd?.steamId;
            }
        }

        public static List<PlayerData> FindPlayersByPartialName(string partialName)
        {
            if (string.IsNullOrWhiteSpace(partialName))
                return [];

            var search = partialName.Trim();

            lock (Players)
            {
                return [.. Players.Values
                    .Where(p =>
                        p != null &&
                        !string.IsNullOrWhiteSpace(p.name) &&
                        p.name.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0)
                    .OrderBy(p => p.name.Length)
                    .ThenBy(p => p.name, StringComparer.OrdinalIgnoreCase)];
            }
        }

        public void ApplyLocalNametag(GameObject player)
        {
            var existing = player.transform.Find("LocalNamePlate");
            if (existing != null)
                return;

            var namePlate = new GameObject("LocalNamePlate");
            namePlate.transform.SetParent(player.transform, false);
            namePlate.transform.localPosition = new Vector3(0, 1.125f, 0);

            var nameText = namePlate.AddComponent<TextMeshPro>();
            nameText.text = SteamClient.Name;
            nameText.fontSize = 6;
            nameText.alignment = TextAlignmentOptions.Center;
            nameText.color = Color.white;

            var mr = nameText.GetComponent<MeshRenderer>();
            mr.sortingLayerName = "UI";
        }

        private IEnumerator RefreshSkillDataEnumerator()
        {
            while (true)
            {
                if (!isReady)
                {
                    Debug.Log("Game not ready, retrying skill refresh in 15 seconds");
                    yield return new WaitForSecondsRealtime(15f);
                    continue;
                }

                RefreshSkillSnapshot(forceDirty: false);
                if (skillDataCache.IsDirty || lastPayload == null)
                    SendStateDelta(BuildSkillPayload(), force: skillDataCache.IsDirty);

                yield return new WaitForSecondsRealtime(SkillRefreshIntervalSeconds);
            }
        }

        private IEnumerator UpdateGhostPlayers()
        {
            while (true)
            {
                if (!isReady)
                {
                    Debug.Log("Game not ready, retrying ghost player update in 15 seconds");
                    yield return new WaitForSecondsRealtime(15f);
                    continue;
                }

                if (!TryResolveLocalPlayer())
                {
                    Debug.LogWarning("BloobCharacter not found.");
                    yield return new WaitForSecondsRealtime(5f);
                    continue;
                }

                if (!nameTagCreated)
                {
                    ApplyLocalNametag(localPlayerCache.PlayerRoot);
                    nameTagCreated = true;
                }

                lock (Players)
                {
                    foreach (var kvp in Players)
                    {
                        var pd = kvp.Value;
                        if (pd == null || string.IsNullOrWhiteSpace(pd.steamId))
                            continue;

                        if (pd.steamId == steamIdCache)
                            continue;

                        CloneManager.UpdateOrCreateClone(localPlayerCache.PlayerRoot, pd);
                    }
                }

                yield return new WaitForSecondsRealtime(GhostRefreshIntervalSeconds);
            }
        }

        private IEnumerator GetPositionEnumerator()
        {
            while (true)
            {
                if (!isReady)
                {
                    Debug.Log("Game not ready, retrying position coroutine in 15 seconds");
                    yield return new WaitForSecondsRealtime(15f);
                    continue;
                }

                if (!TryResolveLocalPlayer())
                {
                    yield return new WaitForSecondsRealtime(PositionSendIntervalSeconds);
                    continue;
                }

                SendStateDelta(BuildMovementPayload());
                yield return new WaitForSecondsRealtime(PositionSendIntervalSeconds);
            }
        }

        private IEnumerator SendPresenceEnumerator()
        {
            while (true)
            {
                if (!isReady)
                {
                    yield return new WaitForSecondsRealtime(15f);
                    continue;
                }

                if (!TryResolveLocalPlayer())
                {
                    yield return new WaitForSecondsRealtime(PresenceSendIntervalSeconds);
                    continue;
                }

                RefreshLocalCustomizationCache();
                RefreshLocalStateFlags();
                SendStateDelta(BuildPresencePayload());
                yield return new WaitForSecondsRealtime(PresenceSendIntervalSeconds);
            }
        }

        private JObject BuildMovementPayload()
        {
            var position = localPlayerCache.PlayerTransform != null ? localPlayerCache.PlayerTransform.position : Vector3.zero;
            return new JObject
            {
                ["currentPosition"] = JObject.FromObject(new Vector3Like
                {
                    x = (float)Math.Round(position.x, 2),
                    y = (float)Math.Round(position.y, 2),
                    z = (float)Math.Round(position.z, 2)
                }),
                ["runSpeed"] = localPlayerCache.GetRunSpeed()
            };
        }

        private JObject BuildPresencePayload()
        {
            var color = localPlayerCache.PlayerSpriteRenderer != null
                ? localPlayerCache.PlayerSpriteRenderer.color
                : Color.white;

            return new JObject
            {
                ["bloobColour"] = JObject.FromObject(new ColourLike
                {
                    a = color.a,
                    b = color.b,
                    g = color.g,
                    r = color.r
                }),
                ["activeHatIndex"] = cachedActiveHatIndex,
                ["activeWingIndex"] = cachedActiveWingIndex,
                ["clanId"] = ChatSystem.Instance?.GetCurrentClanState()?.clanId,
                ["clanName"] = ChatSystem.Instance?.GetCurrentClanState()?.name,
                ["clanTag"] = ChatSystem.Instance?.GetCurrentClanState()?.tag,
                ["isTurboSave"] = cachedIsTurboSave
            };
        }

        private JObject BuildSkillPayload()
        {
            return new JObject
            {
                ["skillData"] = JObject.FromObject(skillDataCache.GetDtoSnapshot()),
                ["skillExperienceData"] = JObject.FromObject(skillDataCache.GetExperienceSnapshot()),
                ["bossKillData"] = JObject.FromObject(bossKillCache.GetSnapshot())
            };
        }

        private JObject BuildFullPayload()
        {
            var payload = new JObject
            {
                ["name"] = SteamClient.IsValid ? SteamClient.Name : nameCache ?? string.Empty,
                ["steamId"] = SteamClient.IsValid ? SteamClient.SteamId.ToString() : steamIdCache ?? string.Empty,
                ["isDisconnecting"] = false,
                ["cloneData"] = new JArray()
            };

            foreach (var property in BuildMovementPayload().Properties())
                payload[property.Name] = property.Value.DeepClone();
            foreach (var property in BuildPresencePayload().Properties())
                payload[property.Name] = property.Value.DeepClone();
            foreach (var property in BuildSkillPayload().Properties())
                payload[property.Name] = property.Value.DeepClone();

            return payload;
        }

        private void ForceSendFullState()
        {
            if (!SteamClient.IsValid)
                return;

            var payload = BuildFullPayload();
            if (ws != null && ws.IsAlive)
                ws.Send(payload.ToString(Formatting.None));

            lastPayload = payload;
            skillDataCache.ClearDirty();
            bossKillCache.ClearDirty();
        }

        private void SendStateDelta(JObject partialPayload, bool force = false)
        {
            if (partialPayload == null || !SteamClient.IsValid)
                return;

            partialPayload["name"] = SteamClient.Name;
            partialPayload["steamId"] = SteamClient.SteamId.ToString();

            var diffPayload = new JObject
            {
                ["name"] = partialPayload["name"],
                ["steamId"] = partialPayload["steamId"]
            };

            foreach (var property in partialPayload.Properties())
            {
                if (property.Name == "name" || property.Name == "steamId")
                    continue;

                if (force || lastPayload == null || !JToken.DeepEquals(property.Value, lastPayload[property.Name]))
                    diffPayload[property.Name] = property.Value;
            }

            if (diffPayload.Properties().Count() <= 2)
                return;

            if (ws != null && ws.IsAlive)
                ws.Send(diffPayload.ToString(Formatting.None));

            if (lastPayload == null)
                lastPayload = BuildFullPayload();

            foreach (var property in diffPayload.Properties())
            {
                if (property.Name == "name" || property.Name == "steamId")
                    continue;

                lastPayload[property.Name] = property.Value.DeepClone();
            }

            skillDataCache.ClearDirty();
            bossKillCache.ClearDirty();
        }

        private void BeginConnectionMonitor()
        {
            if (isMonitoringConnection)
                return;

            connectionMonitorCoroutine = StartCoroutine(ConnectionMonitorCoroutine());
        }

        private IEnumerator ConnectionMonitorCoroutine()
        {
            if (isMonitoringConnection)
                yield break;

            isMonitoringConnection = true;
            Debug.LogWarning("[WS] Connection monitor started.");

            yield return TryReconnectBurst(5, 5f, "Burst 1/4");
            if (IsSocketHealthy())
            {
                OnReconnectSuccess();
                yield break;
            }

            Debug.LogWarning("[WS] Reconnect burst 1 failed. Waiting 30 seconds before next burst.");
            yield return new WaitForSecondsRealtime(30f);

            yield return TryReconnectBurst(5, 5f, "Burst 2/4");
            if (IsSocketHealthy())
            {
                OnReconnectSuccess();
                yield break;
            }

            Debug.LogWarning("[WS] Reconnect burst 2 failed. Waiting 60 seconds before next burst.");
            yield return new WaitForSecondsRealtime(60f);

            yield return TryReconnectBurst(5, 5f, "Burst 3/4");
            if (IsSocketHealthy())
            {
                OnReconnectSuccess();
                yield break;
            }

            Debug.LogWarning("[WS] Reconnect burst 3 failed. Waiting 5 minutes before final burst.");
            yield return new WaitForSecondsRealtime(300f);

            yield return TryReconnectBurst(5, 5f, "Burst 4/4");
            if (IsSocketHealthy())
            {
                OnReconnectSuccess();
                yield break;
            }

            Debug.LogError("[WS] Unable to reconnect to the multiplayer server after all retry stages.");
            Debug.LogError("[WS] Please restart the game if you believe this is an issue or contact the developer of the mod if this issue persists through multiple restarts/days.");

            isMonitoringConnection = false;
            connectionMonitorCoroutine = null;
        }

        private IEnumerator TryReconnectBurst(int attempts, float delayBetweenAttempts, string label)
        {
            for (var attempt = 1; attempt <= attempts; attempt++)
            {
                if (IsSocketHealthy())
                    yield break;

                Debug.LogWarning($"[WS] {label} reconnect attempt {attempt}/{attempts}...");

                RebuildWebSocket();
                ws.ConnectAsync();

                var timer = 0f;
                while (timer < delayBetweenAttempts)
                {
                    if (IsSocketHealthy())
                        yield break;

                    timer += Time.unscaledDeltaTime;
                    yield return null;
                }
            }
        }

        private bool IsSocketHealthy()
        {
            return isConnected && ws != null && ws.IsAlive;
        }

        private void OnReconnectSuccess()
        {
            Debug.Log("[WS] Reconnected successfully! Reinitializing player data");

            if (lastPayload != null && ws != null && ws.IsAlive)
                ws.Send(lastPayload.ToString(Formatting.None));

            isMonitoringConnection = false;
            connectionMonitorCoroutine = null;
        }

        public void SendChatPacket(string message)
        {
            if (ws == null || !isConnected || !ws.IsAlive || !SteamClient.IsValid)
            {
                ChatSystem.Instance?.ReceiveError("Chat is not connected right now.");
                return;
            }

            var payload = new
            {
                type = "chat",
                name = SteamClient.Name,
                steamId = SteamClient.SteamId.ToString(),
                message
            };

            ws.Send(JsonConvert.SerializeObject(payload));
        }

        public void SendSetTitlePacket(string titleId)
        {
            if (ws == null || !isConnected || !ws.IsAlive || !SteamClient.IsValid)
                return;

            var payload = new
            {
                type = "setTitle",
                steamId = SteamClient.SteamId.ToString(),
                titleId = titleId ?? string.Empty
            };

            ws.Send(JsonConvert.SerializeObject(payload));
        }

        public void RequestClanProfile(string clanId)
        {
            if (string.IsNullOrWhiteSpace(clanId) || ws == null || !isConnected || !ws.IsAlive || !SteamClient.IsValid)
                return;

            var payload = new
            {
                type = "requestClanProfile",
                steamId = SteamClient.SteamId.ToString(),
                clanId
            };

            ws.Send(JsonConvert.SerializeObject(payload));
        }

        public void SendClanManagementAction(string action, object data)
        {
            if (string.IsNullOrWhiteSpace(action) || ws == null || !isConnected || !ws.IsAlive || !SteamClient.IsValid)
                return;

            var payload = JObject.FromObject(data ?? new { });
            payload["type"] = "clanManage";
            payload["action"] = action;
            payload["steamId"] = SteamClient.SteamId.ToString();
            ws.Send(payload.ToString(Formatting.None));
        }

        public void ReportBossKill(string bossId, int count = 1)
        {
            bossKillCache.ReportKill(bossId, count);
        }

        public static void ReportLocalBossKill(string bossId, int count = 1)
        {
            instance?.ReportBossKill(bossId, count);
        }

        private void HandleConfiguration()
        {
            enableLevelPanel = Config.Bind("Visual", "Enable Level Panel?", true,
                "Toggles whether or not the skill level panel is displayed when hovering your mouse over a ghost");
            enableContextMenu = Config.Bind("Visual", "Enable Context Menu", true,
                "Enables a context menu when you right click to display overlapping ghosts");
        }

        private void RefreshLocalCustomizationCache()
        {
            var bloobColourChange = GameObject.FindObjectOfType<BloobColourChange>();
            if (bloobColourChange == null)
                return;

            cachedActiveHatIndex = GetPrivateIntField(bloobColourChange, "activeHatIndex", -1);
            cachedActiveWingIndex = GetPrivateIntField(bloobColourChange, "activeWingIndex", -1);
        }

        private int GetPrivateIntField(Component comp, string fieldName, int fallback = -1)
        {
            if (comp == null)
                return fallback;

            var field = comp.GetType().GetField(
                fieldName,
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.IgnoreCase
            );

            if (field != null && field.FieldType == typeof(int))
                return (int)field.GetValue(comp);

            return fallback;
        }

        public void OnLocalHatChanged(int hatIndex)
        {
            if (cachedActiveHatIndex == hatIndex)
                return;

            cachedActiveHatIndex = hatIndex;
        }

        public void OnLocalWingChanged(int wingIndex)
        {
            if (cachedActiveWingIndex == wingIndex)
                return;

            cachedActiveWingIndex = wingIndex;
        }

        public PlayerData BuildLocalPlayerProfileSnapshot()
        {
            if (!SteamClient.IsValid)
                return null;

            RefreshSkillSnapshot(forceDirty: false);
            RefreshLocalStateFlags();

            return new PlayerData
            {
                name = SteamClient.Name,
                steamId = SteamClient.SteamId.ToString(),
                currentPosition = localPlayerCache.PlayerTransform != null
                    ? new Vector3Like
                    {
                        x = localPlayerCache.PlayerTransform.position.x,
                        y = localPlayerCache.PlayerTransform.position.y,
                        z = localPlayerCache.PlayerTransform.position.z
                    }
                    : null,
                runSpeed = localPlayerCache.GetRunSpeed(),
                activeHatIndex = cachedActiveHatIndex,
                activeWingIndex = cachedActiveWingIndex,
                bloobColour = localPlayerCache.PlayerSpriteRenderer != null
                    ? new ColourLike
                    {
                        a = localPlayerCache.PlayerSpriteRenderer.color.a,
                        r = localPlayerCache.PlayerSpriteRenderer.color.r,
                        g = localPlayerCache.PlayerSpriteRenderer.color.g,
                        b = localPlayerCache.PlayerSpriteRenderer.color.b
                    }
                    : null,
                skillData = skillDataCache.Clone(),
                skillExperienceData = skillDataCache.GetExperienceSnapshot(),
                bossKillData = bossKillCache.GetSnapshot(),
                clanId = ChatSystem.Instance?.GetCurrentClanState()?.clanId,
                clanName = ChatSystem.Instance?.GetCurrentClanState()?.name,
                clanTag = ChatSystem.Instance?.GetCurrentClanState()?.tag,
                isTurboSave = cachedIsTurboSave,
                soulData = Array.Empty<string>()
            };
        }

        public void MainMenuClicked()
        {
            if (ghostPlayerCoroutine != null)
            {
                StopCoroutine(ghostPlayerCoroutine);
                ghostPlayerCoroutine = null;
            }

            if (positionCoroutine != null)
            {
                StopCoroutine(positionCoroutine);
                positionCoroutine = null;
            }

            if (presenceCoroutine != null)
            {
                StopCoroutine(presenceCoroutine);
                presenceCoroutine = null;
            }

            if (skillRefreshCoroutine != null)
            {
                StopCoroutine(skillRefreshCoroutine);
                skillRefreshCoroutine = null;
            }

            isReady = false;
            localPlayerCache.Clear();
            skillDataCache.Clear();
            bossKillCache.Clear();

            var hoverUi = GameObject.Find("HoverUIManager");
            var hoverDetector = GameObject.Find("HoverDetector");
            var contextMenu = GameObject.Find("MultiplayerContextMenu");
            var profileUi = GameObject.Find("PlayerProfileUi");
            var clanProfileUi = GameObject.Find("ClanProfileUi");

            if (hoverUi)
                Destroy(hoverUi);

            if (hoverDetector)
                Destroy(hoverDetector);

            if (contextMenu)
                Destroy(contextMenu);

            if (profileUi)
                Destroy(profileUi);

            if (clanProfileUi)
                Destroy(clanProfileUi);

            if (ChatSystem.Instance != null)
                Destroy(ChatSystem.Instance.gameObject);

            atMm = true;
            var payload = new
            {
                type = "RemoveClient",
                reason = "ClientMainMenu",
                name = nameCache,
                steamId = steamIdCache
            };

            if (ws != null && ws.IsAlive)
                ws.Send(JsonConvert.SerializeObject(payload));

            nameTagCreated = false;
        }

        public void AddsettingOptions()
        {
            var canvas = GameObject.Find("GameCanvas");
            if (canvas == null)
            {
                Debug.LogError("GameCanvas not found!");
                return;
            }

            Transform playerMenuBar = null;
            foreach (Transform child in canvas.transform)
            {
                if (child.name == "Player Menu/Bar")
                {
                    playerMenuBar = child;
                    break;
                }
            }

            if (playerMenuBar == null)
            {
                Debug.LogError("Player Menu/Bar not found directly under GameCanvas");
                return;
            }

            var menuBar = playerMenuBar.Find("Menu Bar");
            if (menuBar == null)
            {
                Debug.LogError("Menu Bar not found under Player Menu/Bar");
                return;
            }

            var settingsUi = menuBar.Find("Settings Ui");
            if (settingsUi == null)
            {
                Debug.LogError("Settings Ui not found under Menu Bar");
                return;
            }

            var mainMenu = settingsUi.Find("Main Menu")?.gameObject;
            var button = mainMenu?.GetComponent<Button>();
            button?.onClick.AddListener(MainMenuClicked);

            var soundOff = settingsUi.Find("Sound Off");
            if (soundOff == null)
            {
                Debug.LogError("Sound Off not found under Settings Ui");
                return;
            }

            AddButton(settingsUi, "Enable Level Panel", enableLevelPanel, new Vector2(150, 40));
            AddButton(settingsUi, "Enable Context Menu", enableContextMenu, new Vector2(150, 55));
        }

        private void AddButton(Transform parent, string labelText, ConfigEntry<bool> configEntry, Vector2 position)
        {
            var buttonObj = new GameObject(labelText + " Button");
            buttonObj.transform.SetParent(parent, false);

            var rt = buttonObj.AddComponent<RectTransform>();
            rt.anchoredPosition = position;
            rt.sizeDelta = Vector2.zero;

            var image = buttonObj.AddComponent<Image>();
            image.color = new Color(0.2f, 0.2f, 0.2f, 0.7f);

            var button = buttonObj.AddComponent<Button>();

            var fitter = buttonObj.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var labelObj = new GameObject("Label");
            labelObj.transform.SetParent(buttonObj.transform, false);

            var label = labelObj.AddComponent<TextMeshProUGUI>();
            label.text = labelText + ": " + (configEntry.Value ? "ON" : "OFF");
            label.fontSize = 18;
            label.alignment = TextAlignmentOptions.Center;
            label.color = Color.white;

            var labelRT = label.GetComponent<RectTransform>();
            labelRT.anchorMin = Vector2.zero;
            labelRT.anchorMax = Vector2.one;
            labelRT.offsetMin = Vector2.zero;
            labelRT.offsetMax = Vector2.zero;

            var layoutElement = labelObj.AddComponent<LayoutElement>();
            layoutElement.preferredWidth = -1;
            layoutElement.preferredHeight = -1;

            var layoutGroup = buttonObj.AddComponent<HorizontalLayoutGroup>();
            layoutGroup.childAlignment = TextAnchor.MiddleCenter;
            layoutGroup.padding = new RectOffset(10, 10, 5, 5);
            layoutGroup.childForceExpandWidth = false;
            layoutGroup.childForceExpandHeight = false;

            button.onClick.AddListener(() =>
            {
                configEntry.Value = !configEntry.Value;
                label.text = labelText + ": " + (configEntry.Value ? "ON" : "OFF");
            });
        }
    }
}
