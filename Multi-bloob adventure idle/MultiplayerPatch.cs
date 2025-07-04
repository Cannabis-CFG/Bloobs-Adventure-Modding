using System;
using BepInEx;
using HarmonyLib;
using Newtonsoft.Json;
using Steamworks;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BepInEx.Configuration;
using Newtonsoft.Json.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using WebSocketSharp;


namespace Multi_bloob_adventure_idle
{
    [BepInPlugin("com.cannabis.multibloobidle", "Multiblood Adventure Idle", "0.0.69")]
    public class MultiplayerPatchPlugin : BaseUnityPlugin
    {

        private readonly Queue<string> messageQueue = new Queue<string>();
        private readonly object queueLock = new object();
        public static Dictionary<string, (int level, int prestige)> playerSkills = new Dictionary<string, (int level, int prestige)>();

        private WebSocket ws;
        private bool isConnected = false;

        private Coroutine positionCoroutine;
        private Coroutine ghostPlayerCoroutine;
        private Coroutine playerLevelCoroutine;
        public static bool isReady = false;
        public static ConfigEntry<bool> EnableLevelPanel;
        public static ConfigEntry<bool> EnableGhostSouls;
        private string nameCache;
        private Scene lastScene;
        private JObject lastPayload = null;

        public static MultiplayerPatchPlugin instance;


        public static readonly Dictionary<string, PlayerData> players = new Dictionary<string, PlayerData>();

        private void Awake()
        {
            Debug.Log("Starting to wake up");
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
                Debug.Log("WebSocket Connection Closed");
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
            //Harmony.CreateAndPatchAll(typeof(CharacterMovementTeleportPatch));
            Harmony.CreateAndPatchAll(typeof(TeleportScriptTeleportPatch));
            if (ws == null) { Debug.Log("WS NULL"); }
            ;
            Debug.Log("Fully woken up");
            lock (players)
            {
                Debug.Log($"Have dataset: {players}");
            }
            SceneManager.activeSceneChanged += OnActiveSceneChanged;
            HandleConfiguration();
            instance ??= this;
        }

        private void OnApplicationQuit()
        {
            var payload = new
            {
                reason = "ClientExiting",
                name = nameCache
            };
            var json = JsonConvert.SerializeObject(payload);
            ws.Send(json);
            ws.Close();
        }


        private void Start()
        {
            // Moved to OnActiveSceneChanged
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
                    Debug.Log("Got message from WS: " + json);

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
                            // 1) Full snapshot on connect
                            case "initialState":
                                foreach (var token in (JArray)msg["data"])
                                {
                                    var pd = token.ToObject<PlayerData>();
                                    if (pd.name == SteamClient.Name) continue;

                                    players[pd.name] = pd;
                                    // your existing routine will spawn/update clones next tick
                                }
                                break;

                            // 2) A brand-new player joined
                            case "newPlayer":
                                {
                                    var pd = msg["data"].ToObject<PlayerData>();
                                    if (pd.name != SteamClient.Name)
                                        players[pd.name] = pd;
                                }
                                break;

                            // 3) Incremental update for one player
                            case "update":
                                {
                                    string name = msg["name"]?.ToString();
                                    if (string.IsNullOrEmpty(name) || name == SteamClient.Name)
                                        break;

                                    if (players.TryGetValue(name, out var existing))
                                    {
                                        // apply each changed top-level field
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
                                                    // add other cases here as needed…
                                            }
                                        }
                                        // store it back
                                        players[name] = existing;
                                    }
                                }
                                break;

                            // 4) A player disconnected
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
            // We have to delay here because the Scene change to GameCloud happens quicker than everything can get instantiated
            await Task.Delay(TimeSpan.FromSeconds(7));

