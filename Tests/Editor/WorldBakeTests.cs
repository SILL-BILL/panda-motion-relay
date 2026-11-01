using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.TestTools;
using UnityEngine.Timeline;
using Object = UnityEngine.Object;
using SillBill.PandaMotionRelay.Editor;

namespace SillBill.PandaMotionRelay.Tests
{
    public sealed partial class HumanoidRelayTests
    {
        [Test]
        public void WorldBake_PreservesSerializedModeValues()
        {
            Assert.That((int)BakeSpace.Local, Is.Zero);
            Assert.That((int)BakeSpace.Global, Is.EqualTo(1));
            Assert.That((int)BakeSpace.World, Is.EqualTo(2));
            var window = ScriptableObject.CreateInstance<GenericBakeWindow>();
            try
            {
                var field = typeof(GenericBakeWindow).GetField("bakeSpace", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                foreach (var expected in new[] { BakeSpace.Local, BakeSpace.Global, BakeSpace.World })
                {
                    var serialized = new SerializedObject(window);
                    serialized.FindProperty("bakeSpace").intValue = (int)expected;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                    var restored = ScriptableObject.CreateInstance<GenericBakeWindow>();
                    try
                    {
                        EditorJsonUtility.FromJsonOverwrite(EditorJsonUtility.ToJson(window), restored);
                        Assert.That(field.GetValue(restored), Is.EqualTo(expected));
                    }
                    finally { Object.DestroyImmediate(restored); }
                }
            }
            finally { Object.DestroyImmediate(window); }
        }

        [TestCase("nonuniform")]
        [TestCase("negative")]
        [TestCase("mirror")]
        [TestCase("zero")]
        [TestCase("shear")]
        public void WorldBake_RejectsUnsafeScaleBeforeEvaluation(string kind)
        {
            var n = NestedBakeTimeline();
            var parent = Own(new GameObject("UnsafeParent")).transform;
            n.Relay.Target.transform.SetParent(parent, false);
            parent.localScale = kind == "zero" ? Vector3.zero : kind == "negative" ? -Vector3.one :
                kind == "mirror" ? new Vector3(-1, 1, 1) : new Vector3(2, 1, 1);
            if (kind == "shear") n.Relay.Target.transform.localRotation = Quaternion.Euler(0, 0, 35);
            var request = BakeRequest(n.Relay); request.evaluationDirector = n.Master; request.bakeSpace = BakeSpace.World;
            Assert.Throws<ArgumentException>(() => HumanoidGenericBaker.Validate(request));
            Assert.That(HumanoidGenericBaker.IsBaking, Is.False);
        }

        [UnityTest] public IEnumerator WorldBake_IndependentHierarchyMatchesRootAndHipsWorldPose() { yield return new EnterPlayMode(); yield return CheckWorld("pose"); yield return new ExitPlayMode(); }
        [UnityTest] public IEnumerator WorldBake_NestedParentMotion() { yield return new EnterPlayMode(); yield return CheckWorld("parent"); yield return new ExitPlayMode(); }
        [UnityTest] public IEnumerator WorldBake_ParentConstraint() { yield return new EnterPlayMode(); yield return CheckWorld("constraint"); yield return new ExitPlayMode(); }
        [UnityTest] public IEnumerator WorldBake_ExactCut() { yield return new EnterPlayMode(); yield return CheckWorld("cut"); yield return new ExitPlayMode(); }
        [UnityTest] public IEnumerator WorldBake_FrameZero() { yield return new EnterPlayMode(); yield return CheckWorld("zero"); yield return new ExitPlayMode(); }
        [UnityTest] public IEnumerator WorldBake_Frame144() { yield return new EnterPlayMode(); yield return CheckWorld("144"); yield return new ExitPlayMode(); }
        [UnityTest] public IEnumerator WorldBake_NoScaleCurves() { yield return new EnterPlayMode(); yield return CheckWorld("scale"); yield return new ExitPlayMode(); }
        [UnityTest] public IEnumerator WorldBake_NoSecondaryCurves() { yield return new EnterPlayMode(); yield return CheckWorld("secondary"); yield return new ExitPlayMode(); }
        [UnityTest] public IEnumerator WorldBake_RestoresDirectorState() { yield return new EnterPlayMode(); yield return CheckWorld("restore"); yield return new ExitPlayMode(); }
        [UnityTest] public IEnumerator WorldBake_CancelRestoresDirectorState() { yield return new EnterPlayMode(); yield return CheckWorld("cancel"); yield return new ExitPlayMode(); }
        [UnityTest] public IEnumerator WorldBake_ExceptionRestoresDirectorState() { yield return new EnterPlayMode(); yield return CheckWorld("exception"); yield return new ExitPlayMode(); }
        [UnityTest] public IEnumerator WorldBake_GenericAnimatorReplay() { yield return new EnterPlayMode(); yield return CheckWorld("native"); yield return new ExitPlayMode(); }
        [UnityTest] public IEnumerator WorldBake_TargetFinalWorldPoseWithRelayRootOff() { yield return new EnterPlayMode(); yield return CheckWorld("root-off"); yield return new ExitPlayMode(); }
        [UnityTest] public IEnumerator WorldBake_RejectsAnimatedScaleBeforeRecording() { yield return new EnterPlayMode(); yield return CheckWorld("animated-scale"); yield return new ExitPlayMode(); }
        [UnityTest] public IEnumerator WorldBake_PositionScopeRejectsHelperTranslation() { yield return new EnterPlayMode(); yield return CheckWorld("helper-position"); yield return new ExitPlayMode(); }
        [UnityTest] public IEnumerator WorldBake_PositionScopeRejectsMappedTranslation() { yield return new EnterPlayMode(); yield return CheckWorld("bone-position"); yield return new ExitPlayMode(); }
        [UnityTest] public IEnumerator WorldBake_RejectsAnimatedInternalHelperRotation() { yield return new EnterPlayMode(); yield return CheckWorld("helper-rotation"); yield return new ExitPlayMode(); }

        private sealed class WorldFrame
        {
            public Vector3[] positions;
            public Quaternion[] rotations;
            public Vector3 rootScale;
            public WorldFrame(Transform[] transforms) { positions = transforms.Select(t => t.position).ToArray(); rotations = transforms.Select(t => t.rotation).ToArray(); rootScale = transforms[0].lossyScale; }
        }

        private IEnumerator CheckWorld(string check)
        {
            var n = NestedBakeTimeline();
            n.Relay.Target.gameObject.SetActive(false);
            n.Relay.Target = Rig("WorldTarget", 1.3f, true, true);
            var track = ((TimelineAsset)n.Relay.Director.playableAsset).GetOutputTracks().OfType<MotionRelayTrack>().Single();
            n.Relay.Director.SetGenericBinding(track, n.Relay.Target);
            track.relayMotionRoot = check != "root-off";
            n.Relay.Clips[0].start = 0; n.Relay.Clips[0].duration = 138d / 24;
            n.Relay.Clips[1].start = 138d / 24; n.Relay.Clips[1].duration = 102d / 24;
            n.Relay.Clips[2].start = 240d / 24; n.Relay.Clips[2].duration = 134d / 24;
            ExtendDriverClips(n.Relay, 16);
            foreach (var c in ((TimelineAsset)n.Master.playableAsset).GetOutputTracks().OfType<ControlTrack>().SelectMany(t => t.GetClips())) c.duration = 16;
            foreach (var c in ((TimelineAsset)n.Prop.playableAsset).GetOutputTracks().SelectMany(t => t.GetClips())) { c.duration = 16; c.timeScale = 3d / 16; }
            var offset = Own(new GameObject("FixedYOffset")).transform; offset.position = new Vector3(0, 3.125f, 0);
            var parent = Own(new GameObject("MovingNestedRoot")).transform; parent.SetParent(offset, false); parent.localScale = Vector3.one * 1.2f;
            var parentAnimator = parent.gameObject.AddComponent<Animator>();
            var parentMotion = Own(new AnimationClip()); SetSpaceCurve(parentMotion, "m_LocalPosition.x", 2, 8); SetSpaceCurve(parentMotion, "m_LocalPosition.y", 0, 1);
            var q = Quaternion.Euler(0, 35, 0); for (int i = 0; i < 4; i++) SetSpaceCurve(parentMotion, "m_LocalRotation." + "xyzw"[i], i == 3 ? 1 : 0, q[i]);
            var parentTrack = Own(((TimelineAsset)n.Relay.Director.playableAsset).CreateTrack<AnimationTrack>()); parentTrack.trackOffset = TrackOffset.ApplyTransformOffsets;
            var parentClip = parentTrack.CreateClip(parentMotion); Own(parentClip.asset); parentClip.duration = 16; parentClip.timeScale = 3d / 16; n.Relay.Director.SetGenericBinding(parentTrack, parentAnimator);
            n.Relay.A.transform.SetParent(parent, false); n.Relay.B.transform.SetParent(parent, false); n.Anchor.SetParent(parent, false);
            n.Relay.Target.transform.SetParent(parent, false); n.Relay.Target.transform.localScale = Vector3.one * .8f;
            var hair = new GameObject("SecondaryHair").transform; hair.SetParent(n.Relay.Target.GetBoneTransform(HumanBodyBones.Head), false); hair.localPosition = new Vector3(.1f, .2f, .3f);
            var request = BakeRequest(n.Relay); request.evaluationDirector = n.Master; request.bakeSpace = BakeSpace.World;
            bool reject = check == "animated-scale" || check.EndsWith("position") || check == "helper-rotation";
            if (reject && check != "animated-scale")
            {
                if (check == "bone-position") n.Relay.Clips[0].duration = .5;
                var t = check == "bone-position" ? n.Relay.Target.GetBoneTransform(HumanBodyBones.Head) : n.Relay.Target.transform.Find("Armature");
                var animator = t.gameObject.AddComponent<Animator>();
                var motion = Own(new AnimationClip());
                if (check == "helper-rotation") { var rotation = Quaternion.Euler(0, 25, 0); for (int i = 0; i < 4; i++) SetSpaceCurve(motion, "m_LocalRotation." + "xyzw"[i], i == 3 ? 1 : 0, rotation[i]); }
                else SetSpaceCurve(motion, "m_LocalPosition.x", t.localPosition.x, t.localPosition.x + .5f);
                var animationTrack = Own(((TimelineAsset)n.Relay.Director.playableAsset).CreateTrack<AnimationTrack>()); animationTrack.trackOffset = TrackOffset.ApplyTransformOffsets;
                var animationClip = animationTrack.CreateClip(motion); Own(animationClip.asset); animationClip.duration = 16; animationClip.timeScale = 3d / 16; n.Relay.Director.SetGenericBinding(animationTrack, animator);
            }
            var transforms = n.Relay.Target.GetComponentsInChildren<Transform>(true);
            var paths = transforms.Select(t => AnimationUtility.CalculateTransformPath(t, n.Relay.Target.transform)).ToArray();
            var frames = new List<WorldFrame>();
            GameObject replay = null;
            if (!reject)
            {
                n.Master.time = 0; n.Master.Evaluate(); yield return null; MotionRelayEvaluation.ApplyPendingPoses();
                for (int f = 0; f <= 374; f++)
                {
                    n.Master.time = f / 24d; n.Master.Evaluate(); yield return null; MotionRelayEvaluation.ApplyPendingPoses(); frames.Add(new WorldFrame(transforms));
                    if (f == 0)
                    {
                        var neutral = Own(new GameObject("IndependentIdentityParent")).transform;
                        replay = Own(Object.Instantiate(n.Relay.Target.gameObject, neutral));
                        var animator = replay.GetComponent<Animator>(); animator.avatar = null; animator.runtimeAnimatorController = null; animator.enabled = false;
                        replay.transform.SetPositionAndRotation(transforms[0].position, transforms[0].rotation); replay.transform.localScale = frames[0].rootScale;
                    }
                    if (f == 144 && check == "144") AssertWorld(n.Relay.Target.transform, n.Relay.B.transform.position, n.Relay.B.transform.rotation);
                }
            }
            if (check == "zero") { n.Master.Stop(); n.Relay.Director.Stop(); n.Prop.Stop(); n.Relay.Target.transform.position = Vector3.one * 100; }
            else { n.Master.time = .25; n.Master.Evaluate(); yield return null; MotionRelayEvaluation.ApplyPendingPoses(); }
            var directors = new[] { n.Master, n.Relay.Director, n.Prop };
            foreach (var d in directors) d.Pause();
            var times = directors.Select(d => d.time).ToArray(); var modes = directors.Select(d => d.timeUpdateMode).ToArray(); var states = directors.Select(d => d.state).ToArray(); var graphs = directors.Select(d => d.playableGraph.IsValid()).ToArray();
            var before = Snapshot(n.Relay.Target);
            int samples = 0, validationSamples = 0;
            request.validationProgress = _ => { validationSamples++; if (check == "animated-scale" && validationSamples == 20) n.Relay.Target.transform.localScale *= 1.1f; };
            request.progress = _ => { if (samples == 0 && !reject) Assert.That(SelectedBakeSource(n.Relay.Director), Is.EqualTo(n.Relay.A)); samples++; if (check == "exception" && samples == 3) throw new InvalidOperationException("World sampling injected exception"); };
            if (check == "cancel") request.cancel = () => validationSamples == 3;
            AnimationClip output = null; Exception failure = null; var operation = HumanoidGenericBaker.Bake(request, c => output = c);
            while (true) { bool more = false; try { more = operation.MoveNext(); } catch (Exception e) { failure = e; } if (!more || failure != null) break; yield return operation.Current; }
            (operation as IDisposable)?.Dispose();
            try
            {
                for (int i = 0; i < directors.Length; i++) { Assert.That(directors[i].time, Is.EqualTo(times[i])); Assert.That(directors[i].timeUpdateMode, Is.EqualTo(modes[i])); Assert.That(directors[i].state, Is.EqualTo(states[i])); Assert.That(directors[i].playableGraph.IsValid(), Is.EqualTo(graphs[i])); }
                AssertSnapshot(n.Relay.Target, before); Assert.That(HumanoidGenericBaker.IsBaking, Is.False);
                if (reject || check == "cancel" || check == "exception") { Assert.That(failure, reject ? Is.TypeOf<ArgumentException>() : check == "cancel" ? Is.TypeOf<OperationCanceledException>() : Is.TypeOf<InvalidOperationException>()); Assert.That(output, Is.Null); Assert.That(AssetDatabase.LoadMainAssetAtPath(request.outputPath), Is.Null); if (reject) Assert.That(samples, Is.Zero, "Reject before recording"); yield break; }
                Assert.That(failure, Is.Null, failure?.ToString()); Assert.That(samples, Is.EqualTo(375)); Assert.That(validationSamples, Is.EqualTo(375));
                var bindings = AnimationUtility.GetCurveBindings(output); Assert.That(bindings.Count(b => b.path == ""), Is.EqualTo(7));
                var hipsPath = AnimationUtility.CalculateTransformPath(n.Relay.Target.GetBoneTransform(HumanBodyBones.Hips), n.Relay.Target.transform);
                Assert.That(bindings.Count(b => b.path == hipsPath), Is.EqualTo(7)); Assert.That(bindings.Count(b => b.propertyName.Contains("Position")), Is.EqualTo(6));
                Assert.That(bindings.Any(b => b.propertyName.Contains("Scale") || b.path.Contains("SecondaryHair") || b.propertyName.Contains("Euler")), Is.False);
                var replayTransforms = paths.Select(p => p == "" ? replay.transform : replay.transform.Find(p)).ToArray();
                for (int f = 0; f <= 374; f++) { output.SampleAnimation(replay, f / 24f); AssertWorldFrames(replayTransforms, frames[f]); }
                if (check == "cut")
                    foreach (int boundary in new[] { 138, 240 })
                    {
                        foreach (var binding in bindings) Assert.That(AnimationUtility.GetKeyRightTangentMode(AnimationUtility.GetEditorCurve(output, binding), boundary - 1), Is.EqualTo(AnimationUtility.TangentMode.Constant));
                        output.SampleAnimation(replay, (boundary - .5f) / 24f); AssertWorldFrames(replayTransforms, frames[boundary - 1]);
                    }
                if (check == "native")
                {
                    var controller = AnimatorController.CreateAnimatorControllerAtPath(bakeFolder + "/Replay.controller"); var state = controller.layers[0].stateMachine.AddState("Baked"); state.motion = output; controller.layers[0].stateMachine.defaultState = state;
                    var animator = replay.GetComponent<Animator>(); animator.runtimeAnimatorController = controller; animator.enabled = true; animator.applyRootMotion = false; animator.cullingMode = AnimatorCullingMode.AlwaysAnimate; animator.speed = 0; animator.Rebind();
                    Assert.That(animator.isHuman, Is.False);
                    for (int f = 0; f <= 374; f++) { animator.Play("Baked", 0, (f / 24f) / output.length); animator.Update(0); yield return null; AssertWorldFrames(replayTransforms, frames[f]); }
                }
            }
            finally { TearDown(); CleanBakeAssets(); }
        }

        private static void AssertWorldFrames(Transform[] transforms, WorldFrame frame)
        {
            for (int i = 0; i < transforms.Length; i++)
            {
                Assert.That(Vector3.Distance(transforms[i].position, frame.positions[i]), Is.LessThan(.001f), transforms[i].name + " World position");
                Assert.That(Quaternion.Angle(transforms[i].rotation, frame.rotations[i]), Is.LessThan(.1f), transforms[i].name + " World rotation");
            }
        }
    }
}
