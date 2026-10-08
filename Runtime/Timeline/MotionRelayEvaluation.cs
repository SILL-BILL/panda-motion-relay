using System.Collections.Generic;
using UnityEngine;

namespace SillBill.PandaMotionRelay
{
    /// <summary>Transfers queued poses after animation has written the driver skeletons.</summary>
    public static class MotionRelayEvaluation
    {
        private static readonly List<MotionRelayMixer> pending = new List<MotionRelayMixer>();
        private static MotionRelayLateUpdate host;

        internal static void Schedule(MotionRelayMixer mixer)
        {
            if (!pending.Contains(mixer)) pending.Add(mixer);
            if (Application.isPlaying && host == null)
            {
                var go = new GameObject("Panda Motion Relay Evaluation") { hideFlags = HideFlags.HideAndDontSave };
                host = go.AddComponent<MotionRelayLateUpdate>();
            }
        }

        internal static void Unschedule(MotionRelayMixer mixer)
        {
            pending.Remove(mixer);
        }

        /// <summary>
        /// Call after PlayableDirector.Evaluate() for synchronous programmatic sampling.
        /// Normal playback and Editor preview invoke this automatically after animation evaluation.
        /// </summary>
        public static bool ApplyPendingPoses()
        {
            if (pending.Count == 0) return false;
            while (pending.Count > 0)
            {
                int last = pending.Count - 1;
                var mixer = pending[last];
                pending.RemoveAt(last);
                mixer.ApplyPending();
            }
            return true;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            pending.Clear();
            if (host != null) Object.Destroy(host.gameObject);
            host = null;
        }
    }
}
