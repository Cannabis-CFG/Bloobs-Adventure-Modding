using System.Collections;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Serialization;
using UnityEngine.UI;


namespace Multi_bloob_adventure_idle
{
    public class MultiplayerContextMenu : MonoBehaviour
    {
        public Canvas uiCanvas;
        public GameObject buttonPrefab;

        private GameObject menuGo;
        private RectTransform menuRT;
        private const float CloseMargin = 10f;
        private const float BtnHeight = 30f;
        private const float Padding = 4f;
        public static bool IsContextMenuOpen { get; private set; }


        void Awake()
        {
            // ensure EventSystem exists
            if (FindObjectOfType<EventSystem>() == null)
                new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

            var go = GameObject.Find("HoverCanvas");
            uiCanvas = go.GetComponent<Canvas>();
        }

        void Update()
        {
            if (Input.GetMouseButtonDown(1) && MultiplayerPatchPlugin.enableContextMenu.Value)
            {
                TryShowMenu();
            }

        }

        private void TryShowMenu()
        {

            Camera cam = MultiplayerHoverDetector.cam;
            Vector3 wp3 = cam.ScreenToWorldPoint(Input.mousePosition);
            Vector2 wp2 = new Vector2(wp3.x, wp3.y);
            if (!cam)
            {
                Debug.LogError("Camera is null");
                return;
            }
            var hits = GameObject.FindObjectsOfType<IsMultiplayerClone>()
                .Where(c =>
                {
                    var sr = c.GetComponent<SpriteRenderer>();
                    return sr
                        && sr.bounds.Contains(new Vector3(wp2.x, wp2.y, sr.bounds.center.z));
                })
                .ToList();

            if (hits.Count <= 1)
                return;

            if (menuGo)
                Destroy(menuGo);

            menuGo = new GameObject("CloneContextMenu", typeof(RectTransform), typeof(Image));
            menuGo.transform.SetParent(uiCanvas.transform, false);
            menuRT = menuGo.GetComponent<RectTransform>();

            var bg = menuGo.GetComponent<Image>();
            bg.color = new Color32(30, 30, 30, 220);

            float width = 160f;
            float height = hits.Count * (BtnHeight + Padding) + Padding;
            menuRT.sizeDelta = new Vector2(width, height);
            menuRT.pivot = new Vector2(0, 1);
            menuRT.anchoredPosition = Input.mousePosition / uiCanvas.scaleFactor;

            foreach (var kv in hits.Select((c, i) => (c, i)))
            {
                var clone = kv.c;
                int idx = kv.i;
                string nameKey = clone.name.Replace("BloobClone_", "");

                var btnGo = new GameObject("Btn_" + nameKey, typeof(RectTransform), typeof(Image), typeof(Button));
                btnGo.transform.SetParent(menuGo.transform, false);
                var btnRT = btnGo.GetComponent<RectTransform>();
                btnRT.anchorMin = new Vector2(0, 1);
                btnRT.anchorMax = new Vector2(1, 1);
                btnRT.pivot = new Vector2(0, 1);
                btnRT.sizeDelta = new Vector2(-2 * Padding, BtnHeight);
                btnRT.anchoredPosition = new Vector2(Padding, -Padding - idx * (BtnHeight + Padding));

                var img = btnGo.GetComponent<Image>();
                img.color = new Color32(50, 50, 50, 200);

                var textGo = new GameObject("Label", typeof(RectTransform));
                textGo.transform.SetParent(btnGo.transform, false);
                var textRT = textGo.GetComponent<RectTransform>();
                textRT.anchorMin = Vector2.zero;
                textRT.anchorMax = Vector2.one;
                textRT.offsetMin = Vector2.zero;
                textRT.offsetMax = Vector2.zero;

                var tmp = textGo.AddComponent<TextMeshProUGUI>();
                tmp.text = nameKey;
                tmp.fontSize = 18;
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.color = Color.white;

                var trigger = btnGo.AddComponent<EventTrigger>();

                var enter = new EventTrigger.Entry
                {
                    eventID = EventTriggerType.PointerEnter
                };
                enter.callback.AddListener(_ => ShowCloneInfo(nameKey));
                trigger.triggers.Add(enter);

                var exit = new EventTrigger.Entry
                {
                    eventID = EventTriggerType.PointerExit
                };
                exit.callback.AddListener(_ => HoverUIManager.Instance.HideInfo());
                trigger.triggers.Add(exit);
            }
            StartCoroutine(AutoCloseMenu(uiCanvas));
        }

        private void ShowCloneInfo(string name)
        {
            if (MultiplayerPatchPlugin.Players.TryGetValue(name, out var pd))
            {
                string info = MultiplayerHoverDetector.BuildHoverInfo(name, pd);
                HoverUIManager.Instance.ShowInfo(info, Input.mousePosition);
            }
        }

        private IEnumerator AutoCloseMenu(Canvas canvas)
        {
            while (menuGo)
            {
                Vector2 mpos = Input.mousePosition / canvas.scaleFactor;
                if (!RectTransformUtility.RectangleContainsScreenPoint(
                        menuRT, mpos, null,
                        new Vector4(CloseMargin, CloseMargin, CloseMargin, CloseMargin)))
                {
                    Destroy(menuGo);
                    HoverUIManager.Instance.HideInfo();
                    yield break;
                }
                yield return null;
            }
        }
    }
}
