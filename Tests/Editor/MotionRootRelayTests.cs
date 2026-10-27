using System.Collections;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.TestTools;
using UnityEngine.Timeline;

namespace SillBill.PandaMotionRelay.Tests
{
    public sealed partial class HumanoidRelayTests
    {
        [Test]
        public void MotionRootDisabledByDefaultPreservesTargetRoot()
        {
            var setup = RootTimeline(false);
            var position = setup.Target.transform.position;
            var rotation = setup.Target.transform.rotation;
            foreach (double time in new[] { .5, 1, 1.5, 2, .5 })
            {
                Seek(setup, time);
                AssertSelected(setup, time >= 1 && time < 2);
                AssertWorld(setup.Target.transform, position, rotation);
            }
        }

        [Test]
        public void MotionRootEnabledCopiesWorldPositionAndRotation()
        {
            var setup = RootTimeline();
            foreach (double time in new[] { .5, 1, 2, 1.5 })
            {
                Seek(setup, time);
                AssertRootAndPose(setup, time >= 1 && time < 2);
            }
        }

        [Test]
        public void MotionRootEnabledPreservesTargetAndMappedBoneScale()
        {
            var setup = RootTimeline();
            setup.A.transform.localScale = Vector3.one * .8f;
            setup.B.transform.localScale = Vector3.one * 1.7f;
            setup.Target.transform.localScale = Vector3.one * 1.2f;
            var hips = setup.Target.GetBoneTransform(HumanBodyBones.Hips);
            hips.localScale = new Vector3(1.1f, 1, .9f);
            foreach (double time in new[] { .5, 1, 1.5, 2, .5 })
            {
                Seek(setup, time);
                AssertRootAndPose(setup, time >= 1 && time < 2);
                Assert.That(setup.Target.transform.localScale, Is.EqualTo(Vector3.one * 1.2f));
                Assert.That(hips.localScale, Is.EqualTo(new Vector3(1.1f, 1, .9f)));
            }
        }

        [TestCase(false, true)]
        [TestCase(true, false)]
        [TestCase(true, true)]
        public void MotionRootUsesWorldSpaceWithIndependentParents(bool sourceParent, bool targetParent)
        {
            var setup = RootTimeline();
            var sourceContainer = Own(new GameObject("Vehicle")).transform;
            sourceContainer.SetPositionAndRotation(new Vector3(10, 2, 5), Quaternion.Euler(10, 65, 0));
            sourceContainer.localScale = Vector3.one * 1.5f;
            var targetContainer = Own(new GameObject("TargetContainer")).transform;
            targetContainer.SetPositionAndRotation(new Vector3(-4, 1, 8), Quaternion.Euler(0, -35, 10));
            targetContainer.localScale = Vector3.one * .8f;
            var sourceContainerSnapshot = (sourceContainer.position, sourceContainer.rotation, sourceContainer.localScale);
            var targetContainerSnapshot = (targetContainer.position, targetContainer.rotation, targetContainer.localScale);
            if (sourceParent)
            {
                var driverContainer = new GameObject("DriverContainer").transform;
                driverContainer.SetParent(sourceContainer, false);
                driverContainer.localPosition = new Vector3(2, 1, -3);
                setup.B.transform.SetParent(driverContainer, false);
            }
            if (targetParent) setup.Target.transform.SetParent(targetContainer, false);
            var scale = setup.Target.transform.localScale;
            foreach (double time in new[] { .5, 1, 1.5, 2, .5 })
            {
                Seek(setup, time);
                AssertRootAndPose(setup, time >= 1 && time < 2);
                Assert.That(setup.Target.transform.localScale, Is.EqualTo(scale));
                AssertWorld(sourceContainer, sourceContainerSnapshot.Item1, sourceContainerSnapshot.Item2);
                AssertWorld(targetContainer, targetContainerSnapshot.Item1, targetContainerSnapshot.Item2);
                Assert.That(sourceContainer.localScale, Is.EqualTo(sourceContainerSnapshot.Item3));
                Assert.That(targetContainer.localScale, Is.EqualTo(targetContainerSnapshot.Item3));
            }
        }

