using System;
using BepInEx;
using HarmonyLib;
using Newtonsoft.Json;
using Steamworks;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.Remoting.Messaging;
using System.Threading;
using System.Threading.Tasks;
using BepInEx.Configuration;
using Multi_bloob_adventure_idle;
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
            Harmony.CreateAndPatchAll(typeof(CharacterMovement_UpdatePatch));

            if (ws == null) { Debug.Log("WS NULL"); }
            ;
            Debug.Log("Fully woken up");
            lock (players)
            {
                Debug.Log($"Have dataset: {players}");
            }
            SceneManager.activeSceneChanged += OnActiveSceneChanged;
            HandleConfiguration();
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
            positionCoroutine ??= StartCoroutine(GetPositionEnumerator());
            ghostPlayerCoroutine ??= StartCoroutine(UpdateGhostPlayers());
            playerLevelCoroutine ??= StartCoroutine(UpdatePlayerLevels());
        }

        private void Update()
        {
            if (!isReady || !SteamClient.IsValid) return;
            GameObject player = GameObject.Find("BloobCharacter");
            if (nameCache.IsNullOrEmpty()) nameCache = SteamClient.Name;

            lock (queueLock)
            {
                while (messageQueue.Count > 0)
                {
                    string json = messageQueue.Dequeue();
                    Debug.Log("Got message from WS: " + json);
                    try
                    {
                        var message = JsonConvert.DeserializeObject<WebSocketMessage>(json);
                        //TODO Switch message?.type
                        //TODO Add closing type to handle removing of closing clients
                        if (message.data == null) return;

                        switch (message?.type)
                        {
                            case "allData":
                                int i = 0;
                                lock (players)
                                {
                                    foreach (var playerData in message.data)
                                    {
                                        if (playerData.isDisconnecting)
                                        {
                                            HandleClientDisconnect(playerData.name);
                                            Debug.Log($"Player {playerData.name} has disconnected.");
                                            i++;
                                            continue;
                                        }
                                        if (playerData.name == SteamClient.Name) continue;
                                        players[playerData.name] = playerData;
                                        Debug.Log($"Added player {playerData.name} to cached data. Their starting position is {playerData.currentPosition.ToVector3()}");

                                    }
                                    if (i > 0) Debug.Log($"Detected {i} disconnected clients, removing from game world.");
                                }
                                break;
                        }

                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogError("Failed to handle WS message: " + ex);
                    }
                }
            }
        }

        public async void OnActiveSceneChanged(Scene a, Scene b)
            { 
            //TODO Handle cleaning up of gameObjects when exiting to main menu and recreating gameObjects re-entering back into the game

            await Task.Delay(TimeSpan.FromSeconds(20));

            switch (b.name)
            {
                case "GameCloud":
                    isReady = true;
                    lastScene = b;
                    new GameObject("HoverUIManager").AddComponent<HoverUIManager>();
                    new GameObject("HoverDetector").AddComponent<MultiplayerHoverDetector>();
                    AddsettingOptions();
                    break;
            }

            //if (b.name == "GameCloud")
            //{
            //    isReady = true;
            //    lastScene = b;
            //    //Debug.Log($"Scene A is {a.name} Scene B is {b.name}");
            //}

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
                //{ "SoulBinding", "Soulbinding" }
              
            };

            Dictionary<string, (int level, int prestige)> skillData = new();

            GameObject player = GameObject.Find("BloobCharacter");
            foreach (Transform child in player.transform)
            {
                // Skip irrelevant children
                if (child.name is null or "Weapon Point" or "MagicWeapon Point" or "RangeWeapon Point" or "Melee Weapon" or "MeleeWeapon" or "MagicProjectile" or "RangeProjectile" or "wingSlot" or "hatSlot" or "Canvas")
                {
                    continue; // CUNT
                }

                // Normalize child name
                string childName = NameMap.TryGetValue(child.name, out var value) ? value : child.name;

                // Build skill class name
                string skillClassName = childName + "Skill";

                // Handle known exceptional class names
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

                lock (players)
                {
                    foreach (var kvp in players)
                    {
                        string playerName = kvp.Key;
                        Vector3 targetPos = kvp.Value.currentPosition.ToVector3();

                        // Find or create clone for this player
                        GameObject clone = GameObject.Find("BloobClone_" + playerName);
                        if (clone == null)
                        {


                            clone = Instantiate(original);
                            clone.name = "BloobClone_" + playerName;
                            clone.AddComponent<IsMultiplayerClone>();
                            clone.GetComponent<SpriteRenderer>().color = kvp.Value.bloobColour.ToColor();

                            // Remove unwanted components and children (same as your existing code)
                            //foreach (var collider in clone.GetComponents<CircleCollider2D>())
                                //Destroy(collider);
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

                yield return new WaitForSeconds(30f);
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
                //Foreach GameOject.Contains("Soul")
                //Get a list of the names of the souls
                //Spawn GameObject ("Soul Name")
                //Add component PetFollow
                //Change Object.name to Soul Name

                //Might have to cache a soul to get the component


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
                    runSpeed = player.GetComponent<CharacterMovement>().dexteritySkill.runSpeed

                };

                string json = JsonConvert.SerializeObject(payload);

                Debug.Log($"Sending data: {json} to server");

                ws.Send(json);

                
                Debug.Log("Hey shithead");
                yield return new WaitForSecondsRealtime(30f);
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
            Transform t1 = canvas.transform.Find("Player Menu/Bar");
            if (t1 == null) Debug.LogError("Player Menu not found");
            
                else
                {
                    Transform t2 = t1.Find("Menu Bar");
                    if (t2 == null) Debug.LogError("Menu Bar not found");
                    else
                    {
                        Transform t3 = t2.Find("Settings Ui");
                        if (t3 == null) Debug.LogError("Settings Ui not found");
                        else Debug.Log("Settings Ui found!");
                    }
                }
        }

            //GameObject SettingsUI = settingsTransform.gameObject;
            //if (SettingsUI == null) { Debug.Log("Cry"); return; }
            //foreach (Transform child in SettingsUI.transform)
            //{
            //    GameObject childObject = child.gameObject;

            //    if (childObject == null || string.IsNullOrEmpty(childObject.name)) { Debug.Log("Child.Name Null"); continue; }
            //    Debug.Log(childObject.name);

            //    if (childObject.name == "Sound Off")
            //    {
            //        Instantiate(childObject, SettingsUI.transform);
            //        Debug.Log("Sound Off Button Cloned");
            //    }
            //}

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
    }

    public class Vector3Like
    {
        public float x;
        public float y;
        public float z;

        public Vector3 ToVector3() => new Vector3(x, y, z);
    }

    public class ColourLike
    {
        public float a;
        public float r;
        public float g;
        public float b;

        public Color ToColor() => new Color(r, g, b, a);
    }



    [HarmonyPatch(typeof(CharacterMovement), "Update")]
    public class CharacterMovement_UpdatePatch
    {
        static bool Prefix(CharacterMovement __instance)
        {
            var cloneComp = __instance.gameObject.GetComponent<IsMultiplayerClone>();
            if (cloneComp != null)
            {
                string playerName = __instance.gameObject.name.Replace("BloobClone_", "");
                // Move to grabbing currentPosition of original clones
                if (MultiplayerPatchPlugin.players.TryGetValue(playerName, out PlayerData player))
                {
                    /*if (Vector3.Distance(cloneComp.transform.position, player.currentPosition.ToVector3()) >= 500f)
                    {
                        cloneComp.transform.position.Set(player.currentPosition.x, player.currentPosition.y, player.currentPosition.z);
                        return false;
                    }*/
                    if (cloneComp.lastTargetPosition != player.currentPosition.ToVector3())
                    {
                        __instance.MoveTo(player.currentPosition.ToVector3());
                        cloneComp.lastTargetPosition = player.currentPosition.ToVector3();
                    }
                }
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
