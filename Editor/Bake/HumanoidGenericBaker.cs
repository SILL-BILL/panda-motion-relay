using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using Object = UnityEngine.Object;

namespace SillBill.PandaMotionRelay.Editor
{
    public enum BakeSpace
    {
        Local = 0,
        Global = 1, // World -> Hips. Preserve the serialized value.
        World = 2
    }

    public sealed class GenericBakeRequest
    {
        public PlayableDirector director;
        public PlayableDirector evaluationDirector;
        public MotionRelayTrack track;
        public string outputPath;
        public BakeSpace bakeSpace = BakeSpace.Local;
        public float optimalReduction;
        public Action<float> progress;
        public Action<float> validationProgress;
        public Func<bool> cancel;
    }

    /// <summary>Editor-only raw Transform bake. Run the iterator on a Play Mode coroutine.</summary>
    public static class HumanoidGenericBaker
    {
        private static bool busy;
        public static bool IsBaking => busy;

        public static void Validate(GenericBakeRequest request) => ValidateAndPlan(request, out _, out _, out _);

        private static List<BakeSample> ValidateAndPlan(GenericBakeRequest request, out double fps,
            out TimelineClip[] clips, out BakeEvaluationScope scope)
        {
            if (request == null || request.director == null || request.track == null)
                throw new ArgumentException("Select a PlayableDirector and a Motion Relay Track.");
            if (request.bakeSpace != BakeSpace.Local && request.bakeSpace != BakeSpace.Global && request.bakeSpace != BakeSpace.World)
                throw new ArgumentException("Select Local, World to Root, or World to Hips Bake Mode.");
            if (!Finite(request.optimalReduction) || request.optimalReduction < 0f || request.optimalReduction > 1f)
                throw new ArgumentException("Optimal Reduction must be between 0 and 1.");
            var timeline = request.director.playableAsset as TimelineAsset;
            if (timeline == null || !timeline.GetOutputTracks().Contains(request.track) || request.track.mutedInHierarchy)
                throw new ArgumentException("The selected track must be an enabled output track of the director's Timeline.");
            var target = request.director.GetGenericBinding(request.track) as Animator;
            if (!HumanoidPoseRelay.IsValid(target) || !target.gameObject.activeInHierarchy || !target.enabled)
                throw new ArgumentException("Select a Motion Relay Track bound to an active Animator with a valid Humanoid Avatar. Root-only Animator Tracks are not supported. 身体側のHumanoid TargetへBindingしたTrackを選び、TargetをActiveにしてください。");
            if (!request.director.isActiveAndEnabled)
                throw new ArgumentException("The director must be active and enabled.");
            if (timeline.GetOutputTracks().OfType<MotionRelayTrack>().Count(t => !t.mutedInHierarchy && request.director.GetGenericBinding(t) == target) != 1)
                throw new ArgumentException("Only one enabled Relay Track may drive the target.");
            fps = timeline.editorSettings.frameRate;
            if (double.IsNaN(fps) || double.IsInfinity(fps) || fps <= 0 || fps > 240)
                throw new ArgumentException("Timeline frame rate must be greater than zero and at most 240 fps.");
            clips = request.track.GetClips().OrderBy(c => c.start).ToArray();
            if (clips.Length == 0) throw new ArgumentException("The Relay Track contains no clips.");
            TimelineClip previous = null;
            foreach (var clip in clips)
            {
                if (!Finite(clip.start) || !Finite(clip.end) || !Finite(clip.duration) ||
                    clip.start < 0 || clip.duration <= 0 || clip.end <= clip.start)
                    throw InvalidTiming(clip, fps, "expected finite times, a non-negative start and a positive representable duration");
                if (previous != null && clip.start < previous.end &&
                    !MotionRelayEvaluation.IsBoundaryRoundoff(previous, clip))
                    throw InvalidTiming(clip, fps, "Relay Clip overlap: bake refused");
                var asset = clip.asset as MotionRelayClip;
                var source = asset != null ? asset.source.Resolve(request.director) : null;
                if (!HumanoidPoseRelay.IsValid(source) || !source.enabled || !source.gameObject.activeInHierarchy ||
                    source == target || source.transform.IsChildOf(target.transform) || target.transform.IsChildOf(source.transform))
                    throw new ArgumentException("Every Relay Clip requires an active, valid, independent Humanoid source. Clip: '" + clip.displayName + "', Source: " + (source != null ? source.name : "<unassigned>") + ". 全ClipのDriver本体・親・Animatorを常時有効にしてください。Activation Trackをミュートし、非表示化はRendererだけで行います。");
                previous = clip;
            }
            double previousEnd = previous.end;
            if ((previousEnd - clips[0].start) * fps > 100000)
                throw new ArgumentException("Bake range exceeds 100000 frames. Split the Timeline before baking.");
            scope = new BakeEvaluationScope(request, clips[0].start, previousEnd);
            if (request.bakeSpace != BakeSpace.Local) new WorldBakeValidation(target).ValidateStatic();
            var paths = new HashSet<string>();
            foreach (var bone in MappedBones(target))
                if (bone == target.transform || !bone.IsChildOf(target.transform) || !paths.Add(AnimationUtility.CalculateTransformPath(bone, target.transform)))
                    throw new ArgumentException("Mapped bones require unique paths below the target Animator root.");
            ValidateOutputPath(request.outputPath);
            return BuildSamplePlan(clips, fps, request.track.closeUnrepresentableMicroGaps);
        }

