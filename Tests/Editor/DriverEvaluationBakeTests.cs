using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.Timeline;
using SillBill.PandaMotionRelay.Editor;
using Object = UnityEngine.Object;

namespace SillBill.PandaMotionRelay.Tests
{
    public sealed partial class HumanoidRelayTests
    {
        [UnityTest] public IEnumerator Bake_CulledDriverBody_Local() { yield return new EnterPlayMode(); yield return CheckDriverEvaluation(BakeSpace.Local, "success"); yield return new ExitPlayMode(); }
        [UnityTest] public IEnumerator Bake_CulledDriverBody_World() { yield return new EnterPlayMode(); yield return CheckDriverEvaluation(BakeSpace.World, "success"); yield return new ExitPlayMode(); }
        [UnityTest] public IEnumerator Bake_CulledDriverBody_Global() { yield return new EnterPlayMode(); yield return CheckDriverEvaluation(BakeSpace.Global, "success"); yield return new ExitPlayMode(); }
        [UnityTest] public IEnumerator Bake_RestoresDriverCulling_CancelLocal() { yield return new EnterPlayMode(); yield return CheckDriverEvaluation(BakeSpace.Local, "cancel"); yield return new ExitPlayMode(); }
        [UnityTest] public IEnumerator Bake_RestoresDriverCulling_CancelWorld() { yield return new EnterPlayMode(); yield return CheckDriverEvaluation(BakeSpace.World, "cancel"); yield return new ExitPlayMode(); }
        [UnityTest] public IEnumerator Bake_RestoresDriverCulling_CancelGlobal() { yield return new EnterPlayMode(); yield return CheckDriverEvaluation(BakeSpace.Global, "cancel"); yield return new ExitPlayMode(); }
        [UnityTest] public IEnumerator Bake_RestoresDriverCulling_ExceptionLocal() { yield return new EnterPlayMode(); yield return CheckDriverEvaluation(BakeSpace.Local, "exception"); yield return new ExitPlayMode(); }
        [UnityTest] public IEnumerator Bake_RestoresDriverCulling_ExceptionWorld() { yield return new EnterPlayMode(); yield return CheckDriverEvaluation(BakeSpace.World, "exception"); yield return new ExitPlayMode(); }
        [UnityTest] public IEnumerator Bake_RestoresDriverCulling_ExceptionGlobal() { yield return new EnterPlayMode(); yield return CheckDriverEvaluation(BakeSpace.Global, "exception"); yield return new ExitPlayMode(); }
        [UnityTest] public IEnumerator Bake_RestoresDriverCulling_PreflightCancel() { yield return new EnterPlayMode(); yield return CheckDriverEvaluation(BakeSpace.World, "preflight-cancel"); yield return new ExitPlayMode(); }
        [UnityTest] public IEnumerator Bake_RestoresDriverCulling_PreflightException() { yield return new EnterPlayMode(); yield return CheckDriverEvaluation(BakeSpace.World, "preflight-exception"); yield return new ExitPlayMode(); }
        [UnityTest] public IEnumerator Bake_RestoresDriverCulling_ValidationFailure() { yield return new EnterPlayMode(); yield return CheckDriverEvaluation(BakeSpace.World, "validation-failure"); yield return new ExitPlayMode(); }

