using UnityEngine;

namespace Offshore
{
    /// <summary>Rod-fishing timings and rules shared by every ground. Bite delays live on each FishingGround.</summary>
    [CreateAssetMenu(menuName = "OFFSHORE/Fishing Settings", fileName = "Fishing Settings")]
    public class FishingSettings : ScriptableObject
    {
        [Tooltip("Seconds for the cast to land.")]
        [SerializeField, Min(0.05f)] float castSeconds = 1.4f;
        [Tooltip("Seconds the bite is shown before reeling starts automatically.")]
        [SerializeField, Min(0.05f)] float biteSeconds = 1.1f;
        [Tooltip("Seconds the landed fish is shown before the next cast.")]
        [SerializeField, Min(0.05f)] float landedSeconds = 2f;
        [Tooltip("Fishing can only start once the anchored boat is slower than this, m/s.")]
        [SerializeField, Min(0f)] float maxStartSpeed = 0.3f;
        [Tooltip("Fishing stops (hold full) when less free space than this remains, kg.")]
        [SerializeField, Min(0f)] float minimumFreeKg = 0.5f;
        [Tooltip("Multiplies every fishing duration. 1 = normal; tests use a small value.")]
        [SerializeField, Range(0.01f, 5f)] float durationScale = 1f;

        public float CastSeconds => castSeconds * durationScale;
        public float BiteSeconds => biteSeconds * durationScale;
        public float LandedSeconds => landedSeconds * durationScale;
        public float MaxStartSpeed => maxStartSpeed;
        public float MinimumFreeKg => minimumFreeKg;
        public float DurationScale
        {
            get => durationScale;
            set => durationScale = Mathf.Clamp(value, 0.01f, 5f);
        }
    }
}
