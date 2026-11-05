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
            options.tooltip = "Humanoid pose relay. Each clip selects its own Source Animator. Exact cut at clip start. Gaps do not relay unless Close Unrepresentable Micro Gaps is enabled on the track for float-key collisions up to 10 microseconds. Overlaps beyond boundary roundoff are invalid. Enable Relay Motion Root to copy world position/rotation. No automatic blending.";
            return options;
        }
    }
}
