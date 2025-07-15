using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Windows;
using Input = UnityEngine.Input;

namespace Multi_bloob_adventure_idle;

public class MultiplayerHoverDetector : MonoBehaviour
{
    public static Camera cam;
    public static MultiplayerHoverDetector instance;

    void Start()
    {
        //Debug.Log("Started Hover ShitHead");
        GameObject lcaGameObject = GameObject.Find("LCA");
        if (lcaGameObject == null) return;
        Transform camTransform = lcaGameObject.transform.Find("Main Camera");
        if (camTransform == null) return;
        cam = camTransform.GetComponent<Camera>();
        instance ??= this;
        //Debug.Log("Found camera");
        //cam = Camera.current;
    }

    void Update()
    {
        if (!MultiplayerPatchPlugin.isReady || !cam || !MultiplayerPatchPlugin.EnableLevelPanel.Value || MultiplayerContextMenu.IsContextMenuOpen)
            return;

        Vector3 worldPoint3D = cam.ScreenToWorldPoint(Input.mousePosition);
        Vector2 worldPoint2D = new Vector2(worldPoint3D.x, worldPoint3D.y);
        bool hitOne = false;
        foreach (var cloneComp in GameObject.FindObjectsOfType<IsMultiplayerClone>())
        {
            var sr = cloneComp.GetComponent<SpriteRenderer>();
            if (sr && sr.bounds.Contains(new Vector3(worldPoint2D.x, worldPoint2D.y, sr.bounds.center.z)))
            {
                
                string cloneName = cloneComp.name.Replace("BloobClone_", "");
                if (MultiplayerPatchPlugin.players.TryGetValue(cloneName, out var playerData))
                {
                    string info = BuildHoverInfo(cloneName, playerData);
                    HoverUIManager.Instance.ShowInfo(info, Input.mousePosition);
                    hitOne = true;
                }
                break;
            }
        }

        if (!hitOne)
        {
            HoverUIManager.Instance.HideInfo();
        }
    }

    public static string BuildHoverInfo(string playerName, PlayerData data)
    {
        var columnWidth = 20;
        var sorted = data.skillData
            .Where(kv => kv.Value.level >= 0)
            .OrderByDescending(kv => kv.Value.prestige)
            .ThenByDescending(kv => kv.Value.level)
            .Select(kv =>
            {
                string name = kv.Key;
                int lvl = kv.Value.level;
                int pres = kv.Value.prestige;
                return pres > 0
                    ? $"{name} Lvl {lvl} (P {pres})"
                    : $"{name} Lvl {lvl}";
            })
            .ToList();

        var lines = new List<string>();
        for (int i = 0; i < sorted.Count; i += 2)
        {
            if (i + 1 < sorted.Count)
            {
                lines.Add(string.Format(
                    $"{{0,-{columnWidth}}}    {{1}}",
                    sorted[i],
                    sorted[i + 1]
                ));
            }
            else
            {
                lines.Add(sorted[i]);
            }
        }
        lines.Insert(0, $"<b>{playerName}</b>");
        return string.Join("\n", lines);
    }
}