        private IEnumerator CheckDriverEvaluation(BakeSpace mode, string outcome)
        {
            var setup = CutTimeline();
            ((TimelineAsset)setup.Director.playableAsset).editorSettings.frameRate = 24;
            var unused = Rig("UnreferencedDriver", 1);
            unused.cullingMode = AnimatorCullingMode.CullCompletely;
            setup.A.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
            setup.B.cullingMode = AnimatorCullingMode.CullCompletely;
            setup.Target.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
            var hidden = new[] { AddHiddenBakeRenderer(setup.A), AddHiddenBakeRenderer(setup.B) };
            string[] muscles = { "Head Nod Down-Up", "Left Arm Down-Up", "Right Upper Leg Front-Back" };
            var indices = muscles.Select(n => Array.IndexOf(HumanTrait.MuscleName, n)).ToArray();
            Assert.That(indices.All(i => i >= 0), Is.True);
            var curves = new AnimationCurve[2, 3];
            for (int driver = 0; driver < 2; driver++)
                foreach (var clip in setup.AnimationTracks[driver].GetClips())
                {
                    var animation = (AnimationPlayableAsset)clip.asset;
                    animation.applyFootIK = false;
                    for (int m = 0; m < muscles.Length; m++)
                    {
                        var curve = new AnimationCurve(new Keyframe(0, -.3f + driver * .1f), new Keyframe(1.5f, .5f - driver * .1f), new Keyframe(3, -.2f + driver * .1f));
                        for (int k = 0; k < curve.length; k++) { AnimationUtility.SetKeyLeftTangentMode(curve, k, AnimationUtility.TangentMode.Linear); AnimationUtility.SetKeyRightTangentMode(curve, k, AnimationUtility.TangentMode.Linear); }
                        curves[driver, m] = curve;
                        AnimationUtility.SetEditorCurve(animation.clip, EditorCurveBinding.FloatCurve("", typeof(Animator), muscles[m]), curve);
                    }
                }
            var request = BakeRequest(setup); request.bakeSpace = mode;
            setup.Director.time = .125; setup.Director.Evaluate(); yield return null; MotionRelayEvaluation.ApplyPendingPoses(); setup.Director.Pause();
            var before = Snapshot(setup.Target); var time = setup.Director.time; var updateMode = setup.Director.timeUpdateMode; var playState = setup.Director.state; bool graphValid = setup.Director.playableGraph.IsValid();
            int samples = 0, validation = 0;
            var sourceValues = new List<float>[] { new List<float>(), new List<float>() };
            var expectedHead = new Dictionary<int, Quaternion>();
            var oracle = Rig("IndependentKnownMuscleOracle", 1.3f);
            var oracleRest = ReadPose(oracle);
            var bodyBones = new[] { HumanBodyBones.Head, HumanBodyBones.LeftUpperArm, HumanBodyBones.RightUpperLeg };
            Action checkScope = () =>
            {
                Assert.That(setup.A.cullingMode, Is.EqualTo(AnimatorCullingMode.AlwaysAnimate));
                Assert.That(setup.B.cullingMode, Is.EqualTo(AnimatorCullingMode.AlwaysAnimate));
                Assert.That(setup.Target.cullingMode, Is.EqualTo(AnimatorCullingMode.CullUpdateTransforms));
                Assert.That(unused.cullingMode, Is.EqualTo(AnimatorCullingMode.CullCompletely));
                Assert.That(hidden.All(r => !r.enabled), Is.True);
            };
            request.validationProgress = _ => { validation++; checkScope(); if (outcome == "preflight-exception" && validation == 3) throw new InvalidOperationException("Injected driver preflight exception"); if (outcome == "validation-failure" && validation == 3) setup.Target.transform.localScale *= 1.1f; };
            request.progress = _ =>
            {
                checkScope(); int f = samples++; float t = f / 24f;
                if (f < 72)
                {
                    int driver = f >= 24 && f < 48 ? 1 : 0;
                    Assert.That(SelectedBakeSource(setup.Director), Is.EqualTo(driver == 0 ? setup.A : setup.B));
                    var input = ReadPose(driver == 0 ? setup.A : setup.B); var targetPose = ReadPose(setup.Target);
                    for (int m = 0; m < muscles.Length; m++)
                    {
                        float expected = curves[driver, m].Evaluate(t);
                        Assert.That(input.muscles[indices[m]], Is.EqualTo(expected).Within(.04f), muscles[m] + " independent Source input at " + f);
                        Assert.That(targetPose.muscles[indices[m]], Is.EqualTo(expected).Within(.05f), muscles[m] + " Target input at " + f);
                    }
                    sourceValues[driver].Add(input.muscles[indices[0]]);
                    if (f == 0 || f == 36 || f == 71)
                    {
                        var pose = oracleRest; pose.muscles = (float[])oracleRest.muscles.Clone();
                        for (int m = 0; m < muscles.Length; m++) pose.muscles[indices[m]] = curves[driver, m].Evaluate(t);
                        using (var handler = new HumanPoseHandler(oracle.avatar, oracle.transform)) handler.SetHumanPose(ref pose);
                        expectedHead.Add(f, oracle.GetBoneTransform(HumanBodyBones.Head).localRotation);
                    }
                }
                if (outcome == "exception" && samples == 3) throw new InvalidOperationException("Injected driver sampling exception");
            };
            request.cancel = () => outcome == "cancel" && samples == 3 || outcome == "preflight-cancel" && validation == 3;
            AnimationClip result = null; Exception failure = null; var operation = HumanoidGenericBaker.Bake(request, c => result = c);
            while (true) { bool more = false; try { more = operation.MoveNext(); } catch (Exception e) { failure = e; } if (!more || failure != null) break; yield return operation.Current; }
            (operation as IDisposable)?.Dispose();
            try
            {
                Assert.That(setup.A.cullingMode, Is.EqualTo(AnimatorCullingMode.CullUpdateTransforms));
                Assert.That(setup.B.cullingMode, Is.EqualTo(AnimatorCullingMode.CullCompletely));
                Assert.That(setup.Target.cullingMode, Is.EqualTo(AnimatorCullingMode.CullUpdateTransforms));
                Assert.That(unused.cullingMode, Is.EqualTo(AnimatorCullingMode.CullCompletely));
                Assert.That(HumanoidGenericBaker.IsBaking, Is.False); AssertSnapshot(setup.Target, before);
                Assert.That(setup.Director.time, Is.EqualTo(time)); Assert.That(setup.Director.timeUpdateMode, Is.EqualTo(updateMode)); Assert.That(setup.Director.state, Is.EqualTo(playState)); Assert.That(setup.Director.playableGraph.IsValid(), Is.EqualTo(graphValid));
                Assert.That(hidden.All(r => !r.enabled), Is.True);
                if (outcome != "success")
                {
                    Assert.That(failure, outcome.Contains("cancel") ? Is.TypeOf<OperationCanceledException>() : outcome == "validation-failure" ? Is.TypeOf<ArgumentException>() : Is.TypeOf<InvalidOperationException>());
                    Assert.That(result, Is.Null); Assert.That(AssetDatabase.LoadMainAssetAtPath(request.outputPath), Is.Null); yield break;
                }
                Assert.That(failure, Is.Null, failure?.ToString()); Assert.That(samples, Is.EqualTo(73));
                foreach (var values in sourceValues) Assert.That(values.Max() - values.Min(), Is.GreaterThan(.15f), "Within-driver Source animation");
                foreach (var bone in bodyBones)
                {
                    string path = AnimationUtility.CalculateTransformPath(setup.Target.GetBoneTransform(bone), setup.Target.transform);
                    Assert.That(AnimationUtility.GetCurveBindings(result).Count(b => b.path == path && b.propertyName.StartsWith("m_LocalRotation.")), Is.EqualTo(4));
                    Assert.That(Quaternion.Angle(SpaceRotation(result, path, 0).normalized, SpaceRotation(result, path, 1.5f).normalized), Is.GreaterThan(3), bone + " recorded variation");
                }
                string headPath = AnimationUtility.CalculateTransformPath(setup.Target.GetBoneTransform(HumanBodyBones.Head), setup.Target.transform);
                foreach (var expected in expectedHead) Assert.That(Quaternion.Angle(expected.Value.normalized, SpaceRotation(result, headPath, expected.Key / 24f).normalized), Is.LessThan(.1f), "Known muscle oracle Head at " + expected.Key);
            }
            finally { TearDown(); CleanBakeAssets(); }
        }

        private SkinnedMeshRenderer AddHiddenBakeRenderer(Animator animator)
        {
            var mesh = Own(new Mesh()); mesh.vertices = new[] { Vector3.zero, Vector3.right * .1f, Vector3.up * .1f }; mesh.triangles = new[] { 0, 1, 2 };
            var bone = animator.GetBoneTransform(HumanBodyBones.Head); mesh.bindposes = new[] { bone.worldToLocalMatrix * animator.transform.localToWorldMatrix };
            mesh.boneWeights = Enumerable.Repeat(new BoneWeight { boneIndex0 = 0, weight0 = 1 }, 3).ToArray(); mesh.RecalculateBounds();
            var renderer = animator.gameObject.AddComponent<SkinnedMeshRenderer>(); renderer.sharedMesh = mesh; renderer.bones = new[] { bone }; renderer.rootBone = bone; renderer.enabled = false;
            return renderer;
        }
    }
}
