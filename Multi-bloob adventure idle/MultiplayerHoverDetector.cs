using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Windows;
using Input = UnityEngine.Input;

namespace Multi_bloob_adventure_idle;

public class MultiplayerHoverDetector : MonoBehaviour
{
    private Camera cam;

    void Start()
    {
        Debug.Log("Started Hover ShitHead");
        GameObject lcaGameObject = GameObject.Find("LCA");
        if (lcaGameObject == null) return;
        Transform camTransform = lcaGameObject.transform.Find("Main Camera");
        if (camTransform == null) return;
        cam = camTransform.GetComponent<Camera>();
        Debug.Log("Found camera");
        //cam = Camera.current;
    }

    void Update()
    {
        if (!MultiplayerPatchPlugin.isReady || cam == null || !MultiplayerPatchPlugin.EnableLevelPanel.Value)
            return;
        Vector3 worldPoint = cam.ScreenToWorldPoint(Input.mousePosition);

        foreach (var cloneComp in GameObject.FindObjectsOfType<IsMultiplayerClone>())
        {
            var spriteRendererer = cloneComp.GetComponent<SpriteRenderer>();
            if (spriteRendererer != null &&
                spriteRendererer.bounds.Contains(new Vector3(worldPoint.x, worldPoint.y,
                    spriteRendererer.bounds.center.z)))
            {
                string cloneName = cloneComp.name.Replace("BloobClone_", "");
                if (MultiplayerPatchPlugin.players.TryGetValue(cloneName, out PlayerData playerData))
                {
                    string info = BuildHoverInfo(cloneName, playerData);
                    HoverUIManager.Instance.ShowInfo(info, Input.mousePosition);
                }
            }
            else
            {
                HoverUIManager.Instance.HideInfo();
            }
        }


    }

    private string BuildHoverInfo(string playerName, PlayerData data)
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