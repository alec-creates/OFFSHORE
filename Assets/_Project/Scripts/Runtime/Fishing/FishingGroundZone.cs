using System.Collections.Generic;
using UnityEngine;

namespace Offshore
{
    /// <summary>Puts a <see cref="FishingGround"/> on the map: a circle on the water around this object.</summary>
    public class FishingGroundZone : MonoBehaviour
    {
        static readonly List<FishingGroundZone> active = new List<FishingGroundZone>();

        [SerializeField] FishingGround ground;
        [SerializeField, Min(5f)] float radius = 40f;

        public static IReadOnlyList<FishingGroundZone> All => active;

        public FishingGround Ground => ground;
        public float Radius => radius;
        public Vector3 Center => new Vector3(transform.position.x, 0f, transform.position.z);

        public bool Contains(Vector3 position) => DistanceFromCenter(position) <= radius;

        public float DistanceFromCenter(Vector3 position) => Vector2.Distance(new Vector2(position.x, position.z), new Vector2(Center.x, Center.z));

        /// <summary>The ground at this position, or null for open water.</summary>
        public static FishingGroundZone At(Vector3 position)
        {
            foreach (var zone in active)
            {
                if (zone.ground != null && zone.Contains(position)) return zone;
            }
            return null;
        }

        public static FishingGroundZone Nearest(Vector3 position)
        {
            FishingGroundZone best = null;
            float bestDistance = float.MaxValue;
            foreach (var zone in active)
            {
                float d = zone.DistanceFromCenter(position) - zone.radius;
                if (zone.ground != null && d < bestDistance)
                {
                    best = zone;
                    bestDistance = d;
                }
            }
            return best;
        }

        public static FishingGroundZone Named(string displayName) => active.Find(z => z.ground != null && z.ground.DisplayName == displayName);

        void OnEnable() => active.Add(this);

        void OnDisable() => active.Remove(this);

        void OnDrawGizmos()
        {
            Gizmos.color = ground != null ? ground.MarkerColor : Color.cyan;
            const int segments = 48;
            Vector3 previous = Center + new Vector3(radius, 0.5f, 0f);
            for (int i = 1; i <= segments; i++)
            {
                float a = i * Mathf.PI * 2f / segments;
                Vector3 next = Center + new Vector3(Mathf.Cos(a) * radius, 0.5f, Mathf.Sin(a) * radius);
                Gizmos.DrawLine(previous, next);
                previous = next;
            }
        }
    }
}
