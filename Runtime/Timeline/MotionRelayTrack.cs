using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace SillBill.PandaMotionRelay
{
    [TrackColor(0.32f, 0.72f, 0.55f)]
    [TrackClipType(typeof(MotionRelayClip))]
    [TrackBindingType(typeof(Animator))]
    public sealed class MotionRelayTrack : TrackAsset
    {
        public override Playable CreateTrackMixer(PlayableGraph graph, GameObject go, int inputCount)
        {
            return ScriptPlayable<MotionRelayMixer>.Create(graph, inputCount);
        }

        public override void GatherProperties(PlayableDirector director, IPropertyCollector driver)
        {
            var target = director.GetGenericBinding(this) as Animator;
            if (!HumanoidPoseRelay.IsValid(target)) return;
            for (int i = 0; i < (int)HumanBodyBones.LastBone; i++)
            {
                var bone = target.GetBoneTransform((HumanBodyBones)i);
                if (bone == null) continue;
                foreach (var axis in new[] { "x", "y", "z", "w" })
                    driver.AddFromName<Transform>(bone.gameObject, "m_LocalRotation." + axis);
                foreach (var axis in new[] { "x", "y", "z" })
                    driver.AddFromName<Transform>(bone.gameObject, "m_LocalPosition." + axis);
            }
            base.GatherProperties(director, driver);
        }
    }
}
