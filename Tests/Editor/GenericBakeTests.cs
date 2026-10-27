using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using SillBill.PandaMotionRelay.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.TestTools;
using UnityEngine.Timeline;
using Object = UnityEngine.Object;

namespace SillBill.PandaMotionRelay.Tests
{
    public sealed partial class HumanoidRelayTests
    {
        private string bakeFolder;
        private AnimationClip bakedResult;
        private int bakeSampleCount;
        private Animator bakeTarget;
        private List<PoseFrame> observedBakeFrames;
        private void ObserveBakeFrame(float progress) { observedBakeFrames.Add(new PoseFrame(bakeTarget)); }
        private void SetBakedResult(AnimationClip clip) { bakedResult = clip; }

        [TearDown]
        public void CleanBakeAssets()
        {
            if (!string.IsNullOrEmpty(bakeFolder) && AssetDatabase.IsValidFolder(bakeFolder)) AssetDatabase.DeleteAsset(bakeFolder);
            bakeFolder = null;
        }

        private GenericBakeRequest BakeRequest(CutSetup setup)
        {
            if (string.IsNullOrEmpty(bakeFolder))
            {
                if (!AssetDatabase.IsValidFolder("Assets/LocalOnly")) AssetDatabase.CreateFolder("Assets", "LocalOnly");
                bakeFolder = "Assets/LocalOnly/PandaBakeTests_" + Guid.NewGuid().ToString("N");
                AssetDatabase.CreateFolder("Assets/LocalOnly", Path.GetFileName(bakeFolder));
            }
            return new GenericBakeRequest
            {
                director = setup.Director,
                track = ((TimelineAsset)setup.Director.playableAsset).GetOutputTracks().OfType<MotionRelayTrack>().Single(),
                outputPath = bakeFolder + "/Result.anim"
            };
        }

        [TestCase("missing")]
        [TestCase("generic")]
        [TestCase("invalid")]
        [TestCase("inactive")]
        public void BakeRejectsInvalidTarget(string kind)
        {
            var setup = CutTimeline();
            var request = BakeRequest(setup);
            if (kind == "missing") setup.Director.SetGenericBinding(request.track, null);
            else if (kind == "generic") setup.Target.avatar = null;
            else if (kind == "invalid")
            {
                var deletedAvatar = Object.Instantiate(setup.Target.avatar);
                setup.Target.avatar = deletedAvatar;
                Object.DestroyImmediate(deletedAvatar);
            }
            else setup.Target.gameObject.SetActive(false);
            Assert.Throws<ArgumentException>(() => HumanoidGenericBaker.Validate(request));
        }

        [TestCase("director")]
        [TestCase("timeline")]
        [TestCase("track")]
        [TestCase("source")]
        [TestCase("overlap")]
        [TestCase("subframe")]
        public void BakeRejectsInvalidTimelineConfiguration(string kind)
        {
            var setup = CutTimeline();
            var request = BakeRequest(setup);
            if (kind == "director") request.director = null;
            else if (kind == "timeline") setup.Director.playableAsset = null;
            else if (kind == "track") request.track = null;
            else if (kind == "source") setup.Director.SetReferenceValue(((MotionRelayClip)setup.Clips[1].asset).source.exposedName, null);
            else if (kind == "overlap") setup.Clips[0].duration = 1.5;
            else setup.Clips[0].duration = .123;
            Assert.Throws<ArgumentException>(() => HumanoidGenericBaker.Validate(request));
        }

        [TestCase("Packages/Baked.anim")]
        [TestCase("Assets/../Baked.anim")]
        [TestCase("Assets/MissingFolder/Baked.anim")]
        [TestCase("Assets/Baked.txt")]
        public void BakeRejectsInvalidOutputPath(string path)
        {
            var request = BakeRequest(CutTimeline());
            request.outputPath = path;
            Assert.Throws<ArgumentException>(() => HumanoidGenericBaker.Validate(request));
        }

