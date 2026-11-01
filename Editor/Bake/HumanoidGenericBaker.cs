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
        Global = 1, // Legacy World -> Hips. Preserve serialized/API meaning.
        World = 2
    }

    public sealed class GenericBakeRequest
    {
        public PlayableDirector director;
        public PlayableDirector evaluationDirector;
        public MotionRelayTrack track;
        public string outputPath;
        public BakeSpace bakeSpace = BakeSpace.Local;
        public Action<float> progress;
        public Action<float> validationProgress;
        public Func<bool> cancel;
    }

    /// <summary>Editor-only raw Transform bake. Run the iterator on a Play Mode coroutine.</summary>
    public static class HumanoidGenericBaker
    {
        private static bool busy;
        public static bool IsBaking => busy;

        public static void Validate(GenericBakeRequest request)
        {
            if (request == null || request.director == null || request.track == null)
                throw new ArgumentException("Select a PlayableDirector and a Motion Relay Track.");
            if (request.bakeSpace != BakeSpace.Local && request.bakeSpace != BakeSpace.Global && request.bakeSpace != BakeSpace.World)
                throw new ArgumentException("Select Local, World to Root, or World to Hips Bake Mode.");
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
            double fps = timeline.editorSettings.frameRate;
            if (double.IsNaN(fps) || double.IsInfinity(fps) || fps <= 0 || fps > 240)
                throw new ArgumentException("Timeline frame rate must be greater than zero and at most 240 fps.");
            var clips = request.track.GetClips().OrderBy(c => c.start).ToArray();
            if (clips.Length == 0) throw new ArgumentException("The Relay Track contains no clips.");
            double previousEnd = double.NegativeInfinity;
            foreach (var clip in clips)
            {
                if (clip.start < previousEnd) throw new ArgumentException("Relay Clip overlap: bake refused.");
                if (clip.start < 0 || clip.duration <= 0 || !FrameAligned(clip.start, fps) || !FrameAligned(clip.end, fps))
                    throw new ArgumentException("Relay Clip boundaries must align to Timeline frames and have a positive duration.");
                var asset = clip.asset as MotionRelayClip;
                var source = asset != null ? asset.source.Resolve(request.director) : null;
                if (!HumanoidPoseRelay.IsValid(source) || !source.enabled || !source.gameObject.activeInHierarchy ||
                    source == target || source.transform.IsChildOf(target.transform) || target.transform.IsChildOf(source.transform))
                    throw new ArgumentException("Every Relay Clip requires an active, valid, independent Humanoid source. Clip: '" + clip.displayName + "', Source: " + (source != null ? source.name : "<unassigned>") + ". 全ClipのDriver本体・親・Animatorを常時有効にしてください。Activation Trackをミュートし、非表示化はRendererだけで行います。");
                previousEnd = clip.end;
            }
            if ((previousEnd - clips[0].start) * fps > 100000)
                throw new ArgumentException("Bake range exceeds 100000 frames. Split the Timeline before baking.");
            new BakeEvaluationScope(request, clips[0].start, previousEnd);
            if (request.bakeSpace == BakeSpace.World) new WorldBakeValidation(target).ValidateStatic();
            var paths = new HashSet<string>();
            foreach (var bone in MappedBones(target))
                if (bone == target.transform || !bone.IsChildOf(target.transform) || !paths.Add(AnimationUtility.CalculateTransformPath(bone, target.transform)))
                    throw new ArgumentException("Mapped bones require unique paths below the target Animator root.");
            ValidateOutputPath(request.outputPath);
        }

        private static bool FrameAligned(double time, double fps)
        {
            return !double.IsNaN(time) && !double.IsInfinity(time) && Math.Abs(time * fps - Math.Round(time * fps)) < 0.00001;
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
            Validate(request);
            if (!Application.isPlaying || EditorApplication.isPaused)
                throw new InvalidOperationException("Bake requires unpaused Play Mode so native constraints can evaluate.");
            if (busy) throw new InvalidOperationException("Another bake is already running.");
            var director = request.director;
            var target = (Animator)director.GetGenericBinding(request.track);
            var timeline = (TimelineAsset)director.playableAsset;
            double fps = timeline.editorSettings.frameRate;
            var clips = request.track.GetClips().OrderBy(c => c.start).ToArray();
            long first = (long)Math.Round(clips[0].start * fps);
            long last = (long)Math.Round(clips[clips.Length - 1].end * fps);
            var channels = new List<BoneCurves>();
            bool global = request.bakeSpace == BakeSpace.Global;
            bool world = request.bakeSpace == BakeSpace.World;
            if (world || request.track.relayMotionRoot && !global) channels.Add(new BoneCurves(target.transform, "", true, true, false, world));
            var hips = target.GetBoneTransform(HumanBodyBones.Hips);
            foreach (var bone in MappedBones(target))
                channels.Add(new BoneCurves(bone, AnimationUtility.CalculateTransformPath(bone, target.transform), bone == hips, false, global && bone == hips, world));
            var scope = new BakeEvaluationScope(request, first / fps, last / fps);
            var directorState = new BakeDirectorState(scope.Directors);
            var scene = new BakeSceneState();
            var driverEvaluationState = new BakeDriverEvaluationState(clips.Select(c => ((MotionRelayClip)c.asset).source.Resolve(director)));
            var worldValidation = world ? new WorldBakeValidation(target) : null;
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
                    for (long frame = first; frame <= last; frame++)
                    {
                        if (request.cancel != null && request.cancel()) throw new OperationCanceledException("Bake canceled.");
                        scope.Evaluation.time = scope.EvaluationTime(frame / fps);
                        scope.Evaluation.Evaluate();
                        yield return null;
                        MotionRelayEvaluation.ApplyPendingPoses();
                        worldValidation.Sample();
                        request.validationProgress?.Invoke((float)(frame - first + 1) / (last - first + 1));
                    }
                // Reuse the preview graph: rebuilding can change AnimationTrack scene offsets
                // and its defaults for channels not explicitly animated by a driver clip.
                for (long frame = first; frame <= last; frame++)
                {
                    if (request.cancel != null && request.cancel()) throw new OperationCanceledException("Bake canceled.");
                    scope.Evaluation.time = scope.EvaluationTime(frame / fps);
                    scope.Evaluation.Evaluate();
                    // A normal coroutine resumes on the following Update, after the evaluated
                    // frame's ConstraintManager and Relay LateUpdate. Avoid WaitForEndOfFrame,
                    // which can stall in batch mode or while the Game view is not visible.
                    yield return null;
                    MotionRelayEvaluation.ApplyPendingPoses();
                    worldValidation?.Sample();
                    float time = (float)((frame - first) / fps);
                    foreach (var channel in channels) channel.Sample(time);
                    request.progress?.Invoke((float)(frame - first + 1) / (last - first + 1));
                }
                result = new AnimationClip { name = Path.GetFileNameWithoutExtension(request.outputPath), frameRate = (float)fps, legacy = false };
                foreach (var channel in channels) channel.Write(result);
                result.EnsureQuaternionContinuity();
                // Protect the interval immediately before each ownership change. Gaps also
                // terminate ownership; a cut is never converted into a sub-frame crossfade.
                var boundaries = new HashSet<int>();
                foreach (var clip in clips)
                {
                    boundaries.Add((int)Math.Round(clip.start * fps - first));
                    boundaries.Add((int)Math.Round(clip.end * fps - first));
                }
                foreach (var binding in AnimationUtility.GetCurveBindings(result))
                {
                    var curve = AnimationUtility.GetEditorCurve(result, binding);
                    foreach (int boundary in boundaries)
                        if (boundary > 0 && boundary < curve.length)
                            AnimationUtility.SetKeyRightTangentMode(curve, boundary - 1, AnimationUtility.TangentMode.Constant);
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
            private Matrix4x4 referenceWorldToLocal;
            private Quaternion referenceWorldRotation;
            private bool hasReference;
            private readonly List<Keyframe>[] keys;
            private Quaternion previous;
            private bool hasPrevious;

            public BoneCurves(Transform bone, string path, bool position, bool root, bool globalHips = false, bool world = false)
            {
                this.bone = bone; this.path = path; this.position = position; this.root = root;
                this.globalHips = globalHips;
                this.world = world;
                keys = new List<Keyframe>[position ? 7 : 4];
                for (int i = 0; i < keys.Length; i++) keys[i] = new List<Keyframe>();
            }

            public void Sample(float time)
            {
                if (globalHips && !hasReference)
                {
                    // Sample is called only after the first frame's constraints and relay apply.
                    // Keep values, not a live Transform, so subsequent ancestor motion is retained.
                    referenceWorldToLocal = bone.parent != null ? bone.parent.worldToLocalMatrix : Matrix4x4.identity;
                    referenceWorldRotation = bone.parent != null ? bone.parent.rotation : Quaternion.identity;
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

            public void Write(AnimationClip clip)
            {
                for (int i = 0; i < keys.Length; i++)
                {
                    var curve = new AnimationCurve(keys[i].ToArray());
                    for (int key = 0; key < curve.length; key++)
                    {
                        AnimationUtility.SetKeyLeftTangentMode(curve, key, AnimationUtility.TangentMode.Linear);
                        AnimationUtility.SetKeyRightTangentMode(curve, key, AnimationUtility.TangentMode.Linear);
                    }
                    string property = i < 4 ? "m_LocalRotation." + "xyzw"[i] : "m_LocalPosition." + "xyz"[i - 4];
                    AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, typeof(Transform), property), curve);
                }
            }
        }
    }
}
