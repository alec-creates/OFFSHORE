using UnityEngine;

namespace Offshore
{
    /// <summary>One kind of fish. Which grounds it lives at, and how big it runs there, is set per FishingGround.</summary>
    [CreateAssetMenu(menuName = "OFFSHORE/Fish Species", fileName = "Fish Species")]
    public class FishSpecies : ScriptableObject
    {
        [SerializeField] string displayName = "Fish";
        [TextArea] [SerializeField] string note;

        [Header("Size")]
        [SerializeField, Min(0.05f)] float minKg = 0.5f;
        [SerializeField, Min(0.05f)] float maxKg = 2f;
        [Tooltip("Body length in metres at the smallest and largest weight (used for the catch model).")]
        [SerializeField] Vector2 lengthRange = new Vector2(0.3f, 0.6f);

        [Header("Landing time")]
        [Tooltip("Seconds to reel in the smallest fish.")]
        [SerializeField, Min(0.1f)] float reelBaseSeconds = 2.5f;
        [Tooltip("Extra reel seconds per kg.")]
        [SerializeField, Min(0f)] float reelSecondsPerKg = 0.4f;

        [Header("Look")]
        [SerializeField] Color color = new Color(0.4f, 0.55f, 0.65f);

        public string DisplayName => displayName;
        public string Note => note;
        public float MinKg => minKg;
        public float MaxKg => Mathf.Max(minKg, maxKg);
        public Color Color => color;

        /// <summary>Weight at a 0–1 point of this species' size range.</summary>
        public float KgAt(float size01) => Mathf.Lerp(MinKg, MaxKg, Mathf.Clamp01(size01));

        public float LengthFor(float kg) => Mathf.Lerp(lengthRange.x, lengthRange.y, Mathf.InverseLerp(MinKg, MaxKg, kg));

        public float ReelSecondsFor(float kg) => reelBaseSeconds + reelSecondsPerKg * Mathf.Max(0f, kg - MinKg);

        /// <summary>Sets every field; used by the scene builder to create the default assets.</summary>
        public void Configure(string name, float minimumKg, float maximumKg, Vector2 lengths, float reelBase, float reelPerKg, Color tint, string description)
        {
            displayName = name;
            minKg = minimumKg;
            maxKg = maximumKg;
            lengthRange = lengths;
            reelBaseSeconds = reelBase;
            reelSecondsPerKg = reelPerKg;
            color = tint;
            note = description;
        }
    }
}
