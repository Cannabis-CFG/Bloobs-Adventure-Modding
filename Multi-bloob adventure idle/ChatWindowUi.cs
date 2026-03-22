using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Multi_bloob_adventure_idle
{
    public class ChatWindowUi : MonoBehaviour
    {
        private const bool DebugHitboxes = false;
        private const float HeaderHeight = 38f;
        private const float ButtonHeight = 34f;
        private const float ResizeHandleSize = 28f;
        private const float MessageScrollSensitivity = 32f;
        private const float PrivateTabsScrollSensitivity = 24f;

        private const float GlobalTabWidth = 110f;
        private const float PrivateTabWidth = 110f;
        private const float SystemTabWidth = 110f;
        private const float TitleButtonWidth = 190f;
        private const float PinButtonWidth = 72f;
        private const float HeaderSpacing = 6f;
        private const float WindowSidePadding = 16f;
        private const float ComposerHeight = 54f;
        private const float InputHeight = 46f;
        private const float TitleMenuHeight = 220f;
        private const float TitleMenuWidth = 220f;

        private ChatSystem _chat;
        private ChatThemeSettings _theme;

        private Canvas _canvas;
        private RectTransform _windowRoot;
        private Image _windowBackground;
        private Outline _windowBorder;

        private Button _globalTabButton;
        private Button _privateTabButton;
        private Button _systemTabButton;
        private TextMeshProUGUI _globalTabText;
        private TextMeshProUGUI _privateTabText;
        private TextMeshProUGUI _systemTabText;

        private Button _titleButton;
        private TextMeshProUGUI _titleButtonText;
        private GameObject _titleMenuRoot;
        private ScrollRect _titleMenuScroll;
        private RectTransform _titleMenuContent;
        private bool _titleMenuOpen;

        private Button _pinButton;
        private TextMeshProUGUI _pinButtonText;

        private GameObject _privateTabsRow;
        private RectTransform _privateTabsContent;
        private Button _pmClearButton;
        private Button _pmCloseButton;

        private ScrollRect _messageScroll;
        private RectTransform _messageContent;
        private bool _autoScrollEnabled = true;
        private bool _suppressScrollEvents;
        private float _lastMessageContentHeight;

        private TMP_InputField _inputField;
        private Outline _inputOutline;
        private TextMeshProUGUI _inputText;
        private TextMeshProUGUI _charCounterText;
        private TextMeshProUGUI _statusText;
        private TextMeshProUGUI _inputHintText;

        private int _lastSubmitFrame = -1;

        public ChatTab ActiveTab { get; set; } = ChatTab.Global;

        public string CurrentInputText
        {
            get => _inputField != null ? _inputField.text : "";
            set
            {
                if (_inputField != null)
                    _inputField.text = value ?? "";
                UpdateInputPlaceholderState();
                UpdateInputVisualState();
            }
        }

        public bool IsInputFocused => _inputField != null && _inputField.isFocused;

        public void Initialize(ChatSystem chat, ChatThemeSettings theme)
        {
            _chat = chat;
            _theme = theme;

            EnsureEventSystem();
            BuildUi();
            ApplyTheme();
            SetVisible(_theme.WindowVisible.Value);
        }

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
        }

        private void Update()
        {
            if (IsInputFocused && (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)))
                TrySubmitInput();

            if (_titleMenuOpen && Input.GetMouseButtonDown(0) && !IsPointerOverRect(_titleMenuRoot) && !IsPointerOverRect(_titleButton.gameObject))
                SetTitleMenuOpen(false);

            UpdateInputPlaceholderState();
            UpdateInputVisualState();
        }

        public void SetVisible(bool visible)
        {
            if (_canvas != null)
                _canvas.enabled = visible;

            if (!visible)
                SetTitleMenuOpen(false);
        }

        public void FocusInput()
        {
            if (_inputField == null)
                return;

            _inputField.ActivateInputField();
            _inputField.Select();
            _inputField.caretPosition = _inputField.text?.Length ?? 0;
            UpdateInputPlaceholderState();
            UpdateInputVisualState();
        }

        public void BlurInput()
        {
            if (_inputField != null)
                _inputField.DeactivateInputField();

            UpdateInputPlaceholderState();
            UpdateInputVisualState();
        }

        public bool IsPointerOverChatWindow()
        {
            if (_canvas == null || !_canvas.enabled || _windowRoot == null)
                return false;

            return RectTransformUtility.RectangleContainsScreenPoint(_windowRoot, Input.mousePosition, null) ||
                   IsPointerOverRect(_titleMenuRoot);
        }

        public void Refresh()
        {
            SetVisible(_theme.WindowVisible.Value);
            ApplyTheme();
            RebuildHeader();
            RebuildPrivateTabs();
            RebuildTitleMenu();
            RebuildMessages();
            UpdateInputPlaceholderState();
            UpdateInputVisualState();

            if (!string.IsNullOrWhiteSpace(_chat.StatusLine))
            {
                _statusText.gameObject.SetActive(true);
                _statusText.text = _chat.StatusLine;
            }
            else
            {
                _statusText.gameObject.SetActive(false);
            }
        }

        public void ForceEnableAutoScroll()
        {
            _autoScrollEnabled = true;
            ScrollMessagesToBottom();
        }

        private void EnsureEventSystem()
        {
            if (FindObjectOfType<EventSystem>() != null)
                return;

            var go = new GameObject("ChatUiEventSystem");
            DontDestroyOnLoad(go);
            go.AddComponent<EventSystem>();
            go.AddComponent<StandaloneInputModule>();
        }

        private void BuildUi()
        {
            _canvas = gameObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 32000;
            gameObject.AddComponent<GraphicRaycaster>();

            _windowRoot = CreateRect("WindowRoot", transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f));
            _windowBackground = _windowRoot.gameObject.AddComponent<Image>();
            _windowBorder = _windowRoot.gameObject.AddComponent<Outline>();

            var rootLayout = _windowRoot.gameObject.AddComponent<VerticalLayoutGroup>();
            rootLayout.padding = new RectOffset(8, 8, 8, 8);
            rootLayout.spacing = 6;
            rootLayout.childForceExpandHeight = false;
            rootLayout.childForceExpandWidth = true;

            var dragHandle = _windowRoot.gameObject.AddComponent<ChatWindowDragHandle>();
            dragHandle.Initialize(this);

            BuildHeaderRow();
            BuildPrivateTabsRow();
            BuildMessagesArea();
            BuildComposerRow();
            BuildStatusRow();
            BuildTitleMenu();

            var resizeHandle = CreateRect("ResizeHandle", _windowRoot, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f));
            var resizeLayout = resizeHandle.gameObject.AddComponent<LayoutElement>();
            resizeLayout.ignoreLayout = true;
            resizeHandle.sizeDelta = new Vector2(ResizeHandleSize, ResizeHandleSize);
            resizeHandle.anchoredPosition = new Vector2(-6f, 6f);

            var resizeImage = resizeHandle.gameObject.AddComponent<Image>();
            resizeImage.raycastTarget = true;
            resizeImage.color = DebugHitboxes ? new Color(1f, 0f, 1f, 0.22f) : new Color(1f, 1f, 1f, 0.08f);

            var resizeText = CreateTmpText("ResizeText", resizeHandle, "//", 14f, TextAlignmentOptions.Center);
            resizeText.enableWordWrapping = false;
            resizeText.overflowMode = TextOverflowModes.Overflow;
            Stretch(resizeText.rectTransform);

            var resize = resizeHandle.gameObject.AddComponent<ChatWindowResizeHandle>();
            resize.Initialize(this);

            ApplyWindowRect(_theme.GetWindowRect());
        }

        private void BuildHeaderRow()
        {
            var header = CreateRow("HeaderRow", _windowRoot);
            SetRowHeight(header, HeaderHeight);

            (_globalTabButton, _globalTabText) = CreateHeaderButton("GlobalTab", header, "Global", GlobalTabWidth);
            _globalTabButton.onClick.AddListener(() => _chat.SetActiveTab(ChatTab.Global));

            (_privateTabButton, _privateTabText) = CreateHeaderButton("PrivateTab", header, "Private", PrivateTabWidth);
            _privateTabButton.onClick.AddListener(() => _chat.SetActiveTab(ChatTab.Private));

            (_systemTabButton, _systemTabText) = CreateHeaderButton("SystemTab", header, "System", SystemTabWidth);
            _systemTabButton.onClick.AddListener(() => _chat.SetActiveTab(ChatTab.System));

            _titleButton = CreateButton("TitleButton", header, out _titleButtonText, "Title: None", TitleButtonWidth);
            _titleButton.onClick.AddListener(() => SetTitleMenuOpen(!_titleMenuOpen));

            _pinButton = CreateButton("PinButton", header, out _pinButtonText, "Pin", PinButtonWidth);
            _pinButton.onClick.AddListener(() => _chat.SetPinned(!_chat.IsPinned));
        }

        private void BuildTitleMenu()
        {
            var root = CreateRect("TitleMenuRoot", transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f));
            _titleMenuRoot = root.gameObject;
            _titleMenuRoot.SetActive(false);

            var bg = _titleMenuRoot.AddComponent<Image>();
            bg.color = DebugHitboxes ? new Color(0f, 0f, 1f, 0.22f) : new Color(0.12f, 0.12f, 0.12f, 0.98f);

            var outline = _titleMenuRoot.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.5f);

            var rootLayout = _titleMenuRoot.AddComponent<LayoutElement>();
            rootLayout.ignoreLayout = true;

            var scroll = _titleMenuRoot.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 28f;
            _titleMenuScroll = scroll;

            var viewport = CreateRect("Viewport", root);
            Stretch(viewport);
            var viewportImage = viewport.gameObject.AddComponent<Image>();
            viewportImage.color = new Color(1f, 1f, 1f, DebugHitboxes ? 0.08f : 0.01f);
            viewport.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            scroll.viewport = viewport;

            _titleMenuContent = CreateRect("Content", viewport, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f));
            var contentLayout = _titleMenuContent.gameObject.AddComponent<VerticalLayoutGroup>();
            contentLayout.padding = new RectOffset(4, 4, 4, 4);
            contentLayout.spacing = 4;
            contentLayout.childForceExpandHeight = false;
            contentLayout.childForceExpandWidth = true;

            var fitter = _titleMenuContent.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.content = _titleMenuContent;
        }

        private void BuildPrivateTabsRow()
        {
            _privateTabsRow = CreateRow("PrivateTabsRow", _windowRoot).gameObject;
            SetRowHeight(_privateTabsRow.GetComponent<RectTransform>(), HeaderHeight);

            var scrollRoot = CreateRect("PrivateTabsScrollRoot", _privateTabsRow.transform);
            var scrollRootLayout = scrollRoot.gameObject.AddComponent<LayoutElement>();
            scrollRootLayout.preferredHeight = HeaderHeight;
            scrollRootLayout.minHeight = HeaderHeight;
            scrollRootLayout.flexibleWidth = 1f;

            var scrollImage = scrollRoot.gameObject.AddComponent<Image>();
            scrollImage.color = DebugHitboxes ? new Color(0f, 0.8f, 1f, 0.18f) : new Color(0f, 0f, 0f, 0.15f);

            var scroll = scrollRoot.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = true;
            scroll.vertical = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = PrivateTabsScrollSensitivity;

            var viewport = CreateRect("Viewport", scrollRoot);
            Stretch(viewport);
            var viewportImage = viewport.gameObject.AddComponent<Image>();
            viewportImage.color = DebugHitboxes ? new Color(0f, 1f, 0f, 0.08f) : new Color(1f, 1f, 1f, 0.01f);
            viewport.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            scroll.viewport = viewport;

            _privateTabsContent = CreateRow("Content", viewport);
            var fitter = _privateTabsContent.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;
            scroll.content = _privateTabsContent;

            _pmClearButton = CreateButton("PmClearButton", _privateTabsRow.transform, out _, "Clear", 72f);
            _pmClearButton.onClick.AddListener(() => _chat.ClearSelectedPrivateConversation(false));

            _pmCloseButton = CreateButton("PmCloseButton", _privateTabsRow.transform, out _, "Close PM", 98f);
            _pmCloseButton.onClick.AddListener(() => _chat.ClearSelectedPrivateConversation(true));
        }

        private void BuildMessagesArea()
        {
            var viewport = CreateRect("MessageViewport", _windowRoot);
            var viewportLayout = viewport.gameObject.AddComponent<LayoutElement>();
            viewportLayout.flexibleHeight = 1f;
            viewportLayout.minHeight = 120f;

            var viewportImage = viewport.gameObject.AddComponent<Image>();
            viewportImage.color = DebugHitboxes ? new Color(1f, 1f, 0f, 0.08f) : new Color(0f, 0f, 0f, 0.12f);
            viewport.gameObject.AddComponent<Mask>().showMaskGraphic = false;

            _messageScroll = viewport.gameObject.AddComponent<ScrollRect>();
            _messageScroll.horizontal = false;
            _messageScroll.vertical = true;
            _messageScroll.movementType = ScrollRect.MovementType.Clamped;
            _messageScroll.scrollSensitivity = MessageScrollSensitivity;
            _messageScroll.onValueChanged.AddListener(_ => OnMessageScrollChanged());

            _messageContent = CreateRect("MessageContent", viewport, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f));
            var contentLayout = _messageContent.gameObject.AddComponent<VerticalLayoutGroup>();
            contentLayout.padding = new RectOffset(6, 2, 6, 6);
            contentLayout.spacing = 4;
            contentLayout.childForceExpandHeight = false;
            contentLayout.childForceExpandWidth = true;

            var contentFit = _messageContent.gameObject.AddComponent<ContentSizeFitter>();
            contentFit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            contentFit.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

            _messageScroll.viewport = viewport;
            _messageScroll.content = _messageContent;
        }

        private void BuildComposerRow()
        {
            var composer = CreateRow("ComposerRow", _windowRoot);
            SetRowHeight(composer, ComposerHeight);

            var inputRoot = CreateRect("InputRoot", composer);
            var inputLayout = inputRoot.gameObject.AddComponent<LayoutElement>();
            inputLayout.flexibleWidth = 1f;
            inputLayout.preferredHeight = InputHeight;
            inputLayout.minHeight = InputHeight;

            var inputBg = inputRoot.gameObject.AddComponent<Image>();
            inputBg.color = DebugHitboxes ? new Color(0f, 1f, 1f, 0.15f) : Color.white;
            _inputOutline = inputRoot.gameObject.AddComponent<Outline>();
            _inputOutline.effectDistance = new Vector2(1f, -1f);

            _inputField = inputRoot.gameObject.AddComponent<TMP_InputField>();
            _inputField.lineType = TMP_InputField.LineType.MultiLineSubmit;
            _inputField.richText = false;
            _inputField.characterLimit = 220;
            _inputField.onSubmit.AddListener(_ => TrySubmitInput());
            _inputField.onSelect.AddListener(_ => UpdateInputPlaceholderState());
            _inputField.onDeselect.AddListener(_ => UpdateInputPlaceholderState());
            _inputField.onValueChanged.AddListener(_ =>
            {
                UpdateInputPlaceholderState();
                UpdateInputVisualState();
            });

            var textViewport = CreateRect("TextViewport", inputRoot);
            Stretch(textViewport);
            var textViewportImage = textViewport.gameObject.AddComponent<Image>();
            textViewportImage.color = new Color(1f, 1f, 1f, 0f);

            var textRect = CreateRect("Text", textViewport);
            Stretch(textRect, new Vector2(8f, 4f), new Vector2(-8f, -4f));
            _inputText = textRect.gameObject.AddComponent<TextMeshProUGUI>();
            _inputText.enableWordWrapping = true;
            _inputText.overflowMode = TextOverflowModes.Overflow;
            _inputText.alignment = TextAlignmentOptions.TopLeft;

            _inputField.textViewport = textViewport;
            _inputField.textComponent = _inputText;

            var placeholderRect = CreateRect("Placeholder", textViewport);
            Stretch(placeholderRect, new Vector2(8f, 4f), new Vector2(-8f, -4f));
            _inputHintText = placeholderRect.gameObject.AddComponent<TextMeshProUGUI>();
            _inputHintText.text = "Enter to type, / for command, /help for commands";
            _inputHintText.enableWordWrapping = false;
            _inputHintText.overflowMode = TextOverflowModes.Ellipsis;
            _inputHintText.alignment = TextAlignmentOptions.TopLeft;
            _inputHintText.color = new Color(1f, 1f, 1f, 0.45f);
            _inputHintText.raycastTarget = false;
            _inputField.placeholder = _inputHintText;

            var counterRect = CreateRect("CharCounter", composer);
            var counterLayout = counterRect.gameObject.AddComponent<LayoutElement>();
            counterLayout.preferredWidth = 62f;
            counterLayout.minWidth = 62f;
            counterLayout.preferredHeight = InputHeight;
            counterLayout.minHeight = InputHeight;

            _charCounterText = counterRect.gameObject.AddComponent<TextMeshProUGUI>();
            _charCounterText.alignment = TextAlignmentOptions.MidlineRight;
            _charCounterText.enableWordWrapping = false;
            _charCounterText.overflowMode = TextOverflowModes.Overflow;
            _charCounterText.raycastTarget = false;
        }

        private void BuildStatusRow()
        {
            var status = CreateRect("StatusRow", _windowRoot);
            var statusLayout = status.gameObject.AddComponent<LayoutElement>();
            statusLayout.preferredHeight = 22f;
            statusLayout.minHeight = 22f;

            _statusText = status.gameObject.AddComponent<TextMeshProUGUI>();
            _statusText.alignment = TextAlignmentOptions.Left;
            _statusText.enableWordWrapping = false;
        }

        private void ApplyTheme()
        {
            _windowBackground.color = _theme.GetBackgroundColor();
            _windowBorder.effectColor = _theme.GetBorderColor();
            _windowRoot.localScale = Vector3.one * Mathf.Clamp(_theme.UiScale.Value, 0.75f, 2f);

            float fontSize = _theme.GetScaledFontSize();
            _globalTabText.fontSize = fontSize;
            _privateTabText.fontSize = fontSize;
            _systemTabText.fontSize = fontSize;
            _titleButtonText.fontSize = fontSize;
            _pinButtonText.fontSize = fontSize - 2f;
            _inputText.fontSize = fontSize;
            _inputText.color = _theme.GetInputTextColor();
            _inputHintText.fontSize = fontSize;
            _statusText.fontSize = fontSize - 3f;
            _statusText.color = _theme.GetTimestampColor();
            if (_charCounterText != null)
            {
                _charCounterText.fontSize = fontSize - 3f;
                _charCounterText.color = _theme.GetTimestampColor();
            }

            var inputBg = _inputField.GetComponent<Image>();
            if (inputBg != null)
                inputBg.color = DebugHitboxes ? new Color(0f, 1f, 1f, 0.15f) : _theme.GetInputBackgroundColor();

            ApplyWindowRect(_theme.GetWindowRect());
            PositionTitleMenu();
        }

        private void RebuildHeader()
        {
            _globalTabText.text = _chat.BuildTabLabel(ChatTab.Global);
            _privateTabText.text = _chat.BuildTabLabel(ChatTab.Private);
            _systemTabText.text = _chat.BuildTabLabel(ChatTab.System);

            string titleLabel = "Title: None";
            foreach (var title in _chat.GetUnlockedTitles())
            {
                if (title.id == _chat.ActiveTitleId)
                {
                    titleLabel = $"Title: {title.label}";
                    break;
                }
            }
            _titleButtonText.text = titleLabel;

            _pinButtonText.text = _chat.IsPinned ? "Unpin" : "Pin";

            SetButtonSelected(_globalTabButton, _chat.CurrentTab == ChatTab.Global);
            SetButtonSelected(_privateTabButton, _chat.CurrentTab == ChatTab.Private);
            SetButtonSelected(_systemTabButton, _chat.CurrentTab == ChatTab.System);
            SetButtonSelected(_titleButton, _titleMenuOpen);
        }

        private void RebuildPrivateTabs()
        {
            bool show = _chat.CurrentTab == ChatTab.Private;
            _privateTabsRow.SetActive(show);
            if (!show)
                return;

            foreach (Transform child in _privateTabsContent)
                Destroy(child.gameObject);

            foreach (var tab in _chat.GetPrivateTabs())
            {
                string label = tab.HasUnread ? $"{tab.Name} (*)" : tab.Name;
                var button = CreateButton("PmTab_" + tab.SteamId, _privateTabsContent, out _, label, Mathf.Max(120f, label.Length * 9f));
                SetButtonSelected(button, _chat.SelectedPrivateSteamId == tab.SteamId);
                string sid = tab.SteamId;
                button.onClick.AddListener(() => _chat.SelectPrivateTab(sid));
            }

            bool hasSelection = !string.IsNullOrWhiteSpace(_chat.SelectedPrivateSteamId);
            _pmClearButton.interactable = hasSelection;
            _pmCloseButton.interactable = hasSelection;
        }

        private void RebuildTitleMenu()
        {
            foreach (Transform child in _titleMenuContent)
                Destroy(child.gameObject);

            var noneButton = CreateButton("Title_None", _titleMenuContent, out _, "Title: None", TitleMenuWidth - 16f);
            noneButton.onClick.AddListener(() =>
            {
                _chat.ClearActiveTitle();
                SetTitleMenuOpen(false);
            });

            foreach (var title in _chat.GetUnlockedTitles())
            {
                var button = CreateButton("Title_" + title.id, _titleMenuContent, out _, title.label, TitleMenuWidth - 16f);
                string titleId = title.id;
                button.onClick.AddListener(() =>
                {
                    _chat.SelectTitle(titleId);
                    SetTitleMenuOpen(false);
                });

                if (title.id == _chat.ActiveTitleId)
                    SetButtonSelected(button, true);
            }

            PositionTitleMenu();
        }

        private void UpdateInputPlaceholderState()
        {
            if (_inputHintText == null || _inputField == null)
                return;

            _inputHintText.gameObject.SetActive(string.IsNullOrEmpty(_inputField.text) && !IsInputFocused);
        }

        private void UpdateInputVisualState()
        {
            if (_inputField == null || _charCounterText == null || _inputOutline == null)
                return;

            int current = _inputField.text != null ? _inputField.text.Length : 0;
            int max = _inputField.characterLimit > 0 ? _inputField.characterLimit : 0;

            _charCounterText.text = max > 0 ? $"{current}/{max}" : current.ToString();
            _charCounterText.color = (max > 0 && current >= max)
                ? new Color(1f, 0.35f, 0.35f, 1f)
                : _theme.GetTimestampColor();

            _inputOutline.effectColor = (max > 0 && current >= max)
                ? new Color(1f, 0.2f, 0.2f, 0.95f)
                : (DebugHitboxes ? new Color(0f, 1f, 1f, 0.45f) : new Color(0f, 0f, 0f, 0.55f));
        }

        private void SetTitleMenuOpen(bool open)
        {
            _titleMenuOpen = open;
            if (_titleMenuRoot != null)
                _titleMenuRoot.SetActive(open);

            if (open)
            {
                RebuildTitleMenu();
                PositionTitleMenu();
            }

            RebuildHeader();
        }

        private void PositionTitleMenu()
        {
            if (_titleMenuRoot == null || _titleButton == null)
                return;

            var rootRt = _titleMenuRoot.GetComponent<RectTransform>();
            var titleRt = _titleButton.GetComponent<RectTransform>();
            Vector3[] corners = new Vector3[4];
            titleRt.GetWorldCorners(corners);

            float x = corners[0].x;
            float y = Screen.height - corners[0].y + 2f;

            x = Mathf.Clamp(x, 0f, Mathf.Max(0f, Screen.width - TitleMenuWidth));
            y = Mathf.Clamp(y, 0f, Mathf.Max(0f, Screen.height - TitleMenuHeight));

            rootRt.sizeDelta = new Vector2(TitleMenuWidth, TitleMenuHeight);
            rootRt.anchoredPosition = new Vector2(x, -y);
        }

        private bool IsPointerOverRect(GameObject go)
        {
            if (go == null || !go.activeInHierarchy)
                return false;

            var rt = go.GetComponent<RectTransform>();
            return rt != null && RectTransformUtility.RectangleContainsScreenPoint(rt, Input.mousePosition, null);
        }

        private void RebuildMessages()
        {
            foreach (Transform child in _messageContent)
                Destroy(child.gameObject);

            Canvas.ForceUpdateCanvases();

            float viewportWidth = _messageScroll.viewport.rect.width;
            float leftPadding = 6f;
            float rightPadding = 6f;
            float topPadding = 4f;
            float bottomPadding = 4f;
            float innerWidth = Mathf.Max(50f, viewportWidth - leftPadding - rightPadding);

            foreach (var msg in _chat.GetCurrentTabMessages())
            {
                var row = CreateRect("MessageRow", _messageContent).gameObject;
                var rowRt = row.GetComponent<RectTransform>();

                var rowImage = row.AddComponent<Image>();
                rowImage.color = GetMessageRowColor(msg);

                var rowLayout = row.AddComponent<LayoutElement>();
                rowLayout.minHeight = 32f;

                string headerText = BuildHeaderText(msg);
                string bodyText = BuildBodyText(msg);

                float headerFontSize = _theme.GetScaledFontSize() - 2f;
                float bodyFontSize = _theme.GetScaledFontSize() - 1f;

                // Header viewport
                var headerViewport = CreateRect("HeaderViewport", row.transform);
                headerViewport.anchorMin = new Vector2(0f, 1f);
                headerViewport.anchorMax = new Vector2(0f, 1f);
                headerViewport.pivot = new Vector2(0f, 1f);
                headerViewport.anchoredPosition = new Vector2(leftPadding, -topPadding);
                headerViewport.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, innerWidth);

                const float headerLeftInset = 38f;
                const float headerRightInset = 2f;

                var headerContent = CreateRect("HeaderContent", headerViewport);
                headerContent.anchorMin = new Vector2(0f, 1f);
                headerContent.anchorMax = new Vector2(0f, 1f);
                headerContent.pivot = new Vector2(0f, 1f);
                headerContent.anchoredPosition = new Vector2(headerLeftInset, 0f);

                float headerUsableWidth = Mathf.Max(20f, innerWidth - headerLeftInset - headerRightInset);
                headerContent.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, headerUsableWidth);

                var header = CreateTmpText("HeaderText", headerContent, headerText, headerFontSize, TextAlignmentOptions.TopLeft);
                header.richText = true;
                header.enableWordWrapping = false;
                header.overflowMode = TextOverflowModes.Ellipsis;
                header.color = GetHeaderTextColor(msg);
                header.margin = Vector4.zero;
                header.isTextObjectScaleStatic = true;

                var headerRt = header.rectTransform;
                headerRt.anchorMin = new Vector2(0f, 1f);
                headerRt.anchorMax = new Vector2(0f, 1f);
                headerRt.pivot = new Vector2(0f, 1f);
                headerRt.anchoredPosition = Vector2.zero;
                headerRt.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, headerUsableWidth);

                header.ForceMeshUpdate();
                Vector2 headerPreferred = header.GetPreferredValues(headerText, headerUsableWidth, 100f);

                headerRt.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, headerPreferred.y);
                headerContent.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, headerPreferred.y);
                headerViewport.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, headerPreferred.y);

                // Body viewport
                var bodyViewport = CreateRect("BodyViewport", row.transform);
                bodyViewport.anchorMin = new Vector2(0f, 1f);
                bodyViewport.anchorMax = new Vector2(0f, 1f);
                bodyViewport.pivot = new Vector2(0f, 1f);

                float bodyY = topPadding + headerPreferred.y + 2f;
                bodyViewport.anchoredPosition = new Vector2(leftPadding, -bodyY);
                bodyViewport.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, innerWidth);

                const float bodyLeftInset = 38f;
                const float bodyRightInset = 2f;

                var bodyContent = CreateRect("BodyContent", bodyViewport);
                bodyContent.anchorMin = new Vector2(0f, 1f);
                bodyContent.anchorMax = new Vector2(0f, 1f);
                bodyContent.pivot = new Vector2(0f, 1f);
                bodyContent.anchoredPosition = new Vector2(bodyLeftInset, 0f);

                float bodyUsableWidth = Mathf.Max(20f, innerWidth - bodyLeftInset - bodyRightInset);
                bodyContent.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, bodyUsableWidth);

                var body = CreateTmpText("BodyText", bodyContent, bodyText ?? "", bodyFontSize, TextAlignmentOptions.TopLeft);
                body.richText = false;
                body.enableWordWrapping = true;
                body.overflowMode = TextOverflowModes.Overflow;
                body.color = GetMessageTextColor(msg);
                body.margin = Vector4.zero;
                body.isTextObjectScaleStatic = true;

                body.rectTransform.anchorMin = new Vector2(0f, 1f);
                body.rectTransform.anchorMax = new Vector2(0f, 1f);
                body.rectTransform.pivot = new Vector2(0f, 1f);
                body.rectTransform.anchoredPosition = Vector2.zero;
                body.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, bodyUsableWidth);

                body.ForceMeshUpdate();
                Vector2 bodyPreferred = body.GetPreferredValues(bodyText ?? "", bodyUsableWidth, 10000f);

                body.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, bodyPreferred.y);
                bodyContent.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, bodyPreferred.y);
                bodyViewport.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, bodyPreferred.y);

                float finalHeight = Mathf.Max(
                    32f,
                    topPadding + headerPreferred.y + 2f + bodyPreferred.y + bottomPadding
                );

                rowLayout.preferredHeight = finalHeight;
                rowLayout.minHeight = finalHeight;

                LayoutRebuilder.ForceRebuildLayoutImmediate(bodyViewport);
                LayoutRebuilder.ForceRebuildLayoutImmediate(rowRt);
            }

            Canvas.ForceUpdateCanvases();

            float newHeight = _messageContent.rect.height;
            bool contentGrew = newHeight > _lastMessageContentHeight + 0.5f;
            _lastMessageContentHeight = newHeight;

            if (_autoScrollEnabled || (contentGrew && IsScrolledToBottom(0.05f)))
                ScrollMessagesToBottom();
        }

        private void OnMessageScrollChanged()
        {
            if (_suppressScrollEvents || _messageScroll == null)
                return;

            if (IsScrolledToBottom())
            {
                _autoScrollEnabled = true;
                return;
            }

            _autoScrollEnabled = false;
        }

        private bool IsScrolledToBottom(float threshold = 0.001f)
        {
            if (_messageScroll == null)
                return true;

            return _messageScroll.verticalNormalizedPosition <= threshold;
        }

        private void ScrollMessagesToBottom()
        {
            if (_messageScroll == null)
                return;

            _suppressScrollEvents = true;
            Canvas.ForceUpdateCanvases();
            _messageScroll.verticalNormalizedPosition = 0f;
            _suppressScrollEvents = false;
        }

        private void TrySubmitInput()
        {
            if (Time.frameCount == _lastSubmitFrame)
                return;

            _lastSubmitFrame = Time.frameCount;
            _chat.SubmitChat(CurrentInputText);
        }

        private void SetButtonSelected(Button button, bool selected)
        {
            var image = button.GetComponent<Image>();
            if (image == null)
                return;

            if (selected)
                image.color = DebugHitboxes ? new Color(1f, 0.5f, 0f, 0.45f) : _theme.GetAccentColor();
            else
                image.color = DebugHitboxes ? new Color(1f, 0f, 0f, 0.22f) : new Color(0.18f, 0.18f, 0.18f, 1f);
        }

        private string BuildHeaderText(ChatUiMessage msg)
        {
            string timestampHex = ColorUtility.ToHtmlStringRGB(_theme.GetTimestampColor());
            string prefix = $"<color=#{timestampHex}>[{msg.LocalTimeString}]</color> ";

            string displayName = MultiplayerPatchPlugin.GetPlayerNameFromSteamId(msg.FromSteamId, msg.DisplayName);
            string otherName = MultiplayerPatchPlugin.GetPlayerNameFromSteamId(msg.ToSteamId, msg.OtherPartyName);

            string titledDisplayName = string.IsNullOrWhiteSpace(msg.ActiveTitle)
                ? displayName
                : $"[{msg.ActiveTitle}] {displayName}";

            string titledOtherName = string.IsNullOrWhiteSpace(msg.OtherPartyTitle)
                ? otherName
                : $"[{msg.OtherPartyTitle}] {otherName}";

            return msg.Kind switch
            {
                ChatMessageKind.Global => $"{prefix}{titledDisplayName}",
                ChatMessageKind.Private when msg.IsOutgoingPrivate => $"{prefix}[To {titledOtherName}]",
                ChatMessageKind.Private when msg.IsIncomingPrivate => $"{prefix}[From {titledDisplayName}]",
                ChatMessageKind.SystemRegular => $"{prefix}[SERVER]",
                ChatMessageKind.SystemImportant => $"{prefix}[IMPORTANT]",
                ChatMessageKind.SystemCritical => $"{prefix}[CRITICAL]",
                ChatMessageKind.Error => $"{prefix}[ERROR]",
                _ => $"{prefix}{titledDisplayName}"
            };
        }

        private string BuildBodyText(ChatUiMessage msg)
        {
            return $"{msg.Message}" ?? "";
        }

        private Color GetHeaderTextColor(ChatUiMessage msg)
        {
            return msg.Kind switch
            {
                ChatMessageKind.SystemCritical => new Color(1f, 0.86f, 0.86f, 1f),
                ChatMessageKind.Error => new Color(1f, 0.82f, 0.82f, 1f),
                _ => Color.white
            };
        }

        private static Color GetMessageRowColor(ChatUiMessage msg)
        {
            return msg.Kind switch
            {
                ChatMessageKind.SystemImportant => new Color(0.38f, 0.28f, 0.02f, 0.55f),
                ChatMessageKind.SystemCritical => new Color(0.42f, 0.08f, 0.08f, 0.62f),
                ChatMessageKind.Error => new Color(0.35f, 0.1f, 0.1f, 0.62f),
                ChatMessageKind.Private => new Color(0.08f, 0.14f, 0.32f, 0.38f),
                _ => new Color(1f, 1f, 1f, 0.04f)
            };
        }

        private static Color GetMessageTextColor(ChatUiMessage msg)
        {
            return msg.Kind switch
            {
                ChatMessageKind.SystemCritical => new Color(1f, 0.86f, 0.86f, 1f),
                ChatMessageKind.Error => new Color(1f, 0.82f, 0.82f, 1f),
                _ => Color.white
            };
        }

        private void ApplyWindowRect(Rect rect)
        {
            _windowRoot.anchoredPosition = new Vector2(rect.x, -rect.y);
            _windowRoot.sizeDelta = new Vector2(Mathf.Max(GetMinimumWindowWidth(), rect.width), Mathf.Max(260f, rect.height));
        }

        public void MoveWindow(Vector2 delta)
        {
            if (_chat.IsPinned)
                return;

            float scale = Mathf.Clamp(_windowRoot.localScale.x, 0.75f, 2f);

            var rect = _chat.GetWindowRect();
            rect.x += delta.x / scale;
            rect.y -= delta.y / scale;
            rect = ClampToScreen(rect);

            _chat.SetWindowRect(rect);
            ApplyWindowRect(rect);
            PositionTitleMenu();
        }

        public void ResizeWindow(Vector2 delta)
        {
            float scale = Mathf.Clamp(_windowRoot.localScale.x, 0.75f, 2f);

            var rect = _chat.GetWindowRect();
            float minWidth = GetMinimumWindowWidth();
            float newWidth = Mathf.Max(minWidth, rect.width + (delta.x / scale));
            float newHeight = Mathf.Max(260f, rect.height - (delta.y / scale));

            float maxWidth = Screen.width - rect.x;
            float maxHeight = Screen.height - rect.y;

            rect.width = Mathf.Clamp(newWidth, minWidth, Mathf.Max(minWidth, maxWidth));
            rect.height = Mathf.Clamp(newHeight, 260f, Mathf.Max(260f, maxHeight));

            _chat.SetWindowRect(rect);
            ApplyWindowRect(rect);
            PositionTitleMenu();
        }

        private float GetMinimumWindowWidth()
        {
            return WindowSidePadding + GlobalTabWidth + HeaderSpacing + PrivateTabWidth + HeaderSpacing + SystemTabWidth + HeaderSpacing + TitleButtonWidth + HeaderSpacing + PinButtonWidth + WindowSidePadding;
        }

        private static Rect ClampToScreen(Rect rect)
        {
            float maxX = Mathf.Max(0f, Screen.width - rect.width);
            float maxY = Mathf.Max(0f, Screen.height - rect.height);
            rect.x = Mathf.Clamp(rect.x, 0f, maxX);
            rect.y = Mathf.Clamp(rect.y, 0f, maxY);
            return rect;
        }

        private static RectTransform CreateRect(string name, Transform parent, Vector2? anchorMin = null, Vector2? anchorMax = null, Vector2? pivot = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin ?? new Vector2(0f, 0f);
            rt.anchorMax = anchorMax ?? new Vector2(1f, 1f);
            rt.pivot = pivot ?? new Vector2(0.5f, 0.5f);
            return rt;
        }

        private static void SetRowHeight(RectTransform row, float height)
        {
            var le = row.GetComponent<LayoutElement>();
            if (le != null)
            {
                le.preferredHeight = height;
                le.minHeight = height;
            }
        }

        private static RectTransform CreateRow(string name, Transform parent)
        {
            var row = CreateRect(name, parent);
            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = HeaderSpacing;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = false;
            layout.childAlignment = TextAnchor.MiddleLeft;

            var le = row.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = HeaderHeight;
            le.minHeight = HeaderHeight;
            return row;
        }

        private static (Button button, TextMeshProUGUI text) CreateHeaderButton(string name, Transform parent, string label, float width)
        {
            var button = CreateButton(name, parent, out var text, label, width);
            text.enableWordWrapping = false;
            text.overflowMode = TextOverflowModes.Ellipsis;
            return (button, text);
        }

        private static Button CreateButton(string name, Transform parent, out TextMeshProUGUI text, string label, float width)
        {
            var rt = CreateRect(name, parent);
            rt.sizeDelta = new Vector2(width, ButtonHeight);

            var image = rt.gameObject.AddComponent<Image>();
            image.color = DebugHitboxes ? new Color(1f, 0f, 0f, 0.22f) : new Color(0.18f, 0.18f, 0.18f, 1f);

            var button = rt.gameObject.AddComponent<Button>();

            var layout = rt.gameObject.AddComponent<LayoutElement>();
            layout.preferredWidth = width;
            layout.minWidth = width;
            layout.preferredHeight = ButtonHeight;
            layout.minHeight = ButtonHeight;

            text = CreateTmpText("Text", rt, label, 16f, TextAlignmentOptions.Center);
            text.enableWordWrapping = false;
            text.overflowMode = TextOverflowModes.Ellipsis;
            Stretch(text.rectTransform, new Vector2(6f, 2f), new Vector2(-6f, -2f));

            return button;
        }

        private static TextMeshProUGUI CreateTmpText(string name, Transform parent, string text, float fontSize, TextAlignmentOptions alignment)
        {
            var rt = CreateRect(name, parent, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f));
            var tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.alignment = alignment;
            tmp.color = Color.white;
            tmp.raycastTarget = false;
            tmp.enableAutoSizing = false;
            return tmp;
        }

        private static void Stretch(RectTransform rt, Vector2? offsetMin = null, Vector2? offsetMax = null)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = offsetMin ?? Vector2.zero;
            rt.offsetMax = offsetMax ?? Vector2.zero;
        }
    }

    public sealed class ChatWindowDragHandle : MonoBehaviour, IBeginDragHandler, IDragHandler
    {
        private ChatWindowUi _ui;

        public void Initialize(ChatWindowUi ui)
        {
            _ui = ui;
        }

        public void OnBeginDrag(PointerEventData eventData) { }

        public void OnDrag(PointerEventData eventData)
        {
            if (_ui != null)
                _ui.MoveWindow(eventData.delta);
        }
    }

    public sealed class ChatWindowResizeHandle : MonoBehaviour, IBeginDragHandler, IDragHandler
    {
        private ChatWindowUi _ui;

        public void Initialize(ChatWindowUi ui)
        {
            _ui = ui;
        }

        public void OnBeginDrag(PointerEventData eventData) { }

        public void OnDrag(PointerEventData eventData)
        {
            if (_ui != null)
                _ui.ResizeWindow(eventData.delta);
        }
    }
}
