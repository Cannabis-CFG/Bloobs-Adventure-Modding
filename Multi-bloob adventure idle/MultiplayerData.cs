using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Multi_bloob_adventure_idle
{
    public sealed class LocalPlayerRuntimeCache
    {
        public GameObject PlayerRoot { get; private set; }
        public Transform PlayerTransform { get; private set; }
        public SpriteRenderer PlayerSpriteRenderer { get; private set; }
        public CharacterMovement PlayerMovement { get; private set; }

        public bool TryResolve(bool forceRefresh = false)
        {
            if (forceRefresh || !IsValid())
            {
                PlayerRoot = GameObject.Find("BloobCharacter");
                PlayerTransform = PlayerRoot != null ? PlayerRoot.transform : null;
                PlayerSpriteRenderer = PlayerRoot != null ? PlayerRoot.GetComponent<SpriteRenderer>() : null;
                PlayerMovement = PlayerRoot != null ? PlayerRoot.GetComponent<CharacterMovement>() : null;
            }

            return IsValid();
        }

        public float GetRunSpeed()
        {
            if (PlayerMovement == null || PlayerMovement.dexteritySkill == null)
                return 0f;

            return PlayerMovement.dexteritySkill.runSpeed;
        }

        public void Clear()
        {
            PlayerRoot = null;
            PlayerTransform = null;
            PlayerSpriteRenderer = null;
            PlayerMovement = null;
        }

        private bool IsValid()
        {
            return PlayerRoot != null && PlayerTransform != null;
        }
    }

    public sealed class SkillDataCache
    {
        private static readonly Dictionary<string, string> NameMap = new Dictionary<string, string>
        {
            { "WoodCutting", "Woodcutting" }
        };

        private static readonly HashSet<string> IgnoredChildren = new HashSet<string>
        {
            "Weapon Point",
            "MagicWeapon Point",
            "RangeWeapon Point",
            "Melee Weapon",
            "MeleeWeapon",
            "MagicProjectile",
            "RangeProjectile",
            "wingSlot",
            "hatSlot",
            "Canvas"
        };

        private Dictionary<string, (int level, int prestige)> snapshot = new Dictionary<string, (int level, int prestige)>();
        private Dictionary<string, SkillTupleDto> dtoSnapshot = new Dictionary<string, SkillTupleDto>();

        public bool IsDirty { get; private set; }

        public Dictionary<string, SkillTupleDto> GetDtoSnapshot()
        {
            return dtoSnapshot.ToDictionary(
                kvp => kvp.Key,
                kvp => new SkillTupleDto { Item1 = kvp.Value.Item1, Item2 = kvp.Value.Item2 });
        }

        public Dictionary<string, (int level, int prestige)> Clone()
        {
            return snapshot.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        }

        public bool RefreshFromPlayer(GameObject player)
        {
            if (player == null)
                return false;

            var latest = Capture(player);
            if (AreEqual(snapshot, latest))
                return false;

            snapshot = latest;
            dtoSnapshot = latest.ToDictionary(
                kvp => kvp.Key,
                kvp => new SkillTupleDto { Item1 = kvp.Value.level, Item2 = kvp.Value.prestige });
            IsDirty = true;
            return true;
        }

        public void MarkDirty()
        {
            IsDirty = true;
        }

        public void ClearDirty()
        {
            IsDirty = false;
        }

        public void Clear()
        {
            snapshot.Clear();
            dtoSnapshot.Clear();
            IsDirty = false;
        }

        private Dictionary<string, (int level, int prestige)> Capture(GameObject player)
        {
            var skillData = new Dictionary<string, (int level, int prestige)>();

            foreach (Transform child in player.transform)
            {
                if (child.name == null || IgnoredChildren.Contains(child.name))
                    continue;

                var childName = NameMap.TryGetValue(child.name, out var mappedName) ? mappedName : child.name;
                var skillClassName = ResolveSkillClassName(childName);
                var skillComponent = child.GetComponent(skillClassName);
                if (skillComponent == null)
                    continue;

                var levelFieldName = ResolveLevelFieldName(childName);
                var prestigeFieldName = ResolvePrestigeFieldName(childName);

                var level = ReadIntField(skillComponent, levelFieldName);
                var prestige = ReadIntField(skillComponent, prestigeFieldName);

                skillData[childName] = (level, prestige);
            }

            return skillData;
        }

        private static string ResolveSkillClassName(string childName)
        {
            switch (childName)
            {
                case "SoulBinding":
                    return "SoulBinding";
                case "Homesteading":
                    return "HomeSteadingSkill";
                default:
                    return childName + "Skill";
            }
        }

        private static string ResolveLevelFieldName(string childName)
        {
            switch (childName)
            {
                case "HitPoints":
                    return "HitPointsLevel";
                case "Mining":
                    return "MiningLevel";
                case "WoodCutting":
                    return "woodcuttinglevel";
                case "SoulBinding":
                    return "SoulBindingLevel";
                case "Thieving":
                    return "ThievingLevel";
                case "Fishing":
                    return "FishingLevel";
                default:
                    return childName + "Level";
            }
        }

        private static string ResolvePrestigeFieldName(string childName)
        {
            switch (childName)
            {
                case "HitPoints":
                    return "HitPointsPrestigeLevel";
                case "SoulBinding":
                    return "SoulBindingPrestigeLevel";
                case "BowCrafting":
                    return "bowCraftingPrestigeLevel";
                case "BeastMastery":
                    return "beastMasteryPrestigeLevel";
                default:
                    return childName.ToLower() + "PrestigeLevel";
            }
        }

        private static int ReadIntField(Component comp, string fieldName)
        {
            var type = comp.GetType();
            var field = type.GetField(
                fieldName,
                System.Reflection.BindingFlags.IgnoreCase
                | System.Reflection.BindingFlags.Public
                | System.Reflection.BindingFlags.NonPublic
                | System.Reflection.BindingFlags.Instance
            );

            if (field != null && field.FieldType == typeof(int))
                return (int)field.GetValue(comp);

            Debug.LogWarning($"Field '{fieldName}' not found on {type.Name}");
            return -1;
        }

        private static bool AreEqual(
            Dictionary<string, (int level, int prestige)> a,
            Dictionary<string, (int level, int prestige)> b)
        {
            if (ReferenceEquals(a, b))
                return true;

            if (a == null || b == null || a.Count != b.Count)
                return false;

            foreach (var kvp in a)
            {
                if (!b.TryGetValue(kvp.Key, out var other))
                    return false;

                if (kvp.Value.level != other.level || kvp.Value.prestige != other.prestige)
                    return false;
            }

            return true;
        }
    }
}
