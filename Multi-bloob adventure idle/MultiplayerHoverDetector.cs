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
        if (!MultiplayerPatchPlugin.isReady || cam == null)
            return;
        Vector3 worldPoint = cam.ScreenToWorldPoint(Input.mousePosition);
        //Vector2 worldPoint2D = new Vector2(worldPoint.x, worldPoint.y);

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
        // 1) Pull out skill entries and sort
        var sorted = data.skillData
            .Where(kv => kv.Value.level >= 0)   // filter out any invalid entries
            .OrderByDescending(kv => kv.Value.prestige)
            .ThenByDescending(kv => kv.Value.level)
            .Select(kv =>
            {
                string name = kv.Key;
                int lvl = kv.Value.level;
                int pres = kv.Value.prestige;
                // 2) Format “SkillName Lvl X (P Y)” but drop prestige if zero
                return pres > 0
                    ? $"{name} Lvl {lvl} (P {pres})"
                    : $"{name} Lvl {lvl}";
            })
            .ToList();

        // 3) Build lines with up to two entries per line
        var lines = new List<string>();
        for (int i = 0; i < sorted.Count; i += 2)
        {
            if (i + 1 < sorted.Count)
            {
                // two on one line, padded to 'columnWidth' characters
                lines.Add(string.Format(
                    $"{{0,-{columnWidth}}}    {{1}}",
                    sorted[i],
                    sorted[i + 1]
                ));
            }
            else
            {
                // last odd entry alone
                lines.Add(sorted[i]);
            }
        }

        // 4) Prepend player name or header if you like
        lines.Insert(0, $"<b>{playerName}</b>");

        // 5) Join into one string with newlines
        return string.Join("\n", lines);
    }
}