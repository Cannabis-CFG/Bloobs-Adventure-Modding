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
    [BepInPlugin("com.cannabis.multibloobidle", "Multiblood Adventure Idle", "1.0.0")]
    public class MultiplayerPatchPlugin : BaseUnityPlugin
    {

        private readonly Queue<string> messageQueue = new Queue<string>();
        private readonly object queueLock = new object();
        public static Dictionary<string, (int level, int prestige)> playerSkills = new Dictionary<string, (int level, int prestige)>();

        private WebSocket ws;
        private bool isConnected = false;
        private bool isMonitoringConnection;

        private Coroutine positionCoroutine;
        private Coroutine ghostPlayerCoroutine;
        //private Coroutine playerLevelCoroutine;
        public static bool isReady = false;
        private bool nameTagCreated;
        public static ConfigEntry<bool> EnableLevelPanel;
        public static ConfigEntry<bool> EnableContextMenu;
        private string nameCache;
        private bool atMM;
        private JObject lastPayload = null;
        public static MultiplayerPatchPlugin instance;



        public static readonly Dictionary<string, PlayerData> players = new Dictionary<string, PlayerData>();

        private void Awake()
        {
            //Debug.Log("Starting to wake up");
            // WebServer Connection
            ws = new WebSocket("ws://172.93.111.163:42069");
            ws.OnOpen += (sender, e) =>
            {
                isConnected = true;
                Debug.Log("WebSocket Connected.");
            };
            ws.OnError += (sender, e) => { Debug.Log("WebSocket Error:" + e.Message); };
            ws.OnClose += (sender, e) =>
            {
                isConnected = false;
                Debug.Log($"Close Code: {e.Code}");
                Debug.Log($"Reason: {e.Reason}");

                if (e.Code != 1000)
                {
                    Debug.LogWarning("Connection was closed abnormally");
                    StartCoroutine(ConnectionMonitorCoroutine());
                }
            };
            ws.OnMessage += (sender, e) =>
            {
                lock (queueLock)
                {
                    messageQueue.Enqueue(e.Data);
                }
            };

            ws.ConnectAsync();
            Harmony.CreateAndPatchAll(typeof(CharacterMovementUpdatePatch));
            Harmony.CreateAndPatchAll(typeof(CharacterMovementCloseAllShopsPatch));
            Harmony.CreateAndPatchAll(typeof(TeleportScriptTeleportPatch));
            Harmony.CreateAndPatchAll(typeof(BloobColourChangeWingPatch));
            if (ws == null) { Debug.Log("WS NULL");};
            //Debug.Log("Fully woken up");
            SceneManager.activeSceneChanged += OnActiveSceneChanged;
            //StartCoroutine(ConnectionMonitorCoroutine());
            HandleConfiguration();
            instance ??= this;
        }


        private void OnApplicationQuit()
        {
            ws.Close();
        }


        private void Update()
        {
            if (!isReady || !SteamClient.IsValid)
                return;
            if (nameCache.IsNullOrEmpty()) nameCache = SteamClient.Name;

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

                    lock (players)
                    {
                        switch (type)
                        {
                            case "initialState":
                                foreach (var token in (JArray)msg["data"])
                                {
                                    var pd = token.ToObject<PlayerData>();
                                    if (pd.name == SteamClient.Name) continue;

                                    players[pd.name] = pd;
                                }
                                break;
                            case "newPlayer":
                                {
                                    var pd = msg["data"].ToObject<PlayerData>();
                                    if (pd.name != SteamClient.Name)
                                        players[pd.name] = pd;
                                }
                                break;
                            case "update":
                                {
                                    string name = msg["name"]?.ToString();
                                    if (string.IsNullOrEmpty(name) || name == SteamClient.Name)
                                        break;

                                    if (players.TryGetValue(name, out var existing))
                                    {
                                        foreach (var prop in msg.Properties())
                                        {
                                            switch (prop.Name)
                                            {
                                                case "currentPosition":
                                                    existing.currentPosition = prop.Value
                                                        .ToObject<Vector3Like>();
                                                    break;
                                                case "runSpeed":
                                                    existing.runSpeed = prop.Value
                                                        .ToObject<float>();
                                                    break;
                                                case "cloneData":
                                                    existing.soulData = prop.Value
                                                        .ToObject<string[]>();
                                                    break;
                                                case "bloobColour":
                                                    existing.bloobColour = prop.Value
                                                        .ToObject<ColourLike>();
                                                    break;
                                                case "skillData":
                                                    existing.skillData = prop.Value
                                                        .ToObject<Dictionary<string, (int level, int prestige)>>();
                                                    break;
                                            }
                                        }
                                        players[name] = existing;
                                    }
                                }
                                break;
                            case "clientRemoved":
                            case "disconnect":
                                {
                                    string name = msg["name"]?.ToString();
                                    if (!string.IsNullOrEmpty(name))
                                        HandleClientDisconnect(name);
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
            Debug.Log($"Next Scene {next.name}");
            switch (next.name)
            {
                case "GameCloud":
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
                    ghostPlayerCoroutine ??= StartCoroutine(UpdateGhostPlayers());
                    //playerLevelCoroutine ??= StartCoroutine(UpdatePlayerLevels());
                    positionCoroutine ??= StartCoroutine(GetPositionEnumerator());
                    AddsettingOptions();
                    if (atMM)
                    {
                        ws.Send(lastPayload.ToString(Formatting.None));
                    }
                    break;
            }

        }


        public void HandleClientDisconnect(string name)
        {
            var clone = GameObject.Find($"BloobClone_{name}");
            if (clone == null) return;
            CloneManager.RemoveClone(clone);
            lock (players)
            {
                players.Remove(name);
            }
        }


        private Dictionary<string, (int, int)> UpdatePlayerLevels()
        {

            Dictionary<string, string> NameMap = new()
            {
                { "WoodCutting", "Woodcutting" }

            };

            Dictionary<string, (int level, int prestige)> skillData = new();

            GameObject player = GameObject.Find("BloobCharacter");
            foreach (Transform child in player.transform)
            {

                if (child.name is null or "Weapon Point" or "MagicWeapon Point" or "RangeWeapon Point" or "Melee Weapon" or "MeleeWeapon" or "MagicProjectile" or "RangeProjectile" or "wingSlot" or "hatSlot" or "Canvas")
                {
                    continue; // CUNT
                }

                string childName = NameMap.TryGetValue(child.name, out var value) ? value : child.name;

                string skillClassName = childName + "Skill";

                // Skill Class Name edge cases
                if (childName == "SoulBinding")
                    skillClassName = "SoulBinding";


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
            GameObject original = GameObject.Find("BloobCharacter");
            if (original is null)
            {
                Debug.LogWarning("BloobCharacter not found.");
                yield return new WaitForSecondsRealtime(5f);
            }
            while (true)
            {
                if (!isReady)
                {
                    Debug.Log("Game not ready, retrying ghost player update in 15 seconds");
                    yield return new WaitForSecondsRealtime(15f);
                }


                if (!nameTagCreated)
                {
                    ApplyLocalNametag(original);
                    nameTagCreated = true;
                }

                lock (players)
                {
                    foreach (var kvp in players)
                    {
                        if (kvp.Key == nameCache)
                            continue;

                        CloneManager.UpdateOrCreateClone(original, kvp.Value);
                        
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
                }
                GameObject player = GameObject.Find("BloobCharacter");
                string[] soulData = Array.Empty<string>();

                var payload = new
                {
                    name = SteamClient.Name,
                    currentPosition = new
                    {
                        x = Mathf.Round(player.transform.position.x),
                        y = Mathf.Round(player.transform.position.y),
                        z = Mathf.Round(player.transform.position.z)
                    },
                    isDisconnecting = false,
                    skillData = UpdatePlayerLevels(),
                    bloobColour = new ColourLike()
                    {
                        //playerGameObject.GetComponent<SpriteRenderer>().color
                        a = player.GetComponent<SpriteRenderer>().color.a,
                        b = player.GetComponent<SpriteRenderer>().color.b,
                        g = player.GetComponent<SpriteRenderer>().color.g,
                        r = player.GetComponent<SpriteRenderer>().color.r
                    },
                    runSpeed = player.GetComponent<CharacterMovement>()?.dexteritySkill.runSpeed,
                    cloneData = soulData
                };

                JObject newPayload = JObject.FromObject(payload);

                var diffPayload = new JObject
                {
                    ["name"] = newPayload["name"]
                };

                foreach (var property in newPayload.Properties())
                {
                    if (property.Name == "name") continue;
                    if (lastPayload == null || !JToken.DeepEquals(property.Value, lastPayload[property.Name]))
                    {
                        diffPayload[property.Name] = property.Value;
                    }
                }

                if (diffPayload.Properties().Count() > 1)
                {
                    //TODO Send through Binary serialization, decreases payload and faster to de/serialize
                    string jsonPayload = diffPayload.ToString(Formatting.None);
                    //Debug.Log($"Sending data: {jsonPayload} to server");
                    ws.Send(jsonPayload);
                    lastPayload = newPayload;
                }
                //Debug.Log("Hey shithead");
                yield return new WaitForSecondsRealtime(1f);
            }
        }


        private IEnumerator ConnectionMonitorCoroutine(int maxRetries = 5, float retryDelay = 5f, float checkInterval = 10f)
        {
            if (isMonitoringConnection)
            {
                yield break;
            }

            while (true)
            {
                if (!isConnected || !ws.IsAlive)
                {
                    Debug.LogWarning("[WS] Lost connection — attempting to reconnect...");

                    for (int attempt = 1; attempt <= maxRetries && !isConnected; attempt++)
                    {
                        Debug.Log($"[WS] Reconnect attempt {attempt}/{maxRetries}...");
                        ws.ConnectAsync();

                        float timer = 0f;
                        while (timer < retryDelay && !isConnected)
                        {
                            timer += Time.deltaTime;
                            yield return null;
                        }
                    }

                    if (isConnected)
                    {
                        Debug.Log("[WS] Reconnected successfully! Reinitializing player data");
                        ws.Send(lastPayload.ToString(Formatting.None));
                        //StopCoroutine(ConnectionMonitorCoroutine());
                        break;
                    }

                    else
                        Debug.LogError($"[WS] Failed to reconnect after {maxRetries} attempts.");
                }
                yield return new WaitForSecondsRealtime(checkInterval);
            }
            isMonitoringConnection = false;
        }


        private void HandleConfiguration()
        {
            EnableLevelPanel = Config.Bind("Visual", "Enable Level Panel?", true,
                "Toggles whether or not the skill level panel is displayed when hovering your mouse over a ghost");
            EnableContextMenu = Config.Bind("Visual", "Enable Context Menu", true,
                "Enables a context menu when you right click to display overlapping ghosts");
        }

        public void MainMenuClicked()
        {
            ghostPlayerCoroutine = null;
            positionCoroutine = null;
            isReady = false;
            var GO = GameObject.Find("HoverUIManager");
            var GO1 = GameObject.Find("HoverDetector");
            if (GO != null)
            {
                Destroy(GO);
            }
            if (GO1 != null)
            {
                Destroy(GO1);
            }

            atMM = true;
            var payload = new
            {
                type = "RemoveClient",
                reason = "ClientMainMenu",
                name = nameCache
            };
            var json = JsonConvert.SerializeObject(payload);
            ws.Send(json);
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

            var mainMenu = settingsUi.Find("Main Menu").gameObject;
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
            AddButton(settingsUi, "Enable Level Panel", EnableLevelPanel, new Vector2(150, 40));
            //TODO Setup handling of creating/destroying of context menu behaviour on toggle to clean up properly
            AddButton(settingsUi, "Enable Context Menu", EnableContextMenu, new Vector2(150, 55));

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
    public bool isDisconnecting;
    public Vector3Like currentPosition;
    public float runSpeed;
    public string hatName;
    public string wingName;
    public ColourLike bloobColour;
    public Dictionary<string, (int level, int prestige)> skillData;
    public string[] soulData;
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
    //public Vector3 lastTargetPosition = Vector3.positiveInfinity;
}



/*public class Billboard : MonoBehaviour
{
    void Update()
    {
        transform.rotation = Quaternion.LookRotation(transform.position - Camera.main.transform.position);
    }
}*/