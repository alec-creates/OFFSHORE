using System;
using UnityEngine;

namespace Offshore
{
    /// <summary>
    /// Single-player pause. Everything that matters (boat, waves, later fishing and dangers) runs on scaled
    /// game time, so freezing <see cref="Time.timeScale"/> freezes all of it.
    /// </summary>
    public static class GamePause
    {
        public static bool IsPaused { get; private set; }

        public static event Action<bool> Changed;

        public static void Set(bool paused)
        {
            if (IsPaused == paused) return;
            IsPaused = paused;
            Time.timeScale = paused ? 0f : 1f;
            Changed?.Invoke(paused);
        }

        public static void Toggle() => Set(!IsPaused);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnLoad()
        {
            IsPaused = false;
            Time.timeScale = 1f;
            Changed = null;
        }
    }
}
