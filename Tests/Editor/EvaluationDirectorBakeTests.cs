using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SillBill.PandaMotionRelay.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.TestTools;
using UnityEngine.Timeline;

namespace SillBill.PandaMotionRelay.Tests
{
    public sealed partial class HumanoidRelayTests
    {
        private sealed class NestedBakeSetup
        {
            public CutSetup Relay;
            public PlayableDirector Master, Prop;
            public TimelineClip Control;
            public Transform Anchor;
        }

        private TimelineClip ControlChild(PlayableDirector master, PlayableDirector child)
        {
            var track = Own(((TimelineAsset)master.playableAsset).CreateTrack<ControlTrack>());
            var clip = track.CreateDefaultClip();
            var asset = Own((ControlPlayableAsset)clip.asset);
            asset.sourceGameObject.exposedName = Guid.NewGuid().ToString();
            master.SetReferenceValue(asset.sourceGameObject.exposedName, child.gameObject);
            asset.updateDirector = true;
            asset.active = false; asset.updateParticle = false; asset.updateITimeControl = false;
            clip.duration = 4;
            return clip;
        }

        private NestedBakeSetup NestedBakeTimeline()
        {
            var n = new NestedBakeSetup { Relay = FingerBakeTimeline(true) };
            ((TimelineAsset)n.Relay.Director.playableAsset).editorSettings.frameRate = 24;
            n.Master = Own(new GameObject("BakeMaster")).AddComponent<PlayableDirector>();
            n.Master.playOnAwake = false; n.Master.timeUpdateMode = DirectorUpdateMode.Manual;
            n.Master.playableAsset = Own(ScriptableObject.CreateInstance<TimelineAsset>());
            n.Prop = Own(new GameObject("BakeProp")).AddComponent<PlayableDirector>();
            n.Prop.playOnAwake = false; n.Prop.timeUpdateMode = DirectorUpdateMode.Manual;
            n.Prop.playableAsset = Own(ScriptableObject.CreateInstance<TimelineAsset>());
            n.Anchor = Own(new GameObject("PropAnchor")).transform;
            var animator = n.Anchor.gameObject.AddComponent<Animator>();
            var motion = Own(new AnimationClip());
            SetSpaceCurve(motion, "m_LocalPosition.x", 5, 11);
            SetSpaceCurve(motion, "m_LocalPosition.y", 2, 2);
            SetSpaceCurve(motion, "m_LocalPosition.z", -1, 3);
            var track = Own(((TimelineAsset)n.Prop.playableAsset).CreateTrack<AnimationTrack>());
            track.trackOffset = TrackOffset.ApplyTransformOffsets;
            var clip = track.CreateClip(motion); Own(clip.asset); clip.duration = 4;
            n.Prop.SetGenericBinding(track, animator);
            AddRootConstraint(n.Relay.B, n.Anchor);
            n.Control = ControlChild(n.Master, n.Relay.Director);
            ControlChild(n.Master, n.Prop);
            return n;
        }

        [UnityTest]
        public IEnumerator NestedPreview_FrameZeroReproduction()
        {
            yield return new EnterPlayMode();
            var n = NestedBakeTimeline();
            // Reproduce the production preview's first paused nested evaluation.
            n.Master.Play(); n.Master.time = 0; n.Master.Evaluate();
            yield return null;
            n.Master.Pause(); n.Master.time = 0; n.Master.Evaluate();
            yield return null;
            MotionRelayEvaluation.ApplyPendingPoses();
            AssertRootAndPose(n.Relay, false);
            TearDown(); CleanBakeAssets();
            yield return new ExitPlayMode();
        }

        [Test]
        public void Bake_EvaluationDirector_DefaultsToRelayDirector()
        {
            var request = BakeRequest(CutTimeline());
            Assert.That(request.evaluationDirector, Is.Null);
            Assert.DoesNotThrow(() => HumanoidGenericBaker.Validate(request));
        }

