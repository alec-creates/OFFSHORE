using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Offshore.EditorTools
{
    /// <summary>
    /// Generates the Sea scene from code: light, the stylized sea, the port and harbor, islands, buoys,
    /// the charter boat, the high-angle camera and the HUD.
    /// Menu: OFFSHORE > Build Sea Scene.
    /// Batch: -executeMethod Offshore.EditorTools.WorldBuilder.BuildFromCommandLine
    /// Materials and meshes are created once and updated in place, so tweaks to materials survive a rebuild;
    /// the scene file itself is regenerated (the previous one is backed up to SceneBackups/).
    /// </summary>
    public static class WorldBuilder
    {
        const string Root = "Assets/_Project";
        public const string ScenePath = Root + "/Scenes/Sea.unity";
        const string MaterialFolder = Root + "/Materials";
        const string BackupFolder = "SceneBackups";

        /// <summary>Playable area is ±MapHalfSize on X and Z.</summary>
        public const float MapHalfSize = 300f;
        const float CoastZ = -262f;
        public static readonly Vector3 BoatSpawn = new Vector3(0f, 0f, -238f);

        static readonly Dictionary<string, Material> materials = new Dictionary<string, Material>();
        static readonly List<SeaSurface.Shoal> shoals = new List<SeaSurface.Shoal>();
        static Font font;

        [MenuItem("OFFSHORE/Build Sea Scene")]
        public static void BuildFromMenu()
        {
            if (File.Exists(ScenePath) &&
                !EditorUtility.DisplayDialog("Rebuild the Sea scene?",
                    $"The scene is regenerated from code. The current file is backed up to {BackupFolder}/ first; materials and meshes are kept.",
                    "Rebuild", "Cancel"))
            {
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Build();
        }

        public static void BuildFromCommandLine()
        {
            try
            {
                Build();
                EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                EditorApplication.Exit(1);
            }
        }

        static void Build()
        {
            EnsureFolder(Root + "/Scenes");
            EnsureFolder(MaterialFolder);
            EnsureFolder(LowPolyMeshes.Folder);
            materials.Clear();
            shoals.Clear();
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            BackUpExistingScene();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            ConfigureShadows();
            BuildLight();
            SeaSurface sea = BuildSea();
            BuildPort();
            BuildIslands();
            BuildBuoys();
            BuildMapBounds();
            sea.SetShoals(shoals);

            BoatController boat = BuildBoat(sea);
            BuildCamera(boat);
            BuildHud(boat);

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            Debug.Log($"OFFSHORE: built {ScenePath}. Press Play to take the boat out.");
        }

        static void BackUpExistingScene()
        {
            if (!File.Exists(ScenePath)) return;
            Directory.CreateDirectory(BackupFolder);
            File.Copy(ScenePath, Path.Combine(BackupFolder, $"Sea_{DateTime.Now:yyyyMMdd_HHmmss}.unity"), overwrite: true);
        }

        // ---------------------------------------------------------------- Light, sea, bounds

        static void ConfigureShadows()
        {
            // The orthographic camera sits ~150 m back from what it looks at; default shadow distance would show none.
            foreach (string guid in AssetDatabase.FindAssets("t:UniversalRenderPipelineAsset"))
            {
                var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(AssetDatabase.GUIDToAssetPath(guid));
                if (asset == null || asset.shadowDistance >= 260f) continue;
                asset.shadowDistance = 260f;
                EditorUtility.SetDirty(asset);
            }
        }

        static void BuildLight()
        {
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.95f, 0.85f);
            sun.intensity = 1.25f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.7f;
            sun.transform.rotation = Quaternion.Euler(52f, -25f, 0f);
            RenderSettings.sun = sun;

            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.62f, 0.72f, 0.84f);
            RenderSettings.ambientEquatorColor = new Color(0.46f, 0.56f, 0.64f);
            RenderSettings.ambientGroundColor = new Color(0.22f, 0.3f, 0.36f);
            RenderSettings.fog = false;
        }

        static SeaSurface BuildSea()
        {
            var shader = Shader.Find("Offshore/Stylized Water") ?? throw new InvalidOperationException("Stylized Water shader not found (did it compile?)");
            Material water = LoadOrCreateMaterial("Sea", () => new Material(shader));

            var seaObject = new GameObject("Sea", typeof(MeshFilter), typeof(MeshRenderer));
            var renderer = seaObject.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = water;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            var sea = seaObject.AddComponent<SeaSurface>();

            // Flat deep water far beyond the map, a little below the troughs.
            var far = GameObject.CreatePrimitive(PrimitiveType.Plane);
            far.name = "Open Sea Beyond";
            Object.DestroyImmediate(far.GetComponent<Collider>());
            far.transform.position = new Vector3(0f, -0.45f, 0f);
            far.transform.localScale = new Vector3(300f, 1f, 300f);
            far.GetComponent<MeshRenderer>().sharedMaterial = Lit("Deep Water", new Color(0.04f, 0.2f, 0.38f), 0.3f);
            far.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            return sea;
        }

        static void BuildMapBounds()
        {
            var bounds = new GameObject("Map Bounds").transform;
            const float wall = 10f;
            float span = MapHalfSize * 2f + wall * 2f;
            Wall(bounds, "North Edge", new Vector3(0f, 0f, MapHalfSize + wall * 0.5f), new Vector3(span, 10f, wall));
            Wall(bounds, "South Edge", new Vector3(0f, 0f, -MapHalfSize - wall * 0.5f), new Vector3(span, 10f, wall));
            Wall(bounds, "East Edge", new Vector3(MapHalfSize + wall * 0.5f, 0f, 0f), new Vector3(wall, 10f, span));
            Wall(bounds, "West Edge", new Vector3(-MapHalfSize - wall * 0.5f, 0f, 0f), new Vector3(wall, 10f, span));
        }

        static void Wall(Transform parent, string name, Vector3 position, Vector3 size)
        {
            var wall = new GameObject(name);
            wall.transform.SetParent(parent, false);
            wall.transform.localPosition = position;
            wall.AddComponent<BoxCollider>().size = size;
        }

        // ---------------------------------------------------------------- Port and harbor (south edge)

        static void BuildPort()
        {
            var port = new GameObject("Port").transform;
            Material sand = Lit("Sand", new Color(0.86f, 0.78f, 0.56f));
            Material grass = Lit("Grass", new Color(0.42f, 0.62f, 0.32f));
            Material rock = Lit("Rock", new Color(0.45f, 0.45f, 0.47f));
            Material wood = Lit("Wood", new Color(0.55f, 0.4f, 0.26f));
            Material white = Lit("Paint White", new Color(0.93f, 0.93f, 0.9f));

            // Coastline: sand strip at the water, grass behind.
            Block("Quay", port, new Vector3(0f, 0.6f, CoastZ - 6f), new Vector3(MapHalfSize * 2.6f, 2.4f, 12f), sand);
            Block("Land", port, new Vector3(0f, 0.9f, CoastZ - 52f), new Vector3(MapHalfSize * 2.6f, 2.4f, 80f), grass);
            shoals.Add(new SeaSurface.Shoal(new Vector2(-400f, CoastZ), new Vector2(400f, CoastZ), 0f, 32f));

            // Breakwaters with a 40 m harbor mouth facing north.
            Breakwater(port, rock, new Vector2(-45f, CoastZ), new Vector2(-45f, -215f), new Vector2(-20f, -200f), 11);
            Breakwater(port, rock, new Vector2(45f, CoastZ), new Vector2(45f, -215f), new Vector2(20f, -200f), 23);

            // Lighthouse on the east breakwater head.
            var lighthouse = new GameObject("Lighthouse").transform;
            lighthouse.SetParent(port, false);
            lighthouse.localPosition = new Vector3(20f, 0f, -200f);
            Mesh tower = LowPolyMeshes.Frustum("Lighthouse Tower", 8, 1.6f, 1.1f, 0f, 9f);
            MeshPiece("Tower", lighthouse, tower, Vector3.up * 1.5f, white);
            MeshPiece("Band", lighthouse, LowPolyMeshes.Frustum("Lighthouse Band", 8, 1.42f, 1.33f, 0f, 1.5f), new Vector3(0f, 5f, 0f), Lit("Paint Red", new Color(0.8f, 0.15f, 0.12f)));
            MeshPiece("Lamp", lighthouse, LowPolyMeshes.Frustum("Lighthouse Lamp", 8, 0.9f, 0.9f, 0f, 1f), new Vector3(0f, 10.5f, 0f), Lit("Lamp Glow", new Color(1f, 0.9f, 0.55f), emission: new Color(1f, 0.8f, 0.4f) * 1.5f));
            MeshPiece("Cap", lighthouse, LowPolyMeshes.Frustum("Lighthouse Cap", 8, 1.3f, 0f, 0f, 1.2f), new Vector3(0f, 11.5f, 0f), Lit("Paint Red", Color.red));

            // Pier on posts inside the harbor.
            var pier = new GameObject("Pier").transform;
            pier.SetParent(port, false);
            Block("Pier Deck", pier, new Vector3(-20f, 1.0f, -246f), new Vector3(4f, 0.25f, 32f), wood);
            for (float z = -260f; z <= -231f; z += 4f)
            {
                Block("Post", pier, new Vector3(-21.8f, 0.2f, z), new Vector3(0.35f, 2f, 0.35f), wood, collider: false);
                Block("Post", pier, new Vector3(-18.2f, 0.2f, z), new Vector3(0.35f, 2f, 0.35f), wood, collider: false);
            }
            var pierCollider = new GameObject("Pier Collider");
            pierCollider.transform.SetParent(pier, false);
            pierCollider.transform.localPosition = new Vector3(-20f, 0f, -246f);
            pierCollider.AddComponent<BoxCollider>().size = new Vector3(4.4f, 3f, 32f);

            // Waterfront buildings.
            Building(port, "Fish Market", new Vector3(-22f, 1.8f, CoastZ - 16f), new Vector3(14f, 4f, 9f), white, Lit("Roof Blue", new Color(0.2f, 0.42f, 0.7f)));
            Building(port, "Bait & Tackle", new Vector3(2f, 1.8f, CoastZ - 14f), new Vector3(8f, 3f, 6f), Lit("Paint Yellow", new Color(0.95f, 0.8f, 0.35f)), Lit("Roof Red", new Color(0.72f, 0.2f, 0.16f)));
            Building(port, "Boat Shed", new Vector3(26f, 1.8f, CoastZ - 20f), new Vector3(16f, 5f, 11f), Lit("Paint Grey", new Color(0.62f, 0.64f, 0.66f)), Lit("Roof Dark", new Color(0.3f, 0.32f, 0.36f)));
            var random = new System.Random(5);
            foreach (float x in new[] { -120f, -95f, -70f, 75f, 100f, 130f, 160f })
            {
                Building(port, "House", new Vector3(x, 1.8f, CoastZ - 18f - (float)random.NextDouble() * 20f), new Vector3(7f, 3f, 6f), white,
                    (float)random.NextDouble() < 0.5f ? Lit("Roof Red", Color.red) : Lit("Roof Blue", Color.blue));
            }
        }

        static void Breakwater(Transform parent, Material rock, Vector2 a, Vector2 b, Vector2 c, int seed)
        {
            var group = new GameObject("Breakwater").transform;
            group.SetParent(parent, false);
            var random = new System.Random(seed);
            foreach (var (from, to) in new[] { (a, b), (b, c) })
            {
                float length = Vector2.Distance(from, to);
                Vector3 mid = new Vector3((from.x + to.x) * 0.5f, 0f, (from.y + to.y) * 0.5f);
                var collider = new GameObject("Breakwater Collider");
                collider.transform.SetParent(group, false);
                collider.transform.localPosition = mid;
                collider.transform.localRotation = Quaternion.LookRotation(new Vector3(to.x - from.x, 0f, to.y - from.y));
                collider.AddComponent<BoxCollider>().size = new Vector3(6f, 6f, length + 4f);
                shoals.Add(new SeaSurface.Shoal(from, to, 3.5f, 12f));

                for (float d = 0f; d <= length; d += 3.5f)
                {
                    Vector2 p = Vector2.Lerp(from, to, d / length);
                    float radius = 2.4f + (float)random.NextDouble() * 1.4f;
                    Mesh mesh = LowPolyMeshes.Frustum($"Breakwater Rock {random.Next(6)}", 6, radius, radius * 0.55f, -2f, 1.2f + (float)random.NextDouble() * 0.8f, 0.3f, random.Next());
                    var piece = MeshPiece("Rock", group, mesh, new Vector3(p.x, 0f, p.y), rock);
                    piece.transform.localRotation = Quaternion.Euler(0f, (float)random.NextDouble() * 360f, 0f);
                }
            }
        }

        static void Building(Transform parent, string name, Vector3 position, Vector3 size, Material walls, Material roof)
        {
            var building = new GameObject(name).transform;
            building.SetParent(parent, false);
            building.localPosition = position;
            Block("Walls", building, new Vector3(0f, size.y * 0.5f, 0f), size, walls);
            var roofMesh = LowPolyMeshes.Roof($"Roof {size.x:0}x{size.z:0}", size.x + 0.8f, size.y * 0.45f, size.z + 0.8f);
            MeshPiece("Roof", building, roofMesh, new Vector3(0f, size.y, 0f), roof, collider: false);
        }

        // ---------------------------------------------------------------- Islands, rocks, reef

        static void BuildIslands()
        {
            var islands = new GameObject("Islands").transform;
            Island(islands, "Gull Island", new Vector2(110f, 40f), 26f, 6f, 3, true);
            Island(islands, "Long Key", new Vector2(-150f, 120f), 38f, 4f, 5, true);
            Island(islands, "The Pinnacle", new Vector2(70f, 215f), 13f, 10f, 0, false);

            // Rocks breaking the surface.
            Material rock = Lit("Rock", Color.grey);
            var random = new System.Random(9);
            foreach (var spot in new[] { new Vector2(-70f, -60f), new Vector2(-62f, -52f), new Vector2(-78f, -48f), new Vector2(-66f, -70f), new Vector2(205f, 150f) })
            {
                float radius = 2.5f + (float)random.NextDouble() * 3f;
                Mesh mesh = LowPolyMeshes.Frustum($"Sea Rock {random.Next(4)}", 6, radius, radius * 0.4f, -3f, 1.5f + (float)random.NextDouble() * 2f, 0.3f, random.Next());
                var piece = MeshPiece("Sea Rock", islands, mesh, new Vector3(spot.x, 0f, spot.y), rock, collider: true);
                piece.transform.localRotation = Quaternion.Euler(0f, (float)random.NextDouble() * 360f, 0f);
                shoals.Add(new SeaSurface.Shoal(spot, spot, radius * 0.6f, 14f));
            }

            // Submerged reef: shallow water only, no collision.
            shoals.Add(new SeaSurface.Shoal(new Vector2(170f, -120f), new Vector2(200f, -95f), 0f, 34f));
        }

        static void Island(Transform parent, string name, Vector2 position, float radius, float height, int trees, bool beach)
        {
            var island = new GameObject(name).transform;
            island.SetParent(parent, false);
            island.localPosition = new Vector3(position.x, 0f, position.y);
            int seed = 17;
            foreach (char c in name) seed = seed * 31 + c;

            Material body = beach ? Lit("Grass", Color.green) : Lit("Cliff", new Color(0.52f, 0.5f, 0.46f));
            Mesh bodyMesh = LowPolyMeshes.Frustum($"{name} Body", 9, radius * 0.92f, radius * 0.55f, -3f, height, 0.22f, seed);
            MeshPiece("Body", island, bodyMesh, Vector3.zero, body, collider: true);
            if (beach)
            {
                Mesh beachMesh = LowPolyMeshes.Frustum($"{name} Beach", 11, radius * 1.08f, radius * 0.95f, -2f, 0.6f, 0.12f, seed + 1);
                MeshPiece("Beach", island, beachMesh, Vector3.zero, Lit("Sand", Color.yellow), collider: true);
            }
            shoals.Add(new SeaSurface.Shoal(position, position, radius * 0.95f, 28f));

            Material trunk = Lit("Trunk", new Color(0.45f, 0.32f, 0.2f));
            Material leaves = Lit("Leaves", new Color(0.24f, 0.5f, 0.26f));
            Mesh trunkMesh = LowPolyMeshes.Frustum("Tree Trunk", 5, 0.35f, 0.25f, 0f, 2.5f);
            Mesh canopyMesh = LowPolyMeshes.Frustum("Tree Canopy", 6, 2.2f, 0f, 0f, 4f);
            var random = new System.Random(seed);
            for (int i = 0; i < trees; i++)
            {
                float angle = (float)random.NextDouble() * Mathf.PI * 2f;
                float distance = (float)random.NextDouble() * radius * 0.35f;
                var spot = new Vector3(Mathf.Sin(angle) * distance, height - 0.2f, Mathf.Cos(angle) * distance);
                MeshPiece("Trunk", island, trunkMesh, spot, trunk);
                MeshPiece("Canopy", island, canopyMesh, spot + Vector3.up * 2.2f, leaves);
            }
        }

        static void BuildBuoys()
        {
            var buoys = new GameObject("Buoys").transform;
            Material red = Lit("Buoy Red", new Color(0.85f, 0.15f, 0.12f), 0.4f);
            Material green = Lit("Buoy Green", new Color(0.15f, 0.65f, 0.3f), 0.4f);
            Material yellow = Lit("Buoy Yellow", new Color(0.98f, 0.8f, 0.15f), 0.4f);

            // Channel out of the harbor.
            foreach (float z in new[] { -182f, -155f })
            {
                Buoy(buoys, "Channel Buoy (port)", new Vector3(-13f, 0f, z), red);
                Buoy(buoys, "Channel Buoy (starboard)", new Vector3(13f, 0f, z), green);
            }

            // Reef markers.
            foreach (var p in new[] { new Vector3(150f, 0f, -135f), new Vector3(215f, 0f, -80f), new Vector3(205f, 0f, -135f) })
            {
                Buoy(buoys, "Reef Marker", p, yellow);
            }
        }

        static void Buoy(Transform parent, string name, Vector3 position, Material material)
        {
            var buoy = new GameObject(name).transform;
            buoy.SetParent(parent, false);
            buoy.localPosition = position;
            MeshPiece("Float", buoy, LowPolyMeshes.Frustum("Buoy Float", 6, 0.8f, 0.6f, -0.4f, 0.6f), Vector3.zero, material);
            MeshPiece("Cone", buoy, LowPolyMeshes.Frustum("Buoy Cone", 6, 0.45f, 0f, 0f, 1.6f), new Vector3(0f, 0.6f, 0f), material);
            buoy.gameObject.AddComponent<FloatingBob>();
        }

        // ---------------------------------------------------------------- The charter boat (bow +Z)

        static BoatController BuildBoat(SeaSurface sea)
        {
            var boatObject = new GameObject("Charter Boat");
            boatObject.transform.position = BoatSpawn;
            var body = boatObject.AddComponent<Rigidbody>();
            body.mass = 4000f;
            body.useGravity = false;
            body.linearDamping = 0f;
            body.angularDamping = 0f;
            var hullCollider = boatObject.AddComponent<BoxCollider>();
            hullCollider.size = new Vector3(3.3f, 2f, 9.2f);
            hullCollider.center = new Vector3(0f, 0.3f, 0f);
            const string hullPhysicsPath = MaterialFolder + "/Hull.physicsMaterial";
            var physicsMaterial = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(hullPhysicsPath);
            if (physicsMaterial == null)
            {
                physicsMaterial = new PhysicsMaterial("Hull") { bounciness = 0.15f, dynamicFriction = 0.2f, staticFriction = 0.2f, bounceCombine = PhysicsMaterialCombine.Maximum };
                AssetDatabase.CreateAsset(physicsMaterial, hullPhysicsPath);
            }
            hullCollider.sharedMaterial = physicsMaterial;

            var input = boatObject.AddComponent<HelmInput>();
            var controller = boatObject.AddComponent<BoatController>();
            Assign(controller, "input", input);

            Material white = Lit("Hull White", new Color(0.95f, 0.95f, 0.96f), 0.4f);
            Material navy = Lit("Hull Navy", new Color(0.1f, 0.18f, 0.38f), 0.4f);
            Material teak = Lit("Deck Teak", new Color(0.66f, 0.5f, 0.32f));
            Material roofBlue = Lit("Boat Roof", new Color(0.18f, 0.48f, 0.78f), 0.3f);
            Material glass = Lit("Window Glass", new Color(0.08f, 0.12f, 0.16f), 0.9f);
            Material steel = Lit("Steel", new Color(0.78f, 0.8f, 0.83f), 0.7f);
            Material cooler = Lit("Cooler Orange", new Color(0.95f, 0.5f, 0.15f));

            var model = new GameObject("Model").transform;
            model.SetParent(boatObject.transform, false);
            MeshPiece("Hull", model, LowPolyMeshes.Hull("Boat Hull", 9.4f, 3.4f, -0.6f, 0.9f), Vector3.zero, white);
            MeshPiece("Boot Stripe", model, LowPolyMeshes.Hull("Boat Stripe", 9.5f, 3.48f, 0.05f, 0.4f), Vector3.zero, navy);
            MeshPiece("Deck", model, LowPolyMeshes.Hull("Boat Deck", 8.8f, 2.95f, 0.85f, 0.94f), Vector3.zero, teak);

            Block("Cabin", model, new Vector3(0f, 1.75f, 0.9f), new Vector3(2.5f, 1.65f, 2.8f), white, collider: false);
            Block("Cabin Roof", model, new Vector3(0f, 2.65f, 0.6f), new Vector3(2.9f, 0.16f, 3.6f), roofBlue, collider: false);
            Block("Windscreen", model, new Vector3(0f, 2.0f, 2.31f), new Vector3(2.2f, 0.6f, 0.05f), glass, collider: false);
            Block("Side Window P", model, new Vector3(-1.26f, 2.0f, 0.9f), new Vector3(0.05f, 0.5f, 2.0f), glass, collider: false);
            Block("Side Window S", model, new Vector3(1.26f, 2.0f, 0.9f), new Vector3(0.05f, 0.5f, 2.0f), glass, collider: false);
            Block("Bow Rail", model, new Vector3(0f, 1.35f, 3.6f), new Vector3(0.06f, 0.06f, 1.6f), steel, collider: false);

            // Outriggers and a short mast, angled out so they read from above.
            Pole(model, steel, new Vector3(-1.2f, 2.7f, 0.4f), Quaternion.Euler(-12f, 0f, 55f), 5.5f);
            Pole(model, steel, new Vector3(1.2f, 2.7f, 0.4f), Quaternion.Euler(-12f, 0f, -55f), 5.5f);
            Pole(model, steel, new Vector3(0f, 2.7f, 1.2f), Quaternion.identity, 1.3f);

            // Cockpit: fighting chair, fish box, rod holders.
            Block("Chair Base", model, new Vector3(0f, 1.2f, -2.8f), new Vector3(0.3f, 0.5f, 0.3f), steel, collider: false);
            Block("Chair Seat", model, new Vector3(0f, 1.5f, -2.8f), new Vector3(0.75f, 0.14f, 0.7f), white, collider: false);
            Block("Fish Box", model, new Vector3(-0.95f, 1.25f, -1.1f), new Vector3(0.8f, 0.6f, 1.2f), cooler, collider: false);
            foreach (float x in new[] { -1.5f, 1.5f })
            {
                foreach (float z in new[] { -3.9f, -2.2f })
                {
                    Block("Rod Holder", model, new Vector3(x, 1.25f, z), new Vector3(0.12f, 0.5f, 0.12f), steel, Quaternion.Euler(-20f, 0f, x * 8f), collider: false);
                }
            }

            // Wake trails: two from the stern corners and a broader prop wash down the middle.
            var foamShader = Shader.Find("Offshore/Foam") ?? throw new InvalidOperationException("Foam shader not found (did it compile?)");
            Material foam = LoadOrCreateMaterial("Wake Foam", () => new Material(foamShader));
            var wakes = new[]
            {
                Wake(boatObject.transform, "Wake Port", new Vector3(-1.45f, 0.35f, -4.4f), foam, 0.5f, 2.6f, 2.6f, 0.85f),
                Wake(boatObject.transform, "Wake Starboard", new Vector3(1.45f, 0.35f, -4.4f), foam, 0.5f, 2.6f, 2.6f, 0.85f),
                Wake(boatObject.transform, "Prop Wash", new Vector3(0f, 0.33f, -4.6f), foam, 1.4f, 3.8f, 3.5f, 0.55f),
            };

            var visuals = boatObject.AddComponent<BoatVisuals>();
            Assign(visuals, "boat", controller);
            Assign(visuals, "sea", sea);
            Assign(visuals, "model", model);
            var serialized = new SerializedObject(visuals);
            var wakeArray = serialized.FindProperty("wakes");
            wakeArray.arraySize = wakes.Length;
            for (int i = 0; i < wakes.Length; i++) wakeArray.GetArrayElementAtIndex(i).objectReferenceValue = wakes[i];
            serialized.ApplyModifiedPropertiesWithoutUndo();

            return controller;
        }

        static TrailRenderer Wake(Transform boat, string name, Vector3 position, Material material, float startWidth, float endWidth, float time, float alpha)
        {
            var wakeObject = new GameObject(name);
            wakeObject.transform.SetParent(boat, false);
            wakeObject.transform.localPosition = position;
            // Lay the ribbon flat on the water: TransformZ alignment faces it along the object's Z, pointed down.
            wakeObject.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

            var trail = wakeObject.AddComponent<TrailRenderer>();
            trail.sharedMaterial = material;
            trail.alignment = LineAlignment.TransformZ;
            trail.time = time;
            trail.minVertexDistance = 0.4f;
            trail.widthMultiplier = 1f;
            trail.widthCurve = new AnimationCurve(new Keyframe(0f, startWidth), new Keyframe(1f, endWidth));
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(alpha, 0f), new GradientAlphaKey(alpha * 0.6f, 0.3f), new GradientAlphaKey(0f, 1f) });
            trail.colorGradient = gradient;
            trail.shadowCastingMode = ShadowCastingMode.Off;
            trail.receiveShadows = false;
            trail.emitting = false;
            return trail;
        }

        static void Pole(Transform parent, Material material, Vector3 basePosition, Quaternion rotation, float length)
        {
            var pole = new GameObject("Pole").transform;
            pole.SetParent(parent, false);
            pole.localPosition = basePosition;
            pole.localRotation = rotation;
            Block("Shaft", pole, new Vector3(0f, length * 0.5f, 0f), new Vector3(0.08f, length, 0.08f), material, collider: false);
        }

        // ---------------------------------------------------------------- Camera

        static void BuildCamera(BoatController boat)
        {
            var rig = new GameObject("Camera Rig");
            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.SetParent(rig.transform, false);
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 20f;
            camera.nearClipPlane = 1f;
            camera.farClipPlane = 500f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.04f, 0.2f, 0.38f);
            cameraObject.AddComponent<AudioListener>();

            var follow = rig.AddComponent<HighAngleCamera>();
            Assign(follow, "target", boat.transform);
            Assign(follow, "targetBoat", boat);
            Assign(follow, "view", camera);
            Assign(follow, "input", boat.GetComponent<HelmInput>());

            // Place it where it will start so the scene view and first frame match.
            rig.transform.SetPositionAndRotation(new Vector3(boat.transform.position.x, 0f, boat.transform.position.z), Quaternion.Euler(50f, 45f, 0f));
            cameraObject.transform.localPosition = new Vector3(0f, 0f, -150f);
        }

        // ---------------------------------------------------------------- HUD

        static void BuildHud(BoatController boat)
        {
            var canvasObject = new GameObject("HUD", typeof(Canvas), typeof(CanvasScaler));
            canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            Transform canvas = canvasObject.transform;

            // Helm panel, bottom left: throttle gauge, lever text, speed.
            RectTransform helm = Panel(canvas, "Helm", new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(30f, 30f), new Vector2(300f, 200f), new Color(0.03f, 0.08f, 0.14f, 0.6f));
            RectTransform gauge = Panel(helm, "Throttle Gauge", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(22f, 0f), new Vector2(26f, 160f), new Color(0f, 0f, 0f, 0.5f));
            RectTransform ahead = Fill(gauge, "Ahead", new Color(0.35f, 0.85f, 0.45f), new Vector2(0f, 0.5f), new Vector2(1f, 0.5f));
            RectTransform astern = Fill(gauge, "Astern", new Color(1f, 0.6f, 0.25f), new Vector2(0f, 0.5f), new Vector2(1f, 0.5f));
            Fill(gauge, "Stop Line", Color.white, new Vector2(0f, 0.495f), new Vector2(1f, 0.505f));

            Text speed = Label(helm, "Speed", "0.0 kn", 46, TextAnchor.UpperLeft, new Vector2(0f, 1f), new Vector2(70f, -22f), new Vector2(220f, 56f), new Vector2(0f, 1f));
            Text lever = Label(helm, "Throttle", "STOP", 24, TextAnchor.UpperLeft, new Vector2(0f, 1f), new Vector2(72f, -84f), new Vector2(220f, 30f), new Vector2(0f, 1f));
            lever.color = new Color(0.8f, 0.9f, 1f);
            Text anchor = Label(helm, "Anchor", "", 24, TextAnchor.UpperLeft, new Vector2(0f, 1f), new Vector2(72f, -124f), new Vector2(220f, 30f), new Vector2(0f, 1f));
            anchor.color = new Color(1f, 0.85f, 0.4f);

            Text heading = Label(canvas, "Heading", "N  000°", 28, TextAnchor.UpperCenter, new Vector2(0.5f, 1f), new Vector2(0f, -24f), new Vector2(300f, 40f), new Vector2(0.5f, 1f));
            Text hint = Label(canvas, "Hint", "", 26, TextAnchor.MiddleCenter, new Vector2(0.5f, 0f), new Vector2(0f, 120f), new Vector2(1200f, 40f));
            Label(canvas, "Controls", "W/S throttle · A/D steer · Space anchor\nMouse wheel or +/- zoom · Esc pause", 18,
                TextAnchor.UpperRight, new Vector2(1f, 1f), new Vector2(-30f, -24f), new Vector2(600f, 60f), new Vector2(1f, 1f)).color = new Color(1f, 1f, 1f, 0.75f);

            RectTransform pause = Panel(canvas, "Pause Overlay", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(0f, 0.05f, 0.1f, 0.55f));
            pause.offsetMin = pause.offsetMax = Vector2.zero;
            Label(pause, "Paused", "PAUSED\n<size=22>Esc to resume</size>", 56, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(800f, 200f)).supportRichText = true;
            pause.gameObject.SetActive(false);

            var hud = canvasObject.AddComponent<HelmHud>();
            Assign(hud, "boat", boat);
            Assign(hud, "input", boat.GetComponent<HelmInput>());
            Assign(hud, "speedText", speed);
            Assign(hud, "throttleText", lever);
            Assign(hud, "throttleAheadFill", ahead);
            Assign(hud, "throttleAsternFill", astern);
            Assign(hud, "headingText", heading);
            Assign(hud, "anchorText", anchor);
            Assign(hud, "hintText", hint);
            Assign(hud, "pauseOverlay", pause.gameObject);
        }

        static RectTransform Panel(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 position, Vector2 size, Color color)
        {
            var rect = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = anchorMin;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            var image = rect.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return rect;
        }

        static RectTransform Fill(Transform parent, string name, Color color, Vector2 anchorMin, Vector2 anchorMax)
        {
            var rect = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            var image = rect.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return rect;
        }

        static Text Label(Transform parent, string name, string text, int size, TextAnchor alignment, Vector2 anchor, Vector2 position, Vector2 box, Vector2? pivot = null)
        {
            var rect = new GameObject(name, typeof(RectTransform), typeof(Text), typeof(Shadow)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = pivot ?? new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = box;
            var label = rect.GetComponent<Text>();
            label.font = font;
            label.fontSize = size;
            label.alignment = alignment;
            label.color = Color.white;
            label.text = text;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.raycastTarget = false;
            rect.GetComponent<Shadow>().effectDistance = new Vector2(2f, -2f);
            return label;
        }

        // ---------------------------------------------------------------- Helpers

        static GameObject Block(string name, Transform parent, Vector3 position, Vector3 size, Material material, Quaternion? rotation = null, bool collider = true)
        {
            var block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.name = name;
            if (!collider) Object.DestroyImmediate(block.GetComponent<Collider>());
            block.transform.SetParent(parent, false);
            block.transform.localPosition = position;
            block.transform.localRotation = rotation ?? Quaternion.identity;
            block.transform.localScale = size;
            block.GetComponent<MeshRenderer>().sharedMaterial = material;
            return block;
        }

        static GameObject MeshPiece(string name, Transform parent, Mesh mesh, Vector3 position, Material material, bool collider = false)
        {
            var piece = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            piece.transform.SetParent(parent, false);
            piece.transform.localPosition = position;
            piece.GetComponent<MeshFilter>().sharedMesh = mesh;
            piece.GetComponent<MeshRenderer>().sharedMaterial = material;
            if (collider)
            {
                var meshCollider = piece.AddComponent<MeshCollider>();
                meshCollider.sharedMesh = mesh;
                meshCollider.convex = true;
            }
            return piece;
        }

        /// <summary>URP Lit material, created once by name; the color is only used the first time.</summary>
        static Material Lit(string name, Color color, float smoothness = 0.15f, Color? emission = null) => LoadOrCreateMaterial(name, () =>
        {
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", smoothness);
            if (emission.HasValue)
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", emission.Value);
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            return material;
        });

        static Material LoadOrCreateMaterial(string name, Func<Material> create)
        {
            if (materials.TryGetValue(name, out var cached)) return cached;
            string path = $"{MaterialFolder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = create();
                AssetDatabase.CreateAsset(material, path);
            }
            materials[name] = material;
            return material;
        }

        static void Assign(Object target, string field, Object value)
        {
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(field) ?? throw new InvalidOperationException($"{target.GetType().Name} has no field '{field}'");
            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        static void EnsureFolder(string path)
        {
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            if (!AssetDatabase.IsValidFolder(path)) AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
