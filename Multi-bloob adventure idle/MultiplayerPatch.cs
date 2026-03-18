using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using Multi_bloob_adventure_idle;
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
    [BepInPlugin("com.cannabis.multibloobidle", "Multiblood Adventure Idle", "1.1.0")]
    public class MultiplayerPatchPlugin : BaseUnityPlugin
    {
        private readonly Queue<string> messageQueue = new Queue<string>();
        private readonly object queueLock = new object();
        public static Dictionary<string, (int level, int prestige)> playerSkills = new Dictionary<string, (int level, int prestige)>();
        private Coroutine skillRefreshCoroutine;
        private Dictionary<string, (int level, int prestige)> cachedSkillData = new();
        private bool skillDataDirty;

        private WebSocket ws;
        private bool isConnected = false;
        private bool isMonitoringConnection;
        private Coroutine connectionMonitorCoroutine;

        private Coroutine positionCoroutine;
        private Coroutine ghostPlayerCoroutine;
        //private Coroutine playerLevelCoroutine;
        public static bool isReady = false;
        private bool nameTagCreated;
        public static ConfigEntry<bool> enableLevelPanel;
        public static ConfigEntry<bool> enableContextMenu;
        private string nameCache;
        private string steamIdCache;
        private bool atMm;
        private JObject lastPayload = null;
        public static MultiplayerPatchPlugin instance;

        public static readonly Dictionary<string, PlayerData> Players = new Dictionary<string, PlayerData>();

        private const string WebSocketUrl = "ws://172.93.111.163:42069";

        private void Awake()
        {
            //Debug.Log("Starting to wake up");
            // WebServer Connection
            InitializeWebSocket();
            ws.ConnectAsync();

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

            if (ws == null) { Debug.Log("WS NULL"); }
            //Debug.Log("Fully woken up");
            SceneManager.activeSceneChanged += OnActiveSceneChanged;
            //StartCoroutine(ConnectionMonitorCoroutine());
            HandleConfiguration();
            instance ??= this;
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
                        // Ignore close errors during rebuild
                    }
                }
            }
            catch
            {
                // Ignore cleanup errors during rebuild
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
                // Ignore close exceptions on quit
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

            lock (queueLock)
            {
                while (messageQueue.Count > 0)
                {
                    string json = messageQueue.Dequeue();
                    //Debug.Log("Got message from WS: " + json);
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

                    string type = msg["type"]?.ToString();
                    if (type == null) continue;

                    lock (Players)
                    {
                        switch (type)
                        {
                            case "initialState":
                                {
                                    var dataArray = msg["data"] as JArray;
                                    if (dataArray == null)
                                        break;

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
                                break;

                            case "newPlayer":
                                {
                                    var dataToken = msg["data"];
                                    if (dataToken == null)
                                        break;

                                    var pd = dataToken.ToObject<PlayerData>();
                                    if (pd == null || string.IsNullOrWhiteSpace(pd.steamId))
                                        break;

                                    if (pd.steamId != steamIdCache)
                                        Players[pd.steamId] = pd;
                                }
                                break;

                            case "update":
                                {
                                    string steamId = msg["steamId"]?.ToString();
                                    string name = msg["name"]?.ToString();

                                    // Prefer SteamID. Fallback to name only if older server packet shape still exists.
                                    PlayerData existing = null;
                                    string resolvedSteamId = steamId;

                                    if (!string.IsNullOrWhiteSpace(steamId) && Players.TryGetValue(steamId, out existing))
                                    {
                                        // already found
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
                                        break;

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
                                                    ?? new Dictionary<string, (int level, int prestige)>();
                                                break;
                                            case "name":
                                                existing.name = prop.Value.ToObject<string>();
                                                break;
                                            case "steamId":
                                                existing.steamId = prop.Value.ToObject<string>();
                                                break;
                                        }
                                    }

                                    Players[resolvedSteamId] = existing;
                                }
                                break;

                            case "clientRemoved":
                            case "disconnect":
                                {
                                    string steamId = msg["steamId"]?.ToString();
                                    string name = msg["name"]?.ToString();
                                    HandleClientDisconnect(steamId, name);
                                }
                                break;

                            case "chatHistory":
                                {
                                    ChatSystem.Instance?.ReceiveHistory(msg["messages"] as JArray);
                                }
                                break;

                            case "chatMessage":
                                {
                                    ChatSystem.Instance?.ReceiveChatMessage(msg);
                                }
                                break;

                            case "serverBroadcast":
                                {
                                    ChatSystem.Instance?.ReceiveBroadcast(msg);
                                }
                                break;

                            case "chatError":
                                {
                                    ChatSystem.Instance?.ReceiveError(msg["error"]?.ToString() ?? "Unknown chat error.");
                                }
                                break;
                            case "titleState":
                                {
                                    ChatSystem.Instance?.ReceiveTitleState(msg);
                                }
                                break;
                        }
                    }
                }
            }
        }

        public async void OnActiveSceneChanged(Scene current, Scene next)
        {
            //Scene current doesn't ever instantiate, only use Scene next
            //Debug.Log($"Next Scene {next.name}");
            if (next.name != "GameCloud") return;

            // We have to delay here because the Scene change to GameCloud happens quicker than everything can get instantiated
            await Task.Delay(TimeSpan.FromSeconds(7));

            // We check if Steam has initialized, if it hasn't in time we wait 5 seconds and try again. Ensuring no issues on timings
            if (!SteamClient.IsValid)
            {
                await Task.Delay(TimeSpan.FromSeconds(5));
                OnActiveSceneChanged(current, next);
                return;
            }

            isReady = true;
            new GameObject("HoverUIManager").AddComponent<HoverUIManager>();
            new GameObject("HoverDetector").AddComponent<MultiplayerHoverDetector>();
            //BUG Context menu spams errors, currently non-functional.
            //new GameObject("Multiplayer Context Menu").AddComponent<MultiplayerContextMenu>();
            ChatSystem.Create(Config);
            ghostPlayerCoroutine ??= StartCoroutine(UpdateGhostPlayers());
            //playerLevelCoroutine ??= StartCoroutine(UpdatePlayerLevels());
            skillRefreshCoroutine ??= StartCoroutine(RefreshSkillDataEnumerator());
            positionCoroutine ??= StartCoroutine(GetPositionEnumerator());
            AddsettingOptions();

            if (atMm && lastPayload != null && ws != null && ws.IsAlive)
            {
                ws.Send(lastPayload.ToString(Formatting.None));
                atMm = false;
            }
        }

        public void HandleClientDisconnect(string steamId, string name = null)
        {
            string resolvedSteamId = steamId;

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
                return new List<PlayerData>();

            string search = partialName.Trim();

            lock (Players)
            {
                return Players.Values
                    .Where(p =>
                        p != null &&
                        !string.IsNullOrWhiteSpace(p.name) &&
                        p.name.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0)
                    .OrderBy(p => p.name.Length)
                    .ThenBy(p => p.name, StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }
        }

        private bool AreSkillDictionariesEqual(Dictionary<string, (int level, int prestige)> a, Dictionary<string, (int level, int prestige)> b)
        {
            if (ReferenceEquals(a, b))
                return true;

            if (a == null || b == null)
                return false;

            if (a.Count != b.Count)
                return false;

            foreach (var kvp in a)
            {
                if (!b.TryGetValue(kvp.Key, out var other))
                    return false;

                if (kvp.Value.level != other.level || kvp.Value.prestige != other.prestige)
                    return false;
            }

            return true;
        }


        IEnumerator RefreshSkillDataEnumerator()
        {
            while (true)
            {
                if (!isReady)
                {
                    Debug.Log("Game not ready, retrying skill refresh in 15 seconds");
                    yield return new WaitForSecondsRealtime(15f);
                    continue;
                }

                var latestSkillData = UpdatePlayerLevels();

                if (!AreSkillDictionariesEqual(cachedSkillData, latestSkillData))
                {
                    cachedSkillData = latestSkillData;
                    skillDataDirty = true;
                    //Debug.Log("Skill data changed, marking dirty for next payload send.");
                }

                yield return new WaitForSecondsRealtime(30f);
            }
        }


        private Dictionary<string, (int, int)> UpdatePlayerLevels()
        {
            Dictionary<string, string> nameMap = new()
            {
                { "WoodCutting", "Woodcutting" }
            };

            Dictionary<string, (int level, int prestige)> skillData = new();

            GameObject player = GameObject.Find("BloobCharacter");
            if (player == null)
                return skillData;

            foreach (Transform child in player.transform)
            {
                if (child.name is null or "Weapon Point" or "MagicWeapon Point" or "RangeWeapon Point" or "Melee Weapon" or "MeleeWeapon" or "MagicProjectile" or "RangeProjectile" or "wingSlot" or "hatSlot" or "Canvas")
                {
                    continue; // CUNT
                }

                string childName = nameMap.TryGetValue(child.name, out var value) ? value : child.name;

                string skillClassName = childName + "Skill";

                // Skill Class Name edge cases
                if (childName == "SoulBinding")
                    skillClassName = "SoulBinding";
                if (childName == "Homesteading")
                    skillClassName = "HomeSteadingSkill";

                // Try to get the component dynamically
                Component skillComponent = child.GetComponent(skillClassName);
                if (skillComponent == null)
                {
                    //Debug.LogWarning($"No component {skillClassName} found on {childName}");
                    continue;
                }

                // Determine proper field names
                string levelFieldName = childName + "Level";
                string prestigeFieldName = childName.ToLower() + "PrestigeLevel";

                switch (childName)
                {
                    // Additional edge cases for inconsistent field names
                    case "HitPoints":
                        levelFieldName = "HitPointsLevel";
                        prestigeFieldName = "HitPointsPrestigeLevel";
                        break;
                    case "Mining":
                        levelFieldName = "MiningLevel";
                        break;
                    case "WoodCutting":
                        levelFieldName = "woodcuttinglevel";
                        break;
                    case "SoulBinding":
                        levelFieldName = "SoulBindingLevel";
                        prestigeFieldName = "SoulBindingPrestigeLevel";
                        break;
                    case "BowCrafting":
                        prestigeFieldName = "bowCraftingPrestigeLevel";
                        break;
                    case "BeastMastery":
                        prestigeFieldName = "beastMasteryPrestigeLevel";
                        break;
                    case "Thieving":
                        levelFieldName = "ThievingLevel";
                        break;
                    case "Fishing":
                        levelFieldName = "FishingLevel";
                        break;
                }

                int level = GetIntField(skillComponent, levelFieldName);
                int prestige = GetIntField(skillComponent, prestigeFieldName);

                skillData[childName] = (level, prestige);
                //Debug.Log($"[Skill] {childName}: Level={level} Prestige={prestige}");
            }

            return skillData;
            //yield return new WaitForSecondsRealtime(60f);
        }

        int GetIntField(Component comp, string fieldName)
        {
            var type = comp.GetType();
            var field = type.GetField(
                fieldName,
                System.Reflection.BindingFlags.IgnoreCase
                | System.Reflection.BindingFlags.Public
                | System.Reflection.BindingFlags.NonPublic
                | System.Reflection.BindingFlags.Instance
            );

            if (field != null && field.FieldType == typeof(int))
            {
                return (int)field.GetValue(comp);
            }
            else
            {
                Debug.LogWarning($"Field '{fieldName}' not found on {type.Name}");
                return -1;
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

        IEnumerator UpdateGhostPlayers()
        {
            while (true)
            {
                if (!isReady)
                {
                    Debug.Log("Game not ready, retrying ghost player update in 15 seconds");
                    yield return new WaitForSecondsRealtime(15f);
                    continue;
                }

                GameObject original = GameObject.Find("BloobCharacter");
                if (original is null)
                {
                    Debug.LogWarning("BloobCharacter not found.");
                    yield return new WaitForSecondsRealtime(5f);
                    continue;
                }

                if (!nameTagCreated)
                {
                    ApplyLocalNametag(original);
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

                        CloneManager.UpdateOrCreateClone(original, pd);
                    }
                }

                yield return new WaitForSecondsRealtime(1f);
            }
        }

        IEnumerator GetPositionEnumerator()
        {
            while (true)
            {
                if (!isReady)
                {
                    Debug.Log("Game not ready, retrying position coroutine in 15 seconds");
                    yield return new WaitForSecondsRealtime(15);
                    continue;
                }

                GameObject player = GameObject.Find("BloobCharacter");
                if (player == null)
                {
                    yield return new WaitForSecondsRealtime(1f);
                    continue;
                }

                string[] soulData = Array.Empty<string>();

                var payload = new
                {
                    name = SteamClient.Name,
                    steamId = SteamClient.SteamId.ToString(),
                    currentPosition = new
                    {
                        x = Mathf.Round(player.transform.position.x),
                        y = Mathf.Round(player.transform.position.y),
                        z = Mathf.Round(player.transform.position.z)
                    },
                    isDisconnecting = false,
                    skillData = UpdatePlayerLevels().ToDictionary(
                        kvp => kvp.Key,
                        kvp => new SkillTupleDto { Item1 = kvp.Value.Item1, Item2 = kvp.Value.Item2 }),
                    bloobColour = new ColourLike()
                    {
                        //playerGameObject.GetComponent<SpriteRenderer>().color
                        a = player.GetComponent<SpriteRenderer>().color.a,
                        b = player.GetComponent<SpriteRenderer>().color.b,
                        g = player.GetComponent<SpriteRenderer>().color.g,
                        r = player.GetComponent<SpriteRenderer>().color.r
                    },
                    runSpeed = player.GetComponent<CharacterMovement>()?.dexteritySkill.runSpeed ?? 0f,
                    cloneData = soulData
                };

                JObject newPayload = JObject.FromObject(payload);

                var diffPayload = new JObject
                {
                    ["name"] = newPayload["name"],
                    ["steamId"] = newPayload["steamId"]
                };

                foreach (var property in newPayload.Properties())
                {
                    if (property.Name == "name" || property.Name == "steamId")
                        continue;

                    // Skill data is refreshed on a slower coroutine and only sent when marked dirty.
                    if (property.Name == "skillData")
                    {
                        if (lastPayload == null || skillDataDirty)
                        {
                            diffPayload[property.Name] = property.Value;
                        }
                        continue;
                    }

                    if (lastPayload == null || !JToken.DeepEquals(property.Value, lastPayload[property.Name]))
                    {
                        diffPayload[property.Name] = property.Value;
                    }
                }

                if (diffPayload.Properties().Count() > 2)
                {
                    //TODO Send through Binary serialization, decreases payload and faster to de/serialize
                    string jsonPayload = diffPayload.ToString(Formatting.None);
                    //Debug.Log($"Sending data: {jsonPayload} to server");
                    if (ws != null && ws.IsAlive)
                    {
                        ws.Send(jsonPayload);
                    }
                    lastPayload = newPayload;
                    skillDataDirty = false;
                }

                //Debug.Log("Hey shithead");
                yield return new WaitForSecondsRealtime(1f);
            }
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

            // 5 attempts
            yield return TryReconnectBurst(5, 5f, "Burst 1/4");
            if (IsSocketHealthy())
            {
                OnReconnectSuccess();
                yield break;
            }

            // wait 30 seconds
            Debug.LogWarning("[WS] Reconnect burst 1 failed. Waiting 30 seconds before next burst.");
            yield return new WaitForSecondsRealtime(30f);

            // 5 attempts
            yield return TryReconnectBurst(5, 5f, "Burst 2/4");
            if (IsSocketHealthy())
            {
                OnReconnectSuccess();
                yield break;
            }

            // wait 60 seconds
            Debug.LogWarning("[WS] Reconnect burst 2 failed. Waiting 60 seconds before next burst.");
            yield return new WaitForSecondsRealtime(60f);

            // 5 attempts
            yield return TryReconnectBurst(5, 5f, "Burst 3/4");
            if (IsSocketHealthy())
            {
                OnReconnectSuccess();
                yield break;
            }

            // wait 5 minutes
            Debug.LogWarning("[WS] Reconnect burst 3 failed. Waiting 5 minutes before final burst.");
            yield return new WaitForSecondsRealtime(300f);

            // 5 attempts
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
            for (int attempt = 1; attempt <= attempts; attempt++)
            {
                if (IsSocketHealthy())
                    yield break;

                Debug.LogWarning($"[WS] {label} reconnect attempt {attempt}/{attempts}...");

                RebuildWebSocket();
                ws.ConnectAsync();

                float timer = 0f;
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
            {
                ws.Send(lastPayload.ToString(Formatting.None));
            }

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
                message = message
            };

            string json = JsonConvert.SerializeObject(payload);
            ws.Send(json);
        }

        public void SendSetTitlePacket(string titleId)
        {
            if (ws == null || !isConnected || !ws.IsAlive || !SteamClient.IsValid)
                return;

            var payload = new
            {
                type = "setTitle",
                steamId = SteamClient.SteamId.ToString(),
                titleId = titleId ?? ""
            };

            ws.Send(JsonConvert.SerializeObject(payload));
        }

        private void HandleConfiguration()
        {
            enableLevelPanel = Config.Bind("Visual", "Enable Level Panel?", true,
                "Toggles whether or not the skill level panel is displayed when hovering your mouse over a ghost");
            enableContextMenu = Config.Bind("Visual", "Enable Context Menu", true,
                "Enables a context menu when you right click to display overlapping ghosts");
        }

        public void MainMenuClicked()
        {
            ghostPlayerCoroutine = null;
            positionCoroutine = null;
            isReady = false;

            var go = GameObject.Find("HoverUIManager");
            var go1 = GameObject.Find("HoverDetector");
            //var go2 = GameObject.Find("Multiplayer Context Menu");

            if (go)
            {
                Destroy(go);
            }

            if (go1)
            {
                Destroy(go1);
            }

            //if (go2)
            //{
            //    Destroy(go2);
            //}

            if (ChatSystem.Instance != null)
            {
                Destroy(ChatSystem.Instance.gameObject);
            }

            atMm = true;
            var payload = new
            {
                type = "RemoveClient",
                reason = "ClientMainMenu",
                name = nameCache,
                steamId = steamIdCache
            };
            var json = JsonConvert.SerializeObject(payload);

            if (ws != null && ws.IsAlive)
            {
                ws.Send(json);
            }

            nameTagCreated = false;
        }

        public void AddsettingOptions()
        {
            GameObject canvas = GameObject.Find("GameCanvas");
            if (canvas == null)
            {
                Debug.LogError("GameCanvas not found!");
                return;
            }

            // Find literal GameObject named "Player Menu/Bar"
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

            // Find "Menu Bar" under "Player Menu/Bar"
            Transform menuBar = playerMenuBar.Find("Menu Bar");
            if (menuBar == null)
            {
                Debug.LogError("Menu Bar not found under Player Menu/Bar");
                return;
            }

            // Find "Settings Ui" under "Menu Bar"
            Transform settingsUi = menuBar.Find("Settings Ui");
            if (settingsUi == null)
            {
                Debug.LogError("Settings Ui not found under Menu Bar");
                return;
            }

            Debug.Log("Settings Ui found!");

            var mainMenu = settingsUi.Find("Main Menu")?.gameObject;
            var button = mainMenu?.GetComponent<Button>();
            if (button != null)
            {
                button.onClick.AddListener(MainMenuClicked);
                Debug.Log("Found Main Menu button, added listener for going to main menu.");
            }

            // Look for "Sound Off" under "Settings Ui"
            Transform soundOff = settingsUi.Find("Sound Off");
            if (soundOff == null)
            {
                Debug.LogError("Sound Off not found under Settings Ui");
                return;
            }

            Debug.Log("Sound Off Found");

            // Add toggles
            AddButton(settingsUi, "Enable Level Panel", enableLevelPanel, new Vector2(150, 40));
            //TODO Setup handling of creating/destroying of context menu behaviour on toggle to clean up properly
            AddButton(settingsUi, "Enable Context Menu", enableContextMenu, new Vector2(150, 55));
        }

        private void AddButton(Transform parent, string labelText, ConfigEntry<bool> configEntry, Vector2 position)
        {
            // Create Button 
            GameObject buttonObj = new GameObject(labelText + " Button");
            buttonObj.transform.SetParent(parent, false);

            RectTransform rt = buttonObj.AddComponent<RectTransform>();

            // Set position
            rt.anchoredPosition = position;
            rt.sizeDelta = Vector2.zero;

            Image image = buttonObj.AddComponent<Image>();
            image.color = new Color(0.2f, 0.2f, 0.2f, 0.7f);

            // Button
            Button button = buttonObj.AddComponent<Button>();

            // ContentSizeFitter to make button resize to fit content
            ContentSizeFitter fitter = buttonObj.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // Create Label child object
            GameObject labelObj = new GameObject("Label");
            labelObj.transform.SetParent(buttonObj.transform, false);

            TextMeshProUGUI label = labelObj.AddComponent<TextMeshProUGUI>();
            label.text = labelText + ": " + (configEntry.Value ? "ON" : "OFF");
            label.fontSize = 18;
            label.alignment = TextAlignmentOptions.Center;
            label.color = Color.white;

            // Stretch label to fill button
            RectTransform labelRT = label.GetComponent<RectTransform>();
            labelRT.anchorMin = Vector2.zero;
            labelRT.anchorMax = Vector2.one;
            labelRT.offsetMin = Vector2.zero;
            labelRT.offsetMax = Vector2.zero;

            LayoutElement layoutElement = labelObj.AddComponent<LayoutElement>();
            layoutElement.preferredWidth = -1;
            layoutElement.preferredHeight = -1;

            var layoutGroup = buttonObj.AddComponent<HorizontalLayoutGroup>();
            layoutGroup.childAlignment = TextAnchor.MiddleCenter;
            layoutGroup.padding = new RectOffset(10, 10, 5, 5);
            layoutGroup.childForceExpandWidth = false;
            layoutGroup.childForceExpandHeight = false;

            // Update text and button on click
            button.onClick.AddListener(() =>
            {
                configEntry.Value = !configEntry.Value;
                Debug.Log($"[{labelText}] toggled to {configEntry.Value}");
                label.text = labelText + ": " + (configEntry.Value ? "ON" : "OFF");
            });
        }
    }
}

