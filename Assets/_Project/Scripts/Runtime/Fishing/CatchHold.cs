using System;
using System.Collections.Generic;
using UnityEngine;

namespace Offshore
{
    /// <summary>The boat's fish hold: limited by total weight.</summary>
    public class CatchHold : MonoBehaviour
    {
        [Tooltip("Total kg of fish the hold carries.")]
        [SerializeField, Min(1f)] float capacityKg = 80f;

        readonly List<CaughtFish> fish = new List<CaughtFish>();

        public event Action Changed;

        public IReadOnlyList<CaughtFish> Fish => fish;
        public int Count => fish.Count;
        public float CapacityKg => capacityKg;
        public float TotalKg { get; private set; }
        public float RemainingKg => Mathf.Max(0f, capacityKg - TotalKg);

        public bool CanFit(float kg) => kg <= RemainingKg + 0.0001f;

        public bool TryStore(CaughtFish caught)
        {
            if (!CanFit(caught.Kg)) return false;
            fish.Add(caught);
            TotalKg += caught.Kg;
            Changed?.Invoke();
            return true;
        }

        /// <summary>Hold size; upgrades will change it later. Tests use it for small holds.</summary>
        public void SetCapacity(float kg)
        {
            capacityKg = Mathf.Max(1f, kg);
            Changed?.Invoke();
        }

        /// <summary>DEVELOPMENT ONLY: throw everything out so fishing can be tested again. Selling replaces this in M3.</summary>
        public void DevEmpty()
        {
            fish.Clear();
            TotalKg = 0f;
            Debug.Log("[OFFSHORE][DEV] Hold emptied.");
            Changed?.Invoke();
        }
    }
}
