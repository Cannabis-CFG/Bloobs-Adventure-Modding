using System;
using System.Globalization;
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

    public sealed class ChatUiMessage
    {
        public ChatMessageKind Kind;
        public string TimestampUtc;
        public string DisplayName;
        public string OtherPartyName;
        public string FromSteamId;
        public string ToSteamId;
        public string Message;
        public string ActiveTitle;
        public string OtherPartyTitle;
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

    public sealed class PrivateTabInfo
    {
        public string SteamId;
        public string Name;
        public bool HasUnread;
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

        private static string ClampBubbleText(string text, int max = 60)
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
                transform.rotation = Quaternion.LookRotation(transform.position - _cam.transform.position);

            if (Time.unscaledTime > _until && gameObject.activeSelf)
                gameObject.SetActive(false);
        }
    }
}
