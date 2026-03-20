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
        public string hatName;
        public string wingName;
        public ColourLike bloobColour;

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
                    ?? new Dictionary<string, (int level, int prestige)>();
            }
        }

        public string[] soulData;
    }

    public class SkillTupleDto
    {
        public int Item1;
        public int Item2;
    }

    public class Vector3Like
    {
        public float x;
        public float y;
        public float z;

        public Vector3 ToVector3() => new Vector3(x, y, z);
        public Vector2 ToVector2() => new Vector2(x, y);
    }

    public class ColourLike
    {
        public float a;
        public float r;
        public float g;
        public float b;

        public Color ToColor() => new Color(r, g, b, a);
    }
}