        private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);

        private static ArgumentException InvalidTiming(TimelineClip clip, double fps, string reason) =>
            new ArgumentException(FormattableString.Invariant(
                $"Relay Clip '{clip.displayName}' (start={clip.start:R}, end={clip.end:R}, duration={clip.duration:R}, FPS={fps:R}): {reason}."));

        private readonly struct BakeSample
        {
            public readonly double TimelineTime;
            public readonly float KeyTime;
            public readonly bool StartsStep;

            public BakeSample(double timelineTime, float keyTime, bool startsStep = false)
            {
                TimelineTime = timelineTime;
                KeyTime = keyTime;
                StartsStep = startsStep;
            }
        }

        private static bool OnFrame(double time, double fps)
        {
            double frame = time * fps;
            // Ignore only round-off in the seconds-to-frames conversion.
            return Math.Abs(frame - Math.Round(frame)) <= Math.Max(1, Math.Abs(frame)) * 8.881784197001252e-16;
        }

        private static List<BakeSample> BuildSamplePlan(TimelineClip[] clips, double fps, bool closeMicroGaps)
        {
            const int maxSamples = 100001;
            double first = clips[0].start;
            double last = clips[clips.Length - 1].end;
            if (last * fps >= 4503599627370496d)
                throw InvalidTiming(clips[clips.Length - 1], fps, "frame indices exceed the precision of Timeline time");

            // AnimationClip stores key times as floats. Reserve authored boundaries before
            // inserting the frame grid so a near-coincident grid sample cannot replace a cut.
            var byKey = new SortedDictionary<float, BakeSample>();
            bool hasSubframes = false;
            for (int i = 0; i < clips.Length; i++)
            {
                var clip = clips[i];
                AddBoundary(clip.start, clip);
                double end = i + 1 < clips.Length && (MotionRelayEvaluation.IsBoundaryRoundoff(clip, clips[i + 1]) ||
                    closeMicroGaps && MotionRelayEvaluation.IsUnrepresentableMicroGap(clip, clips[i + 1], first))
                    ? clips[i + 1].start : clip.end;
                AddBoundary(end, clip);
            }

            long firstFrame = (long)Math.Ceiling(first * fps);
            long lastFrame = (long)Math.Floor(last * fps);
            for (long frame = firstFrame; frame <= lastFrame; frame++)
            {
                double time = frame / fps;
                if (time < first || time > last) continue;
                float key = (float)(time - first);
                if (!byKey.ContainsKey(key)) byKey.Add(key, new BakeSample(time, key));
            }

            var samples = new List<BakeSample>();
            foreach (var sample in byKey.Values)
            {
                if (samples.Count > 0 && hasSubframes && sample.StartsStep)
                {
                    // Leave a few float ULPs for native AnimationClipPlayable's time conversion.
                    // The only held interval is from this key to the actual cut key.
                    int bits = BitConverter.SingleToInt32Bits(sample.KeyTime);
                    float key = BitConverter.Int32BitsToSingle(Math.Max(0, bits - 4));
                    var previous = samples[samples.Count - 1];
                    if (key <= previous.KeyTime)
                        key = BitConverter.Int32BitsToSingle(bits - 1);
                    if (key > previous.KeyTime)
                    {
                        double time = first + key;
                        if (time <= previous.TimelineTime || time >= sample.TimelineTime)
                            time = previous.TimelineTime + (sample.TimelineTime - previous.TimelineTime) * .5;
                        if (time > previous.TimelineTime && time < sample.TimelineTime)
                            samples.Add(new BakeSample(time, key));
                    }
                }
                samples.Add(sample);
                if (samples.Count > maxSamples)
                    throw new ArgumentException("Bake exceeds 100001 samples including subframe keys. Split the Timeline before baking.");
            }
            return samples;

            void AddBoundary(double time, TimelineClip clip)
            {
                float key = (float)(time - first);
                if (byKey.TryGetValue(key, out var existing) && existing.TimelineTime != time)
                    throw InvalidTiming(clip, fps, "distinct boundaries coincide at AnimationClip float key precision");
                byKey[key] = new BakeSample(time, key, true);
                hasSubframes |= !OnFrame(time, fps);
                if (byKey.Count > maxSamples)
                    throw new ArgumentException("Bake exceeds 100001 samples including subframe keys. Split the Timeline before baking.");
            }
        }
        private static void ValidateOutputPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !path.StartsWith("Assets/", StringComparison.Ordinal) || path.Contains("\\") ||
                path.Split('/').Any(p => p == ".." || p == "." || p.Length == 0 || p.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) ||
                !path.EndsWith(".anim", StringComparison.OrdinalIgnoreCase) || string.IsNullOrEmpty(Path.GetFileNameWithoutExtension(path)))
                throw new ArgumentException("Choose a new .anim asset under Assets/.");
            string folder = Path.GetDirectoryName(path)?.Replace('\\', '/');
            if (!AssetDatabase.IsValidFolder(folder)) throw new ArgumentException("The output folder does not exist.");
            if (File.Exists(path) || File.Exists(path + ".meta") || AssetDatabase.LoadMainAssetAtPath(path) != null)
                throw new ArgumentException("The output asset already exists. Choose a new path; overwriting is not supported.");
        }

        private static IEnumerable<Transform> MappedBones(Animator target)
        {
            var seen = new HashSet<Transform>();
            for (int i = 0; i < (int)HumanBodyBones.LastBone; i++)
            {
                var bone = target.GetBoneTransform((HumanBodyBones)i);
                if (bone != null && seen.Add(bone)) yield return bone;
            }
        }

        public static IEnumerator Bake(GenericBakeRequest request, Action<AnimationClip> completed)
        {
            var samples = ValidateAndPlan(request, out double fps, out var clips, out var scope);
            float reduction = request.optimalReduction;
            if (!Application.isPlaying || EditorApplication.isPaused)
                throw new InvalidOperationException("Bake requires unpaused Play Mode so native constraints can evaluate.");
            if (busy) throw new InvalidOperationException("Another bake is already running.");
            var director = request.director;
            var target = (Animator)director.GetGenericBinding(request.track);
            var channels = new List<BoneCurves>();
            bool global = request.bakeSpace == BakeSpace.Global;
            bool world = request.bakeSpace == BakeSpace.World;
            if (world || request.track.relayMotionRoot && !global) channels.Add(new BoneCurves(target.transform, "", true, true, false, world));
            var hips = target.GetBoneTransform(HumanBodyBones.Hips);
            foreach (var bone in MappedBones(target))
                channels.Add(new BoneCurves(bone, AnimationUtility.CalculateTransformPath(bone, target.transform), bone == hips, false, global && bone == hips, world, target.transform));
            var directorState = new BakeDirectorState(scope.Directors);
            var scene = new BakeSceneState();
            var driverEvaluationState = new BakeDriverEvaluationState(clips.Select(c => ((MotionRelayClip)c.asset).source.Resolve(director)));
            var worldValidation = world || global ? new WorldBakeValidation(target) : null;
            AnimationClip result = null;
            bool saved = false;
            try
            {
                busy = true;
                driverEvaluationState.Prepare();
                directorState.Prepare();
                // Validate the complete evaluated range before recording any World curves.
                // Procedural/constraint results cannot be inferred from clip bindings alone.
                if (worldValidation != null)
                    for (int i = 0; i < samples.Count; i++)
                    {
                        if (request.cancel != null && request.cancel()) throw new OperationCanceledException("Bake canceled.");
                        scope.EvaluateSample(director, samples[i].TimelineTime);
                        yield return null;
                        MotionRelayEvaluation.ApplyPendingPoses();
                        worldValidation.Sample();
                        request.validationProgress?.Invoke((float)(i + 1) / samples.Count);
                    }
                // Reuse the preview graph: rebuilding can change AnimationTrack scene offsets
                // and its defaults for channels not explicitly animated by a driver clip.
                for (int i = 0; i < samples.Count; i++)
                {
                    if (request.cancel != null && request.cancel()) throw new OperationCanceledException("Bake canceled.");
                    scope.EvaluateSample(director, samples[i].TimelineTime);
                    // A normal coroutine resumes on the following Update, after the evaluated
                    // frame's ConstraintManager and Relay LateUpdate. Avoid WaitForEndOfFrame,
                    // which can stall in batch mode or while the Game view is not visible.
                    yield return null;
                    MotionRelayEvaluation.ApplyPendingPoses();
                    worldValidation?.Sample();
                    float time = samples[i].KeyTime;
                    foreach (var channel in channels) channel.Sample(time);
                    request.progress?.Invoke((float)(i + 1) / samples.Count);
                }
                result = new AnimationClip { name = Path.GetFileNameWithoutExtension(request.outputPath), frameRate = (float)fps, legacy = false };
                // Protect the interval immediately before each ownership change. Gaps also
                // terminate ownership; a cut is never converted into a sub-frame crossfade.
                var anchors = new SortedSet<int> { 0, samples.Count - 1 };
                var stepStarts = new HashSet<float>();
                for (int i = 1; i < samples.Count; i++)
                {
                    if (!samples[i].StartsStep) continue;
                    anchors.Add(i - 1);
                    anchors.Add(i);
                    stepStarts.Add(samples[i - 1].KeyTime);
                }
                foreach (var channel in channels)
                {
                    if (request.cancel != null && request.cancel()) throw new OperationCanceledException("Bake canceled.");
                    channel.Write(result, reduction, anchors);
                    if (reduction > 0f) yield return null;
                }
                result.EnsureQuaternionContinuity();
                foreach (var binding in AnimationUtility.GetCurveBindings(result))
                {
                    var curve = AnimationUtility.GetEditorCurve(result, binding);
                    for (int i = 0; i < curve.length; i++)
                        if (stepStarts.Contains(curve[i].time))
                            AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.Constant);
                    AnimationUtility.SetEditorCurve(result, binding, curve);
                }
                // Check again immediately before saving to prevent an accidental overwrite.
                ValidateOutputPath(request.outputPath);
                AssetDatabase.CreateAsset(result, request.outputPath);
                AssetDatabase.SaveAssetIfDirty(result);
                saved = true;
            }
            finally
            {
                try
                {
                    try { directorState.Restore(); }
                    finally
                    {
                        try { scene.Restore(); }
                        finally { driverEvaluationState.Restore(); }
                    }
                }
                finally
                {
                    busy = false;
                    if (!saved && result != null)
                    {
                        if (AssetDatabase.Contains(result)) AssetDatabase.DeleteAsset(request.outputPath);
                        else Object.DestroyImmediate(result);
                    }
                }
            }
            completed?.Invoke(result);
        }

        private sealed class BoneCurves
        {
            private readonly Transform bone;
            private readonly string path;
            private readonly bool position;
            private readonly bool root;
            private readonly bool globalHips;
            private readonly bool world;
            private readonly Transform animatorRoot;
            private Matrix4x4 referenceWorldToLocal;
            private Quaternion referenceWorldRotation;
            private bool hasReference;
            private readonly List<Keyframe>[] keys;
            private Quaternion previous;
            private bool hasPrevious;

            public BoneCurves(Transform bone, string path, bool position, bool root, bool globalHips = false, bool world = false, Transform animatorRoot = null)
            {
                this.bone = bone; this.path = path; this.position = position; this.root = root;
                this.globalHips = globalHips;
                this.world = world;
                this.animatorRoot = animatorRoot;
                keys = new List<Keyframe>[position ? 7 : 4];
                for (int i = 0; i < keys.Length; i++) keys[i] = new List<Keyframe>();
            }

            public void Sample(float time)
            {
                if (globalHips && !hasReference)
                {
                    // Build the independent output hierarchy, not the original world parent:
                    // identity root position/rotation, constant effective character scale,
                    // and the first evaluated internal helper transforms. Validation ensures
                    // that these helpers and scales remain fixed throughout the bake.
                    var internalParent = animatorRoot.worldToLocalMatrix * bone.parent.localToWorldMatrix;
                    var outputParent = Matrix4x4.Scale(animatorRoot.lossyScale) * internalParent;
                    referenceWorldToLocal = outputParent.inverse;
                    referenceWorldRotation = Quaternion.Inverse(animatorRoot.rotation) * bone.parent.rotation;
                    hasReference = true;
                }
                var rotation = world ? (root || bone.parent == null ? bone.rotation : Quaternion.Inverse(bone.parent.rotation) * bone.rotation) :
                    globalHips ? Quaternion.Inverse(referenceWorldRotation) * bone.rotation :
                    root && bone.parent != null ? Quaternion.Inverse(bone.parent.rotation) * bone.rotation : bone.localRotation;
                if (hasPrevious && Quaternion.Dot(previous, rotation) < 0)
                    rotation = new Quaternion(-rotation.x, -rotation.y, -rotation.z, -rotation.w);
                previous = rotation; hasPrevious = true;
                keys[0].Add(new Keyframe(time, rotation.x)); keys[1].Add(new Keyframe(time, rotation.y));
                keys[2].Add(new Keyframe(time, rotation.z)); keys[3].Add(new Keyframe(time, rotation.w));
                if (!position) return;
                var value = world ? (root || bone.parent == null ? bone.position : bone.parent.worldToLocalMatrix.MultiplyPoint3x4(bone.position)) :
                    globalHips ? referenceWorldToLocal.MultiplyPoint3x4(bone.position) :
                    root && bone.parent != null ? bone.parent.InverseTransformPoint(bone.position) : bone.localPosition;
                keys[4].Add(new Keyframe(time, value.x)); keys[5].Add(new Keyframe(time, value.y)); keys[6].Add(new Keyframe(time, value.z));
            }

            public void Write(AnimationClip clip, float reduction, SortedSet<int> anchors)
            {
                bool[] rotationKeys = null;
                bool[] positionKeys = null;
                if (reduction > 0f)
                {
                    // Quaternion chord length is stable even for angles too small for a float dot/acos test.
                    float rotationLimit = 2f * Mathf.Sin(0.05f * reduction * Mathf.Deg2Rad / 4f);
                    rotationKeys = Reduce(anchors, rotationLimit * rotationLimit, RotationErrorSquared);
                    if (position)
                    {
                        float positionLimit = 0.0001f * reduction;
                        positionKeys = Reduce(anchors, positionLimit * positionLimit, PositionErrorSquared);
                    }
                }
                for (int i = 0; i < keys.Length; i++)
                {
                    var retained = i < 4 ? rotationKeys : positionKeys;
                    var curve = new AnimationCurve(retained == null ? keys[i].ToArray() :
                        keys[i].Where((key, index) => retained[index]).ToArray());
                    for (int key = 0; key < curve.length; key++)
                    {
                        AnimationUtility.SetKeyLeftTangentMode(curve, key, AnimationUtility.TangentMode.Linear);
                        AnimationUtility.SetKeyRightTangentMode(curve, key, AnimationUtility.TangentMode.Linear);
                    }
                    string property = i < 4 ? "m_LocalRotation." + "xyzw"[i] : "m_LocalPosition." + "xyz"[i - 4];
                    AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, typeof(Transform), property), curve);
                }
            }

            private bool[] Reduce(SortedSet<int> anchors, float limitSquared, Func<int, int, int, float, float> errorSquared)
            {
                var retained = new bool[keys[0].Count];
                var spans = new Stack<(int Start, int End)>();
                int previousAnchor = 0;
                foreach (int anchor in anchors)
                {
                    retained[anchor] = true;
                    if (anchor - previousAnchor > 1) spans.Push((previousAnchor, anchor));
                    previousAnchor = anchor;
                }

                // Split at the worst sample until every retained span fits the error limit.
                while (spans.Count > 0)
                {
                    var (start, end) = spans.Pop();
                    float startTime = keys[0][start].time;
                    float duration = keys[0][end].time - startTime;
                    float worstError = limitSquared;
                    int split = -1;
                    for (int i = start + 1; i < end; i++)
                    {
                        float t = (keys[0][i].time - startTime) / duration;
                        float error = errorSquared(start, end, i, t);
                        if (float.IsNaN(error) || float.IsPositiveInfinity(error))
                        {
                            split = start + (end - start) / 2;
                            break;
                        }
                        if (error <= worstError) continue;
                        worstError = error;
                        split = i;
                    }
                    if (split < 0) continue;
                    // Keep long, pathological spans from repeatedly peeling one key off an end.
                    int quarter = (end - start) / 4;
                    if (quarter > 8 && (split - start < quarter || end - split < quarter))
                        split = start + (end - start) / 2;
                    retained[split] = true;
                    if (split - start > 1) spans.Push((start, split));
                    if (end - split > 1) spans.Push((split, end));
                }
                return retained;
            }

            private Vector3 PositionAt(int index) =>
                new Vector3(keys[4][index].value, keys[5][index].value, keys[6][index].value);

            private Quaternion RotationAt(int index) =>
                new Quaternion(keys[0][index].value, keys[1][index].value, keys[2][index].value, keys[3][index].value);

            private float PositionErrorSquared(int start, int end, int index, float t) =>
                (Vector3.LerpUnclamped(PositionAt(start), PositionAt(end), t) - PositionAt(index)).sqrMagnitude;

            private float RotationErrorSquared(int start, int end, int index, float t)
            {
                var from = RotationAt(start);
                var to = RotationAt(end);
                // Retain intermediate samples when removing them would reverse the quaternion path.
                if (Quaternion.Dot(from, to) <= 0f) return float.PositiveInfinity;
                var interpolated = Quaternion.LerpUnclamped(from, to, t);
                var expected = RotationAt(index);
                var difference = new Vector4(interpolated.x - expected.x, interpolated.y - expected.y,
                    interpolated.z - expected.z, interpolated.w - expected.w);
                return difference.sqrMagnitude;
            }
        }
    }
}
