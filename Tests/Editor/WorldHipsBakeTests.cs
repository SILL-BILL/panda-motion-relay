using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SillBill.PandaMotionRelay.Editor;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.TestTools;
using UnityEngine.Timeline;

namespace SillBill.PandaMotionRelay.Tests
{
    public sealed partial class HumanoidRelayTests
    {
        [UnityTest] public IEnumerator WorldToHips_IndependentIdentityHierarchyMatchesWorldPose() { yield return new EnterPlayMode(); yield return CheckWorld("pose", BakeSpace.Global); yield return new ExitPlayMode(); }
        [UnityTest] public IEnumerator WorldToHips_RootHasNoCurves() { yield return new EnterPlayMode(); yield return CheckWorld("root", BakeSpace.Global); yield return new ExitPlayMode(); }
        [UnityTest] public IEnumerator WorldToHips_HipsContainsFullWorldTranslation() { yield return new EnterPlayMode(); yield return CheckWorld("translation", BakeSpace.Global); yield return new ExitPlayMode(); }
        [UnityTest] public IEnumerator WorldToHips_HipsContainsFullWorldRotation() { yield return new EnterPlayMode(); yield return CheckWorld("rotation", BakeSpace.Global); yield return new ExitPlayMode(); }
        [UnityTest] public IEnumerator WorldToHips_NestedParentTranslation() { yield return new EnterPlayMode(); yield return CheckWorld("parent", BakeSpace.Global); yield return new ExitPlayMode(); }
        [UnityTest] public IEnumerator WorldToHips_NestedParentRotation() { yield return new EnterPlayMode(); yield return CheckWorld("parent-rotation", BakeSpace.Global); yield return new ExitPlayMode(); }
        [UnityTest] public IEnumerator WorldToHips_ParentConstraint() { yield return new EnterPlayMode(); yield return CheckWorld("constraint", BakeSpace.Global); yield return new ExitPlayMode(); }
        [UnityTest] public IEnumerator WorldToHips_FrameZero() { yield return new EnterPlayMode(); yield return CheckWorld("zero", BakeSpace.Global); yield return new ExitPlayMode(); }
        [UnityTest] public IEnumerator WorldToHips_ExactCut() { yield return new EnterPlayMode(); yield return CheckWorld("cut", BakeSpace.Global); yield return new ExitPlayMode(); }
        [UnityTest] public IEnumerator WorldToHips_Frame144() { yield return new EnterPlayMode(); yield return CheckWorld("144", BakeSpace.Global); yield return new ExitPlayMode(); }
        [UnityTest] public IEnumerator WorldToHips_NoScaleCurves() { yield return new EnterPlayMode(); yield return CheckWorld("scale", BakeSpace.Global); yield return new ExitPlayMode(); }
        [UnityTest] public IEnumerator WorldToHips_NoSecondaryCurves() { yield return new EnterPlayMode(); yield return CheckWorld("secondary", BakeSpace.Global); yield return new ExitPlayMode(); }
        [UnityTest] public IEnumerator WorldToHips_GenericAnimatorReplay() { yield return new EnterPlayMode(); yield return CheckWorld("native", BakeSpace.Global); yield return new ExitPlayMode(); }
        [UnityTest] public IEnumerator WorldToHips_DriverCullingEvaluation() { yield return new EnterPlayMode(); yield return CheckDriverEvaluation(BakeSpace.Global, "success"); yield return new ExitPlayMode(); }
        [UnityTest] public IEnumerator WorldToHips_TimelineReplay() { yield return new EnterPlayMode(); yield return CheckWorld("timeline", BakeSpace.Global); yield return new ExitPlayMode(); }
        [UnityTest] public IEnumerator WorldToRoot_TimelineReplay_RemoveStartOffsetOff() { yield return new EnterPlayMode(); yield return CheckWorld("timeline"); yield return new ExitPlayMode(); }
        [UnityTest] public IEnumerator Local_TimelineReplay_MotionRootOff() { yield return new EnterPlayMode(); yield return CheckSpace(BakeSpace.Local, SpaceCheck.TimelineLocal); yield return new ExitPlayMode(); }
        [UnityTest] public IEnumerator Local_TimelineReplay_MotionRootOn() { yield return new EnterPlayMode(); yield return CheckSpace(BakeSpace.Local, SpaceCheck.TimelineRoot); yield return new ExitPlayMode(); }

