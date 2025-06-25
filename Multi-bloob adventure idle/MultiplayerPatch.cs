using System.Collections;
using System.Collections.Generic;
using BepInEx;
using HarmonyLib;
using Newtonsoft.Json;
using Steamworks;
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

        private WebSocket ws;
        private bool isConnected = false;
        public static Vector3 currentPosition;
        public static Vector3 desiredPosition;
        private Coroutine positionCoroutine;
        private Coroutine ghostPlayerCoroutine;
        public static bool isReady = false;
        private Scene lastScene;

        //public static Dictionary<string, Vector3> dummyPositions = new Dictionary<string, Vector3>();
        //public static Dictionary<string, Vector3> desiredPositions = new Dictionary<string, Vector3>();
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

            ws.ConnectAsync ();
            Harmony.CreateAndPatchAll(typeof(GetDesiredPositionPatch));
            Harmony.CreateAndPatchAll(typeof(CharacterMovement_UpdatePatch));

            if (ws == null) { Debug.Log("WS NULL"); };
            Debug.Log("Fully woken up");
            lock (players)
            {
                Debug.Log($"Have dataset: {players}");
            }
            SceneManager.activeSceneChanged += OnActiveSceneChanged;
            StartCoroutine(Test());
        }

        private void OnApplicationQuit()
        {
            var payload = new
            {
                reason = "ClientExiting",
                name = SteamClient.Name
            };
            var json = JsonConvert.SerializeObject(payload);
            ws.Send(json);
            ws.Close();
        }


        IEnumerator Test()
        {
            while (true)
            {
                Debug.Log($"Retrying dataset post: {string.Join(", ", players.Keys)}\nRetrying in 10 seconds");
                yield return new WaitForSecondsRealtime(10);
            }
        }


        private void Start()
        {
            if (positionCoroutine == null) positionCoroutine = StartCoroutine(GetPositionEnumerator());
            if (ghostPlayerCoroutine == null) ghostPlayerCoroutine = StartCoroutine(UpdateGhostPlayers());
        }

        private void Update()
        {
            if (!isReady) return;
                GameObject player = GameObject.Find("BloobCharacter");
                if (player != null)
                    currentPosition = player.transform.position;

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
                            if (message?.type == "allData" && message.data != null)
                            {
                                lock (players)
                                {
                                    foreach (var playerData in message.data)
                                    {
                                        if (playerData.name == SteamClient.Name) break;
                                        players[playerData.name] = playerData;
                                        Debug.Log($"Added player {playerData.name} to cached data. Their starting position is {playerData.currentPosition.ToVector3()}");
                                    }
                                }
                            }
                        }
                        catch (System.Exception ex)
                        {
                            Debug.LogError("Failed to handle WS message: " + ex);
                        }
                    }
                }
        }

        public void OnActiveSceneChanged(Scene a, Scene b)
        {
            //TODO Handle cleaning up of gameObjects when exiting to main menu and recreating gameObjects re-entering back into the game
            switch (b.name)
            {
                case "GameCloud":
                    isReady = true;
                    lastScene = b;
                    break;
            }



            //if (b.name == "GameCloud")
            //{
            //    isReady = true;
            //    lastScene = b;
            //    //Debug.Log($"Scene A is {a.name} Scene B is {b.name}");
            //}

        }


        IEnumerator UpdateGhostPlayers()
        {

            while (true)
            {
                if (!isReady)
                {
                    Debug.Log("Game not ready, retrying ghost player update in 5 seconds");
                    yield return new WaitForSecondsRealtime(5f);
                }
                //TODO Refactor to fit new data structure
                foreach (var kvp in players)
                {
                    string playerName = kvp.Key;
                    Vector3 targetPos = kvp.Value.desiredPosition.ToVector3();

                    // Find or create clone for this player
                    GameObject clone = GameObject.Find("BloobClone_" + playerName);
                    if (clone == null)
                    {
                        GameObject original = GameObject.Find("BloobCharacter");
                        if (original == null)
                        {
                            Debug.LogWarning("BloobCharacter not found.");
                            continue;
                        }

                        clone = Instantiate(original);
                        clone.name = "BloobClone_" + playerName;
                        clone.AddComponent<IsMultiplayerClone>();

                        // Remove unwanted components and children (same as your existing code)
                        foreach (var collider in clone.GetComponents<CircleCollider2D>())
                            Destroy(collider);
                        foreach (Transform child in clone.transform)
                            if (child.name != "wingSlot" && child.name != "Canvas")
                                Destroy(child.gameObject);

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
                            }
                            text.text = SteamClient.Name; // set player name text
                        }
                        else
                        {
                            Debug.LogWarning("No Canvas found in BloobCharacter.");
                        }



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

                    // Update desiredPositions dictionary for patch to use
                    players[playerName].desiredPosition.x = targetPos.x;
                    players[playerName].desiredPosition.y = targetPos.y;
                    players[playerName].desiredPosition.z = targetPos.z;
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
                    Debug.Log("Game not ready, retrying position coroutine in 5 seconds");
                    yield return new WaitForSecondsRealtime(5);
                }
                CharacterData data = new CharacterData();

                if (SteamClient.IsValid)
                {
                    data.name = SteamClient.Name;
                }
                if (currentPosition == null || desiredPosition == null) yield break;

                data.desiredPosition = desiredPosition;
                Debug.Log(desiredPosition);
                data.currentPosition = currentPosition;
                Debug.Log(currentPosition);

                var payload = new
                {
                    name = data.name,
                    currentPosition = new
                    {
                        x = Mathf.Round(data.currentPosition.x),
                        y = Mathf.Round(data.currentPosition.y),
                        z = Mathf.Round(data.currentPosition.z)
                    },
                    desiredPosition = new
                    {
                        x = Mathf.Round(data.desiredPosition.x),
                        y = Mathf.Round(data.desiredPosition.y),
                        z = Mathf.Round(data.desiredPosition.z)
                    }
                };

                string json = JsonConvert.SerializeObject(payload);

                Debug.Log($"Sending data: {json} to server");

                ws.Send(json);

                
                Debug.Log("Hey shithead");
                yield return new WaitForSecondsRealtime(30f);
            }

        }
    }

    public class PlayerData
    {
        public string name;
        public Vector3Like currentPosition;
        public Vector3Like desiredPosition;
    }

    public class Vector3Like
    {
        public float x;
        public float y;
        public float z;

        public Vector3 ToVector3() => new Vector3(x, y, z);
    }

    public class CharacterData
    {
        public string name { get; set; }

        public Vector3 currentPosition { get; set; }

        public Vector3 desiredPosition { get; set; }
    }


    [HarmonyPatch(typeof(CharacterMovement), nameof(CharacterMovement.MoveTo))]
    public static class GetDesiredPositionPatch
    {
        [HarmonyPostfix]
        public static void GetDesiredPosition(ref Vector2 destination)
        {
            MultiplayerPatchPlugin.desiredPosition = new Vector3(destination.x, destination.y, 0);
        }
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
                    if (Vector3.Distance(cloneComp.transform.position, player.desiredPosition.ToVector3()) >= 500f)
                    {
                        cloneComp.transform.position.Set(player.desiredPosition.x, player.desiredPosition.y, player.desiredPosition.z);
                        return false;
                    }
                    if (cloneComp.lastTargetPosition != player.desiredPosition.ToVector3())
                    {
                        __instance.MoveTo(player.desiredPosition.ToVector3());
                        cloneComp.lastTargetPosition = player.desiredPosition.ToVector3();
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
}

/*TODO
 * Cache only original player's current position to pass along
 * Rewrite data structure to only pass currentPosition
 * Update clone's movement to just goto clone data currentPosition
 * On WB disconnect, clear data from all clients and handle removing of that specific clients clone from all connected clients
 */

