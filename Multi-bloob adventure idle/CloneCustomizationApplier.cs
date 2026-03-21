using UnityEngine;

namespace Multi_bloob_adventure_idle
{
    public class CloneCustomizationApplier : MonoBehaviour
    {
        [SerializeField] private GameObject hatObject;
        [SerializeField] private GameObject wingObject;

        [SerializeField] private SpriteRenderer hatRenderer;
        [SerializeField] private SpriteRenderer wingRenderer;

        [SerializeField] private SpriteMask hatMask;
        [SerializeField] private SpriteMask wingMask;

        public void Initialize(
            GameObject hatObj,
            GameObject wingObj,
            SpriteRenderer hatSpriteRenderer,
            SpriteRenderer wingSpriteRenderer,
            SpriteMask hatSpriteMask,
            SpriteMask wingSpriteMask)
        {
            hatObject = hatObj;
            wingObject = wingObj;
            hatRenderer = hatSpriteRenderer;
            wingRenderer = wingSpriteRenderer;
            hatMask = hatSpriteMask;
            wingMask = wingSpriteMask;
        }

        public void ApplyHat(int hatIndex)
        {
            if (hatObject == null)
                return;

            if (hatIndex < 0)
            {
                hatObject.SetActive(false);
                return;
            }

            var sprite = CloneCustomizationCache.GetHatSprite(hatIndex);
            if (sprite == null)
            {
                hatObject.SetActive(false);
                return;
            }

            if (hatRenderer != null)
                hatRenderer.sprite = sprite;

            if (hatMask != null)
                hatMask.sprite = sprite;

            hatObject.SetActive(true);
        }

        public void ApplyWing(int wingIndex)
        {
            if (wingObject == null)
                return;

            if (wingIndex < 0)
            {
                wingObject.SetActive(false);
                return;
            }

            var sprite = CloneCustomizationCache.GetWingSprite(wingIndex);
            if (sprite == null)
            {
                wingObject.SetActive(false);
                return;
            }

            if (wingRenderer != null)
                wingRenderer.sprite = sprite;

            if (wingMask != null)
                wingMask.sprite = sprite;

            wingObject.SetActive(true);
        }

        public void ApplyAll(int hatIndex, int wingIndex)
        {
            ApplyHat(hatIndex);
            ApplyWing(wingIndex);
        }
    }
}