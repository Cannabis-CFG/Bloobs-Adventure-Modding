using BepInEx.Configuration;
using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Networking;
using UnityEngine.UI;

namespace Multi_bloob_adventure_idle
{
    public class ModUpdateManager : MonoBehaviour
    {
        public sealed class LatestReleaseInfo
        {
            public string Version;
            public string Name;
            public string Body;
            public string HtmlUrl;
            public string AssetName;
            public string AssetUrl;
        }

        public static ModUpdateManager Instance { get; private set; }

        private ConfigEntry<bool> _checkForUpdatesConfig;
        private ConfigEntry<string> _repoOwnerConfig;
        private ConfigEntry<string> _repoNameConfig;
        private ConfigEntry<string> _releaseNameContainsConfig;
        private ConfigEntry<string> _assetNameContainsConfig;
        private ConfigEntry<string> _ignoredVersionConfig;

        private string _currentVersion;
        private string _pluginLocation;
        private bool _isChecking;
        private bool _isDownloading;
        private bool _installerLaunched;
        private bool _installOnClose;
        private string _pendingAssetPath;
        private string _pendingVersion;
        private ChatThemeSettings _theme;
        private LatestReleaseInfo _latestRelease;
        private ModUpdatePromptUi _promptUi;

        public static ModUpdateManager Create(ConfigFile config, string currentVersion, string pluginLocation)
        {
            if (Instance != null)
                return Instance;

            var go = new GameObject("ModUpdateManager");
            DontDestroyOnLoad(go);
            var manager = go.AddComponent<ModUpdateManager>();
            manager.Initialize(config, currentVersion, pluginLocation);
            return manager;
        }

