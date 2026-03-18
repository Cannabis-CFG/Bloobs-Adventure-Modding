using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Multi_bloob_adventure_idle
{
    public class ChatInputOverlay : MonoBehaviour
    {
        public static ChatInputOverlay Instance { get; private set; }

        private Canvas _canvas;
        private RectTransform _rootRect;
        private Image _background;
        private TMP_InputField _inputField;
        private TextMeshProUGUI _text;
        private RectTransform _textRect;

        public Action<string> OnSubmit;

        public static ChatInputOverlay Create()
        {
            if (Instance != null)
                return Instance;

            var go = new GameObject("ChatInputOverlay");
            DontDestroyOnLoad(go);
            Instance = go.AddComponent<ChatInputOverlay>();
            Instance.Build();
            return Instance;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Build()
        {
            _canvas = gameObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 32001;

            gameObject.AddComponent<GraphicRaycaster>();

            var root = new GameObject("InputRoot");
            root.transform.SetParent(transform, false);

            _rootRect = root.AddComponent<RectTransform>();
            _background = root.AddComponent<Image>();
            _background.color = new Color(0.15f, 0.15f, 0.15f, 0.35f);

            _inputField = root.AddComponent<TMP_InputField>();
            _inputField.lineType = TMP_InputField.LineType.SingleLine;
            _inputField.richText = false;
            _inputField.characterLimit = 220;

            var textObj = new GameObject("Text");
            textObj.transform.SetParent(root.transform, false);

            _textRect = textObj.AddComponent<RectTransform>();
            _textRect.anchorMin = new Vector2(0f, 0f);
            _textRect.anchorMax = new Vector2(1f, 1f);
            _textRect.offsetMin = new Vector2(8f, 4f);
            _textRect.offsetMax = new Vector2(-8f, -4f);

            _text = textObj.AddComponent<TextMeshProUGUI>();
            _text.fontSize = 18f;
            _text.color = new Color(1,1,1,1);
            _text.enableWordWrapping = true;
            _text.alignment = TextAlignmentOptions.Left;

            _inputField.textViewport = _rootRect;
            _inputField.textComponent = _text;

            _inputField.onSubmit.AddListener(HandleSubmit);

            SetVisible(false);
        }

        private void HandleSubmit(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return;

            OnSubmit?.Invoke(value);
        }

        public void SetVisible(bool visible)
        {
            if (_canvas != null)
                _canvas.enabled = visible;
        }

        public void SetText(string value)
        {
            if (_inputField != null)
                _inputField.text = value ?? "";
        }

        public string GetText()
        {
            return _inputField != null ? _inputField.text : "";
        }

        public void Clear()
        {
            if (_inputField != null)
                _inputField.text = "";
        }

        public void Focus()
        {
            if (_inputField == null)
                return;

            _inputField.ActivateInputField();
            _inputField.Select();
            _inputField.caretPosition = _inputField.text?.Length ?? 0;
        }

        public void Unfocus()
        {
            if (_inputField == null)
                return;

            _inputField.DeactivateInputField();
        }

        public bool HasFocus()
        {
            return _inputField != null && _inputField.isFocused;
        }

        public void SetScreenRect(Rect rect)
        {
            if (_rootRect == null)
                return;

            float x = rect.x + 8f;
            float y = Screen.height - rect.y - rect.height + 40f;
            float width = Mathf.Max(200f, rect.width - 16f);
            float height = 28f;

            _rootRect.anchorMin = new Vector2(0f, 0f);
            _rootRect.anchorMax = new Vector2(0f, 0f);
            _rootRect.pivot = new Vector2(0f, 0f);
            _rootRect.anchoredPosition = new Vector2(x, y);
            _rootRect.sizeDelta = new Vector2(width - 16f, height);
        }
    }
}