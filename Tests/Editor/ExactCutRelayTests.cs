using System;
using System.Collections;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.TestTools;
using UnityEngine.Timeline;

namespace SillBill.PandaMotionRelay.Tests
{
    public sealed partial class HumanoidRelayTests
    {
        private const string OverlapWarning = "Panda Motion Relay: overlapping Relay Clips are invalid. No pose will be relayed until exactly one clip occupies the current time.";

        [TestCase(false)]
        [TestCase(true)]
        public void EachDriverCanRelayAsTheOnlySource(bool useB)
        {
            var setup = CutTimeline();
            var clip = (MotionRelayClip)setup.Clips[0].asset;
            clip.source.defaultValue = useB ? setup.B : setup.A;
            clip.source.exposedName = default;
            Seek(setup, .5);
            AssertSelected(setup, useB);
        }

        [TestCase(24)]
        [TestCase(30)]
        [TestCase(60)]
        public void ExactCutOwnsIncomingFrameInBothDirections(int frameRate)
        {
            var setup = CutTimeline();
            double firstCut = 100d / frameRate;
            double secondCut = 200d / frameRate;
            setup.Clips[0].start = 0;
            setup.Clips[0].duration = firstCut;
            setup.Clips[1].start = firstCut;
            setup.Clips[1].duration = secondCut - firstCut;
            setup.Clips[2].start = secondCut;
            setup.Clips[2].duration = 100d / frameRate;
            // Cover the longest 24fps sequence with constant but distinct driver motion.
            ExtendDriverClips(setup, 14);
            foreach (var sample in new[]
            {
                (99, false), (100, true), (101, true),
                (199, true), (200, false), (201, false),
                (200, false), (199, true), (100, true), (99, false),
                (150, true), (50, false), (250, false), (100, true), (100, true)
            })
            {
                Seek(setup, (double)sample.Item1 / frameRate);
                AssertSelected(setup, sample.Item2);
            }
        }

        [Test]
        public void ForwardSequenceAndArbitrarySeeksAreHistoryIndependent()
        {
            var setup = CutTimeline();
            for (int frame = 0; frame < 180; frame++)
            {
                Seek(setup, frame / 60d);
                AssertSelected(setup, frame >= 60 && frame < 120);
            }
            foreach (double time in new[] { 1.8, .2, 2.7, 1, 0, 2, 1.999, .999, 1.8, 1.8 })
            {
                Seek(setup, time);
                AssertSelected(setup, time >= 1 && time < 2);
            }
        }

        [Test]
        public void LatestSeekReplacesPendingSourceBeforeApplication()
        {
            var setup = CutTimeline();
            EvaluateOnly(setup, .5);
            EvaluateOnly(setup, 1.5);
            MotionRelayEvaluation.ApplyPendingPoses();
            AssertSelected(setup, true);
            EvaluateOnly(setup, 1.5);
            EvaluateOnly(setup, .5);
            MotionRelayEvaluation.ApplyPendingPoses();
            AssertSelected(setup, false);
        }

        [TestCase(.999)]
        [TestCase(1d)]
        [TestCase(1.5)]
        [TestCase(1.999)]
        public void GapCancelsPendingRelayAndDoesNotTouchTarget(double time)
        {
            var setup = CutTimeline();
            setup.Clips[0].duration = .75;
            setup.Clips[1].start = 2;
            setup.Clips[1].duration = .5;
            setup.Clips[2].start = 2.5;
            EvaluateOnly(setup, .5);
            Pose(setup.Target, .13f);
            var before = Snapshot(setup.Target);
            EvaluateOnly(setup, time);
            MotionRelayEvaluation.ApplyPendingPoses();
            AssertSnapshot(setup.Target, before);
            Seek(setup, 2);
            AssertSelected(setup, true);
            Seek(setup, .25);
            AssertSelected(setup, false);
        }

