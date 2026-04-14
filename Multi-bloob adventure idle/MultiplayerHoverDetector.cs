using System.Collections.Generic;
using System.Linq;
using UnityEngine;
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
        if (!MultiplayerPatchPlugin.isReady || !cam || !MultiplayerPatchPlugin.enableLevelPanel.Value || MultiplayerContextMenu.IsContextMenuOpen)
            return;

        var hovered = GetPlayersAtScreenPosition(Input.mousePosition);
        if (hovered.Count > 0)
        {
            var playerData = hovered[0];
            string playerName = MultiplayerPatchPlugin.GetPlayerNameFromSteamId(playerData.steamId);
            string info = BuildHoverInfo(playerName, playerData);
            HoverUIManager.Instance.ShowInfo(info, Input.mousePosition);
            return;
        }

        HoverUIManager.Instance.HideInfo();
    }

    public static List<PlayerData> GetPlayersAtScreenPosition(Vector3 screenPosition)
    {
        var results = new List<(PlayerData data, float distance, int sortingOrder)>();
        if (!cam)
            return [];

        Vector3 worldPoint3D = cam.ScreenToWorldPoint(screenPosition);
        Vector2 worldPoint2D = new(worldPoint3D.x, worldPoint3D.y);

        foreach (var clone in CloneManager.GetAllClones())
        {
            var cloneObject = clone?.GameObject;
            if (cloneObject == null)
                continue;

            var marker = cloneObject.GetComponent<IsMultiplayerClone>();
            var sr = cloneObject.GetComponent<SpriteRenderer>();
            if (marker == null || sr == null)
                continue;

            var bounds = sr.bounds;
            bounds.Expand(0.15f);
            if (!bounds.Contains(new Vector3(worldPoint2D.x, worldPoint2D.y, bounds.center.z)))
                continue;

            if (!MultiplayerPatchPlugin.Players.TryGetValue(marker.steamId, out var playerData) || playerData == null)
                continue;

            float distance = Vector2.Distance(worldPoint2D, new Vector2(bounds.center.x, bounds.center.y));
            results.Add((playerData, distance, sr.sortingOrder));
        }

        return [.. results
            .OrderByDescending(x => x.sortingOrder)
            .ThenBy(x => x.distance)
            .Select(x => x.data)];
    }

    public static string BuildHoverInfo(string playerName, PlayerData data)
    {
        var columnWidth = 20;
        var sorted = (data.skillData ?? [])
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

        var clanPrefix = string.IsNullOrWhiteSpace(data?.clanTag)
            ? string.Empty
            : $"[{data.clanTag}] ";
        var turboSuffix = data != null && data.isTurboSave ? " <color=#00FEEE>[Turbo]</color>" : string.Empty;

        lines.Insert(0, $"<b>{clanPrefix}{playerName}</b>{turboSuffix}");
        return string.Join("\n", lines);
    }
}
