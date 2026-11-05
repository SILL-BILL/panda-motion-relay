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
        [Tooltip("Copy the selected Source Animator root's final world position and rotation. Target scale is preserved. Enabled by default for new tracks.")]
        public bool relayMotionRoot = true;

        [Tooltip("Opt in to closing gaps up to 10 microseconds only when their edges collapse to the same AnimationClip float key. Preview and Bake keep the outgoing Source until the incoming Clip starts. Clip timings are preserved.")]
        public bool closeUnrepresentableMicroGaps;

        public override Playable CreateTrackMixer(PlayableGraph graph, GameObject go, int inputCount)
        {
            var mixer = ScriptPlayable<MotionRelayMixer>.Create(graph, inputCount);
            mixer.GetBehaviour().Clips = new List<TimelineClip>(GetClips()).ToArray();
            var ordered = new List<TimelineClip>(GetClips());
            ordered.Sort((a, b) => a.start.CompareTo(b.start));
            var next = new Dictionary<TimelineClip, TimelineClip>();
            for (int i = 0; i + 1 < ordered.Count; i++) next.Add(ordered[i], ordered[i + 1]);
            mixer.GetBehaviour().NextClips = next;
            mixer.GetBehaviour().Origin = ordered.Count > 0 ? ordered[0].start : 0;
            mixer.GetBehaviour().Track = this;
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
