using UnityEngine;

namespace Offshore
{
    /// <summary>Makes a buoy or piece of debris ride the swell at its spot. Purely visual.</summary>
    public class FloatingBob : MonoBehaviour
    {
        [Tooltip("Height of the object's pivot above the water surface.")]
        [SerializeField] float waterlineOffset;
        [Tooltip("How strongly it tilts with the wave slope.")]
        [SerializeField] float tiltScale = 1.5f;
        [SerializeField] float probe = 1.2f;

        Vector3 rest;
        Quaternion restRotation;

        void Awake()
        {
            rest = transform.position;
            restRotation = transform.rotation;
        }

        void LateUpdate()
        {
            SeaSurface sea = SeaSurface.Active;
            if (sea == null) return;

            float h = sea.HeightAt(rest.x, rest.z);
            float dx = sea.HeightAt(rest.x + probe, rest.z) - sea.HeightAt(rest.x - probe, rest.z);
            float dz = sea.HeightAt(rest.x, rest.z + probe) - sea.HeightAt(rest.x, rest.z - probe);
            float tiltX = Mathf.Atan2(dz, probe * 2f) * Mathf.Rad2Deg * tiltScale;
            float tiltZ = -Mathf.Atan2(dx, probe * 2f) * Mathf.Rad2Deg * tiltScale;

            transform.SetPositionAndRotation(new Vector3(rest.x, h + waterlineOffset, rest.z), Quaternion.Euler(-tiltX, 0f, tiltZ) * restRotation);
        }
    }
}