        [Test]
        public void BakeDoesNotOverwriteAnExistingAsset()
        {
            var request = BakeRequest(CutTimeline());
            var existing = new AnimationClip { name = "Existing" };
            AssetDatabase.CreateAsset(existing, request.outputPath);
            string guid = AssetDatabase.AssetPathToGUID(request.outputPath);
            string originalName = AssetDatabase.LoadAssetAtPath<AnimationClip>(request.outputPath).name;
            byte[] originalContents = File.ReadAllBytes(request.outputPath);
            Assert.Throws<ArgumentException>(() => HumanoidGenericBaker.Validate(request));
            Assert.That(AssetDatabase.AssetPathToGUID(request.outputPath), Is.EqualTo(guid));
            Assert.That(AssetDatabase.LoadAssetAtPath<AnimationClip>(request.outputPath).name, Is.EqualTo(originalName));
            Assert.That(File.ReadAllBytes(request.outputPath), Is.EqualTo(originalContents));
        }

        [Test]
        public void BakeRequiresPlayModeForConstraintEvaluation()
        {
            var request = BakeRequest(CutTimeline());
            var operation = HumanoidGenericBaker.Bake(request, null);
            Assert.Throws<InvalidOperationException>(() => operation.MoveNext());
            Assert.That(HumanoidGenericBaker.IsBaking, Is.False);
        }

