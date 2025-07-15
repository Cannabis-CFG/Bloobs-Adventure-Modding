using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Multi_bloob_adventure_idle
{
    public class CloneManager
    {
        private static Dictionary<string, Clone> _clones = new Dictionary<string, Clone>();

        public static Clone GetClone(string playerName)
        {
            _clones.TryGetValue(playerName, out var clone);
            return clone;
        }

        public static Clone CreateClone(GameObject originalPrefab, PlayerData playerData)
        {
            var obj = Object.Instantiate(originalPrefab);
            obj.name = "BloobClone_" + playerData.name;
            obj.AddComponent<IsMultiplayerClone>();

            var clone = new Clone(playerData.name, obj);

            clone.SetColor(playerData.bloobColour.ToColor());
            clone.SetPosition(playerData.currentPosition.ToVector3());

            // Remove unwanted stuff:
            foreach (var collider in obj.GetComponents<CircleCollider2D>())
                Object.Destroy(collider);

            foreach (Transform child in obj.transform)
            {
                if (child.name != "wingSlot" && child.name != "Canvas" && child.name != "HatSlot")
                    Object.Destroy(child.gameObject);
            }

            // Create nameplate
            if (obj.transform.Find("NamePlate") is null)
            {
                var namePlate = new GameObject("NamePlate");
                namePlate.transform.SetParent(obj.transform, false);
                namePlate.transform.localPosition = new Vector3(0, 1.125f, 0);
                var text = namePlate.AddComponent<TextMeshPro>();
                text.text = playerData.name;
                text.fontSize = 6;
                text.alignment = TextAlignmentOptions.Center;
                text.color = Color.white;
                var mr = text.GetComponent<MeshRenderer>();
                if (mr)
                    mr.sortingLayerName = "UI";
            }

            _clones[playerData.name] = clone;
            return clone;
        }

        public static void UpdateOrCreateClone(GameObject original, PlayerData playerData)
        {
            if (!_clones.TryGetValue(playerData.name, out var clone))
            {
                CreateClone(original, playerData);
            }
            else
            {
                clone.SetColor(playerData.bloobColour.ToColor());
                clone.MoveTo(playerData.currentPosition.ToVector2(), playerData.runSpeed);
            }
        }

        public static void RemoveClone(GameObject clone)
        {
            Object.Destroy(clone);
        }

        public IEnumerable<Clone> GetAllClones() => _clones.Values;
    }

    public class Clone
    {
        public string PlayerName { get; }
        public GameObject GameObject { get; }
        public CharacterMovement Movement => GameObject.GetComponent<CharacterMovement>();

        public Clone(string playerName, GameObject obj)
        {
            PlayerName = playerName;
            GameObject = obj;
        }

        public void SetColor(Color color)
        {
            var renderer = GameObject.GetComponent<SpriteRenderer>();
            if (renderer)
                renderer.color = color;
        }

        public void SetPosition(Vector3 pos)
        {
            var cm = Movement;
            if (cm)
            {
                cm.StopMoving();
                GameObject.transform.position = pos;
            }
            else
            {
                GameObject.transform.position = pos;
            }
        }

        public void UpdateCustomizations(string slot, string asset)
        {
            //TODO Pass hat/wings and update here
        }


        public void MoveTo(Vector2 target, float speed = -1f)
        {
            var cm = Movement;
            if (cm)
            {
                if (speed > 0)
                    cm.moveSpeed = speed;

                cm.MoveTo(target);
            }
        }

    }

}