        [TestCase("nonuniform")]
        [TestCase("negative")]
        [TestCase("mirror")]
        [TestCase("shear")]
        public void WorldToHips_RejectsUnsafeScale(string kind)
        {
            var setup = CutTimeline();
            var parent = Own(new GameObject("UnsafeOutputParent")).transform;
            setup.Target.transform.SetParent(parent, false);
            parent.localScale = kind == "negative" ? -Vector3.one : kind == "mirror" ? new Vector3(-1, 1, 1) : new Vector3(2, 1, 1);
            if (kind == "shear") setup.Target.transform.localRotation = Quaternion.Euler(0, 0, 35);
            var request = BakeRequest(setup); request.bakeSpace = BakeSpace.Global;
            Assert.Throws<ArgumentException>(() => HumanoidGenericBaker.Validate(request));
        }

        [UnityTest] public IEnumerator WorldToHips_RejectsAnimatedScale() { yield return new EnterPlayMode(); yield return CheckWorld("animated-scale", BakeSpace.Global); yield return new ExitPlayMode(); }
        [UnityTest] public IEnumerator WorldToHips_RejectsMappedTranslation() { yield return new EnterPlayMode(); yield return CheckWorld("bone-position", BakeSpace.Global); yield return new ExitPlayMode(); }
        [UnityTest] public IEnumerator WorldToHips_RejectsHelperTranslation() { yield return new EnterPlayMode(); yield return CheckWorld("helper-position", BakeSpace.Global); yield return new ExitPlayMode(); }
        [UnityTest] public IEnumerator WorldToHips_RejectsHelperRotation() { yield return new EnterPlayMode(); yield return CheckWorld("helper-rotation", BakeSpace.Global); yield return new ExitPlayMode(); }

        [UnityTest] public IEnumerator Subframe_Local24() { yield return new EnterPlayMode(); yield return CheckSubframe(BakeSpace.Local, 24); yield return new ExitPlayMode(); }
        [UnityTest] public IEnumerator Subframe_WorldRoot24() { yield return new EnterPlayMode(); yield return CheckSubframe(BakeSpace.World, 24); yield return new ExitPlayMode(); }
        [UnityTest] public IEnumerator Subframe_WorldHips24() { yield return new EnterPlayMode(); yield return CheckSubframe(BakeSpace.Global, 24); yield return new ExitPlayMode(); }
        [UnityTest] public IEnumerator Subframe_Local30RootOff() { yield return new EnterPlayMode(); yield return CheckSubframe(BakeSpace.Local, 30); yield return new ExitPlayMode(); }
        [UnityTest] public IEnumerator Subframe_WorldHips60WithGap() { yield return new EnterPlayMode(); yield return CheckSubframe(BakeSpace.Global, 60); yield return new ExitPlayMode(); }
        [UnityTest] public IEnumerator Subframe_BoundaryRoundoffUsesIncomingSource() { yield return new EnterPlayMode(); yield return CheckSubframe(BakeSpace.Local, 24, true); yield return new ExitPlayMode(); }
        [UnityTest] public IEnumerator Subframe_MicroGapLocal() { yield return new EnterPlayMode(); yield return CheckSubframe(BakeSpace.Local, 24, microGap: true); yield return new ExitPlayMode(); }
        [UnityTest] public IEnumerator Subframe_MicroGapWorldRoot() { yield return new EnterPlayMode(); yield return CheckSubframe(BakeSpace.World, 24, microGap: true); yield return new ExitPlayMode(); }
        [UnityTest] public IEnumerator Subframe_MicroGapWorldHips() { yield return new EnterPlayMode(); yield return CheckSubframe(BakeSpace.Global, 24, microGap: true); yield return new ExitPlayMode(); }

        [TestCase(false)]
        [TestCase(true)]
        public void Subframe_ReportedMicroGapRequiresOptIn(bool enabled)
        {
            var setup = CutTimeline();
            ((TimelineAsset)setup.Director.playableAsset).editorSettings.frameRate = 24;
            setup.Clips[0].start = 9.916666666666657;
            setup.Clips[0].duration = 63.79166666666667;
            setup.Clips[1].start = 73.70833587646484;
            setup.Clips[1].duration = 10.791664123535156;
            setup.Clips[2].start = 84.5;
            setup.Clips[2].duration = 1;
            var request = BakeRequest(setup);
            Assert.That(request.track.closeUnrepresentableMicroGaps, Is.False);
            request.track.closeUnrepresentableMicroGaps = enabled;
            if (enabled) Assert.DoesNotThrow(() => HumanoidGenericBaker.Validate(request));
            else Assert.Throws<ArgumentException>(() => HumanoidGenericBaker.Validate(request));
            Assert.That(setup.Clips[0].end, Is.EqualTo(73.70833333333333));
            Assert.That(setup.Clips[1].start, Is.EqualTo(73.70833587646484));
        }

