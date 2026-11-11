using System;
using System.Collections.Generic;
using UnityEngine;

namespace Offshore
{
    public enum DangerRating
    {
        Safe,
        Moderate,
        High,
    }

    /// <summary>
    /// What a fishing ground is like and what bites there. Where it is on the map is set by a
    /// <see cref="FishingGroundZone"/> in the scene.
    /// </summary>
    [CreateAssetMenu(menuName = "OFFSHORE/Fishing Ground", fileName = "Fishing Ground")]
    public class FishingGround : ScriptableObject
    {
        [Serializable]
        public class CatchEntry
        {
            public FishSpecies species;
            [Tooltip("Relative chance of this species biting here.")]
            [Min(0f)] public float chance = 1f;
            [Tooltip("Which part of the species' weight range turns up here (0 = smallest, 1 = largest).")]
            [Range(0f, 1f)] public float minSize;
            [Range(0f, 1f)] public float maxSize = 1f;
        }

        [SerializeField] string displayName = "Fishing Ground";
        [TextArea] [SerializeField] string description;
        [SerializeField] string depth = "10–20 m";
        [SerializeField] string bottom = "Sand";
        [Tooltip("Shown to the player now; hazards that use it arrive with the storm milestone.")]
        [SerializeField] DangerRating danger = DangerRating.Safe;
        [Tooltip("Seconds of waiting before a bite, random between x and y.")]
        [SerializeField] Vector2 biteDelay = new Vector2(4f, 9f);
        [SerializeField] Color markerColor = Color.cyan;
        [SerializeField] List<CatchEntry> catches = new List<CatchEntry>();

        public string DisplayName => displayName;
        public string Description => description;
        public string Depth => depth;
        public string Bottom => bottom;
        public DangerRating Danger => danger;
        public Vector2 BiteDelay => biteDelay;
        public Color MarkerColor => markerColor;
        public IReadOnlyList<CatchEntry> Catches => catches;

        public float RollBiteDelay(System.Random rng) => Mathf.Lerp(biteDelay.x, biteDelay.y, (float)rng.NextDouble());

        /// <summary>Picks what bites: species by chance, weight within this ground's size band, skewed towards smaller fish.</summary>
        public CaughtFish RollCatch(System.Random rng)
        {
            float total = 0f;
            foreach (var entry in catches)
            {
                if (entry.species != null) total += entry.chance;
            }
            if (total <= 0f) throw new InvalidOperationException($"{displayName} has no catch entries.");

            float roll = (float)rng.NextDouble() * total;
            CatchEntry chosen = null;
            foreach (var entry in catches)
            {
                if (entry.species == null) continue;
                chosen = entry;
                roll -= entry.chance;
                if (roll < 0f) break;
            }

            float size = Mathf.Lerp(chosen.minSize, Mathf.Max(chosen.minSize, chosen.maxSize), Mathf.Pow((float)rng.NextDouble(), 1.6f));
            float kg = Mathf.Round(chosen.species.KgAt(size) * 10f) / 10f;
            kg = Mathf.Clamp(kg, chosen.species.MinKg, chosen.species.MaxKg);
            return new CaughtFish(chosen.species, kg, displayName);
        }

        public string SpeciesList()
        {
            var names = new List<string>();
            foreach (var entry in catches)
            {
                if (entry.species != null && !names.Contains(entry.species.DisplayName)) names.Add(entry.species.DisplayName);
            }
            return string.Join(", ", names);
        }

        public void Configure(string name, string text, string depthText, string bottomText, DangerRating rating, Vector2 delay, Color marker, List<CatchEntry> entries)
        {
            displayName = name;
            description = text;
            depth = depthText;
            bottom = bottomText;
            danger = rating;
            biteDelay = delay;
            markerColor = marker;
            catches = entries;
        }
    }
}
