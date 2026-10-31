using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace SillBill.PandaMotionRelay.Editor
{
    /// <summary>A single, unambiguous parent-to-relay clock mapping. No graph is evaluated during validation.</summary>
    internal sealed class BakeEvaluationScope
    {
        public readonly PlayableDirector Evaluation;
        public readonly List<PlayableDirector> Directors = new List<PlayableDirector>();
        private TimelineClip control;

        public double EvaluationTime(double relayTime)
        {
            return control == null ? relayTime : control.start + (relayTime - control.clipIn) / control.timeScale;
        }

        public BakeEvaluationScope(GenericBakeRequest request, double first, double last)
        {
            Evaluation = request.evaluationDirector != null ? request.evaluationDirector : request.director;
            if (!Evaluation.isActiveAndEnabled || !(Evaluation.playableAsset is TimelineAsset))
                throw new ArgumentException("The Evaluation Director must be active and have a Timeline asset.");
            Directors.Add(Evaluation);
            var matches = new List<TimelineClip>();
            foreach (var clip in Controls(Evaluation))
            {
                var asset = (ControlPlayableAsset)clip.asset;
                if (asset.prefabGameObject != null)
                    throw new ArgumentException("Bake does not support prefab-instantiated controlled Directors.");
                var source = asset.sourceGameObject.Resolve(Evaluation);
                if (source == null) continue;
                var children = asset.searchHierarchy ? source.GetComponentsInChildren<PlayableDirector>(true) : source.GetComponents<PlayableDirector>();
                if (asset.searchHierarchy && children.Length > 1)
                    throw new ArgumentException("Control Clip searchHierarchy resolves multiple Directors; use a direct source.");
                foreach (var child in children)
                {
                    if (child == Evaluation || child.playableAsset == Evaluation.playableAsset)
                        throw new ArgumentException("Self-referencing Control Directors are not supported for baking.");
                    if (Controls(child).Any())
                        throw new ArgumentException("Bake supports direct Master/Child control only, not multiple nested levels.");
                    if (!Directors.Contains(child)) Directors.Add(child);
                    if (child == request.director) matches.Add(clip);
                }
            }
            if (Evaluation == request.director) return;
            if (matches.Count != 1)
                throw new ArgumentException("The Evaluation Timeline must have exactly one enabled Control Clip directly controlling the Relay Director.");
            control = matches[0];
            if (!Finite(control.timeScale) || control.timeScale <= 0 || !Finite(control.start) || !Finite(control.clipIn) || !Finite(control.end))
                throw new ArgumentException("Control Clip timing must be finite with a positive timeScale.");
            var start = EvaluationTime(first);
            var end = EvaluationTime(last);
            if (!Finite(start) || !Finite(end) || start < control.start - 1e-9 || end > control.end + 1e-9)
                throw new ArgumentException("The entire Relay bake range must fit inside the unique Control Clip.");
            // DirectorControlPlayable clamps/wraps child time. Do not silently bake a different range.
            if (request.director.extrapolationMode == DirectorWrapMode.Loop && last >= request.director.duration ||
                last > request.director.duration + 1e-6)
                throw new ArgumentException("The Relay bake range must not loop or exceed the controlled Timeline duration.");
        }

        private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);

        private static IEnumerable<TimelineClip> Controls(PlayableDirector director)
        {
            var timeline = director.playableAsset as TimelineAsset;
            if (timeline == null) yield break;
            foreach (var track in timeline.GetOutputTracks().OfType<ControlTrack>())
                if (!track.mutedInHierarchy)
                    foreach (var clip in track.GetClips())
                        if (clip.asset is ControlPlayableAsset asset && asset.updateDirector) yield return clip;
        }
    }
}
