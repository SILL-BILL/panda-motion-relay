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
            options.tooltip = "One-source Humanoid pose relay. Bind the track to the target Animator and assign Source on the clip. No automatic blending.";
            return options;
        }
    }
}
