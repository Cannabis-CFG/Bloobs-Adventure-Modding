using System.Linq;
using UnityEngine;

namespace Multi_bloob_adventure_idle
{
    public static class CloneCustomizationCache
    {
        private static Sprite[] _hatSprites = System.Array.Empty<Sprite>();
        private static Sprite[] _wingSprites = System.Array.Empty<Sprite>();

        public static bool IsReady => _hatSprites.Length > 0 || _wingSprites.Length > 0;

        public static void RefreshFromLocalPlayer()
        {
            var bloobColourChange = Object.FindObjectOfType<BloobColourChange>();
            if (bloobColourChange == null)
                return;

            _hatSprites = bloobColourChange.hatChoices != null
                ? bloobColourChange.hatChoices.Select(x => x != null ? x.image : null).ToArray()
                : System.Array.Empty<Sprite>();

            _wingSprites = bloobColourChange.wingChoices != null
                ? bloobColourChange.wingChoices.Select(x => x != null ? x.image : null).ToArray()
                : System.Array.Empty<Sprite>();
        }

        public static Sprite GetHatSprite(int index)
        {
            if (index < 0 || index >= _hatSprites.Length)
                return null;

            return _hatSprites[index];
        }

        public static Sprite GetWingSprite(int index)
        {
            if (index < 0 || index >= _wingSprites.Length)
                return null;

            return _wingSprites[index];
        }
    }
}