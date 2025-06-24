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
        //public static Vector3 startPosition;
        //public static Vector3 desiredPosition;

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
            ws.ConnectAsync();
            ////Harmony.CreateAndPatchAll(typeof(GetStartPositionPatch));
            ////Harmony.CreateAndPatchAll(typeof(GetDesiredPositionPatch));
            //StartCoroutine(GetPositionEnumerator());

            if (ws == null) { Debug.Log("WS NULL"); };
            Debug.Log("Fully woken up");
        }

        private void Start()
        {
            //StartCoroutine(GetPositionEnumerator());
        }

        IEnumerator GetPositionEnumerator()
        {
            while (true)
            {
                //CharacterData data = new CharacterData();

                //if (SteamClient.IsValid)
                //{
                //    data.name = SteamClient.Name;
                //}
                //if (startPosition == null || desiredPosition == null) yield break;

                //data.desiredPosition = desiredPosition;
                //data.startPosition = startPosition;

                //string json = JsonConvert.SerializeObject(data);

                //ws.Send(json);
                Debug.Log("Hey shithead");
                yield return new WaitForSeconds(2f);
            }



        }

        void OnDestroy()
        {
            //if (ws != null && ws.IsAlive)
            //    ws.Close();
        }

    }

    /*public class CharacterData
    {
        public string name { get; set; }

        public Vector3 startPosition { get; set; }

        public Vector3 desiredPosition { get; set; }
    }

    [HarmonyPatch(typeof(CharacterMovement), nameof(CharacterMovement.GetStartPosition))]
        public static class GetStartPositionPatch
        {
            [HarmonyPostfix]
            public static void GetStartPosition(ref Vector3 __result)
            {
                MultiplayerPatchPlugin.startPosition = __result;
            }

        }

        [HarmonyPatch(typeof(CharacterMovement), nameof(CharacterMovement.MoveTo))]
        public static class GetDesiredPositionPatch
        {
            [HarmonyPostfix]
            public static void GetDesiredPosition(ref Vector2 __result)
            {
                MultiplayerPatchPlugin.desiredPosition = new Vector3(__result.x, __result.y);
            }
        }*/
}