        [Test]
        public void Subframe_MicroGapOptInStillRejectsLargerGapCollision()
        {
            var setup = CutTimeline();
            setup.Clips[0].duration = 1000;
            setup.Clips[1].start = 1000.00002;
            setup.Clips[1].duration = 1;
            setup.Clips[2].start = setup.Clips[1].end;
            setup.Clips[2].duration = 1;
            var request = BakeRequest(setup);
            request.track.closeUnrepresentableMicroGaps = true;
            Assert.That((float)setup.Clips[0].end, Is.EqualTo((float)setup.Clips[1].start));
            Assert.Throws<ArgumentException>(() => HumanoidGenericBaker.Validate(request));
        }

        [Test]
        public void Subframe_AcceptsReportedBoundaryRoundoff()
        {
            var setup = CutTimeline();
            ((TimelineAsset)setup.Director.playableAsset).editorSettings.frameRate = 24;
            setup.Clips[0].duration = 84.5;
            setup.Clips[1].start = 84.5;
            setup.Clips[1].duration = 91.33333333333334;
            setup.Clips[2].start = 175.83333333333303;
            setup.Clips[2].duration = 19.79167175293;
            Assert.That(setup.Clips[1].end, Is.GreaterThan(setup.Clips[2].start));
            Assert.DoesNotThrow(() => HumanoidGenericBaker.Validate(BakeRequest(setup)));
        }

        [Test]
        public void Subframe_AcceptsVeryShortPositiveDuration()
        {
            var setup = CutTimeline();
            // The public Timeline setter clamps to 1/60 second. Exercise serialized timing.
            typeof(TimelineClip).GetField("m_Duration", System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic).SetValue(setup.Clips[0], 1e-8);
            Assert.That(setup.Clips[0].duration, Is.EqualTo(1e-8));
            Assert.DoesNotThrow(() => HumanoidGenericBaker.Validate(BakeRequest(setup)));
        }

        [Test]
        public void Subframe_RejectsUnrepresentableDistinctBoundariesWithContext()
        {
            var setup = CutTimeline();
            setup.Clips[0].duration = 1 - 1e-10;
            var error = Assert.Throws<ArgumentException>(() => HumanoidGenericBaker.Validate(BakeRequest(setup)));
            Assert.That(error.Message, Does.Contain("float key precision").And.Contain("start=").And.Contain("FPS=60"));
        }