        [Test]
        public void MotionRootIsReadAtLateApplyNotAtProcessFrame()
        {
            var setup = RootTimeline();
            EvaluateOnly(setup, 1.5);
            setup.B.transform.SetPositionAndRotation(new Vector3(9, 2, -7), Quaternion.Euler(10, 125, 5));
            MotionRelayEvaluation.ApplyPendingPoses();
            AssertRootAndPose(setup, true);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void HumanPoseAndRootOrderIsMeasuredAgainstBodyPlacement(bool rootFirst)
        {
            var source = Rig("OrderSource", 1.1f);
            var target = Rig("OrderTarget", 1.3f);
            Pose(source, .6f);
            source.transform.SetPositionAndRotation(new Vector3(7, 2, -4), Quaternion.Euler(0, 65, 0));
            target.transform.SetPositionAndRotation(new Vector3(-5, 3, 8), Quaternion.Euler(0, -30, 0));
            var originalHips = target.GetBoneTransform(HumanBodyBones.Hips).localPosition;
            var pose = ReadPose(source);
            pose.bodyPosition = Quaternion.Inverse(source.transform.rotation) *
                (pose.bodyPosition - source.transform.position / source.humanScale);
            pose.bodyRotation = Quaternion.Inverse(source.transform.rotation) * pose.bodyRotation;
            if (rootFirst) target.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
            using (var handler = new HumanPoseHandler(target.avatar, target.transform)) handler.SetHumanPose(ref pose);
            if (!rootFirst) target.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
            AssertWorld(target.transform, source.transform.position, source.transform.rotation);
            AssertPoseMatches(source, target);
            Assert.That(Vector3.Distance(target.GetBoneTransform(HumanBodyBones.Hips).localPosition, originalHips), Is.LessThan(.05f),
                rootFirst ? "Root then HumanPose" : "HumanPose then root");
        }

        [TestCase(24)]
        [TestCase(30)]
        [TestCase(60)]
        public void ExactCutSwitchesPoseAndRootTogetherAcrossBoundaryFrames(int frameRate)
        {
            var setup = RootTimeline();
            setup.Clips[0].duration = 100d / frameRate;
            setup.Clips[1].start = 100d / frameRate;
            setup.Clips[1].duration = 100d / frameRate;
            setup.Clips[2].start = 200d / frameRate;
            setup.Clips[2].duration = 100d / frameRate;
            foreach (int frame in new[] { 99, 100, 101, 199, 200, 201, 199, 100, 99, 150, 50, 150, 150 })
            {
                Seek(setup, frame / (double)frameRate);
                AssertRootAndPose(setup, frame >= 100 && frame < 200);
            }
        }

        [Test]
        public void GapDoesNotWritePoseOrRootAndCancelsPendingApplication()
        {
            var setup = RootTimeline();
            setup.Clips[0].duration = .75;
            setup.Clips[1].start = 2;
            setup.Clips[1].duration = .5;
            setup.Clips[2].start = 2.5;
            EvaluateOnly(setup, .5);
            var before = Snapshot(setup.Target);
            foreach (double time in new[] { .75, 1, 1.999 })
            {
                Seek(setup, time);
                AssertSnapshot(setup.Target, before);
            }
        }

        [Test]
        public void OverlapDoesNotWritePoseOrRootAndCancelsPendingApplication()
        {
            var setup = RootTimeline();
            setup.Clips[0].duration = 1.75;
            EvaluateOnly(setup, .5);
            var before = Snapshot(setup.Target);
            LogAssert.Expect(LogType.Warning, OverlapWarning);
            foreach (double time in new[] { 1d, 1.25, 1.5 })
            {
                Seek(setup, time);
                AssertSnapshot(setup.Target, before);
            }
        }

        [Test]
        public void MotionRootPreservesLiveSecondaryLocalPoseAndAllRendererStates()
        {
            var setup = RootTimeline();
            var hair = new GameObject("SecondaryHair").transform;
            hair.SetParent(setup.Target.GetBoneTransform(HumanBodyBones.Head), false);
            hair.localPosition = new Vector3(.1f, .2f, .3f);
            hair.localScale = new Vector3(.9f, 1, 1.1f);
            var sourceHair = new GameObject("DifferentSourceHair").transform;
            sourceHair.SetParent(setup.B.GetBoneTransform(HumanBodyBones.Head), false);
            sourceHair.localPosition = Vector3.one * 9;
            var targetRenderer = setup.Target.gameObject.AddComponent<MeshRenderer>();
            var aRenderer = setup.A.gameObject.AddComponent<MeshRenderer>();
            var bRenderer = setup.B.gameObject.AddComponent<MeshRenderer>();
            aRenderer.enabled = false;
            var activation = setup.Target.gameObject.AddComponent<RelayActivationProbe>();
            foreach (double time in new[] { .5, 1, 2, .25, 1.5 })
            {
                hair.localRotation = Quaternion.Euler(12, (float)time * 10, 34);
                var rotation = hair.localRotation;
                Seek(setup, time);
                AssertRootAndPose(setup, time >= 1 && time < 2);
                Assert.That(hair.localPosition, Is.EqualTo(new Vector3(.1f, .2f, .3f)));
                Assert.That(hair.localScale, Is.EqualTo(new Vector3(.9f, 1, 1.1f)));
                Assert.That(Quaternion.Angle(hair.localRotation, rotation), Is.LessThan(.001f));
                Assert.That(targetRenderer.enabled && bRenderer.enabled && !aRenderer.enabled, Is.True);
                Assert.That(setup.A.gameObject.activeSelf && setup.B.gameObject.activeSelf, Is.True);
                Assert.That(activation.DisableCount, Is.Zero);
            }
        }

        [UnityTest]
        public IEnumerator EditorPreviewReadsFinalParentSpaceRootAtLateApply()
        {
            var setup = RootTimeline();
            var parent = Own(new GameObject("PreviewVehicle")).transform;
            setup.B.transform.SetParent(parent, true);
            foreach (double time in new[] { 1d, 1.5, 1.25, .5, 1d })
            {
                EvaluateOnly(setup, time);
                // Change the final world transform after ProcessFrame queued the source.
                parent.SetPositionAndRotation(new Vector3((float)time * 4, 2, 5), Quaternion.Euler(10, (float)time * 35, 0));
                yield return null;
                yield return null;
                AssertRootAndPose(setup, time >= 1 && time < 2);
            }
        }

        [UnityTest]
        public IEnumerator ParentConstraintAndCutUseSameFinalSourceInPlayMode()
        {
            yield return new EnterPlayMode();
            var setup = RootTimeline();
            var anchor = Own(new GameObject("VehicleAnchor")).transform;
            AddRootConstraint(setup.B, anchor);
            setup.Director.Play();
            foreach (double time in new[] { 59 / 60d, 60 / 60d, 61 / 60d, 60 / 60d, 59 / 60d, .5, 1.5 })
            {
                // Use the existing cuts at t=1 and t=2 for these manual evaluations.
                anchor.SetPositionAndRotation(new Vector3((float)time * 3, 2, -4), Quaternion.Euler(0, (float)time * 45, 5));
                EvaluateOnly(setup, time);
                yield return null;
                AssertWorld(setup.B.transform, anchor.position, anchor.rotation);
                AssertRootAndPose(setup, time >= 1 && time < 2);
            }
            TearDown();
            yield return new ExitPlayMode();
        }

        [Test]
        public void MotionRootTrackOptionRoundTripsAndDefaultsOff()
        {
            var track = Own(ScriptableObject.CreateInstance<MotionRelayTrack>());
            Assert.That(track.relayMotionRoot, Is.False);
            track.relayMotionRoot = true;
            var json = EditorJsonUtility.ToJson(track);
            var restored = Own(ScriptableObject.CreateInstance<MotionRelayTrack>());
            EditorJsonUtility.FromJsonOverwrite(json, restored);
            Assert.That(restored.relayMotionRoot, Is.True);
        }

        private CutSetup RootTimeline(bool enabled = true)
        {
            var setup = CutTimeline();
            foreach (var track in ((TimelineAsset)setup.Director.playableAsset).GetOutputTracks())
                if (track is MotionRelayTrack relayTrack)
                {
                    Assert.That(relayTrack.relayMotionRoot, Is.False);
                    relayTrack.relayMotionRoot = enabled;
                }
            setup.A.transform.SetPositionAndRotation(new Vector3(0, 1, 2), Quaternion.Euler(0, 25, 0));
            setup.B.transform.SetPositionAndRotation(new Vector3(3, 2, -4), Quaternion.Euler(0, -55, 0));
            setup.Target.transform.SetPositionAndRotation(new Vector3(-6, 3, 8), Quaternion.Euler(0, 80, 0));
            return setup;
        }

        private static ParentConstraint AddRootConstraint(Animator animator, Transform anchor)
        {
            var constraint = animator.gameObject.AddComponent<ParentConstraint>();
            constraint.AddSource(new ConstraintSource { sourceTransform = anchor, weight = 1 });
            constraint.translationAxis = Axis.X | Axis.Y | Axis.Z;
            constraint.rotationAxis = Axis.X | Axis.Y | Axis.Z;
            constraint.SetTranslationOffset(0, Vector3.zero);
            constraint.SetRotationOffset(0, Vector3.zero);
            constraint.weight = 1;
            constraint.locked = true;
            constraint.constraintActive = true;
            return constraint;
        }

        private static void AssertRootAndPose(CutSetup setup, bool useB)
        {
            var source = useB ? setup.B : setup.A;
            AssertSelected(setup, useB);
            AssertWorld(setup.Target.transform, source.transform.position, source.transform.rotation);
            // Root motion must not be counted twice by HumanPoseHandler.
            var hips = setup.Target.GetBoneTransform(HumanBodyBones.Hips);
            Assert.That(hips.localPosition.magnitude, Is.LessThan(3f));
            Assert.That(Quaternion.Angle(hips.rotation, source.GetBoneTransform(HumanBodyBones.Hips).rotation), Is.LessThan(.1f), "Hips world rotation");
        }

        private static void AssertWorld(Transform target, Vector3 position, Quaternion rotation)
        {
            Assert.That(Vector3.Distance(target.position, position), Is.LessThan(.001f), "World position");
            Assert.That(Quaternion.Angle(target.rotation, rotation), Is.LessThan(.05f), "World rotation");
        }
    }
}