        [Test]
        public void TrackEndDoesNotHoldOrExtrapolateSource()
        {
            var setup = CutTimeline();
            EvaluateOnly(setup, 2.5);
            Pose(setup.Target, .13f);
            var before = Snapshot(setup.Target);
            EvaluateOnly(setup, 3);
            MotionRelayEvaluation.ApplyPendingPoses();
            AssertSnapshot(setup.Target, before);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void OverlapRejectsAllSourcesIncludingSameSource(bool sameSource)
        {
            var setup = CutTimeline();
            setup.Clips[0].duration = 1.75;
            if (sameSource)
            {
                var clip = (MotionRelayClip)setup.Clips[1].asset;
                setup.Director.SetReferenceValue(clip.source.exposedName, setup.A);
            }
            EvaluateOnly(setup, .5);
            Pose(setup.Target, .13f);
            var before = Snapshot(setup.Target);
            LogAssert.Expect(LogType.Warning, OverlapWarning);
            Seek(setup, 1);
            AssertSnapshot(setup.Target, before);
            Seek(setup, 1.5);
            AssertSnapshot(setup.Target, before);
            Seek(setup, .5);
            AssertSelected(setup, false);
            LogAssert.Expect(LogType.Warning, OverlapWarning);
            Seek(setup, 1.25);
        }

        [Test]
        public void InvalidIncomingSourceDoesNotReusePreviousPose()
        {
            var setup = CutTimeline();
            var incoming = (MotionRelayClip)setup.Clips[1].asset;
            setup.Director.SetReferenceValue(incoming.source.exposedName, null);
            Seek(setup, .5);
            Pose(setup.Target, .13f);
            var before = Snapshot(setup.Target);
            LogAssert.Expect(LogType.Warning, new Regex("Panda Motion Relay: use two independent Humanoid Animators"));
            Seek(setup, 1);
            AssertSnapshot(setup.Target, before);
            Seek(setup, .5);
            AssertSelected(setup, false);
        }

        [Test]
        public void SwitchingPreservesRootScaleSecondaryPoseAndVisibility()
        {
            var setup = CutTimeline();
            setup.Target.transform.SetPositionAndRotation(new Vector3(3, 2, -1), Quaternion.Euler(0, 35, 0));
            setup.Target.transform.localScale = Vector3.one * 1.2f;
            var hips = setup.Target.GetBoneTransform(HumanBodyBones.Hips);
            hips.localScale = new Vector3(1.1f, 1, .9f);
            var hair = new GameObject("Secondary_Hair").transform;
            hair.SetParent(setup.Target.GetBoneTransform(HumanBodyBones.Head), false);
            hair.localPosition = new Vector3(.1f, .2f, .3f);
            hair.localScale = new Vector3(.9f, 1, 1.1f);
            var renderer = new GameObject("VisibleTargetMesh").AddComponent<MeshRenderer>();
            renderer.transform.SetParent(setup.Target.transform, false);
            var activation = setup.Target.gameObject.AddComponent<RelayActivationProbe>();
            int disableCount = activation.DisableCount;
            foreach (double time in new[] { .5, 1, 1.5, 2, .5, 2.5, 1, 1 })
            {
                hair.localRotation = Quaternion.Euler((float)time * 10, 23, 34);
                var rotation = hair.localRotation;
                Seek(setup, time);
                AssertSelected(setup, time >= 1 && time < 2);
                Assert.That(setup.Target.transform.position, Is.EqualTo(new Vector3(3, 2, -1)));
                Assert.That(Quaternion.Angle(setup.Target.transform.rotation, Quaternion.Euler(0, 35, 0)), Is.LessThan(.001f));
                Assert.That(setup.Target.transform.localScale, Is.EqualTo(Vector3.one * 1.2f));
                Assert.That(hips.localScale, Is.EqualTo(new Vector3(1.1f, 1, .9f)));
                Assert.That(hair.localPosition, Is.EqualTo(new Vector3(.1f, .2f, .3f)));
                Assert.That(hair.localScale, Is.EqualTo(new Vector3(.9f, 1, 1.1f)));
                Assert.That(Quaternion.Angle(hair.localRotation, rotation), Is.LessThan(.001f));
                Assert.That(renderer.enabled && renderer.gameObject.activeInHierarchy, Is.True);
                Assert.That(activation.DisableCount, Is.EqualTo(disableCount));
            }
        }

        [UnityTest]
        public IEnumerator EditorPreviewAutomaticallySwitchesAcrossCuts()
        {
            var setup = CutTimeline();
            foreach (double time in new[] { 59 / 60d, 1, 61 / 60d, 2, 1.5, .5, .5 })
            {
                EvaluateOnly(setup, time);
                yield return null;
                yield return null;
                AssertSelected(setup, time >= 1 && time < 2);
            }
        }

        [UnityTest]
        public IEnumerator PlayModeForwardPlaybackSwitchesWithoutManualFlush()
        {
            yield return new EnterPlayMode();
            var setup = CutTimeline();
            setup.Director.timeUpdateMode = DirectorUpdateMode.GameTime;
            setup.Director.time = .9;
            setup.Director.Play();
            int aSamples = 0;
            int bSamples = 0;
            int returnedSamples = 0;
            float deadline = Time.realtimeSinceStartup + 10;
            while (setup.Director.time < 2.1 && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
                double time = setup.Director.time;
                AssertSelected(setup, time >= 1 && time < 2);
                if (time < 1) aSamples++;
                else if (time < 2) bSamples++;
                else returnedSamples++;
            }
            Assert.That(aSamples, Is.GreaterThan(0));
            Assert.That(bSamples, Is.GreaterThan(0));
            Assert.That(returnedSamples, Is.GreaterThan(0));
            TearDown();
            yield return new ExitPlayMode();
        }

        private sealed class CutSetup
        {
            public PlayableDirector Director;
            public Animator A, B, Target;
            public TimelineClip[] Clips;
            public AnimationTrack[] AnimationTracks;
        }

        private CutSetup CutTimeline()
        {
            var setup = new CutSetup { A = Rig("Driver_A", 1), B = Rig("Driver_B", 1.1f), Target = Rig("Target", 1.3f) };
            setup.Director = Own(new GameObject("CutDirector")).AddComponent<PlayableDirector>();
            setup.Director.timeUpdateMode = DirectorUpdateMode.Manual;
            setup.Director.extrapolationMode = DirectorWrapMode.Hold;
            var timeline = Own(ScriptableObject.CreateInstance<TimelineAsset>());
            timeline.editorSettings.frameRate = 60;
            setup.Director.playableAsset = timeline;
            // Relay first also verifies that animation output order does not delay a cut.
            var relayTrack = Own(timeline.CreateTrack<MotionRelayTrack>(null, "Relay"));
            setup.Director.SetGenericBinding(relayTrack, setup.Target);
            setup.AnimationTracks = new AnimationTrack[2];
            for (int i = 0; i < 2; i++)
            {
                var animationTrack = Own(timeline.CreateTrack<AnimationTrack>(null, i == 0 ? "Motion_A" : "Motion_B"));
                setup.AnimationTracks[i] = animationTrack;
                animationTrack.trackOffset = TrackOffset.ApplySceneOffsets;
                var animation = Own(new AnimationClip());
                AnimationUtility.SetEditorCurve(animation,
                    EditorCurveBinding.FloatCurve("", typeof(Animator), "Left Arm Down-Up"),
                    AnimationCurve.Linear(0, i == 0 ? -.65f : .65f, 14, i == 0 ? -.35f : .35f));
                var clip = animationTrack.CreateClip(animation);
                Own(clip.asset);
                clip.duration = 14;
                setup.Director.SetGenericBinding(animationTrack, i == 0 ? setup.A : setup.B);
            }
            setup.Clips = new TimelineClip[3];
            for (int i = 0; i < 3; i++)
            {
                var clip = relayTrack.CreateClip<MotionRelayClip>();
                clip.start = i;
                clip.duration = 1;
                setup.Clips[i] = clip;
                var asset = Own((MotionRelayClip)clip.asset);
                asset.source.exposedName = new PropertyName("source_" + i);
                setup.Director.SetReferenceValue(asset.source.exposedName, i == 1 ? setup.B : setup.A);
            }
            return setup;
        }

        private static void ExtendDriverClips(CutSetup setup, double duration)
        {
            foreach (var track in setup.AnimationTracks)
                foreach (var clip in track.GetClips()) clip.duration = duration;
        }

        private static void EvaluateOnly(CutSetup setup, double time)
        {
            setup.Director.time = time;
            setup.Director.Evaluate();
        }

        private static void Seek(CutSetup setup, double time)
        {
            EvaluateOnly(setup, time);
            MotionRelayEvaluation.ApplyPendingPoses();
        }

        private static void AssertSelected(CutSetup setup, bool useB)
        {
            var expected = useB ? setup.B : setup.A;
            var wrong = useB ? setup.A : setup.B;
            AssertPoseMatches(expected, setup.Target);
            int muscle = Array.IndexOf(HumanTrait.MuscleName, "Left Arm Down-Up");
            Assert.That(Mathf.Abs(ReadPose(wrong).muscles[muscle] - ReadPose(setup.Target).muscles[muscle]), Is.GreaterThan(.4f));
            Assert.That(setup.Target.gameObject.activeInHierarchy && setup.Target.enabled, Is.True);
        }

        private static (Vector3, Quaternion, Vector3)[] Snapshot(Animator animator)
        {
            var transforms = animator.GetComponentsInChildren<Transform>(true);
            var values = new (Vector3, Quaternion, Vector3)[transforms.Length];
            for (int i = 0; i < transforms.Length; i++)
                values[i] = (transforms[i].localPosition, transforms[i].localRotation, transforms[i].localScale);
            return values;
        }

        private static void AssertSnapshot(Animator animator, (Vector3, Quaternion, Vector3)[] values)
        {
            var transforms = animator.GetComponentsInChildren<Transform>(true);
            Assert.That(transforms.Length, Is.EqualTo(values.Length));
            for (int i = 0; i < transforms.Length; i++)
            {
                Assert.That(transforms[i].localPosition, Is.EqualTo(values[i].Item1));
                Assert.That(Quaternion.Angle(transforms[i].localRotation, values[i].Item2), Is.LessThan(.001f));
                Assert.That(transforms[i].localScale, Is.EqualTo(values[i].Item3));
            }
        }
    }

    public sealed class RelayActivationProbe : MonoBehaviour
    {
        public int DisableCount { get; private set; }
        private void OnDisable() { DisableCount++; }
    }

}