        private IEnumerator CheckSubframe(BakeSpace mode, int fps, bool boundaryRoundoff = false, bool microGap = false)
        {
            var nested = NestedBakeTimeline();
            var setup = nested.Relay;
            var timeline = (TimelineAsset)setup.Director.playableAsset;
            timeline.editorSettings.frameRate = fps;
            var request = BakeRequest(setup);
            request.bakeSpace = mode;
            request.evaluationDirector = nested.Master;
            request.track.relayMotionRoot = fps != 30;
            double first = fps == 24 ? 0 : .25 / fps;
            double cut = 138.5 / fps;
            double second = 239.75 / fps;
            double last = 374.25 / fps;
            setup.Clips[0].start = first; setup.Clips[0].duration = cut - first;
            setup.Clips[1].start = fps == 60 ? 138.75 / fps : setup.Clips[0].end;
            if (boundaryRoundoff) setup.Clips[1].start -= 3e-13;
            setup.Clips[1].duration = second - setup.Clips[1].start;
            setup.Clips[2].start = setup.Clips[1].end; setup.Clips[2].duration = last - setup.Clips[2].start;
            if (microGap)
            {
                setup.Clips[0].duration -= 1e-8;
                request.track.closeUnrepresentableMicroGaps = true;
            }
            foreach (var control in ((TimelineAsset)nested.Master.playableAsset).GetOutputTracks().OfType<ControlTrack>().SelectMany(t => t.GetClips()))
                control.duration = 18;
            if (microGap)
            {
                double middle = (setup.Clips[0].end + setup.Clips[1].start) * .5;
                nested.Master.time = middle;
                nested.Master.Evaluate();
                Assert.That(SelectedBakeSource(setup.Director), Is.EqualTo(setup.A));
                nested.Master.time = setup.Clips[1].start;
                nested.Master.Evaluate();
                Assert.That(SelectedBakeSource(setup.Director), Is.EqualTo(setup.B));
                nested.Master.time = middle;
                nested.Master.Evaluate();
                Assert.That(SelectedBakeSource(setup.Director), Is.EqualTo(setup.A));
            }

            var parent = Own(new GameObject("SubframeOriginalParent")).transform;
            parent.SetPositionAndRotation(new Vector3(1, 2, -3), Quaternion.Euler(0, 19, 0));
            setup.Target.transform.SetParent(parent, false);
            var input = new Dictionary<Animator, AnimationCurve>();
            var sources = new[] { setup.A, setup.B };
            for (int i = 0; i < sources.Length; i++)
            {
                var animation = (AnimationPlayableAsset)setup.AnimationTracks[i].GetClips().Single().asset;
                animation.applyFootIK = false;
                var curve = AnimationCurve.Linear(0, i == 0 ? -.8f : .8f, 18, i == 0 ? -.15f : .15f);
                AnimationUtility.SetEditorCurve(animation.clip, EditorCurveBinding.FloatCurve("", typeof(Animator), "Left Arm Down-Up"), curve);
                setup.AnimationTracks[i].GetClips().Single().duration = 18;
                input.Add(sources[i], curve);
                AddHiddenBakeRenderer(sources[i]);
                sources[i].cullingMode = AnimatorCullingMode.CullUpdateTransforms;
            }
            var transforms = setup.Target.GetComponentsInChildren<Transform>(true);
            var frames = new List<WorldFrame>();
            var times = new List<float>();
            int arm = Array.IndexOf(HumanTrait.MuscleName, "Left Arm Down-Up");
            GameObject replay = null;
            request.progress = _ =>
            {
                double time = nested.Master.time;
                var active = setup.Clips.LastOrDefault(c => c.start <= time && time < c.end);
                if (microGap && time >= setup.Clips[0].end && time < setup.Clips[1].start) active = setup.Clips[0];
                var source = active == null ? null : ((MotionRelayClip)active.asset).source.Resolve(setup.Director);
                Assert.That(SelectedBakeSource(setup.Director), Is.EqualTo(source), $"Ownership at {time:R}");
                if (source != null)
                {
                    float expected = input[source].Evaluate((float)time);
                    Assert.That(ReadPose(source).muscles[arm], Is.EqualTo(expected).Within(.04f));
                    Assert.That(ReadPose(setup.Target).muscles[arm], Is.EqualTo(expected).Within(.05f));
                }
                Assert.That(sources.All(a => a.cullingMode == AnimatorCullingMode.AlwaysAnimate), Is.True);
                times.Add((float)(time - first));
                frames.Add(new WorldFrame(transforms));
                if (replay == null)
                {
                    replay = Own(UnityEngine.Object.Instantiate(setup.Target.gameObject,
                        mode == BakeSpace.Local ? parent : null, false));
                    var animator = replay.GetComponent<Animator>();
                    animator.avatar = null;
                    animator.enabled = false;
                    if (mode != BakeSpace.Local) replay.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                }
            };

            try
            {
                AnimationClip baked = null;
                yield return HumanoidGenericBaker.Bake(request, value => baked = value);
                Assert.That(sources.All(a => a.cullingMode == AnimatorCullingMode.CullUpdateTransforms), Is.True);
                var bindings = AnimationUtility.GetCurveBindings(baked);
                Assert.That(bindings.Any(b => b.propertyName.Contains("Scale")), Is.False);
                Assert.That(bindings.Count(b => b.path == ""), Is.EqualTo(mode == BakeSpace.World || mode == BakeSpace.Local && fps != 30 ? 7 : 0));
                var curve = AnimationUtility.GetEditorCurve(baked, bindings.First());
                Assert.That(curve.keys.Select(k => k.time), Is.EqualTo(times));
                for (int frame = (int)Math.Ceiling(first * fps); frame <= Math.Floor(last * fps); frame++)
                    Assert.That(times, Does.Contain((float)(frame / (double)fps - first)));
                var replayTimes = new List<float>(times);
                var replayFrames = new List<WorldFrame>(frames);
                var boundaries = setup.Clips.SelectMany(c => new[] { c.start, c.end }).Distinct().Skip(1);
                if (boundaryRoundoff || microGap)
                    boundaries = boundaries.Where(t => t != setup.Clips[0].end);
                foreach (double boundary in boundaries)
                {
                    float time = (float)(boundary - first);
                    int index = times.IndexOf(time);
                    Assert.That(index, Is.GreaterThan(0));
                    Assert.That(time - times[index - 1], Is.LessThan(.000005f));
                    Assert.That(AnimationUtility.GetKeyRightTangentMode(curve, index - 1), Is.EqualTo(AnimationUtility.TangentMode.Constant));
                    float after = BitConverter.Int32BitsToSingle(BitConverter.SingleToInt32Bits(time) + 4);
                    if (index + 1 < times.Count && after < times[index + 1])
                    {
                        replayTimes.Add(after);
                        replayFrames.Add(frames[index]);
                    }
                }
                var animator = replay.GetComponent<Animator>();
                yield return AssertTimelineReplay(animator, baked, replayFrames, mode == BakeSpace.Global,
                    sampleTime: i => replayTimes[i]);
                // Reverse seeks exercise the same boundary pairs with no dependence on playback history.
                var replayTransforms = replay.GetComponentsInChildren<Transform>(true);
                animator.enabled = false;
                for (int i = times.Count - 1; i >= 0; i--)
                {
                    baked.SampleAnimation(replay, times[i]);
                    AssertWorldFrames(replayTransforms, frames[i], mode == BakeSpace.Global);
                }
                var controller = AnimatorController.CreateAnimatorControllerAtPath(bakeFolder + "/SubframeReplay.controller");
                var state = controller.layers[0].stateMachine.AddState("Subframe");
                state.motion = baked;
                animator.runtimeAnimatorController = controller;
                animator.enabled = true;
                animator.speed = 0;
                animator.Rebind();
                // Normalized Animator time itself rounds. Check regular samples away from the step.
                for (int i = 0; i < times.Count; i++)
                {
                    double frame = (times[i] + first) * fps;
                    if (Math.Abs(frame - Math.Round(frame)) > .00001) continue;
                    animator.Play("Subframe", 0, times[i] / baked.length);
                    animator.Update(0);
                    yield return null;
                    AssertWorldFrames(replayTransforms, frames[i], mode == BakeSpace.Global);
                }
            }
            finally { TearDown(); CleanBakeAssets(); }
        }

