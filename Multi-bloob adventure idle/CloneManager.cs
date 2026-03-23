using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Multi_bloob_adventure_idle
{
    public class CloneManager
    {
        private static readonly Dictionary<string, Clone> _clones = [];

        public static Clone GetClone(string steamId)
        {
            if (string.IsNullOrWhiteSpace(steamId))
                return null;

            _clones.TryGetValue(steamId, out var clone);
            return clone;
        }

        public static Clone CreateClone(GameObject originalPrefab, PlayerData playerData)
        {
            if (originalPrefab == null || playerData == null || string.IsNullOrWhiteSpace(playerData.steamId))
                return null;

            var obj = Object.Instantiate(originalPrefab);
            obj.name = "BloobClone_" + playerData.steamId;

            var cloneMarker = obj.GetComponent<IsMultiplayerClone>() ?? obj.AddComponent<IsMultiplayerClone>();
            cloneMarker.steamId = playerData.steamId;
            cloneMarker.displayName = playerData.name;

            Clone clone = new(playerData.steamId, playerData.name, obj);

            clone.SetColor(playerData.bloobColour?.ToColor() ?? Color.white);
            clone.SetPosition(playerData.currentPosition?.ToVector3() ?? Vector3.zero);

            // Remove unwanted stuff
            foreach (var collider in obj.GetComponents<CircleCollider2D>())
                Object.Destroy(collider);

            foreach (Transform child in obj.transform)
            {
                if (child.name != "wingSlot" && child.name != "Canvas" && child.name != "HatSlot")
                    Object.Destroy(child.gameObject);
            }

            var customizationApplier = GetOrCreateCustomizationApplier(obj);
            customizationApplier.ApplyAll(playerData.activeHatIndex, playerData.activeWingIndex);

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
            else
            {
                // If something already created the nameplate somehow, keep its text updated.
                var existingText = obj.transform.Find("NamePlate")?.GetComponent<TextMeshPro>();
                if (existingText != null)
                    existingText.text = playerData.name;
            }

            _clones[playerData.steamId] = clone;
            return clone;
        }

        public static void UpdateOrCreateClone(GameObject original, PlayerData playerData)
        {
            if (original == null || playerData == null || string.IsNullOrWhiteSpace(playerData.steamId))
                return;

            if (!_clones.TryGetValue(playerData.steamId, out var clone) || clone == null || clone.GameObject == null)
            {
                CreateClone(original, playerData);
            }
            else
            {
                // Keep display name in sync in case the Steam display name changed mid-session.
                clone.UpdateDisplayName(playerData.name);

                if (playerData.bloobColour != null)
                    clone.SetColor(playerData.bloobColour.ToColor());

                if (playerData.currentPosition != null)
                    clone.MoveTo(playerData.currentPosition.ToVector2(), playerData.runSpeed);
                if (playerData.activeHatIndex >= -1 || playerData.activeWingIndex >= -1)
                {
                    var applier = GetOrCreateCustomizationApplier(clone.GameObject);
                    applier.ApplyAll(playerData.activeHatIndex, playerData.activeWingIndex);
                }

            }
        }

        public static void RemoveClone(GameObject clone)
        {
            if (clone == null)
                return;

            var marker = clone.GetComponent<IsMultiplayerClone>();
            if (marker != null && !string.IsNullOrWhiteSpace(marker.steamId))
            {
                _clones.Remove(marker.steamId);
            }
            else
            {
                // Fallback cleanup path if marker somehow went missing.
                string toRemove = null;
                foreach (var kvp in _clones)
                {
                    if (kvp.Value != null && kvp.Value.GameObject == clone)
                    {
                        toRemove = kvp.Key;
                        break;
                    }
                }

                if (!string.IsNullOrWhiteSpace(toRemove))
                    _clones.Remove(toRemove);
            }

            Object.Destroy(clone);
        }

        public static void RemoveCloneBySteamId(string steamId)
        {
            if (string.IsNullOrWhiteSpace(steamId))
                return;

            if (_clones.TryGetValue(steamId, out var clone) && clone != null && clone.GameObject != null)
            {
                Object.Destroy(clone.GameObject);
            }

            _clones.Remove(steamId);
        }

        public static IEnumerable<Clone> GetAllClones() => _clones.Values;

        private static CloneCustomizationApplier GetOrCreateCustomizationApplier(GameObject obj)
        {
            var existing = obj.GetComponent<CloneCustomizationApplier>();
            if (existing != null)
                return existing;

            Transform hatSlot = obj.transform.Find("HatSlot") ?? obj.transform.Find("hatSlot");
            Transform wingSlot = obj.transform.Find("wingSlot") ?? obj.transform.Find("WingSlot");

            GameObject hatObject = hatSlot?.gameObject;
            GameObject wingObject = wingSlot?.gameObject;

            SpriteRenderer hatRenderer = hatSlot?.GetComponent<SpriteRenderer>();
            SpriteRenderer wingRenderer = wingSlot?.GetComponent<SpriteRenderer>();

            SpriteMask hatMask = null;
            SpriteMask wingMask = null;

            if (hatSlot != null)
            {
                var hatMaskTransform = hatSlot.Find("Sprite Mask");
                if (hatMaskTransform != null)
                    hatMask = hatMaskTransform.GetComponent<SpriteMask>();
            }

            if (wingSlot != null)
            {
                var wingMaskTransform = wingSlot.Find("Sprite Mask");
                if (wingMaskTransform != null)
                    wingMask = wingMaskTransform.GetComponent<SpriteMask>();
            }

            var applier = obj.AddComponent<CloneCustomizationApplier>();
            applier.Initialize(
                hatObject,
                wingObject,
                hatRenderer,
                wingRenderer,
                hatMask,
                wingMask
            );

            return applier;
        }

    }

    public class Clone(string steamId, string playerName, GameObject obj)
    {
        public string SteamId { get; } = steamId;
        public string PlayerName { get; private set; } = playerName;
        public GameObject GameObject { get; } = obj;
        public CharacterMovement Movement => GameObject?.GetComponent<CharacterMovement>();

        //BUG NRE at GetComponent at unknown times, did not catch error live. Only happened 1 time so far.
        public void SetColor(Color color)
        {
            if (GameObject == null) return;

            var renderer = GameObject.GetComponent<SpriteRenderer>();
            if (!renderer) return;
            renderer.color = color;
        }

        public void SetPosition(Vector3 pos)
        {
            if (GameObject == null) return;

            var cm = Movement;
            if (cm)
            {
                cm.StopMoving();
            }

            GameObject.transform.position = pos;
        }

        public void UpdateDisplayName(string newName)
        {
            if (string.IsNullOrWhiteSpace(newName))
                return;

            PlayerName = newName;

            if (GameObject == null)
                return;

            var marker = GameObject.GetComponent<IsMultiplayerClone>();
            if (marker != null)
                marker.displayName = newName;

            var namePlate = GameObject.transform.Find("NamePlate");
            if (namePlate != null)
            {
                var text = namePlate.GetComponent<TextMeshPro>();
                if (text != null)
                    text.text = newName;
            }
        }


        private void ApplyHat(BloobColourChange bloobColourChange, int hatIndex)
        {
            if (bloobColourChange.hatGameObject == null)
                return;

            if (hatIndex < 0)
            {
                bloobColourChange.hatGameObject.SetActive(false);
                return;
            }

            if (bloobColourChange.hatChoices == null || hatIndex >= bloobColourChange.hatChoices.Length)
                return;

            var choice = bloobColourChange.hatChoices[hatIndex];
            if (choice == null || choice.image == null)
                return;

            bloobColourChange.playerMask.sprite = choice.image;
            bloobColourChange.playerImage.sprite = choice.image;
            bloobColourChange.hatGameObject.SetActive(true);
        }

        private void ApplyWing(BloobColourChange bloobColourChange, int wingIndex)
        {
            if (bloobColourChange.wingGameObject == null)
                return;

            if (wingIndex < 0)
            {
                bloobColourChange.wingGameObject.SetActive(false);
                return;
            }

            if (bloobColourChange.wingChoices == null || wingIndex >= bloobColourChange.wingChoices.Length)
                return;

            var choice = bloobColourChange.wingChoices[wingIndex];
            if (choice == null || choice.image == null)
                return;

            bloobColourChange.wingMask.sprite = choice.image;
            bloobColourChange.wingImage.sprite = choice.image;
            bloobColourChange.wingGameObject.SetActive(true);
        }

        public void MoveTo(Vector2 target, float speed = -1f)
        {
            if (GameObject == null) return;

            var cm = Movement;
            if (!cm) return;

            if (Vector2.Distance(target, ToVector2(GameObject.transform.position)) >= 400f)
                speed = 400f;

            if (speed > 0)
                cm.moveSpeed = speed;

            cm.MoveTo(target);
        }

        private Vector2 ToVector2(Vector3 vector3) => new(vector3.x, vector3.y);
    }
}
