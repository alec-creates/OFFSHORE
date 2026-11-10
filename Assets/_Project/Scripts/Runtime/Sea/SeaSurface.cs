using System;
using System.Collections.Generic;
using UnityEngine;

namespace Offshore
{
    /// <summary>
    /// The sea: one static grid mesh over the whole bounded map. Swells are animated on the GPU by the
    /// Stylized Water shader from globals pushed here, and <see cref="HeightAt(Vector3)"/> evaluates the same
    /// formula so floating things match the water. Water color is baked into vertex colors from shoals
    /// (land and reefs): turquoise shallows and a foam line near shore, deep blue elsewhere.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class SeaSurface : MonoBehaviour
    {
        public const int MaxWaves = 3;

        [Serializable]
        public struct Wave
        {
            [Tooltip("Direction the swell travels, degrees clockwise from north (+Z).")]
            public float directionDegrees;
            [Min(1f)] public float wavelength;
            [Min(0f)] public float amplitude;
        }

        /// <summary>
        /// Shallow water around a line segment (a point if both ends match): land up to landRadius,
        /// then shallows fading to deep over shallowWidth.
        /// </summary>
        [Serializable]
        public struct Shoal
        {
            public Vector2 from;
            public Vector2 to;
            [Min(0f)] public float landRadius;
            [Min(1f)] public float shallowWidth;

            public Shoal(Vector2 from, Vector2 to, float landRadius, float shallowWidth)
            {
                this.from = from;
                this.to = to;
                this.landRadius = landRadius;
                this.shallowWidth = shallowWidth;
            }

            public float DepthFraction(Vector2 p)
            {
                Vector2 ab = to - from;
                float t = ab.sqrMagnitude > 0.0001f ? Mathf.Clamp01(Vector2.Dot(p - from, ab) / ab.sqrMagnitude) : 0f;
                float distance = Vector2.Distance(p, from + ab * t);
                return Mathf.Clamp01((distance - landRadius) / shallowWidth);
            }
        }

        [Header("Mesh")]
        [Tooltip("Side length of the square sea mesh, metres. Should cover the map plus what the camera sees past the edge.")]
        [SerializeField] float size = 760f;
        [Tooltip("Grid spacing, metres. Larger = chunkier low-poly facets.")]
        [SerializeField] float cellSize = 4f;

        [Header("Swell")]
        [SerializeField] Wave[] waves =
        {
            new Wave { directionDegrees = 25f, wavelength = 26f, amplitude = 0.16f },
            new Wave { directionDegrees = -40f, wavelength = 14f, amplitude = 0.09f },
            new Wave { directionDegrees = 80f, wavelength = 8.5f, amplitude = 0.045f },
        };

        [Header("Colors")]
        [SerializeField] Color deepColor = new Color(0.04f, 0.2f, 0.38f);
        [SerializeField] Color deepVariationColor = new Color(0.05f, 0.26f, 0.44f);
        [SerializeField] Color shallowColor = new Color(0.15f, 0.62f, 0.64f);
        [Tooltip("Fraction of the shallow band (from the shoreline) drawn as foam.")]
        [SerializeField, Range(0f, 0.5f)] float shoreFoam = 0.1f;
        [SerializeField] List<Shoal> shoals = new List<Shoal>();

        static readonly int SeaTimeId = Shader.PropertyToID("_SeaTime");
        static readonly int[] WaveIds = { Shader.PropertyToID("_SeaWave0"), Shader.PropertyToID("_SeaWave1"), Shader.PropertyToID("_SeaWave2") };

        Mesh mesh;
        float seaTime;

        public static SeaSurface Active { get; private set; }

        /// <summary>Seconds of game time the swell has run; stops while paused.</summary>
        public float SeaTime => seaTime;
        public float MaxWaveHeight
        {
            get
            {
                float total = 0f;
                for (int i = 0; i < Mathf.Min(MaxWaves, waves.Length); i++) total += waves[i].amplitude;
                return total;
            }
        }

        public IReadOnlyList<Shoal> Shoals => shoals;

        public float HeightAt(Vector3 world) => HeightAt(world.x, world.z);

        public float HeightAt(float x, float z)
        {
            float height = 0f;
            for (int i = 0; i < Mathf.Min(MaxWaves, waves.Length); i++)
            {
                Vector4 w = Packed(waves[i]);
                if (w.w <= 0f) continue;
                float k = 2f * Mathf.PI / w.z;
                float speed = Mathf.Sqrt(9.81f / k);
                height += w.w * Mathf.Sin(k * (w.x * x + w.y * z - speed * seaTime));
            }
            return transform.position.y + height;
        }

        /// <summary>0 at a shoreline, 1 in open deep water. Later used for depth-dependent fishing.</summary>
        public float DepthFractionAt(float x, float z)
        {
            float depth = 1f;
            var p = new Vector2(x, z);
            foreach (var shoal in shoals) depth = Mathf.Min(depth, shoal.DepthFraction(p));
            return depth;
        }

        public void SetShoals(IEnumerable<Shoal> newShoals)
        {
            shoals = new List<Shoal>(newShoals);
            BuildMesh();
        }

        void OnEnable()
        {
            Active = this;
            BuildMesh();
            PushGlobals();
        }

        void OnDisable()
        {
            if (Active == this) Active = null;
            if (mesh != null)
            {
                if (Application.isPlaying) Destroy(mesh);
                else DestroyImmediate(mesh);
                mesh = null;
            }
        }

        void OnValidate()
        {
            if (isActiveAndEnabled)
            {
                BuildMesh();
                PushGlobals();
            }
        }

        void Update()
        {
            if (Application.isPlaying) seaTime += Time.deltaTime;
            PushGlobals();
        }

        void PushGlobals()
        {
            Shader.SetGlobalFloat(SeaTimeId, seaTime);
            for (int i = 0; i < MaxWaves; i++)
            {
                Shader.SetGlobalVector(WaveIds[i], i < waves.Length ? Packed(waves[i]) : new Vector4(0f, 1f, 1f, 0f));
            }
        }

        static Vector4 Packed(Wave wave)
        {
            float angle = wave.directionDegrees * Mathf.Deg2Rad;
            return new Vector4(Mathf.Sin(angle), Mathf.Cos(angle), Mathf.Max(1f, wave.wavelength), Mathf.Max(0f, wave.amplitude));
        }

        void BuildMesh()
        {
            if (mesh == null)
            {
                mesh = new Mesh { name = "Sea Surface", hideFlags = HideFlags.DontSave };
                GetComponent<MeshFilter>().sharedMesh = mesh;
            }

            int cells = Mathf.Clamp(Mathf.RoundToInt(size / Mathf.Max(0.5f, cellSize)), 2, 250);
            int line = cells + 1;
            float step = size / cells;
            var vertices = new Vector3[line * line];
            var colors = new Color[vertices.Length];
            Vector3 origin = transform.position;

            for (int z = 0; z < line; z++)
            {
                for (int x = 0; x < line; x++)
                {
                    int i = z * line + x;
                    var local = new Vector3(x * step - size * 0.5f, 0f, z * step - size * 0.5f);
                    vertices[i] = local;
                    colors[i] = ColorAt(origin.x + local.x, origin.z + local.z);
                }
            }

            var triangles = new int[cells * cells * 6];
            int t = 0;
            for (int z = 0; z < cells; z++)
            {
                for (int x = 0; x < cells; x++)
                {
                    int i = z * line + x;
                    triangles[t++] = i;
                    triangles[t++] = i + line;
                    triangles[t++] = i + 1;
                    triangles[t++] = i + 1;
                    triangles[t++] = i + line;
                    triangles[t++] = i + line + 1;
                }
            }

            mesh.Clear();
            mesh.indexFormat = vertices.Length > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
            mesh.vertices = vertices;
            mesh.colors = colors;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            // Swells move vertices on the GPU; pad the bounds so culling never clips them.
            mesh.bounds = new Bounds(Vector3.zero, new Vector3(size, 4f, size));
        }

        Color ColorAt(float x, float z)
        {
            float depth = DepthFractionAt(x, z);
            Color deep = Color.Lerp(deepColor, deepVariationColor, Mathf.PerlinNoise(x * 0.012f + 31.7f, z * 0.012f + 11.3f));
            Color water = Color.Lerp(shallowColor, deep, Mathf.SmoothStep(0f, 1f, depth));
            water.a = shoreFoam > 0f ? 1f - Mathf.SmoothStep(0f, 1f, depth / shoreFoam) : 0f;
            return water;
        }
    }
}