        private IEnumerator AssertTimelineReplay(Animator animator, AnimationClip clip, List<WorldFrame> frames, bool hipsOutput,
            Action<int> beforeEvaluate = null, Func<int, double> sampleTime = null)
        {
            animator.avatar = null; animator.runtimeAnimatorController = null;
            animator.enabled = true; animator.cullingMode = AnimatorCullingMode.AlwaysAnimate; animator.applyRootMotion = false;
            animator.Rebind();
            var director = Own(new GameObject("IndependentGenericTimelineReplay")).AddComponent<PlayableDirector>();
            director.playOnAwake = false; director.timeUpdateMode = DirectorUpdateMode.Manual;
            var timeline = Own(ScriptableObject.CreateInstance<TimelineAsset>());
            director.playableAsset = timeline;
            var track = Own(timeline.CreateTrack<AnimationTrack>());
            track.trackOffset = TrackOffset.ApplyTransformOffsets;
            var timelineClip = track.CreateClip(clip); Own(timelineClip.asset);
            timelineClip.duration = clip.length + 1d / clip.frameRate;
            var asset = (AnimationPlayableAsset)timelineClip.asset;
            asset.removeStartOffset = false; asset.applyFootIK = false;
            director.SetGenericBinding(track, animator);
            var transforms = animator.GetComponentsInChildren<Transform>(true);
            for (int f = 0; f < frames.Count; f++)
            {
                beforeEvaluate?.Invoke(f);
                director.time = sampleTime != null ? sampleTime(f) : f / (double)clip.frameRate;
                director.Evaluate();
                yield return null;
                Assert.That(animator.isHuman, Is.False);
                try { AssertWorldFrames(transforms, frames[f], hipsOutput); }
                catch (AssertionException error) when (sampleTime != null)
                {
                    throw new AssertionException($"Timeline sample {f}, time {director.time:R}: {error.Message}");
                }
                if (hipsOutput) AssertWorld(animator.transform, Vector3.zero, Quaternion.identity);
            }
            director.Stop();
        }
    }
}
