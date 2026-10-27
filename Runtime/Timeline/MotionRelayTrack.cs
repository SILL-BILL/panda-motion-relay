using System.Collections.Generic;
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
        [Tooltip("Copy the selected Source Animator root's final world position and rotation. Target scale is preserved. Disabled by default.")]
        public bool relayMotionRoot;

        public override Playable CreateTrackMixer(PlayableGraph graph, GameObject go, int inputCount)
        {
            var mixer = ScriptPlayable<MotionRelayMixer>.Create(graph, inputCount);
            mixer.GetBehaviour().Clips = new List<TimelineClip>(GetClips()).ToArray();
            mixer.GetBehaviour().Director = graph.GetResolver() as PlayableDirector;
            mixer.GetBehaviour().RelayMotionRoot = relayMotionRoot;
            return mixer;
        }

        public override void GatherProperties(PlayableDirector director, IPropertyCollector driver)
        {
            var target = director.GetGenericBinding(this) as Animator;
            if (!HumanoidPoseRelay.IsValid(target)) return;
            if (relayMotionRoot)
            {
                foreach (var axis in new[] { "x", "y", "z" })
                    driver.AddFromName<Transform>(target.gameObject, "m_LocalPosition." + axis);
                foreach (var axis in new[] { "x", "y", "z", "w" })
                    driver.AddFromName<Transform>(target.gameObject, "m_LocalRotation." + axis);
            }
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
