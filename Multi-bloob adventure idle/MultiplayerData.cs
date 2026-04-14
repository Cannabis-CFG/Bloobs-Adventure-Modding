using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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
                PlayerTransform = PlayerRoot?.transform;
                PlayerSpriteRenderer = PlayerRoot?.GetComponent<SpriteRenderer>();
                PlayerMovement = PlayerRoot?.GetComponent<CharacterMovement>();
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
        private static readonly Dictionary<string, string> NameMap = new()
        {
            { "WoodCutting", "Woodcutting" }
        };

        private static readonly HashSet<string> IgnoredChildren =
        [
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
        ];

        private static readonly Dictionary<string, FieldInfo> ExperienceFieldCache = [];

        private Dictionary<string, (int level, int prestige)> snapshot = [];
        private Dictionary<string, SkillTupleDto> dtoSnapshot = [];
        private Dictionary<string, double> experienceSnapshot = [];

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

        public Dictionary<string, double> GetExperienceSnapshot()
        {
            return experienceSnapshot.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        }

        public bool RefreshFromPlayer(GameObject player)
        {
            if (player == null)
                return false;

            var latestLevels = CaptureLevels(player, out var latestExperience);
            if (AreEqual(snapshot, latestLevels) && AreExperienceEqual(experienceSnapshot, latestExperience))
                return false;

            snapshot = latestLevels;
            experienceSnapshot = latestExperience;
            dtoSnapshot = latestLevels.ToDictionary(
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
            experienceSnapshot.Clear();
            IsDirty = false;
        }

        private Dictionary<string, (int level, int prestige)> CaptureLevels(GameObject player, out Dictionary<string, double> experienceData)
        {
            var skillData = new Dictionary<string, (int level, int prestige)>();
            experienceData = [];

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

                if (TryReadExperienceField(skillComponent, childName, out var expValue))
                    experienceData[childName] = expValue;
            }

            return skillData;
        }

        private static string ResolveSkillClassName(string childName)
        {
            return childName switch
            {
                "SoulBinding" => "SoulBinding",
                "Homesteading" => "HomeSteadingSkill",
                _ => childName + "Skill",
            };
        }

        private static string ResolveLevelFieldName(string childName)
        {
            return childName switch
            {
                "HitPoints" => "HitPointsLevel",
                "Mining" => "MiningLevel",
                "WoodCutting" => "woodcuttinglevel",
                "SoulBinding" => "SoulBindingLevel",
                "Thieving" => "ThievingLevel",
                "Fishing" => "FishingLevel",
                _ => childName + "Level",
            };
        }

        private static string ResolvePrestigeFieldName(string childName)
        {
            return childName switch
            {
                "HitPoints" => "HitPointsPrestigeLevel",
                "SoulBinding" => "SoulBindingPrestigeLevel",
                "BowCrafting" => "bowCraftingPrestigeLevel",
                "BeastMastery" => "beastMasteryPrestigeLevel",
                _ => childName.ToLower() + "PrestigeLevel",
            };
        }

        private static int ReadIntField(Component comp, string fieldName)
        {
            var type = comp.GetType();
            var field = type.GetField(
                fieldName,
                BindingFlags.IgnoreCase
                | BindingFlags.Public
                | BindingFlags.NonPublic
                | BindingFlags.Instance
            );

            if (field != null && field.FieldType == typeof(int))
                return (int)field.GetValue(comp);

            Debug.LogWarning($"Field '{fieldName}' not found on {type.Name}");
            return -1;
        }

        private static bool TryReadExperienceField(Component comp, string childName, out double value)
        {
            value = 0d;
            if (comp == null)
                return false;

            var type = comp.GetType();
            var cacheKey = type.FullName + "::" + childName;
            if (ExperienceFieldCache.TryGetValue(cacheKey, out var cachedField))
            {
                if (cachedField == null)
                    return false;

                return TryConvertNumeric(cachedField.GetValue(comp), out value) && value >= 0d;
            }

            var candidates = new[]
            {
                childName + "Experience",
                childName + "Exp",
                childName + "XP",
                childName.ToLowerInvariant() + "Experience",
                childName.ToLowerInvariant() + "Exp",
                childName.ToLowerInvariant() + "XP",
                "currentExperience",
                "currentExp",
                "currentXP",
                "experience",
                "exp",
                "xp"
            };

            var flags = BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            FieldInfo field = null;

            foreach (var candidate in candidates)
            {
                field = type.GetField(candidate, flags);
                if (IsSupportedNumericField(field))
                    break;
                field = null;
            }

            if (field == null)
            {
                field = type
                    .GetFields(flags)
                    .Where(IsSupportedNumericField)
                    .FirstOrDefault(f =>
                    {
                        var name = f.Name ?? string.Empty;
                        if (name.IndexOf("xp", StringComparison.OrdinalIgnoreCase) < 0 &&
                            name.IndexOf("exp", StringComparison.OrdinalIgnoreCase) < 0 &&
                            name.IndexOf("experience", StringComparison.OrdinalIgnoreCase) < 0)
                            return false;

                        if (name.IndexOf("level", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            name.IndexOf("prestige", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            name.IndexOf("next", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            name.IndexOf("needed", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            name.IndexOf("required", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            name.IndexOf("threshold", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            name.IndexOf("bonus", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            name.IndexOf("mult", StringComparison.OrdinalIgnoreCase) >= 0)
                            return false;

                        return true;
                    });
            }

            ExperienceFieldCache[cacheKey] = field;
            if (field == null)
                return false;

            return TryConvertNumeric(field.GetValue(comp), out value) && value >= 0d;
        }

        private static bool IsSupportedNumericField(FieldInfo field)
        {
            if (field == null)
                return false;

            var type = field.FieldType;
            return type == typeof(int) || type == typeof(long) || type == typeof(float) || type == typeof(double);
        }

        private static bool TryConvertNumeric(object raw, out double value)
        {
            switch (raw)
            {
                case int i:
                    value = i;
                    return true;
                case long l:
                    value = l;
                    return true;
                case float f:
                    value = f;
                    return true;
                case double d:
                    value = d;
                    return true;
                default:
                    value = 0d;
                    return false;
            }
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

        private static bool AreExperienceEqual(Dictionary<string, double> a, Dictionary<string, double> b)
        {
            if (ReferenceEquals(a, b))
                return true;

            if (a == null || b == null || a.Count != b.Count)
                return false;

            foreach (var kvp in a)
            {
                if (!b.TryGetValue(kvp.Key, out var other))
                    return false;

                if (Math.Abs(kvp.Value - other) > 0.0001d)
                    return false;
            }

            return true;
        }
    }

    public sealed class BossKillCache
    {
        private readonly Dictionary<string, long> snapshot = [];

        public bool IsDirty { get; private set; }

        public Dictionary<string, long> GetSnapshot()
        {
            return snapshot.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        }

        public void ReportKill(string bossId, int amount = 1)
        {
            if (string.IsNullOrWhiteSpace(bossId) || amount <= 0)
                return;

            if (!snapshot.ContainsKey(bossId))
                snapshot[bossId] = 0;

            snapshot[bossId] += amount;
            IsDirty = true;
        }

        public void SetCount(string bossId, long count)
        {
            if (string.IsNullOrWhiteSpace(bossId))
                return;

            snapshot[bossId] = Math.Max(0, count);
            IsDirty = true;
        }

        public void ClearDirty()
        {
            IsDirty = false;
        }

        public void Clear()
        {
            snapshot.Clear();
            IsDirty = false;
        }
    }

}
