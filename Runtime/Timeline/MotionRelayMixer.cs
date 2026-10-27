using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace SillBill.PandaMotionRelay
{
    public sealed class MotionRelayMixer : PlayableBehaviour
    {
        private HumanoidPoseRelay relay;
        private Animator currentSource;
        private Animator currentTarget;
        private string lastWarning;
        private bool pending;

        internal TimelineClip[] Clips { get; set; }
        internal PlayableDirector Director { get; set; }

        public override void ProcessFrame(Playable playable, FrameData info, object playerData)
        {
            var target = playerData as Animator;
            Animator source = null;
            int activeCount = 0;
            // Clip occupancy, not blend/ease weights, is authoritative. The half-open
            // interval gives an adjacent incoming clip sole ownership of its start time.
            double time = Director != null ? Director.time : playable.GetTime();
            for (int i = 0; Clips != null && i < Clips.Length; i++)
            {
                if (time < Clips[i].start || time >= Clips[i].end) continue;
                activeCount++;
                source = ((ScriptPlayable<MotionRelayBehaviour>)playable.GetInput(i)).GetBehaviour().Source;
            }

            if (activeCount == 0)
            {
                Release();
                lastWarning = null;
                return;
            }
            if (activeCount > 1)
            {
                Release();
                Warn("Panda Motion Relay: overlapping Relay Clips are invalid. No pose will be relayed until exactly one clip occupies the current time.", target);
                return;
            }
            if (!HumanoidPoseRelay.IsValid(source) ||
                !HumanoidPoseRelay.IsValid(target) || source == target ||
                source.transform.IsChildOf(target.transform) || target.transform.IsChildOf(source.transform))
            {
                Release();
                Warn("Panda Motion Relay: use two independent Humanoid Animators for the active Relay Clip.", target);
                return;
            }

            lastWarning = null;
            if (relay == null || source != currentSource || target != currentTarget)
            {
                Release();
                currentSource = source;
                currentTarget = target;
                relay = new HumanoidPoseRelay(source, target);
            }
            // ProcessFrame runs before animation streams are written to scene Transforms.
            pending = true;
            MotionRelayEvaluation.Schedule(this);
        }

        internal void ApplyPending()
        {
            if (!pending) return;
            pending = false;
            if (relay != null && !relay.Apply()) Release();
        }

        public override void OnGraphStop(Playable playable) { Release(); }
        public override void OnPlayableDestroy(Playable playable) { Release(); }

        private void Warn(string message, Animator target)
        {
            if (lastWarning == message) return;
            Debug.LogWarning(message, target);
            lastWarning = message;
        }

        private void Release()
        {
            pending = false;
            MotionRelayEvaluation.Unschedule(this);
            relay?.Dispose();
            relay = null;
            currentSource = null;
            currentTarget = null;
        }
    }
}
