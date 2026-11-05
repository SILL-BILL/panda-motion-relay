using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;

#if UNITY_EDITOR
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("SillBill.PandaMotionRelay.Editor")]
#endif

namespace SillBill.PandaMotionRelay
{
    /// <summary>Transfers queued poses after animation has written the driver skeletons.</summary>
    public static class MotionRelayEvaluation
    {
        private static readonly List<MotionRelayMixer> pending = new List<MotionRelayMixer>();
        private static MotionRelayLateUpdate host;

#if UNITY_EDITOR
        private static PlayableDirector bakeDirector;
        private static double bakeTime;

        internal static void EvaluateBakeSample(PlayableDirector evaluation, PlayableDirector relay, double relayTime)
        {
            var previousDirector = bakeDirector;
            double previousTime = bakeTime;
            bakeDirector = relay;
            bakeTime = relayTime;
            try { evaluation.Evaluate(); }
            finally
            {
                bakeDirector = previousDirector;
                bakeTime = previousTime;
            }
        }

        internal static double OwnershipTime(PlayableDirector director) =>
            bakeDirector != null && director == bakeDirector ? bakeTime : director.time;
#endif

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

        /// <summary>Discard this director's queued writes when restoring an Editor sampling transaction.</summary>
        public static void DiscardPendingPoses(PlayableDirector director)
        {
            for (int i = pending.Count - 1; i >= 0; i--)
                if (pending[i].Director == director) pending[i].DiscardPending();
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
