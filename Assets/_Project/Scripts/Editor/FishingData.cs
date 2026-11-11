using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Offshore.EditorTools
{
    /// <summary>
    /// Creates the default fish species, fishing grounds and fishing settings as assets under Data/, once.
    /// Existing assets are never overwritten, so tuning done in the Inspector survives scene rebuilds.
    /// </summary>
    public static class FishingData
    {
        public const string Folder = "Assets/_Project/Data";
        const string SpeciesFolder = Folder + "/Species";
        const string GroundsFolder = Folder + "/Grounds";
        const string SettingsPath = Folder + "/Fishing Settings.asset";

        public const string HarborFlats = "Harbor Flats";
        public const string GullReef = "Gull Reef";
        public const string DeepDrop = "Deep Drop";

        public class Grounds
        {
            readonly Dictionary<string, FishingGround> byName = new Dictionary<string, FishingGround>();

            public void Add(FishingGround ground, string name) => byName[name] = ground;

            public FishingGround ByName(string name) => byName.TryGetValue(name, out var g) ? g : throw new InvalidOperationException($"No fishing ground '{name}'.");
        }

        public static FishingSettings LoadOrCreate(out Grounds grounds)
        {
            EnsureFolder(SpeciesFolder);
            EnsureFolder(GroundsFolder);

            FishSpecies mackerel = Species("Mackerel", 0.3f, 1.6f, new Vector2(0.25f, 0.45f), 2f, 0.6f, new Color(0.35f, 0.55f, 0.62f),
                "Fast, schooling and everywhere. The bread and butter of the harbor.");
            FishSpecies flounder = Species("Flounder", 0.4f, 4.5f, new Vector2(0.25f, 0.6f), 2.5f, 0.5f, new Color(0.62f, 0.52f, 0.38f),
                "A flatfish that lies hidden on sandy bottoms.");
            FishSpecies bass = Species("Sea Bass", 0.8f, 9f, new Vector2(0.3f, 0.8f), 3f, 0.45f, new Color(0.5f, 0.55f, 0.6f),
                "Hunts around rocks and structure; the big ones live on the reef.");
            FishSpecies snapper = Species("Red Snapper", 1.5f, 14f, new Vector2(0.35f, 0.85f), 3.5f, 0.4f, new Color(0.88f, 0.32f, 0.26f),
                "A reef fish, bright red and good eating.");
            FishSpecies mahi = Species("Mahi-mahi", 3f, 22f, new Vector2(0.7f, 1.4f), 4f, 0.3f, new Color(0.42f, 0.78f, 0.3f),
                "Fast and colourful, it follows warm blue water.");
            FishSpecies tuna = Species("Yellowfin Tuna", 8f, 70f, new Vector2(0.7f, 1.8f), 5f, 0.15f, new Color(0.25f, 0.32f, 0.55f),
                "A powerful pelagic fish of deep water. One fills a good part of the hold.");

            grounds = new Grounds();
            grounds.Add(Ground(HarborFlats, "Shallow sandy flats just outside the harbor. Small fish and quick bites.",
                "3–6 m", "Sand and seagrass", DangerRating.Safe, new Vector2(3f, 7f), new Color(0.2f, 0.82f, 0.76f),
                Entry(mackerel, 50f, 0f, 0.7f), Entry(flounder, 35f, 0f, 0.6f), Entry(bass, 15f, 0f, 0.35f)), HarborFlats);
            grounds.Add(Ground(GullReef, "Coral reef south-east of Gull Island. Mid-size reef fish.",
                "8–15 m", "Coral and rock", DangerRating.Moderate, new Vector2(5f, 10f), new Color(0.98f, 0.8f, 0.15f),
                Entry(snapper, 40f, 0f, 0.8f), Entry(bass, 35f, 0.2f, 1f), Entry(mackerel, 15f, 0.3f, 1f), Entry(mahi, 10f, 0f, 0.4f)), GullReef);
            grounds.Add(Ground(DeepDrop, "Where the shelf falls away north of the islands. Slow bites, big pelagic fish.",
                "60–200 m", "Steep drop-off", DangerRating.High, new Vector2(8f, 16f), new Color(1f, 0.5f, 0.2f),
                Entry(mahi, 50f, 0.2f, 1f), Entry(tuna, 35f, 0f, 0.75f), Entry(mackerel, 15f, 0.6f, 1f)), DeepDrop);

            var settings = AssetDatabase.LoadAssetAtPath<FishingSettings>(SettingsPath);
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<FishingSettings>();
                AssetDatabase.CreateAsset(settings, SettingsPath);
            }
            AssetDatabase.SaveAssets();
            return settings;
        }

        static FishSpecies Species(string name, float minKg, float maxKg, Vector2 length, float reelBase, float reelPerKg, Color color, string note)
        {
            string path = $"{SpeciesFolder}/{name}.asset";
            var species = AssetDatabase.LoadAssetAtPath<FishSpecies>(path);
            if (species != null) return species;

            species = ScriptableObject.CreateInstance<FishSpecies>();
            species.Configure(name, minKg, maxKg, length, reelBase, reelPerKg, color, note);
            AssetDatabase.CreateAsset(species, path);
            return species;
        }

        static FishingGround Ground(string name, string description, string depth, string bottom, DangerRating danger, Vector2 biteDelay, Color marker, params FishingGround.CatchEntry[] entries)
        {
            string path = $"{GroundsFolder}/{name}.asset";
            var ground = AssetDatabase.LoadAssetAtPath<FishingGround>(path);
            if (ground != null) return ground;

            ground = ScriptableObject.CreateInstance<FishingGround>();
            ground.Configure(name, description, depth, bottom, danger, biteDelay, marker, new List<FishingGround.CatchEntry>(entries));
            AssetDatabase.CreateAsset(ground, path);
            return ground;
        }

        static FishingGround.CatchEntry Entry(FishSpecies species, float chance, float minSize, float maxSize) =>
            new FishingGround.CatchEntry { species = species, chance = chance, minSize = minSize, maxSize = maxSize };

        static void EnsureFolder(string path)
        {
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            if (!AssetDatabase.IsValidFolder(path)) AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
