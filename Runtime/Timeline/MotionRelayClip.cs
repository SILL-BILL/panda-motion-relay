using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace SillBill.PandaMotionRelay
{
    [System.Serializable]
    public sealed class MotionRelayClip : PlayableAsset, ITimelineClipAsset
    {
        [Tooltip("A separate driver Animator with a valid Humanoid Avatar.")]
        public ExposedReference<Animator> source;
        public ClipCaps clipCaps => ClipCaps.None;

        public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
        {
            var playable = ScriptPlayable<MotionRelayBehaviour>.Create(graph);
            playable.GetBehaviour().Source = source.Resolve(graph.GetResolver());
            return playable;
        }
    }

    public sealed class MotionRelayBehaviour : PlayableBehaviour
    {
        public Animator Source { get; set; }
    }
}
