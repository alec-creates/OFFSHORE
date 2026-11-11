using UnityEngine;

namespace Offshore
{
    /// <summary>
    /// Shows the fishing state on the boat: the bobber flies out and floats, dips on a bite, comes back while
    /// reeling with the rod bent, and the landed fish swings up at the stern. Purely visual.
    /// </summary>
    public class FishingVisuals : MonoBehaviour
    {
        [SerializeField] AutoFisher fisher;
        [SerializeField] SeaSurface sea;
        [SerializeField] Transform rodPivot;
        [SerializeField] Transform rodTip;
        [SerializeField] Transform bobber;
        [SerializeField] LineRenderer line;
        [SerializeField] Transform catchDisplay;
        [SerializeField] Renderer[] catchRenderers = new Renderer[0];

        [Header("Placement")]
        [Tooltip("Where the bobber lands, relative to the boat (right, up, forward).")]
        [SerializeField] Vector3 castOffset = new Vector3(5f, 0f, -13f);
        [Tooltip("Where a reeled fish comes up, relative to the boat.")]
        [SerializeField] Vector3 landingOffset = new Vector3(1.8f, 0f, -5.5f);
        [SerializeField] float castArcHeight = 4f;
        [SerializeField] float reelBendDegrees = 22f;

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        Quaternion rodRest;
        Vector3 castFrom;
        FishingState lastState;
        MaterialPropertyBlock tint;

        void Awake()
        {
            rodRest = rodPivot != null ? rodPivot.localRotation : Quaternion.identity;
            tint = new MaterialPropertyBlock();
            SetVisible(false, false);
        }

        void LateUpdate()
        {
            if (fisher == null) return;

            FishingState state = fisher.State;
            if (state != lastState)
            {
                if (state == FishingState.Casting) castFrom = rodTip.position;
                if (state == FishingState.Landed) ShowCatch();
                lastState = state;
            }

            Transform boat = fisher.Boat.transform;
            Vector3 target = Flat(boat.TransformPoint(castOffset));
            Vector3 landing = Flat(boat.TransformPoint(landingOffset));
            float t = fisher.Progress;
            float bend = 0f;
            float time = Time.time;

            switch (state)
            {
                case FishingState.Casting:
                    Vector3 arc = Vector3.Lerp(castFrom, target + Vector3.up * WaterAt(target), t);
                    arc.y += Mathf.Sin(t * Mathf.PI) * castArcHeight;
                    bobber.position = arc;
                    bend = -12f * (1f - t);
                    break;

                case FishingState.Waiting:
                    // Small occasional nibbles keep the wait alive.
                    float nibble = Mathf.Max(0f, Mathf.Sin(time * 1.7f) * Mathf.Sin(time * 4.3f) - 0.6f) * 0.25f;
                    bobber.position = target + Vector3.up * (WaterAt(target) + 0.05f - nibble);
                    break;

                case FishingState.Bite:
                    bobber.position = target + new Vector3(Mathf.Sin(time * 40f) * 0.08f, WaterAt(target) - 0.3f, 0f);
                    bend = 10f;
                    break;

                case FishingState.Reeling:
                    Vector3 p = Vector3.Lerp(target, landing, t);
                    p += boat.right * Mathf.Sin(time * 2.2f) * 0.8f * (1f - t);
                    bobber.position = p + Vector3.up * (WaterAt(p) - 0.2f);
                    bend = reelBendDegrees + Mathf.Sin(time * 9f) * 3f;
                    break;
            }

            bool lineOut = state == FishingState.Casting || state == FishingState.Waiting || state == FishingState.Bite || state == FishingState.Reeling;
            SetVisible(lineOut, state == FishingState.Landed);
            if (state == FishingState.Landed) SwingCatch(time);
            if (lineOut) DrawLine(state == FishingState.Reeling || state == FishingState.Bite ? 0.1f : 0.6f);

            if (rodPivot != null)
            {
                Quaternion goal = rodRest * Quaternion.Euler(bend, 0f, 0f);
                rodPivot.localRotation = Quaternion.Slerp(rodPivot.localRotation, goal, 1f - Mathf.Exp(-10f * Mathf.Max(Time.deltaTime, 0f)));
            }
        }

        void ShowCatch()
        {
            if (!fisher.LastCatch.HasValue || catchDisplay == null) return;
            CaughtFish fish = fisher.LastCatch.Value;
            catchDisplay.localScale = Vector3.one * fish.Species.LengthFor(fish.Kg);
            tint.SetColor(BaseColorId, fish.Species.Color);
            foreach (var r in catchRenderers)
            {
                if (r != null) r.SetPropertyBlock(tint);
            }
        }

        void SwingCatch(float time)
        {
            if (catchDisplay == null) return;
            catchDisplay.localRotation = Quaternion.Euler(Mathf.Sin(time * 5f) * 12f, 90f + Mathf.Sin(time * 3f) * 20f, Mathf.Sin(time * 7f) * 10f);
        }

        void DrawLine(float sag)
        {
            if (line == null) return;
            Vector3 a = rodTip.position, b = bobber.position;
            int count = line.positionCount;
            float droop = sag * Mathf.Min(Vector3.Distance(a, b) * 0.15f, 2f);
            for (int i = 0; i < count; i++)
            {
                float s = i / (count - 1f);
                line.SetPosition(i, Vector3.Lerp(a, b, s) + Vector3.down * (droop * 4f * s * (1f - s)));
            }
        }

        void SetVisible(bool lineOut, bool showCatch)
        {
            if (bobber != null && bobber.gameObject.activeSelf != lineOut) bobber.gameObject.SetActive(lineOut);
            if (line != null) line.enabled = lineOut;
            if (catchDisplay != null && catchDisplay.gameObject.activeSelf != showCatch) catchDisplay.gameObject.SetActive(showCatch);
        }

        float WaterAt(Vector3 p) => sea != null ? sea.HeightAt(p) : 0f;

        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
    }
}