        private void Initialize(ConfigFile config, string currentVersion, string pluginLocation)
        {
            _currentVersion = currentVersion ?? "0.0.0";
            _pluginLocation = pluginLocation ?? string.Empty;
            _theme = UiThemeUtility.GetSharedTheme();

            _checkForUpdatesConfig = config.Bind("Updates", "Check For Updates", true, "Checks GitHub releases for a newer mod version.");
            _repoOwnerConfig = config.Bind("Updates", "GitHub Owner", "Cannabis-CFG", "GitHub repository owner used for native update checks.");
            _repoNameConfig = config.Bind("Updates", "GitHub Repo", "Bloobs-Adventure-Modding", "GitHub repository name used for native update checks.");
            _releaseNameContainsConfig = config.Bind("Updates", "Release Name Contains", "Multiplayer", "Optional text filter used to choose the latest matching GitHub release by release name or tag.");
            _assetNameContainsConfig = config.Bind("Updates", "Asset Name Contains", "", "Optional text filter used to prefer a specific release asset name.");
            _ignoredVersionConfig = config.Bind("Updates", "Ignored Version", "", "A release tag/version to ignore until a different version is published.");

            _promptUi = ModUpdatePromptUi.Create(this);
            StartCoroutine(InitialCheckCoroutine());
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

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        private void OnApplicationQuit()
        {
            if (_installOnClose)
                LaunchInstaller();
        }

        private IEnumerator InitialCheckCoroutine()
        {
            yield return new WaitForSecondsRealtime(12f);
            yield return CheckForUpdatesCoroutine(false);
        }

        public void CheckForUpdatesNow()
        {
            if (_isChecking)
                return;

            StartCoroutine(CheckForUpdatesCoroutine(true));
        }

        private IEnumerator CheckForUpdatesCoroutine(bool manual)
        {
            if (_isChecking)
                yield break;

            if (!_checkForUpdatesConfig.Value)
                yield break;

            string owner = (_repoOwnerConfig.Value ?? string.Empty).Trim();
            string repo = (_repoNameConfig.Value ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(owner) || string.IsNullOrWhiteSpace(repo))
            {
                if (manual)
                    ChatSystem.Instance?.ReceiveError("Update checking is not configured yet. Set the GitHub owner and repo in the mod config.");
                yield break;
            }

            _isChecking = true;
            string url = $"https://api.github.com/repos/{owner}/{repo}/releases?per_page=20";
            using var request = UnityWebRequest.Get(url);
            request.SetRequestHeader("Accept", "application/vnd.github+json");
            request.SetRequestHeader("X-GitHub-Api-Version", "2026-03-10");
            request.timeout = 20;

            yield return request.SendWebRequest();
            _isChecking = false;

            if (request.result != UnityWebRequest.Result.Success)
            {
                if (manual)
                    ChatSystem.Instance?.ReceiveError($"Update check failed: {request.error}");
                yield break;
            }

            JToken json;
            try
            {
                json = JToken.Parse(request.downloadHandler.text);
            }
            catch (Exception ex)
            {
                if (manual)
                    ChatSystem.Instance?.ReceiveError($"Update check returned invalid JSON: {ex.Message}");
                yield break;
            }

            _latestRelease = ParseLatestRelease(json);
            if (_latestRelease == null)
            {
                if (manual)
                    ChatSystem.Instance?.ReceiveError("A GitHub release was found, but no downloadable DLL or ZIP asset matched the current updater settings.");
                yield break;
            }

            if (string.Equals(_ignoredVersionConfig.Value, _latestRelease.Version, StringComparison.OrdinalIgnoreCase))
                yield break;

            if (!IsRemoteVersionNewer(_latestRelease.Version))
            {
                if (manual)
                    ChatSystem.Instance?.ReceiveError($"You already have the latest mod version installed ({_currentVersion}).");
                yield break;
            }

            _promptUi.ShowRelease(_latestRelease, _currentVersion);
        }

        private LatestReleaseInfo ParseLatestRelease(JToken payload)
        {
            if (payload == null)
                return null;

            var release = SelectRelease(payload);
            if (release == null)
                return null;

            var asset = SelectAsset(release["assets"] as JArray);
            if (asset == null)
                return null;

            return new LatestReleaseInfo
            {
                Version = release["tag_name"]?.ToString() ?? release["name"]?.ToString() ?? "unknown",
                Name = release["name"]?.ToString() ?? release["tag_name"]?.ToString() ?? "Latest Release",
                Body = release["body"]?.ToString() ?? string.Empty,
                HtmlUrl = release["html_url"]?.ToString() ?? string.Empty,
                AssetName = asset["name"]?.ToString() ?? "update_asset",
                AssetUrl = asset["browser_download_url"]?.ToString() ?? string.Empty
            };
        }

        private JToken SelectRelease(JToken payload)
        {
            if (payload is JObject single)
                return ReleaseMatchesFilter(single) ? single : null;

            if (payload is not JArray releases || releases.Count == 0)
                return null;

            foreach (var release in releases.OfType<JObject>())
            {
                if (release["draft"]?.Value<bool>() == true)
                    continue;
                if (release["prerelease"]?.Value<bool>() == true)
                    continue;
                if (ReleaseMatchesFilter(release))
                    return release;
            }

            return null;
        }

        private bool ReleaseMatchesFilter(JObject release)
        {
            if (release == null)
                return false;

            string filter = (_releaseNameContainsConfig?.Value ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(filter))
                return true;

            string name = release["name"]?.ToString() ?? string.Empty;
            string tag = release["tag_name"]?.ToString() ?? string.Empty;
            return name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0 || tag.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private JToken SelectAsset(JArray assets)
        {
            if (assets == null || assets.Count == 0)
                return null;

            string filter = (_assetNameContainsConfig.Value ?? string.Empty).Trim();
            var candidates = assets
                .Where(x => x != null && x["name"] != null && x["browser_download_url"] != null)
                .ToList();

            if (!string.IsNullOrWhiteSpace(filter))
            {
                var filtered = candidates.Where(x => x["name"]?.ToString().IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0).ToList();
                if (filtered.Count > 0)
                    candidates = filtered;
            }

            var dll = candidates.FirstOrDefault(x => x["name"]?.ToString().EndsWith(".dll", StringComparison.OrdinalIgnoreCase) == true);
            if (dll != null)
                return dll;

            var zip = candidates.FirstOrDefault(x => x["name"]?.ToString().EndsWith(".zip", StringComparison.OrdinalIgnoreCase) == true);
            if (zip != null)
                return zip;

            return candidates.FirstOrDefault();
        }

        private bool IsRemoteVersionNewer(string remoteVersion)
        {
            if (!TryParseComparableVersion(_currentVersion, out var currentParsed))
                return !string.Equals(remoteVersion, _currentVersion, StringComparison.OrdinalIgnoreCase);

            if (!TryParseComparableVersion(remoteVersion, out var remoteParsed))
                return !string.Equals(remoteVersion, _currentVersion, StringComparison.OrdinalIgnoreCase);

            return remoteParsed > currentParsed;
        }

        private static bool TryParseComparableVersion(string raw, out Version version)
        {
            version = null;
            if (string.IsNullOrWhiteSpace(raw))
                return false;

            string trimmed = raw.Trim();
            if (trimmed.StartsWith("v", StringComparison.OrdinalIgnoreCase))
                trimmed = trimmed.Substring(1);

            var chars = trimmed.TakeWhile(c => char.IsDigit(c) || c == '.').ToArray();
            string normalized = new string(chars);
            if (string.IsNullOrWhiteSpace(normalized))
                return false;

            var segments = normalized.Split(new[] { '.' }, StringSplitOptions.RemoveEmptyEntries).Take(4).ToList();
            while (segments.Count < 3)
                segments.Add("0");

            return Version.TryParse(string.Join(".", segments), out version);
        }

        public void IgnoreLatestRelease()
        {
            if (_latestRelease == null)
                return;

            _ignoredVersionConfig.Value = _latestRelease.Version;
            _promptUi.HidePrompt();
        }

        public void InstallLatestNow()
        {
            if (_latestRelease == null || _isDownloading)
                return;

            StartCoroutine(StageLatestReleaseCoroutine(installNow: true));
        }

        public void InstallLatestOnClose()
        {
            if (_latestRelease == null || _isDownloading)
                return;

            StartCoroutine(StageLatestReleaseCoroutine(installNow: false));
        }

        private IEnumerator StageLatestReleaseCoroutine(bool installNow)
        {
            if (_latestRelease == null)
                yield break;

            if (Application.platform != RuntimePlatform.WindowsPlayer && Application.platform != RuntimePlatform.WindowsEditor)
            {
                _promptUi.SetStatus("Automatic install is currently implemented for Windows only.");
                yield break;
            }

            _isDownloading = true;
            _promptUi.SetStatus($"Downloading {_latestRelease.AssetName}...");

            using var request = UnityWebRequest.Get(_latestRelease.AssetUrl);
            request.timeout = 60;
            yield return request.SendWebRequest();
            _isDownloading = false;

            if (request.result != UnityWebRequest.Result.Success)
            {
                _promptUi.SetStatus($"Download failed: {request.error}");
                yield break;
            }

            string pendingDirectory = Path.Combine(Path.GetDirectoryName(_pluginLocation) ?? Application.persistentDataPath, "_multibloob_update_pending");
            Directory.CreateDirectory(pendingDirectory);
            _pendingAssetPath = Path.Combine(pendingDirectory, _latestRelease.AssetName);
            File.WriteAllBytes(_pendingAssetPath, request.downloadHandler.data);
            _pendingVersion = _latestRelease.Version;
            _installOnClose = true;

            if (installNow)
            {
                _promptUi.SetStatus("Update downloaded. Closing the game to install...");
                LaunchInstaller();
                Application.Quit();
                yield break;
            }

            _promptUi.HidePrompt();
        }

        private void LaunchInstaller()
        {
            if (_installerLaunched || string.IsNullOrWhiteSpace(_pendingAssetPath) || string.IsNullOrWhiteSpace(_pluginLocation))
                return;

            try
            {
                string pluginDirectory = Path.GetDirectoryName(_pluginLocation) ?? Application.persistentDataPath;
                string installerDirectory = Path.Combine(pluginDirectory, "_multibloob_update_pending");
                Directory.CreateDirectory(installerDirectory);

                string scriptPath = Path.Combine(installerDirectory, "apply_update.ps1");
                string batchPath = Path.Combine(installerDirectory, "apply_update.bat");
                File.WriteAllText(scriptPath, BuildPowerShellScript(_pendingAssetPath, pluginDirectory, _pluginLocation));
                File.WriteAllText(batchPath, BuildBatchScript(scriptPath));

                var startInfo = new ProcessStartInfo
                {
                    FileName = batchPath,
                    UseShellExecute = true,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                };

                Process.Start(startInfo);
                _installerLaunched = true;
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError("[ModUpdate] Failed to launch installer: " + ex);
            }
        }

        private static string BuildBatchScript(string powerShellPath)
        {
            string escapedPowerShellPath = powerShellPath.Replace("\"", "\"\"");
            int pid = Process.GetCurrentProcess().Id;
            return $"@echo off\r\nset PID={pid}\r\n:waitloop\r\ntasklist /FI \"PID eq %PID%\" 2>NUL | find \"%PID%\" >NUL\r\nif not errorlevel 1 (\r\n  timeout /t 1 /nobreak >NUL\r\n  goto waitloop\r\n)\r\npowershell -NoProfile -ExecutionPolicy Bypass -File \"{escapedPowerShellPath}\"\r\n";
        }

        private static string BuildPowerShellScript(string pendingAssetPath, string pluginDirectory, string currentDllPath)
        {
            string Escape(string value) => value.Replace("'", "''");
            return $@"
$ErrorActionPreference = 'Stop'
$AssetPath = '{Escape(pendingAssetPath)}'
$PluginDirectory = '{Escape(pluginDirectory)}'
$CurrentDllPath = '{Escape(currentDllPath)}'
$ExtractDirectory = Join-Path $PluginDirectory '_multibloob_update_extract'

if (!(Test-Path -LiteralPath $AssetPath)) {{
    exit 1
}}

if (Test-Path -LiteralPath $ExtractDirectory) {{
    Remove-Item -LiteralPath $ExtractDirectory -Recurse -Force -ErrorAction SilentlyContinue
}}

New-Item -ItemType Directory -Path $ExtractDirectory -Force | Out-Null
$Extension = [IO.Path]::GetExtension($AssetPath).ToLowerInvariant()

if ($Extension -eq '.zip') {{
    Expand-Archive -LiteralPath $AssetPath -DestinationPath $ExtractDirectory -Force
    $Children = Get-ChildItem -LiteralPath $ExtractDirectory
    $SourcePath = $ExtractDirectory
    if ($Children.Count -eq 1 -and $Children[0].PSIsContainer) {{
        $SourcePath = $Children[0].FullName
    }}

    Get-ChildItem -LiteralPath $SourcePath | ForEach-Object {{
        Copy-Item -LiteralPath $_.FullName -Destination $PluginDirectory -Recurse -Force
    }}
}} else {{
    Copy-Item -LiteralPath $AssetPath -Destination $CurrentDllPath -Force
}}

Remove-Item -LiteralPath $AssetPath -Force -ErrorAction SilentlyContinue
if (Test-Path -LiteralPath $ExtractDirectory) {{
    Remove-Item -LiteralPath $ExtractDirectory -Recurse -Force -ErrorAction SilentlyContinue
}}
";
        }
    }

    public class ModUpdatePromptUi : MonoBehaviour
    {
        public static ModUpdatePromptUi Instance { get; private set; }

        private ModUpdateManager _manager;
        private Canvas _canvas;
        private RectTransform _panel;
        private Image _panelImage;
        private Outline _panelOutline;
        private TextMeshProUGUI _headerText;
        private TextMeshProUGUI _bodyText;
        private TextMeshProUGUI _statusText;
        private Button _installNowButton;
        private TextMeshProUGUI _installNowText;
        private Button _installOnCloseButton;
        private TextMeshProUGUI _installOnCloseText;
        private Button _ignoreButton;
        private TextMeshProUGUI _ignoreText;
        private ChatThemeSettings _theme;

        public static ModUpdatePromptUi Create(ModUpdateManager manager)
        {
            if (Instance != null)
            {
                Instance._manager = manager;
                return Instance;
            }

            var go = new GameObject("ModUpdatePromptUi");
            DontDestroyOnLoad(go);
            var ui = go.AddComponent<ModUpdatePromptUi>();
            ui.Initialize(manager);
            return ui;
        }

        private void Initialize(ModUpdateManager manager)
        {
            _manager = manager;
            _theme = UiThemeUtility.GetSharedTheme();
            EnsureEventSystem();
            BuildUi();
            ApplyTheme();
            HidePrompt();
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

        private void Update()
        {
            if (_panel != null && _panel.gameObject.activeSelf && Input.GetKeyDown(KeyCode.Escape))
                HidePrompt();
        }

        public void ShowRelease(ModUpdateManager.LatestReleaseInfo release, string currentVersion)
        {
            if (release == null)
                return;

            _theme = UiThemeUtility.GetSharedTheme();
            ApplyTheme();
            _headerText.text = $"Update available: {release.Name}";
            _bodyText.text = $"Installed: {currentVersion}\nLatest: {release.Version}\nAsset: {release.AssetName}\n\n{TrimBody(release.Body)}";
            _statusText.text = "Install now to close and update immediately, or stage it for the next game close.";
            _panel.gameObject.SetActive(true);
        }

        public void SetStatus(string text)
        {
            _statusText.text = text ?? string.Empty;
            _panel.gameObject.SetActive(true);
        }

        public void HidePrompt()
        {
            if (_panel != null)
                _panel.gameObject.SetActive(false);
        }

        private static string TrimBody(string body)
        {
            if (string.IsNullOrWhiteSpace(body))
                return "No release notes were provided.";

            body = body.Trim();
            return body.Length <= 900 ? body : body.Substring(0, 900) + "...";
        }

        private void BuildUi()
        {
            _canvas = gameObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 32020;
            gameObject.AddComponent<GraphicRaycaster>();

            _panel = UiThemeUtility.CreateRect("UpdatePanel", transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            _panel.sizeDelta = new Vector2(520f, 340f);
            _panel.anchoredPosition = Vector2.zero;

            _panelImage = _panel.gameObject.AddComponent<Image>();
            _panelOutline = _panel.gameObject.AddComponent<Outline>();

            var rootLayout = _panel.gameObject.AddComponent<VerticalLayoutGroup>();
            rootLayout.padding = new RectOffset(12, 12, 12, 12);
            rootLayout.spacing = 8;
            rootLayout.childForceExpandHeight = false;
            rootLayout.childForceExpandWidth = true;

            var headerRect = UiThemeUtility.CreateRect("HeaderText", _panel);
            var headerLE = headerRect.gameObject.AddComponent<LayoutElement>();
            headerLE.preferredHeight = 30f;
            _headerText = headerRect.gameObject.AddComponent<TextMeshProUGUI>();
            _headerText.alignment = TextAlignmentOptions.MidlineLeft;
            _headerText.raycastTarget = false;

            var bodyRect = UiThemeUtility.CreateRect("BodyText", _panel);
            var bodyLE = bodyRect.gameObject.AddComponent<LayoutElement>();
            bodyLE.flexibleHeight = 1f;
            bodyLE.minHeight = 180f;
            _bodyText = bodyRect.gameObject.AddComponent<TextMeshProUGUI>();
            _bodyText.alignment = TextAlignmentOptions.TopLeft;
            _bodyText.enableWordWrapping = true;
            _bodyText.overflowMode = TextOverflowModes.Ellipsis;
            _bodyText.raycastTarget = false;

            var statusRect = UiThemeUtility.CreateRect("StatusText", _panel);
            var statusLE = statusRect.gameObject.AddComponent<LayoutElement>();
            statusLE.preferredHeight = 38f;
            _statusText = statusRect.gameObject.AddComponent<TextMeshProUGUI>();
            _statusText.alignment = TextAlignmentOptions.TopLeft;
            _statusText.enableWordWrapping = true;
            _statusText.raycastTarget = false;

            var buttonsRow = UiThemeUtility.CreateRect("ButtonsRow", _panel);
            var rowLayout = buttonsRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            rowLayout.spacing = 6f;
            rowLayout.childForceExpandHeight = false;
            rowLayout.childForceExpandWidth = true;
            var rowLE = buttonsRow.gameObject.AddComponent<LayoutElement>();
            rowLE.preferredHeight = 36f;
            rowLE.minHeight = 36f;

            _installNowButton = UiThemeUtility.CreateButton("InstallNowButton", buttonsRow, out _installNowText, "Install Now", 160f, 34f);
            _installNowButton.onClick.AddListener(() => _manager?.InstallLatestNow());

            _installOnCloseButton = UiThemeUtility.CreateButton("InstallOnCloseButton", buttonsRow, out _installOnCloseText, "Install On Close", 170f, 34f);
            _installOnCloseButton.onClick.AddListener(() => _manager?.InstallLatestOnClose());

            _ignoreButton = UiThemeUtility.CreateButton("IgnoreButton", buttonsRow, out _ignoreText, "Ignore", 120f, 34f);
            _ignoreButton.onClick.AddListener(() => _manager?.IgnoreLatestRelease());
        }

        private void ApplyTheme()
        {
            UiThemeUtility.ApplyPanelStyle(_panelImage, _panelOutline, _theme);
            UiThemeUtility.ApplyButtonStyle(_installNowButton, _installNowText, _theme, selected: true);
            UiThemeUtility.ApplyButtonStyle(_installOnCloseButton, _installOnCloseText, _theme);
            UiThemeUtility.ApplyButtonStyle(_ignoreButton, _ignoreText, _theme);

            _panel.localScale = Vector3.one * Mathf.Clamp(_theme?.UiScale.Value ?? 1f, 0.75f, 2f);

            _headerText.fontSize = UiThemeUtility.GetScaledFont(_theme, 20f);
            _headerText.color = _theme != null ? _theme.GetHeaderTextColor() : Color.white;

            _bodyText.fontSize = UiThemeUtility.GetScaledFont(_theme, 15f);
            _bodyText.color = _theme != null ? _theme.GetBodyTextColor() : Color.white;

            _statusText.fontSize = UiThemeUtility.GetScaledFont(_theme, 13f);
            _statusText.color = _theme != null ? _theme.GetStatusTextColor() : new Color(0.75f, 0.75f, 0.75f, 1f);
        }

        private void EnsureEventSystem()
        {
            if (FindObjectOfType<EventSystem>() != null)
                return;

            var go = new GameObject("ModUpdateEventSystem");
            DontDestroyOnLoad(go);
            go.AddComponent<EventSystem>();
            go.AddComponent<StandaloneInputModule>();
        }
    }
}
