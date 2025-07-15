using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;


namespace Multi_bloob_adventure_idle
{
    public class MultiplayerContextMenu : MonoBehaviour
    {
        public Canvas UICanvas;          
        public GameObject ButtonPrefab;  

        private GameObject _menuGO;
        private RectTransform _menuRT;
        private float _closeMargin = 10f;  
        public static bool IsContextMenuOpen { get; private set; }

        void Update()
        {
            if (Input.GetMouseButtonDown(1))
            {
                TryShowMenu();
            }

            if (!_menuGO) return;
            Vector2 mousePos = Input.mousePosition;
            if (RectTransformUtility.RectangleContainsScreenPoint(
                    _menuRT, mousePos, null,
                    new Vector4(_closeMargin, _closeMargin, _closeMargin, _closeMargin))) return;
            CloseMenu();
            HoverUIManager.Instance.HideInfo();
        }

        private void TryShowMenu()
        {

            Camera cam = MultiplayerHoverDetector.cam;
            Vector3 wp3 = cam.ScreenToWorldPoint(Input.mousePosition);
            Vector2 wp2 = new Vector2(wp3.x, wp3.y);

            var hits = GameObject.FindObjectsOfType<IsMultiplayerClone>()
                .Where(c =>
                {
                    var sr = c.GetComponent<SpriteRenderer>();
                    return sr != null &&
                           sr.bounds.Contains(new Vector3(wp2.x, wp2.y, sr.bounds.center.z));
                })
                .ToList();

            if (hits.Count <= 1)
                return;  

            
            if (_menuGO) Destroy(_menuGO);

            
            _menuGO = new GameObject("CloneContextMenu", typeof(RectTransform), typeof(Image));
            _menuGO.transform.SetParent(UICanvas.transform, false);
            _menuRT = _menuGO.GetComponent<RectTransform>();
            IsContextMenuOpen = true;
            var bg = _menuGO.GetComponent<Image>();
            bg.color = new Color32(30, 30, 30, 220);

            
            float btnH = 30f, padding = 4f;
            _menuRT.sizeDelta = new Vector2(150f, hits.Count * (btnH + padding) + padding);
            _menuRT.pivot = new Vector2(0, 1);
            _menuRT.anchoredPosition = Input.mousePosition / UICanvas.scaleFactor;

            
            for (int i = 0; i < hits.Count; i++)
            {
                var clone = hits[i];
                string cloneName = clone.name.Replace("BloobClone_", "");

                
                var btnGO = Instantiate(ButtonPrefab, _menuRT);
                var btnRT = btnGO.GetComponent<RectTransform>();
                btnRT.anchorMin = new Vector2(0, 1);
                btnRT.anchorMax = new Vector2(1, 1);
                btnRT.pivot = new Vector2(0, 1);
                btnRT.anchoredPosition = new Vector2(padding, -padding - i * (btnH + padding));
                btnRT.sizeDelta = new Vector2(-2 * padding, btnH);

                
                var label = btnGO.GetComponentInChildren<TextMeshProUGUI>();
                label.text = cloneName;

                
                var trigger = btnGO.AddComponent<UnityEngine.EventSystems.EventTrigger>();
                var entry = new UnityEngine.EventSystems.EventTrigger.Entry
                {
                    eventID = UnityEngine.EventSystems.EventTriggerType.PointerEnter
                };
                entry.callback.AddListener(_ => ShowCloneInfo(cloneName));
                trigger.triggers.Add(entry);

                var exitEntry = new UnityEngine.EventSystems.EventTrigger.Entry
                {
                    eventID = UnityEngine.EventSystems.EventTriggerType.PointerExit
                };
                exitEntry.callback.AddListener(_ => HoverUIManager.Instance.HideInfo());
                trigger.triggers.Add(exitEntry);
            }
        }

        private void ShowCloneInfo(string name)
        {
            if (MultiplayerPatchPlugin.players.TryGetValue(name, out var pd))
            {
                string info = MultiplayerHoverDetector.BuildHoverInfo(name, pd);
                HoverUIManager.Instance.ShowInfo(info, Input.mousePosition);
            }
        }

        private void CloseMenu()
        {
            Destroy(_menuGO);
            IsContextMenuOpen = false;
        }
    }
}