public class PlayerData
{
    public string name;
    public string steamId;
    public bool isDisconnecting;
    public Vector3Like currentPosition;
    public float runSpeed;
    public string hatName;
    public string wingName;
    public ColourLike bloobColour;

    // Stored locally as tuple for convenience. JSON round-tripping is done through SkillTupleDto when needed.
    [JsonIgnore]
    public Dictionary<string, (int level, int prestige)> skillData;

    [JsonProperty("skillData")]
    private Dictionary<string, SkillTupleDto> SkillDataSurrogate
    {
        get
        {
            return skillData?.ToDictionary(
                kvp => kvp.Key,
                kvp => new SkillTupleDto { Item1 = kvp.Value.level, Item2 = kvp.Value.prestige });
        }
        set
        {
            skillData = value?.ToDictionary(
                kvp => kvp.Key,
                kvp => (kvp.Value.Item1, kvp.Value.Item2))
                ?? new Dictionary<string, (int level, int prestige)>();
        }
    }

    public string[] soulData;
}

public class SkillTupleDto
{
    public int Item1;
    public int Item2;
}

public class Vector3Like
{
    public float x;
    public float y;
    public float z;

    public Vector3 ToVector3() => new Vector3(x, y, z);
    public Vector2 ToVector2() => new Vector2(x, y);
}

