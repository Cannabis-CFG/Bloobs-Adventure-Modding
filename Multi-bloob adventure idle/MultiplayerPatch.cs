using System.Collections;
using BepInEx;
using HarmonyLib;
using Newtonsoft.Json;
using Steamworks;
using UnityEngine;
using WebSocketSharp;

namespace Multi_bloob_adventure_idle
{
    [BepInPlugin("com.cannabis.multibloobidle", "Multiblood Adventure Idle", "0.0.69")]
    public class MultiplayerPatchPlugin : BaseUnityPlugin
    {

        private WebSocket ws;
        private bool isConnected = false;
        public static Vector3 curentPosition;
        public static Vector3 desiredPosition;
        private Coroutine positionCoroutine;

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
            ws.Connect();
            Harmony.CreateAndPatchAll(typeof(GetDesiredPositionPatch));

            if (ws == null) { Debug.Log("WS NULL"); };
            Debug.Log("Fully woken up");
        }

        private void Start()
        {
            if (positionCoroutine == null) positionCoroutine = StartCoroutine(GetPositionEnumerator());

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


        IEnumerator GetPositionEnumerator()
        {
            while (true)
            {
                CharacterData data = new CharacterData();

                if (SteamClient.IsValid)
                {
                    data.name = SteamClient.Name;
                }
                if (curentPosition == null || desiredPosition == null) yield break;

                data.desiredPosition = desiredPosition;
                Debug.Log(desiredPosition);
                data.curentPosition = curentPosition;
                Debug.Log(curentPosition);

                object[] flat = new object[]

                {
                    data.curentPosition.x,
                    data.curentPosition.y,
                    data.curentPosition.z,
                    data.name,
                    data.desiredPosition.x,
                    data.desiredPosition.y,
                    data.desiredPosition.z
                };

                string json = JsonConvert.SerializeObject(flat);

                ws.Send(json);
                Debug.Log("Hey shithead");
                yield return new WaitForSeconds(30f);
            }

        }

        void OnDestroy()
        {
            if (ws != null && ws.IsAlive)
                ws.Close();
        }

    }

    public class CharacterData
    {
        public string name { get; set; }

        public Vector3 curentPosition { get; set; }

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
}

