using System.Collections;
using System.Collections.Generic;
using BepInEx;
using HarmonyLib;
using Newtonsoft.Json;
using Steamworks;
using TMPro;
using UnityEngine;
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

            ws.Connect();
            Harmony.CreateAndPatchAll(typeof(GetDesiredPositionPatch));
            //Harmony.CreateAndPatchAll(typeof(CharacterMovement_UpdatePatch));

            if (ws == null) { Debug.Log("WS NULL"); };
            Debug.Log("Fully woken up");
            lock (players)
            {
                Debug.Log($"Have dataset: {players}");
            }

            StartCoroutine(Test());
        }


        IEnumerator Test()
        {
            while (true)
            {
                Debug.Log($"Retrying dataset post: {string.Join(", ", players.Keys)}\nRetrying in 10 seconds");
                yield return new WaitForSeconds(10);
            }
        }


        private void Start()
        {
            if (positionCoroutine == null) positionCoroutine = StartCoroutine(GetPositionEnumerator());
        }

        private void Update()
        {
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
                            if (message?.type == "allData" && message.data != null)
                            {
                                lock (players)
                                {
                                    foreach (var playerData in message.data)
                                    {
                                        players[playerData.name] = playerData;
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

        /*
         * TODO
         * If we grab the gameObject of the playerCharacter and get the gameObject.transform we get the positoin of the player character. We can cache this
         * and on update we can see if the cache == the current gameObject.transform. If different we can then move the player character to this location.
         *
         *
         * When creating custom gameObject that acts as the multiplayer ghost players, attach the playerController to said gameObject to be able to manipulate
         * it directly using the games built in systems without messing with the live player
         *
         *
         * Create dynamic ws send function so we can get live data as it updates through the hooks and data can be manually adjusted and sent, example
         *
         * Create BloobCharacter GameObjects named with Steam name so data can be sent back and updated on a per GameObject basis rather than foreach them all to update the correct one
         *
         */



        //IEnumerator GetPositionEnumeratorDummy()
        //{
        //    // Seed dummy players for testing:
        //    //dummyPositions["Alice"] = new Vector3(1, 2, 0);
        //    //dummyPositions["Bob"] = new Vector3(-3, 4, 0);

        //    while (true)
        //    {
        //        //TODO Refactor to fit new data structure
        //        foreach (var kvp in dummyPositions)
        //        {
        //            string playerName = kvp.Key;
        //            Vector3 targetPos = kvp.Value;

        //            // Find or create clone for this player
        //            GameObject clone = GameObject.Find("BloobClone_" + playerName);
        //            if (clone == null)
        //            {
        //                GameObject original = GameObject.Find("BloobCharacter");
        //                if (original == null)
        //                {
        //                    Debug.LogWarning("BloobCharacter not found.");
        //                    continue;
        //                }

        //                clone = Instantiate(original);
        //                clone.name = "BloobClone_" + playerName;
        //                clone.AddComponent<IsMultiplayerClone>();

        //                // Remove unwanted components and children (same as your existing code)
        //                foreach (var collider in clone.GetComponents<CircleCollider2D>())
        //                    Destroy(collider);
        //                foreach (Transform child in clone.transform)
        //                    if (child.name != "wingSlot" && child.name != "Canvas")
        //                        Destroy(child.gameObject);

        //                // Setup UI Text with playerName
        //                Canvas canvas = clone.GetComponentInChildren<Canvas>();
        //                if (canvas != null)
        //                {
        //                    // Create or find PlayerName text object to avoid duplicates
        //                    var existingText = canvas.transform.Find("PlayerName");
        //                    TextMeshProUGUI text;
        //                    if (existingText != null)
        //                        text = existingText.GetComponent<TextMeshProUGUI>();
        //                    else
        //                    {
        //                        GameObject textGO = new GameObject("PlayerName");
        //                        textGO.transform.SetParent(canvas.transform, false);
        //                        text = textGO.AddComponent<TextMeshProUGUI>();
        //                        RectTransform rt = text.GetComponent<RectTransform>();
        //                        rt.anchoredPosition = new Vector2(0, 50); // position above character
        //                        text.fontSize = 24;
        //                        text.alignment = TextAlignmentOptions.Center;
        //                        text.color = Color.white;
        //                    }
        //                    text.text = playerName; // set player name text
        //                }
        //                else
        //                {
        //                    Debug.LogWarning("No Canvas found in BloobClone.");
        //                }
        //            }

        //            // Update desiredPositions dictionary for patch to use
        //            desiredPositions[playerName] = targetPos;
        //        }

        //        yield return new WaitForSeconds(30f);
        //    }
        //}



        IEnumerator GetPositionEnumerator()
        {
            while (true)
            {
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
                        x = data.currentPosition.x,
                        y = data.currentPosition.y,
                        z = data.currentPosition.z
                    },
                    desiredPosition = new
                    {
                        x = data.desiredPosition.x,
                        y = data.desiredPosition.y,
                        z = data.desiredPosition.z
                    }
                };

                string json = JsonConvert.SerializeObject(payload);

                Debug.Log($"Sending data: {json} to server");

                ws.Send(json);

                


                //  {
                //      type: "addData"
                //   data: [
                //    {
                //    "name" : "Bob",
                //    "currentPosition": { "x": 1, "y": 2, "z": 0 },
                //    "desiredPosition": { "x": 1, "y": 2, "z": 0 }
                //    }
                //    ]


                /*
                 * public class Position
                 * {
                 * public float x {get; set;}
                 * 
                 * WebSocketResponse response = JsonConvert.DeserializeObject<WebSocketResonse>(message);
                 * 
                 * if (response.type == "allData")
                 * {
                 *  foreach (var client in response.data)
                 *  {
                 *      string client = client.name
                 *      vector3 clientCurrentPos = new Vector3 (client.currectPosition.x, client.currectPosition.y, client.currectPosition.z)
                 */

                Debug.Log("Hey shithead");
                yield return new WaitForSeconds(30f);
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
            //object[] blah = new object[]{name, "desired", position}
            //SendData(blah);
        }
    }

    //[HarmonyPatch(typeof(CharacterMovement), "Update")]
    //public class CharacterMovement_UpdatePatch
    //{
    //    static bool Prefix(CharacterMovement __instance)
    //    {
    //        var cloneComp = __instance.gameObject.GetComponent<IsMultiplayerClone>();
    //        if (cloneComp != null)
    //        {
    //            string playerName = __instance.gameObject.name.Replace("BloobClone_", "");
    //            if (MultiplayerPatchPlugin.desiredPositions.TryGetValue(playerName, out Vector3 targetPos))
    //            {
    //                if (cloneComp.lastTargetPosition != targetPos)
    //                {
    //                    __instance.MoveTo(targetPos);
    //                    cloneComp.lastTargetPosition = targetPos;
    //                }
    //            }
    //            // Skip original Update for clones
    //            return false;
    //        }

    //        // Normal Update for local player
    //        return true;
    //    }
    //}



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

/*
 * Cache player location when player spawns. BloobCharacter.gameObject.transform.position
 * When player moves, update the cache and send the new position to the server.
 * On Update loop, check if the cache position is different from the desired position and only send and update data when it changes.
 * On player spawn, send some kind of initalisation data to the server so it can create a new clone on each connected client.
 * on Player disconnect, send a message to the server to remove the clone from all clients.
 * 
 */

