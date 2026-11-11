using UnityEngine;

namespace Offshore
{
    public static class Compass
    {
        static readonly string[] Points = { "N", "NE", "E", "SE", "S", "SW", "W", "NW" };

        /// <summary>Eight-point compass name for a bearing in degrees (0 = north, +Z).</summary>
        public static string Point(float bearingDegrees) => Points[Mathf.RoundToInt(Mathf.Repeat(bearingDegrees, 360f) / 45f) % 8];
    }
}
