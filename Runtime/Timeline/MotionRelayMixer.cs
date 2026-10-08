using UnityEngine;
using UnityEngine.Playables;

namespace SillBill.PandaMotionRelay
{
    public sealed class MotionRelayMixer : PlayableBehaviour
    {
        private HumanoidPoseRelay relay;
        private Animator currentSource;
        private Animator currentTarget;
        private bool warned;
        private bool pending;

        public override void ProcessFrame(Playable playable, FrameData info, object playerData)
        {
            var target = playerData as Animator;
            Animator source = null;
            int activeCount = 0;
            for (int i = 0; i < playable.GetInputCount(); i++)
            {
                if (playable.GetInputWeight(i) <= 0f) continue;
                activeCount++;
                source = ((ScriptPlayable<MotionRelayBehaviour>)playable.GetInput(i)).GetBehaviour().Source;
            }

            // Phase 1 intentionally rejects overlap instead of silently inventing blend behavior.
            if (activeCount != 1 || !HumanoidPoseRelay.IsValid(source) ||
                !HumanoidPoseRelay.IsValid(target) || source == target ||
                source.transform.IsChildOf(target.transform) || target.transform.IsChildOf(source.transform))
            {
                Release();
                if (activeCount > 0 && !warned)
                {
                    Debug.LogWarning("Panda Motion Relay: use one non-overlapping clip and two independent Humanoid Animators.", target);
                    warned = true;
                }
                return;
            }

            warned = false;
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