        [TestCase("missing")]
        [TestCase("ambiguous")]
        [TestCase("zero-speed")]
        [TestCase("negative-speed")]
        [TestCase("nan-speed")]
        [TestCase("inactive")]
        [TestCase("no-timeline")]
        [TestCase("range-start")]
        [TestCase("range-end")]
        [TestCase("hierarchy")]
        [TestCase("prefab")]
        [TestCase("disabled-control")]
        [TestCase("muted")]
        [TestCase("loop")]
        public void Bake_NestedMaster_RejectsUnsafeMapping(string kind)
        {
            var n = NestedBakeTimeline();
            var request = BakeRequest(n.Relay); request.evaluationDirector = n.Master;
            var control = (ControlPlayableAsset)n.Control.asset;
            if (kind == "missing") n.Master.SetReferenceValue(control.sourceGameObject.exposedName, null);
            if (kind == "ambiguous") ControlChild(n.Master, n.Relay.Director);
            if (kind.EndsWith("speed")) typeof(TimelineClip).GetField("m_TimeScale", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .SetValue(n.Control, kind == "zero-speed" ? 0d : kind == "negative-speed" ? -1d : double.NaN);
            if (kind == "inactive") n.Master.enabled = false;
            if (kind == "no-timeline") n.Master.playableAsset = null;
            if (kind == "range-start") n.Control.clipIn = .5;
            if (kind == "range-end") n.Control.duration = 2;
            if (kind == "hierarchy")
            {
                control.searchHierarchy = true;
                n.Prop.transform.SetParent(n.Relay.Director.transform);
            }
            if (kind == "prefab") control.prefabGameObject = n.Relay.A.gameObject;
            if (kind == "disabled-control") control.updateDirector = false;
            if (kind == "muted") n.Control.GetParentTrack().muted = true;
            if (kind == "loop")
            {
                ((TimelineAsset)n.Relay.Director.playableAsset).durationMode = TimelineAsset.DurationMode.FixedLength;
                ((TimelineAsset)n.Relay.Director.playableAsset).fixedDuration = 3;
                n.Relay.Director.extrapolationMode = DirectorWrapMode.Loop;
            }
            Assert.Throws<ArgumentException>(() => HumanoidGenericBaker.Validate(request));
        }

        [UnityTest] public IEnumerator Bake_SingleDirector_RemainsBackwardCompatible() { yield return new EnterPlayMode(); yield return CheckNested("single"); yield return new ExitPlayMode(); }
        [UnityTest] public IEnumerator Bake_NestedMaster_MapsRelayTimeToEvaluationTime() { yield return new EnterPlayMode(); yield return CheckNested("mapping"); yield return new ExitPlayMode(); }
        [UnityTest] public IEnumerator Bake_NestedMaster_ControlClipStartOffset() { yield return new EnterPlayMode(); yield return CheckNested("offset"); yield return new ExitPlayMode(); }
        [UnityTest] public IEnumerator Bake_NestedMaster_ControlClipClipIn() { yield return new EnterPlayMode(); yield return CheckNested("clipin"); yield return new ExitPlayMode(); }
        [UnityTest] public IEnumerator Bake_NestedMaster_ControlClipTimeScale() { yield return new EnterPlayMode(); yield return CheckNested("speed"); yield return new ExitPlayMode(); }
        [UnityTest] public IEnumerator Bake_NestedMaster_PropConstraintMatchesMasterPreview() { yield return new EnterPlayMode(); yield return CheckNested("constraint"); yield return new ExitPlayMode(); }
        [UnityTest] public IEnumerator Bake_NestedMaster_ExactCutMatchesPreview() { yield return new EnterPlayMode(); yield return CheckNested("cuts"); yield return new ExitPlayMode(); }
        [UnityTest] public IEnumerator Bake_NestedMaster_FrameZeroHasExpectedSource() { yield return new EnterPlayMode(); yield return CheckNested("zero"); yield return new ExitPlayMode(); }
        [UnityTest] public IEnumerator Bake_NestedMaster_RestoresAllThreeDirectors() { yield return new EnterPlayMode(); yield return CheckNested("restore"); yield return new ExitPlayMode(); }
        [UnityTest] public IEnumerator Bake_NestedMaster_RestoresPlayingDirectorStates() { yield return new EnterPlayMode(); yield return CheckNested("playing"); yield return new ExitPlayMode(); }
        [UnityTest] public IEnumerator Bake_NestedMaster_CancelRestoresAllThreeDirectors() { yield return new EnterPlayMode(); yield return CheckNested("cancel"); yield return new ExitPlayMode(); }
        [UnityTest] public IEnumerator Bake_NestedMaster_ExceptionRestoresAllThreeDirectors() { yield return new EnterPlayMode(); yield return CheckNested("exception"); yield return new ExitPlayMode(); }
        [UnityTest] public IEnumerator Bake_NestedMaster_RestoresInitiallyInvalidGraphs() { yield return new EnterPlayMode(); yield return CheckNested("invalid-graphs"); yield return new ExitPlayMode(); }
        [UnityTest] public IEnumerator Bake_NestedMaster_LocalBakeMatchesPreview() { yield return new EnterPlayMode(); yield return CheckNested("local"); yield return new ExitPlayMode(); }
        [UnityTest] public IEnumerator Bake_NestedMaster_GlobalBakeMatchesPreview() { yield return new EnterPlayMode(); yield return CheckNested("global"); yield return new ExitPlayMode(); }

        private IEnumerator CheckNested(string check)
        {
            var n = NestedBakeTimeline();
            var request = BakeRequest(n.Relay);
            request.evaluationDirector = check == "single" ? n.Relay.Director : n.Master;
            if (check == "offset" || check == "mapping") n.Control.start = 2;
            if (check == "clipin" || check == "mapping")
            {
                n.Control.clipIn = .5;
                n.Relay.Clips[0].start = .5; n.Relay.Clips[0].duration = .5;
            }
            if (check == "speed" || check == "mapping") n.Control.timeScale = 2;
            if (check == "global") request.bakeSpace = BakeSpace.Global;
            var initialA = Snapshot(n.Relay.A); var initialB = Snapshot(n.Relay.B); var initialTarget = Snapshot(n.Relay.Target);
            n.Relay.Director.time = 1.5; n.Relay.Director.Evaluate();
            yield return null; MotionRelayEvaluation.ApplyPendingPoses();
            var childOnlyPosition = n.Relay.Target.transform.position;
            n.Relay.Director.Stop();
            RestoreSnapshot(n.Relay.A, initialA); RestoreSnapshot(n.Relay.B, initialB); RestoreSnapshot(n.Relay.Target, initialTarget);
            double first = n.Relay.Clips[0].start;
            var evaluation = request.evaluationDirector;
            Func<double, double> map = t => check == "single" ? t : n.Control.start + (t - n.Control.clipIn) / n.Control.timeScale;
            var reference = new List<PoseFrame>(); var spaces = new List<SpaceFrame>();
            evaluation.time = map(first); evaluation.Evaluate();
            yield return null; MotionRelayEvaluation.ApplyPendingPoses();
            for (int frame = (int)Math.Round(first * 24); frame <= 72; frame++)
            {
                evaluation.time = map(frame / 24d); evaluation.Evaluate();
                yield return null; MotionRelayEvaluation.ApplyPendingPoses();
                reference.Add(new PoseFrame(n.Relay.Target)); spaces.Add(new SpaceFrame(n.Relay.Target));
            }
            if (check == "constraint") Assert.That(Vector3.Distance(childOnlyPosition, reference[36].position[0]), Is.GreaterThan(1));
            if (check == "zero")
            {
                n.Master.Stop(); n.Relay.Director.Stop(); n.Prop.Stop();
                ((ControlPlayableAsset)n.Control.asset).active = true;
                n.Relay.Target.transform.position = Vector3.one * 100;
                Pose(n.Relay.Target, .1f);
            }
            else if (check == "invalid-graphs") n.Master.Stop();
            else
            {
                if (check != "single") { n.Master.time = .35; n.Master.Evaluate(); }
                n.Master.Pause(); n.Relay.Director.Pause(); n.Prop.Pause();
            }
            if (check == "playing")
            {
                n.Master.Play(); n.Relay.Director.Play(); n.Prop.Play();
            }
            n.Master.time = .35; n.Relay.Director.time = .42; n.Prop.time = .27;
            n.Master.timeUpdateMode = DirectorUpdateMode.GameTime;
            n.Relay.Director.timeUpdateMode = DirectorUpdateMode.UnscaledGameTime;
            n.Prop.timeUpdateMode = DirectorUpdateMode.Manual;
            var directors = new[] { n.Master, n.Relay.Director, n.Prop };
            var times = directors.Select(d => d.time).ToArray();
            var modes = directors.Select(d => d.timeUpdateMode).ToArray();
            var states = directors.Select(d => d.state).ToArray();
            var graphs = directors.Select(d => d.playableGraph.IsValid()).ToArray();
            var before = Snapshot(n.Relay.Target);
            var samples = new List<PoseFrame>();
            request.progress = _ =>
            {
                int frame = samples.Count + (int)Math.Round(first * 24);
                Assert.That(evaluation.time, Is.EqualTo(map(frame / 24d)).Within(1e-7));
                Assert.That(n.Relay.Director.time, Is.EqualTo(frame / 24d).Within(1e-6));
                samples.Add(new PoseFrame(n.Relay.Target));
                if (check == "zero" && frame == 0)
                {
                    AssertRootAndPose(n.Relay, false);
                    Assert.That(SelectedBakeSource(n.Relay.Director), Is.EqualTo(n.Relay.A), "The first sample must have an actual selected Source, not just a held pose");
                }
                if (check == "cuts" && frame < 72) AssertRootAndPose(n.Relay, frame >= 24 && frame < 48);
                if (check == "exception" && samples.Count == 3) throw new InvalidOperationException("Injected sampling failure");
            };
            if (check == "cancel") request.cancel = () => samples.Count == 3;
            AnimationClip result = null;
            var operation = HumanoidGenericBaker.Bake(request, c => result = c);
            Exception failure = null;
            while (true)
            {
                bool more = false;
                try { more = operation.MoveNext(); }
                catch (Exception error) { failure = error; }
                if (!more || failure != null) break;
                yield return operation.Current;
            }
            (operation as IDisposable)?.Dispose();
            try
            {
                if (check == "cancel") Assert.That(failure, Is.TypeOf<OperationCanceledException>());
                else if (check == "exception") Assert.That(failure, Is.TypeOf<InvalidOperationException>());
                else Assert.That(failure, Is.Null, failure?.ToString());
                if (check != "single")
                    for (int i = 0; i < directors.Length; i++)
                    {
                        Assert.That(directors[i].time, Is.EqualTo(times[i]), directors[i].name + " time");
                        Assert.That(directors[i].timeUpdateMode, Is.EqualTo(modes[i]));
                        Assert.That(directors[i].state, Is.EqualTo(states[i]));
                        Assert.That(directors[i].playableGraph.IsValid(), Is.EqualTo(graphs[i]), directors[i].name + " graph validity");
                    }
                AssertSnapshot(n.Relay.Target, before);
                if (failure != null) { Assert.That(result, Is.Null); yield break; }
                Assert.That(samples.Count, Is.EqualTo(reference.Count));
                for (int f = 0; f < samples.Count; f++)
                    for (int b = 0; b < samples[f].position.Length; b++)
                    {
                        Assert.That(Vector3.Distance(samples[f].position[b], reference[f].position[b]), Is.LessThan(.001f), "Sampling position frame " + f);
                        Assert.That(Quaternion.Angle(samples[f].rotation[b], reference[f].rotation[b]), Is.LessThan(.1f), "Sampling rotation frame " + f);
                    }
                if (check == "local" || check == "single") AssertRoundTrip(n.Relay, result, reference, "nested-" + check);
                if (check == "global")
                {
                    Assert.That(AnimationUtility.GetCurveBindings(result).Any(b => b.path == "" || b.propertyName.Contains("Scale")), Is.False);
                    var replay = Own(UnityEngine.Object.Instantiate(n.Relay.Target.gameObject));
                    var animator = replay.GetComponent<Animator>(); animator.avatar = null; animator.Rebind(); animator.enabled = false;
                    replay.transform.SetPositionAndRotation(spaces[0].rootPosition, spaces[0].rootRotation);
                    replay.transform.localScale = spaces[0].rootScale;
                    var hipsPath = AnimationUtility.CalculateTransformPath(n.Relay.Target.GetBoneTransform(HumanBodyBones.Hips), n.Relay.Target.transform);
                    for (int f = 0; f < spaces.Count; f++)
                    {
                        result.SampleAnimation(replay, f / 24f);
                        AssertWorld(replay.transform.Find(hipsPath), spaces[f].worldPosition, spaces[f].worldRotation);
                    }
                }
            }
            finally { TearDown(); CleanBakeAssets(); }
        }

        private static Animator SelectedBakeSource(PlayableDirector director)
        {
            var seen = new HashSet<Playable>();
            var graph = director.playableGraph;
            for (int i = 0; i < graph.GetOutputCount(); i++)
            {
                var source = SelectedBakeSource(graph.GetOutput(i).GetSourcePlayable(), seen);
                if (source != null) return source;
            }
            return null;
        }

        private static Animator SelectedBakeSource(Playable playable, HashSet<Playable> seen)
        {
            if (!playable.IsValid() || !seen.Add(playable)) return null;
            if (playable.GetPlayableType() == typeof(MotionRelayMixer))
                return typeof(MotionRelayMixer).GetField("currentSource", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    .GetValue(((ScriptPlayable<MotionRelayMixer>)playable).GetBehaviour()) as Animator;
            for (int i = 0; i < playable.GetInputCount(); i++)
            {
                var source = SelectedBakeSource(playable.GetInput(i), seen);
                if (source != null) return source;
            }
            return null;
        }
    }
}
