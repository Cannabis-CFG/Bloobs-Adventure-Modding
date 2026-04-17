using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Multi_bloob_adventure_idle
{
    public class PlayerData
    {
        public string name;
        public string steamId;
        public bool isDisconnecting;
        public Vector3Like currentPosition;
        public float runSpeed;
        public int activeHatIndex = -1;
        public int activeWingIndex = -1;
        public ColourLike bloobColour;
        public string clanId;
        public string clanName;
        public string clanTag;
        public Dictionary<string, double> skillExperienceData = [];
        public Dictionary<string, long> bossKillData = [];
        public bool isTurboSave;

        // Tuple storage is local. DTO storage is wire-safe.
        [JsonIgnore]
        public Dictionary<string, (int level, int prestige)> skillData;

        [JsonProperty("skillData")]
        private Dictionary<string, SkillTupleDto> SkillDataSurrogate
        {
            get
            {
                return skillData?.ToDictionary(
                    kvp => kvp.Key,
                    kvp => new SkillTupleDto { Item1 = kvp.Value.level, Item2 = kvp.Value.prestige });
            }
            set
            {
                skillData = value?.ToDictionary(
                    kvp => kvp.Key,
                    kvp => (kvp.Value.Item1, kvp.Value.Item2))
                    ?? [];
            }
        }

        public string[] soulData;
    }

    public class SkillTupleDto
    {
        public int Item1;
        public int Item2;
    }

    public class ClanStateDto
    {
        public string clanId;
        public string name;
        public string tag;
        public string ownerSteamId;
        public string description;
        public string viewerRole;
        public bool viewerIsMember;
        public bool viewerCanManageMembers;
        public bool viewerCanManagePermissions;
        public bool viewerCanManageUpgrades;
        public bool viewerCanPrestigeSkills;
        public bool isPublicProfile;
        public Dictionary<string, ClanSkillDto> skills;
        public List<ClanMemberDto> members;
        public Dictionary<string, ClanContributionDto> contributionsByMember;
        public Dictionary<string, long> totalBossKillsByBoss;
        public Dictionary<string, Dictionary<string, bool>> rolePermissions;
        public List<ClanUpgradeDto> upgrades;
        public ClanAggregateStatsDto aggregateStats;
    }

    public class ClanSkillDto
    {
        public int level;
        public int prestige;
        public double xp;
        public double totalExperience;
        public double nextLevelRequirement;
        public int nextPrestigeLevel;
        public int levelCap;
        public bool canPrestige;
    }

    public class ClanMemberDto
    {
        public string steamId;
        public string name;
        public string role;
        public string joinedAtUtc;
        public bool isOnline;
    }

    public class ClanContributionDto
    {
        public string steamId;
        public string name;
        public double totalExperience;
        public int totalLevelsGained;
        public int totalPrestigeGained;
        public long totalBossKills;
        public Dictionary<string, long> bossKills;
    }

    public class ClanUpgradeDto
    {
        public string id;
        public string name;
        public string description;
        public string bonusText;
        public bool unlocked;
        public bool purchased;
        public bool active;
        public string purchasedAtUtc;
        public List<string> requirementText;
        public int currentTier;
        public int nextTier;
        public int maxTier;
        public bool isInfinite;
        public bool canPurchaseNextTier;
        public string nextTierBonusText;
    }

    public class ClanAggregateStatsDto
    {
        public double totalExperience;
        public int totalLevelsGained;
        public int totalPrestigeGained;
        public long totalBossKills;
        public int memberCount;
    }

    public class Vector3Like
    {
        public float x;
        public float y;
        public float z;

        public Vector3 ToVector3() => new(x, y, z);
        public Vector2 ToVector2() => new(x, y);
    }

    public class ColourLike
    {
        public float a;
        public float r;
        public float g;
        public float b;

        public Color ToColor() => new(r, g, b, a);
    }
}