            switch (next.name)
            {
                case "GameCloud":
                    // We check if Steam has initialized, if it hasn't in time we wait 5 seconds and try again. Ensuring no issues on timings
                    if (!SteamClient.IsValid)
                    {
                        await Task.Delay(TimeSpan.FromSeconds(5));
                        OnActiveSceneChanged(current, next);
                        return;
                    }
                    isReady = true;
                    lastScene = next; // Cache latest scene change to handle main menu changes
                    new GameObject("HoverUIManager").AddComponent<HoverUIManager>();
                    new GameObject("HoverDetector").AddComponent<MultiplayerHoverDetector>();
                    ghostPlayerCoroutine ??= StartCoroutine(UpdateGhostPlayers());
                    playerLevelCoroutine ??= StartCoroutine(UpdatePlayerLevels());
                    positionCoroutine ??= StartCoroutine(GetPositionEnumerator());
                    AddsettingOptions();
                    break;
            }

        }

        public void HandleClientDisconnect(string name)
        {
            var clone = GameObject.Find($"BloobClone_{name}");
            if (clone == null) return;
            GameObject.DestroyImmediate(clone);
            players.Remove(name);
        }


        IEnumerator UpdatePlayerLevels()
        {
            if (!isReady)
            {
                Debug.Log("Game not ready, retrying player level update in 30 seconds.");
                yield return new WaitForSecondsRealtime(30f);
            }

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
                    Debug.LogWarning($"No component {skillClassName} found on {childName}");
                    continue;
                }

                // Determine proper field names
                string levelFieldName = childName + "Level";
                string prestigeFieldName = childName.ToLower() + "PrestigeLevel";

                // Additional edge cases for inconsistent field names
                if (childName.Equals("HitPoints"))
                {
                    levelFieldName = "HitPointsLevel";
                    prestigeFieldName = "HitPointsPrestigeLevel";
                }

                if (childName.Equals("Mining"))
                {
                    levelFieldName = "MiningLevel";
                }

                if (childName.Equals("WoodCutting"))
                {
                    levelFieldName = "woodcuttinglevel";
                }

                if (childName.Equals("SoulBinding"))
                {
                    levelFieldName = "SoulBindingLevel";
                    prestigeFieldName = "SoulBindingPrestigeLevel";
                }

                if (childName.Equals("BowCrafting"))
                {
                    prestigeFieldName = "bowCraftingPrestigeLevel";
                }

                if (childName.Equals("BeastMastery"))
                {
                    prestigeFieldName = "beastMasteryPrestigeLevel";
                }

                if (childName.Equals("Thieving"))
                {
                    levelFieldName = "ThievingLevel";
                }

                if (childName.Equals("Fishing"))
                {
                    levelFieldName = "FishingLevel";
                }

                int level = GetIntField(skillComponent, levelFieldName);
                int prestige = GetIntField(skillComponent, prestigeFieldName);

                skillData[childName] = (level, prestige);
                Debug.Log($"[Skill] {childName}: Level={level} Prestige={prestige}");
            }

            playerSkills = skillData;
            yield return new WaitForSecondsRealtime(600f);
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

        IEnumerator UpdateGhostPlayers()
        {

            while (true)
            {
                if (!isReady)
                {
                    Debug.Log("Game not ready, retrying ghost player update in 15 seconds");
                    yield return new WaitForSecondsRealtime(15f);
                }

                GameObject original = GameObject.Find("BloobCharacter");
                if (original == null)
                {
                    Debug.LogWarning("BloobCharacter not found.");
                    continue;
                }

                //Debug.Log($"Object name to clone is {original.gameObject.name} Of type {original.GetType()}");

                lock (players)
                {
                    foreach (var kvp in players)
                    {
                        string playerName = kvp.Key;

                        if (playerName == nameCache) continue;

                        //Debug.LogError($"Currently doing shit for {playerName}");

                        // Find or create clone for this player
                        GameObject clone = GameObject.Find("BloobClone_" + playerName);
                        if (clone == null)
                        {


                            clone = Instantiate(original);
                            clone.name = "BloobClone_" + playerName;
                            clone.AddComponent<IsMultiplayerClone>();
                            clone.GetComponent<SpriteRenderer>().color = kvp.Value.bloobColour.ToColor();
                            clone.GetComponent<CharacterMovement>().moveSpeed = kvp.Value.runSpeed;
                            clone.transform.position = kvp.Value.currentPosition.ToVector3();

                            // Remove unwanted components and children (same as your existing code)
                            foreach (var collider in clone.GetComponents<CircleCollider2D>())
                                Destroy(collider);
                            //Remove Children From BloobCharacter(Player Character) Game Object
                            foreach (Transform child in clone.transform)
                                if (child.name != "wingSlot" && child.name != "Canvas" && child.name != "HatSlot")
                                    Destroy(child.gameObject);



                            // Setup UI Text with playerName
                            Canvas canvas = clone.GetComponentInChildren<Canvas>();
                            if (canvas != null)
                            {
                                // Create or find PlayerName text object to avoid duplicates
                                var existingText = canvas.transform.Find("PlayerName");
                                TextMeshProUGUI text;
                                if (existingText != null)
                                    text = existingText.GetComponent<TextMeshProUGUI>();
                                else
                                {
                                    GameObject textGO = new GameObject("PlayerName");
                                    textGO.transform.SetParent(canvas.transform, false);
                                    text = textGO.AddComponent<TextMeshProUGUI>();
                                    RectTransform rt = text.GetComponent<RectTransform>();
                                    rt.anchoredPosition = new Vector2(0, 50); // position above character
                                    text.fontSize = 24;
                                    text.alignment = TextAlignmentOptions.Center;
                                    text.color = Color.white;
                                }
                                text.text = playerName; // set player name text
                            }
                            else
                            {
                                Debug.LogWarning("No Canvas found in BloobClone.");
                            }
                        }
                        //Debug.Log($"Attempting to update {kvp.Value.name}'s clone location");
                        if (kvp.Value.currentPosition.ToVector3() == clone.transform.position)
                        {
                            //Debug.Log("Position is the same, skipping");
                            continue;
                        }
                        clone.GetComponent<CharacterMovement>().moveSpeed = kvp.Value.runSpeed;
                        if (Vector3.Distance(clone.transform.position, kvp.Value.currentPosition.ToVector3()) >= 750f)
                        {
                            //Debug.Log($"Detected potential different zone for clone from previous dataset, adjusting movement speed to zip to {kvp.Value.currentPosition.ToVector3()}");
                            clone.GetComponent<CharacterMovement>().moveSpeed = 400;
                        }
                        //Debug.Log($"Attempting to move clone to {kvp.Value.currentPosition.ToVector3()}");
                        clone.GetComponent<CharacterMovement>().MoveTo(kvp.Value.currentPosition.ToVector2());


                    }

                    Canvas originalCanvas = original.GetComponentInChildren<Canvas>();
                    if (originalCanvas != null)
                    {
                        // Create or find PlayerName text object to avoid duplicates
                        var existingText = originalCanvas.transform.Find("PlayerName");
                        TextMeshProUGUI text;
                        if (existingText != null)
                            text = existingText.GetComponent<TextMeshProUGUI>();
                        else
                        {
                            GameObject textGO = new GameObject("PlayerName");
                            textGO.transform.SetParent(originalCanvas.transform, false);
                            text = textGO.AddComponent<TextMeshProUGUI>();
                            RectTransform rt = text.GetComponent<RectTransform>();
                            rt.anchoredPosition = new Vector2(0, 50); // position above character
                            text.fontSize = 24;
                            text.alignment = TextAlignmentOptions.Center;
                            text.color = Color.white;
                            Debug.LogWarning("PlayerName Text Created");
                        }
                        text.text = nameCache; // set player name text
                    }
                    else
                    {
                        Debug.LogWarning("No Canvas found in BloobCharacter.");
                    }
                }

                yield return new WaitForSecondsRealtime(3f);
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
                //TODO Pass along live current positioning and hat, wing and color parameters
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
                    skillData = playerSkills,
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
                    string jsonPayload = diffPayload.ToString(Formatting.None);
                    Debug.Log($"Sending data: {jsonPayload} to server");
                    ws.Send(jsonPayload);
                    lastPayload = newPayload;
                }


                /*string json = JsonConvert.SerializeObject(payload);
                Debug.Log($"Sending data: {json} to server");
                ws.Send(json);
                Debug.Log("Hey shithead");*/
                yield return new WaitForSecondsRealtime(5f);
            }
        }


        private void HandleConfiguration()
        {
            EnableLevelPanel = Config.Bind("Visual", "Enable Level Panel?", true,
                "Toggles whether or not the skill level panel is displayed when hovering your mouse over a ghost");
            EnableGhostSouls = Config.Bind("Visual", "Enable Ghost Souls", true,
                "Toggles whether or not to see souls that ghost players have equipped");
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

            // Look for "Sound Off" under "Settings Ui"
            Transform soundOff = settingsUi.Find("Sound Off");
            if (soundOff == null)
            {
                Debug.LogError("Sound Off not found under Settings Ui");
                return;
            }

            Debug.Log("Sound Off Found");

            // Add toggles
            AddButton(settingsUi, "Enable Ghost Souls", EnableGhostSouls, new Vector2(150, 160));
            AddButton(settingsUi, "Enable Level Panel", EnableLevelPanel, new Vector2(150, 115));

        }


        private void AddButton(Transform parent, string labelText, ConfigEntry<bool> configEntry, Vector2 position)
        {
            // Create button object
            GameObject buttonObj = new GameObject(labelText + " Button");
            buttonObj.transform.SetParent(parent, false);

            RectTransform rt = buttonObj.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(300, 40);
            rt.anchoredPosition = position;

            // Add Button + Image (Unity UI requirement)
            Image image = buttonObj.AddComponent<Image>();
            image.color = new Color(0.2f, 0.2f, 0.2f, 0.7f);
            Button button = buttonObj.AddComponent<Button>();

            // Create label for button text
            GameObject labelObj = new GameObject("Label");
            labelObj.transform.SetParent(buttonObj.transform, false);
            TextMeshProUGUI label = labelObj.AddComponent<TextMeshProUGUI>();
            label.text = labelText + ": " + (configEntry.Value ? "ON" : "OFF");
            label.fontSize = 18;
            label.alignment = TextAlignmentOptions.Center;
            label.color = Color.white;

            RectTransform labelRT = label.GetComponent<RectTransform>();
            labelRT.anchorMin = Vector2.zero;
            labelRT.anchorMax = Vector2.one;
            labelRT.offsetMin = Vector2.zero;
            labelRT.offsetMax = Vector2.zero;

            // Click handler (must come after label is declared)
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


    [HarmonyPatch(typeof(CharacterMovement), "Teleport")]
    public class CharacterMovementTeleportPatch
    {
        static bool Prefix(CharacterMovement __instance)
        {
            var cloneComp = __instance.gameObject.GetComponent<IsMultiplayerClone>();
            if (cloneComp != null)
            {
                // Skip original Update for clones
                return false;
            }
            // Normal Update for local player
            return true;
        }
    }

    //TODO Test to see if patch works
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
                /*string playerName = __instance.gameObject.name.Replace("BloobClone_", "");
                // Move to grabbing currentPosition of original clones
                if (MultiplayerPatchPlugin.players.TryGetValue(playerName, out PlayerData player))
                {
                    /*if (Vector3.Distance(cloneComp.transform.position, player.currentPosition.ToVector3()) >= 500f)
                    {
                        cloneComp.transform.position.Set(player.currentPosition.x, player.currentPosition.y, player.currentPosition.z);
                        return false;
                    }#1#
                    if (cloneComp.lastTargetPosition != player.currentPosition.ToVector3())
                    {
                        __instance.MoveTo(player.currentPosition.ToVector3());
                        cloneComp.lastTargetPosition = player.currentPosition.ToVector3();
                    }
                }*/
                // Skip original Update for clones
                return false;
            }

            // Normal Update for local player
            return true;
        }
    }



public class IsMultiplayerClone : MonoBehaviour
    {
        public Vector3 lastTargetPosition = Vector3.positiveInfinity;
    }

    public class WebSocketMessage
    {
        public string type { get; set; }
        public PlayerData[] data { get; set; }
    }


    public class Billboard : MonoBehaviour
    {
        void Update()
        {
            transform.rotation = Quaternion.LookRotation(transform.position - Camera.main.transform.position);
        }
    }




/*TODO
 * 
 * Add button to settings to disable/enable ghoust souls and level gui
 *
 */