public class ColourLike
{
    public float a;
    public float r;
    public float g;
    public float b;

    public Color ToColor() => new Color(r, g, b, a);
}


[HarmonyPatch(typeof(OfflineProgression), "LateUpdate")]
public class OfflineProgressionLateUpdatePatch
{
    static bool Prefix()
    {
        if (ChatSystem.ShouldBlockKeyboardInput)
            return false;

        return true;
    }
}

[HarmonyPatch(typeof(MenuBar), "LateUpdate")]
public class MenuBarLateUpdatePatch
{
    static bool Prefix()
    {
        if (ChatSystem.ShouldBlockKeyboardInput)
            return false;

        return true;
    }
}

[HarmonyPatch(typeof(CameraMovement), "LateUpdate")]
public class CameraMovementLateUpdatePatch
{
    static bool Prefix()
    {
        if (ChatSystem.ShouldBlockKeyboardInput)
            return false;

        return true;
    }
}

[HarmonyPatch(typeof(MapToggle), "LateUpdate")]
public class MapToggleLateUpdatePatch
{
    static bool Prefix()
    {
        if (ChatSystem.ShouldBlockKeyboardInput)
            return false;

        return true;
    }
}

[HarmonyPatch(typeof(PrestigeManager), "LateUpdate")]
public class PrestigeManagerLateUpdatePatch
{
    static bool Prefix()
    {
        if (ChatSystem.ShouldBlockKeyboardInput)
            return false;

        return true;
    }
}