        [UnityTest]
        public IEnumerator BakeMappedCurvesAndExactCutsMatchPreview()
        {
            yield return new EnterPlayMode();
            var setup = FingerBakeTimeline(false);
            var request = BakeRequest(setup);
            ((TimelineAsset)setup.Director.playableAsset).editorSettings.frameRate = 30;
            var hair = new GameObject("Hair_Secondary").transform;
            hair.SetParent(setup.Target.GetBoneTransform(HumanBodyBones.Head), false);
            hair.localPosition = new Vector3(.1f, .2f, .3f);
            hair.localRotation = Quaternion.Euler(10, 20, 30);
            var initial = Snapshot(setup.Target);
            var reference = new List<PoseFrame>();
            yield return ReferenceFrames(setup, request, reference);
            RestoreSnapshot(setup.Target, initial);
            bakedResult = null;
            bakeTarget = setup.Target;
            observedBakeFrames = new List<PoseFrame>();
            request.progress = ObserveBakeFrame;
            yield return HumanoidGenericBaker.Bake(request, SetBakedResult);
            Assert.That(bakedResult, Is.Not.Null);
            Assert.That(bakedResult.humanMotion || bakedResult.legacy, Is.False);
            Assert.That(bakedResult.frameRate, Is.EqualTo(30));
            Assert.That(bakedResult.length, Is.EqualTo(3).Within(.0001));
            AssertSnapshot(setup.Target, initial);
            AssertBindings(setup.Target, bakedResult, false);
            AssertRoundTrip(setup, bakedResult, observedBakeFrames, "mapped-exact-cut");
            TearDown(); CleanBakeAssets();
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator BakeMotionRootAndParentConstraintMatchPreviewUnderTargetParent()
        {
            yield return new EnterPlayMode();
            var setup = FingerBakeTimeline(true);
            var parent = Own(new GameObject("BakeTargetContainer")).transform;
            parent.SetPositionAndRotation(new Vector3(-5, 2, 8), Quaternion.Euler(0, 55, 0));
            parent.localScale = Vector3.one * .8f;
            setup.Target.transform.SetParent(parent, true);
            var anchor = Own(new GameObject("BakeConstraintAnchor")).transform;
            anchor.SetPositionAndRotation(new Vector3(6, 1, -4), Quaternion.Euler(0, 100, 0));
            AddRootConstraint(setup.B, anchor);
            var request = BakeRequest(setup);
            ((TimelineAsset)setup.Director.playableAsset).editorSettings.frameRate = 30;
            var initial = Snapshot(setup.Target);
            var reference = new List<PoseFrame>();
            yield return ReferenceFrames(setup, request, reference);
            Assert.That(Vector3.Distance(reference[30].position[0], anchor.position), Is.LessThan(.001f));
            RestoreSnapshot(setup.Target, initial);
            bakedResult = null;
            bakeTarget = setup.Target;
            observedBakeFrames = new List<PoseFrame>();
            request.progress = ObserveBakeFrame;
            yield return HumanoidGenericBaker.Bake(request, SetBakedResult);
            AssertBindings(setup.Target, bakedResult, true);
            AssertRoundTrip(setup, bakedResult, observedBakeFrames, "root-parent-constraint");
            Assert.That(parent.position, Is.EqualTo(new Vector3(-5, 2, 8)));
            TearDown(); CleanBakeAssets();
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator BakeGapSamplesActualTargetAndRestoresSceneState()
        {
            yield return new EnterPlayMode();
            var setup = FingerBakeTimeline(true);
            setup.Clips[0].duration = .5;
            setup.Clips[1].start = 1.5;
            setup.Clips[1].duration = .5;
            var request = BakeRequest(setup);
            ((TimelineAsset)setup.Director.playableAsset).editorSettings.frameRate = 20;
            var initial = Snapshot(setup.Target);
            var reference = new List<PoseFrame>();
            yield return ReferenceFrames(setup, request, reference);
            RestoreSnapshot(setup.Target, initial);
            setup.Director.time = .2;
            var driverA = Snapshot(setup.A);
            var driverB = Snapshot(setup.B);
            var renderer = setup.Target.gameObject.AddComponent<MeshRenderer>();
            renderer.enabled = false;
            bakedResult = null;
            bakeTarget = setup.Target;
            observedBakeFrames = new List<PoseFrame>();
            request.progress = ObserveBakeFrame;
            yield return HumanoidGenericBaker.Bake(request, SetBakedResult);
            Assert.That(setup.Director.time, Is.EqualTo(.2));
            Assert.That(setup.Director.timeUpdateMode, Is.EqualTo(DirectorUpdateMode.Manual));
            AssertSnapshot(setup.Target, initial);
            AssertSnapshot(setup.A, driverA); AssertSnapshot(setup.B, driverB);
            Assert.That(renderer.enabled, Is.False);
            AssertRoundTrip(setup, bakedResult, observedBakeFrames, "gap");
            TearDown(); CleanBakeAssets();
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator BakeCancellationRestoresSceneAndDoesNotSaveAsset()
        {
            yield return new EnterPlayMode();
            var setup = FingerBakeTimeline(true);
            var request = BakeRequest(setup);
            var initial = Snapshot(setup.Target);
            bakeSampleCount = 0;
            request.progress = value => bakeSampleCount++;
            request.cancel = () => bakeSampleCount >= 2;
            var operation = HumanoidGenericBaker.Bake(request, null);
            Exception failure = null;
            while (true)
            {
                bool more = false;
                try { more = operation.MoveNext(); }
                catch (Exception exception) { failure = exception; }
                if (!more || failure != null) break;
                yield return operation.Current;
            }
            (operation as IDisposable)?.Dispose();
            Assert.That(failure, Is.TypeOf<OperationCanceledException>());
            AssertSnapshot(setup.Target, initial);
            Assert.That(AssetDatabase.LoadMainAssetAtPath(request.outputPath), Is.Null);
            Assert.That(HumanoidGenericBaker.IsBaking, Is.False);
            TearDown(); CleanBakeAssets();
            yield return new ExitPlayMode();
        }

        private CutSetup FingerBakeTimeline(bool motionRoot)
        {
            var setup = RootTimeline(motionRoot);
            setup.Target.gameObject.SetActive(false);
            setup.Target = Rig("BakeTarget", 1.3f, true);
            var track = ((TimelineAsset)setup.Director.playableAsset).GetOutputTracks().OfType<MotionRelayTrack>().Single();
            setup.Director.SetGenericBinding(track, setup.Target);
            return setup;
        }

        private sealed class PoseFrame
        {
            public Vector3[] position, localPosition;
            public Quaternion[] rotation;
            public float arm;
            public PoseFrame(Animator target)
            {
                var transforms = target.GetComponentsInChildren<Transform>(true);
                position = transforms.Select(t => t.position).ToArray();
                localPosition = transforms.Select(t => t.localPosition).ToArray();
                rotation = transforms.Select(t => t.rotation).ToArray();
                arm = ReadPose(target).muscles[Array.IndexOf(HumanTrait.MuscleName, "Left Arm Down-Up")];
            }
        }

        private static IEnumerator ReferenceFrames(CutSetup setup, GenericBakeRequest request, List<PoseFrame> result)
        {
            double fps = ((TimelineAsset)setup.Director.playableAsset).editorSettings.frameRate;
            for (int frame = 0; frame <= (int)Math.Round(3 * fps); frame++)
            {
                EvaluateOnly(setup, frame / fps);
                yield return null;
                MotionRelayEvaluation.ApplyPendingPoses();
                result.Add(new PoseFrame(setup.Target));
            }
        }

        private static void AssertBindings(Animator target, AnimationClip baked, bool root)
        {
            var bindings = AnimationUtility.GetCurveBindings(baked);
            var mapped = new HashSet<string>();
            for (int i = 0; i < (int)HumanBodyBones.LastBone; i++)
            {
                var bone = target.GetBoneTransform((HumanBodyBones)i);
                if (bone != null) mapped.Add(AnimationUtility.CalculateTransformPath(bone, target.transform));
            }
            string hips = AnimationUtility.CalculateTransformPath(target.GetBoneTransform(HumanBodyBones.Hips), target.transform);
            string finger = AnimationUtility.CalculateTransformPath(target.GetBoneTransform(HumanBodyBones.LeftIndexProximal), target.transform);
            Assert.That(bindings.Count(b => b.path == hips), Is.EqualTo(7));
            Assert.That(bindings.Count(b => b.path == finger), Is.EqualTo(4));
            Assert.That(bindings.Count(b => b.path == ""), Is.EqualTo(root ? 7 : 0));
            foreach (var binding in bindings)
            {
                Assert.That(binding.type, Is.EqualTo(typeof(Transform)));
                Assert.That(mapped.Contains(binding.path) || (root && binding.path == ""), Is.True);
                Assert.That(binding.propertyName.Contains("Scale") || binding.propertyName.Contains("Euler"), Is.False);
                Assert.That(binding.propertyName.StartsWith("m_LocalRotation.") ||
                    (binding.propertyName.StartsWith("m_LocalPosition.") && (binding.path == hips || binding.path == "")), Is.True);
                Assert.That(AnimationUtility.GetEditorCurve(baked, binding).length, Is.EqualTo((int)Math.Round(3 * baked.frameRate) + 1));
            }
            foreach (var path in mapped) Assert.That(bindings.Count(b => b.path == path && b.propertyName.StartsWith("m_LocalRotation.")), Is.EqualTo(4));
        }

        private static void AssertRoundTrip(CutSetup setup, AnimationClip clip, List<PoseFrame> reference, string scenario)
        {
            setup.Director.Stop();
            // Generic Transform clips are target-specific. Replay on an identical hierarchy
            // configured as Generic, avoiding a second Humanoid retargeting pass.
            var replay = Object.Instantiate(setup.Target.gameObject, setup.Target.transform.parent);
            var animator = replay.GetComponent<Animator>();
            animator.avatar = null;
            animator.Rebind();
            animator.enabled = false;
            var transforms = replay.GetComponentsInChildren<Transform>(true);
            float maxPosition = 0, maxAngle = 0;
            try
            {
                for (int frame = 0; frame < reference.Count; frame++)
                {
                    clip.SampleAnimation(replay, frame / clip.frameRate);
                    for (int bone = 0; bone < transforms.Length; bone++)
                    {
                        maxPosition = Mathf.Max(maxPosition, Vector3.Distance(transforms[bone].position, reference[frame].position[bone]));
                        maxAngle = Mathf.Max(maxAngle, Quaternion.Angle(transforms[bone].rotation, reference[frame].rotation[bone]));
                        Assert.That(Vector3.Distance(transforms[bone].localPosition, reference[frame].localPosition[bone]), Is.LessThan(.001f), "Local position: " + transforms[bone].name + " frame " + frame);
                    }
                }
                // The interval just before an incoming source must hold the outgoing frame.
                clip.SampleAnimation(replay, 1f - .0001f);
                int outgoing = (int)clip.frameRate - 1;
                for (int bone = 0; bone < transforms.Length; bone++)
                    Assert.That(Quaternion.Angle(transforms[bone].rotation, reference[outgoing].rotation[bone]), Is.LessThan(.1f), "Sub-frame cut protection");
            }
            finally { Object.DestroyImmediate(replay); }
            string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "../../LocalOnly/Verification"));
            Directory.CreateDirectory(folder);
            File.AppendAllText(Path.Combine(folder, "phase3-roundtrip-errors.txt"), scenario + ": position=" + maxPosition.ToString("R") + ", angle=" + maxAngle.ToString("R") + "\n");
            Assert.That(maxPosition, Is.LessThan(.001f), "Maximum world position error");
            Assert.That(maxAngle, Is.LessThan(.1f), "Maximum world quaternion angle error");
        }

        private static void RestoreSnapshot(Animator animator, (Vector3, Quaternion, Vector3)[] values)
        {
            var transforms = animator.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                transforms[i].localPosition = values[i].Item1; transforms[i].localRotation = values[i].Item2; transforms[i].localScale = values[i].Item3;
            }
        }
    }
}
