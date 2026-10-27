using UnityEngine.Timeline;
using UnityEditor.Timeline;

namespace SillBill.PandaMotionRelay.Editor
{
    [CustomTimelineEditor(typeof(MotionRelayClip))]
    public sealed class MotionRelayClipEditor : ClipEditor
    {
        public override ClipDrawOptions GetClipOptions(TimelineClip clip)
        {
            var options = base.GetClipOptions(clip);
            options.tooltip = "Humanoid pose relay. Each clip selects its own Source Animator. Exact cut at clip start; gaps do not relay; overlaps are invalid. Enable Relay Motion Root on the track to also copy world position/rotation. No automatic blending.";
            return options;
        }
    }
}