[HarmonyPatch(typeof(BuildingManager), "LateUpdate")]
public class BuildingManagerLateUpdatePatch
{
    static bool Prefix()
    {
        if (ChatSystem.ShouldBlockKeyboardInput)
            return false;

        return true;
    }
}


[HarmonyPatch(typeof(TeleportScript), "OnTriggerEnter2D")]
public class TeleportScriptTeleportPatch
{
    static bool Prefix(TeleportScript __instance)
    {
        var cloneComp = __instance.gameObject.GetComponent<IsMultiplayerClone>();
        if (cloneComp != null)
        {
            return false;
        }
        // Normal Update for local player
        return true;
    }
}

[HarmonyPatch(typeof(CharacterMovement), "IsPointerOverUI")]
public class CharacterMovementIsPointerOverUiPatch
{
    static bool Prefix(ref bool __result)
    {
        if (ChatSystem.ShouldBlockGameInput)
        {
            __result = true;
            return false;
        }

        return true;
    }
}

[HarmonyPatch(typeof(CharacterMovement), "Update")]
public class CharacterMovementUpdatePatch
{
    static bool Prefix(CharacterMovement __instance)
    {
        var cloneComp = __instance.gameObject.GetComponent<IsMultiplayerClone>();
        if (cloneComp != null)
        {
            // Handled within UpdateGhostPlayers
            return false;
        }

        // Normal Update for local player
        return true;
    }
}

