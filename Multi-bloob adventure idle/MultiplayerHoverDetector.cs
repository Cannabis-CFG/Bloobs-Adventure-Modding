using UnityEngine;
using UnityEngine.Windows;
using Input = UnityEngine.Input;

namespace Multi_bloob_adventure_idle;

public class MultiplayerHoverDetector : MonoBehaviour
{
    private Camera cam;

    void Start()
    {
        cam = Camera.main;
    }

    void Update()
    {
        if (!MultiplayerPatchPlugin.isReady || cam == null)
            return;

        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            var cloneComp = hit.collider?.GetComponent<IsMultiplayerClone>();
            if (cloneComp != null)
            {
                string ghostName = hit.collider.gameObject.name.Replace("BloobClone_", "");
                Debug.Log($"Hit something, {ghostName}");
                if (MultiplayerPatchPlugin.players.TryGetValue(ghostName, out PlayerData playerData))
                {
                    string info = BuildHoverInfo(ghostName, playerData);
                    HoverUIManager.Instance.ShowInfo(info, Input.mousePosition);
                }
            }
            else
            {
                HoverUIManager.Instance.HideInfo();
            }
        }
        else
        {
            HoverUIManager.Instance.HideInfo();
        }
    }

    private string BuildHoverInfo(string ghostName, PlayerData data)
    {
        System.Text.StringBuilder sb = new System.Text.StringBuilder();

        //sb.AppendLine($"<b>{ghostName}</b>");
        //sb.AppendLine("");
        //sb.AppendLine($"Run Speed: {data.runSpeed}");
        //sb.AppendLine($"Color: ({data.bloobColour.r:F2}, {data.bloobColour.g:F2}, {data.bloobColour.b:F2}, {data.bloobColour.a:F2})");

        if (MultiplayerPatchPlugin.EnableLevelPanel.Value && data.skillData != null)
        {
            //sb.AppendLine("");
            sb.AppendLine("Skills:");
            foreach (var kv in data.skillData)
            {
                sb.AppendLine($"{kv.Key}: Lvl {kv.Value.level} (P{kv.Value.prestige})");
            }
        }

        return sb.ToString();
    }
}