[HarmonyPatch(typeof(CharacterMovement), nameof(CharacterMovement.CloseAllShops))]
public class CharacterMovementCloseAllShopsPatch
{
    static bool Prefix(CharacterMovement __instance)
    {
        var cloneComp = __instance.gameObject.GetComponent<IsMultiplayerClone>();
        if (cloneComp != null) { return false; }

        foreach (var shopUi in __instance.shops)
        {
            shopUi.CloseShop();
        }

        return false;
    }
}

//TODO Implement syncing of hat/wings
[HarmonyPatch(typeof(BloobColourChange), nameof(BloobColourChange.SetPlayerWing))]
public class BloobColourChangeWingPatch
{
    [HarmonyPostfix]
    private static void Postfix(BloobColourChange __instance, int wingIndex)
    {
        Debug.Log($"Index changed to {wingIndex}");
    }
}

[HarmonyPatch(typeof(BloobColourChange), nameof(BloobColourChange.SetPlayerHat))]
public class BloobColourChangeHatPatch
{
    [HarmonyPostfix]
    private static void Postfix(BloobColourChange __instance)
    {

    }
}

public class IsMultiplayerClone : MonoBehaviour
{
    public string steamId;
    public string displayName;
    //public Vector3 lastTargetPosition = Vector3.positiveInfinity;
}

/*public class Billboard : MonoBehaviour
{
    void Update()
    {
        transform.rotation = Quaternion.LookRotation(transform.position - Camera.main.transform.position);
    }
}*/
