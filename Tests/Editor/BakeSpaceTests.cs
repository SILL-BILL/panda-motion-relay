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
using Object = UnityEngine.Object;

namespace SillBill.PandaMotionRelay.Tests
{
    public sealed partial class HumanoidRelayTests
    {
        [Test]
        public void BakeSpace_DefaultIsLocal()
        {
            Assert.That(new GenericBakeRequest().bakeSpace, Is.EqualTo(BakeSpace.Local));
        }

        [Test]
        public void BakeSpace_RejectsUnknownMode()
        {
            var request = BakeRequest(CutTimeline());
            request.bakeSpace = (BakeSpace)99;
            Assert.Throws<ArgumentException>(() => HumanoidGenericBaker.Validate(request));
        }

        [Test]
        public void GlobalBake_OverlapIsStillRejected()
        {
            var setup = CutTimeline();
            setup.Clips[0].duration = 1.5;
            var request = BakeRequest(setup);
            request.bakeSpace = BakeSpace.Global;
            Assert.Throws<ArgumentException>(() => HumanoidGenericBaker.Validate(request));
        }

        [UnityTest]
        public IEnumerator LocalBake_MatchesExistingPhase3Behavior()
        {
            yield return new EnterPlayMode();
            yield return CheckSpace(BakeSpace.Local, SpaceCheck.Local);
            yield return new ExitPlayMode();
        }
        [UnityTest]
        public IEnumerator LocalBake_WithMotionRoot_PreservesSeparateRootAndHipsCurves()
        {
            yield return new EnterPlayMode();
            yield return CheckSpace(BakeSpace.Local, SpaceCheck.Root);
            yield return new ExitPlayMode();
        }
        [UnityTest]
        public IEnumerator GlobalBake_DoesNotCreateAnimatorRootCurves()
        {
            yield return new EnterPlayMode();
            yield return CheckSpace(BakeSpace.Global, SpaceCheck.Root);
            yield return new ExitPlayMode();
        }
        [UnityTest]
        public IEnumerator GlobalBake_HipsContainsPositionAndQuaternionRotation()
        {
            yield return new EnterPlayMode();
            yield return CheckSpace(BakeSpace.Global, SpaceCheck.Hips);
            yield return new ExitPlayMode();
        }
        [UnityTest]
        public IEnumerator GlobalBake_OtherBonesRemainLocalRotation()
        {
            yield return new EnterPlayMode();
            yield return CheckSpace(BakeSpace.Global, SpaceCheck.OtherBones);
            yield return new ExitPlayMode();
        }
        [UnityTest]
        public IEnumerator GlobalBake_UpperParentTranslationIsIntegratedIntoHips()
        {
            yield return new EnterPlayMode();
            yield return CheckSpace(BakeSpace.Global, SpaceCheck.Translation);
            yield return new ExitPlayMode();
        }
        [UnityTest]
        public IEnumerator GlobalBake_UpperParentRotationIsIntegratedIntoHips()
        {
            yield return new EnterPlayMode();
            yield return CheckSpace(BakeSpace.Global, SpaceCheck.Rotation);
            yield return new ExitPlayMode();
        }
        [UnityTest]
        public IEnumerator GlobalBake_UsesFrozenReferenceParent()
        {
            yield return new EnterPlayMode();
            yield return CheckSpace(BakeSpace.Global, SpaceCheck.Reference);
            yield return new ExitPlayMode();
        }
        [UnityTest]
        public IEnumerator GlobalBake_DoesNotCollapseBackToLocalWhenParentMoves()
        {
            yield return new EnterPlayMode();
            yield return CheckSpace(BakeSpace.Global, SpaceCheck.CompareLocal);
            yield return new ExitPlayMode();
        }
        [UnityTest]
        public IEnumerator GlobalBake_ReplayWithFrozenParentMatchesOriginalHipsWorldPose()
        {
            yield return new EnterPlayMode();
            yield return CheckSpace(BakeSpace.Global, SpaceCheck.Replay);
            yield return new ExitPlayMode();
        }
        [UnityTest]
        public IEnumerator GlobalBake_RelayMotionRootOn_DoesNotDoubleMotion()
        {
            yield return new EnterPlayMode();
            yield return CheckSpace(BakeSpace.Global, SpaceCheck.RootReplay);
            yield return new ExitPlayMode();
        }
        [UnityTest]
        public IEnumerator GlobalBake_ExactCutMatchesPreview()
        {
            yield return new EnterPlayMode();
            yield return CheckSpace(BakeSpace.Global, SpaceCheck.Cut);
            yield return new ExitPlayMode();
        }
        [UnityTest]
        public IEnumerator GlobalBake_ParentConstraintMotionIsIntegrated()
        {
            yield return new EnterPlayMode();
            yield return CheckSpace(BakeSpace.Global, SpaceCheck.Constraint);
            yield return new ExitPlayMode();
        }
        [UnityTest]
        public IEnumerator GlobalBake_NoScaleCurves()
        {
            yield return new EnterPlayMode();
            yield return CheckSpace(BakeSpace.Global, SpaceCheck.Scale);
            yield return new ExitPlayMode();
        }
        [UnityTest]
        public IEnumerator GlobalBake_NoSecondaryBoneCurves()
        {
            yield return new EnterPlayMode();
            yield return CheckSpace(BakeSpace.Global, SpaceCheck.Secondary);
            yield return new ExitPlayMode();
        }
        [UnityTest]
        public IEnumerator GlobalBake_RestoresSceneState()
        {
            yield return new EnterPlayMode();
            yield return CheckSpace(BakeSpace.Global, SpaceCheck.Restore);
            yield return new ExitPlayMode();
        }
        [UnityTest]
        public IEnumerator GlobalBake_GapSamplesFinalPose()
        {
            yield return new EnterPlayMode();
            yield return CheckSpace(BakeSpace.Global, SpaceCheck.Gap);
            yield return new ExitPlayMode();
        }
        [UnityTest]
        public IEnumerator GlobalBake_CancellationRestoresSceneState()
        {
            yield return new EnterPlayMode();
            yield return CheckSpace(BakeSpace.Global, SpaceCheck.Cancel);
            yield return new ExitPlayMode();
        }

        private enum SpaceCheck
        {
            Local, Root, Hips, OtherBones, Translation, Rotation, Reference, CompareLocal,
            Replay, RootReplay, Cut, Constraint, Scale, Secondary, Restore, Gap, Cancel
        }

        private sealed class SpaceFrame
        {
            public Vector3 worldPosition, localPosition, rootPosition, rootScale, rootLocalPosition;
            public Quaternion worldRotation, localRotation, rootRotation, rootLocalRotation;
            public Matrix4x4 parentMatrix;
            public Quaternion parentRotation;
            public Dictionary<string, Quaternion> otherRotations;

            public SpaceFrame(Animator target)
            {
                var hips = target.GetBoneTransform(HumanBodyBones.Hips);
                worldPosition = hips.position; worldRotation = hips.rotation;
                localPosition = hips.localPosition; localRotation = hips.localRotation;
                parentMatrix = hips.parent.localToWorldMatrix; parentRotation = hips.parent.rotation;
                rootPosition = target.transform.position; rootRotation = target.transform.rotation;
                rootScale = target.transform.lossyScale;
                rootLocalPosition = target.transform.localPosition; rootLocalRotation = target.transform.localRotation;
                otherRotations = new Dictionary<string, Quaternion>();
                for (int bone = 0; bone < (int)HumanBodyBones.LastBone; bone++)
                {
                    var transform = target.GetBoneTransform((HumanBodyBones)bone);
                    if (transform != null && transform != hips)
                        otherRotations[AnimationUtility.CalculateTransformPath(transform, target.transform)] = transform.localRotation;
                }
            }
        }

        private IEnumerator CheckSpace(BakeSpace space, SpaceCheck check)
        {
            bool root = check == SpaceCheck.Root || check == SpaceCheck.RootReplay || check == SpaceCheck.Cut;
            var setup = RootTimeline(root);
            setup.Target.gameObject.SetActive(false);
            setup.Target = Rig("SpaceTarget", 1.3f, true, true);
            var timeline = (TimelineAsset)setup.Director.playableAsset;
            var relayTrack = timeline.GetOutputTracks().OfType<MotionRelayTrack>().Single();
            setup.Director.SetGenericBinding(relayTrack, setup.Target);
            timeline.editorSettings.frameRate = check == SpaceCheck.Cut ? 24 : 10;
            if (check == SpaceCheck.Reference) { setup.Clips[0].start = .5; setup.Clips[0].duration = .5; }
            if (check == SpaceCheck.Gap) { setup.Clips[0].duration = .5; setup.Clips[1].start = 1.5; setup.Clips[1].duration = .5; }

            var animatedParent = Own(new GameObject("AnimatedParent")).transform;
            animatedParent.SetPositionAndRotation(new Vector3(40, 5, -20), Quaternion.Euler(0, -70, 0));
            animatedParent.localScale = Vector3.one * 1.2f;
            var parentAnimator = animatedParent.gameObject.AddComponent<Animator>();
            var parentClip = Own(new AnimationClip());
            bool translate = check != SpaceCheck.Rotation;
            bool rotate = check != SpaceCheck.Translation;
            SetSpaceCurve(parentClip, "m_LocalPosition.x", 2, translate ? 8 : 2);
            SetSpaceCurve(parentClip, "m_LocalPosition.y", 1, 1);
            SetSpaceCurve(parentClip, "m_LocalPosition.z", -3, translate ? 1 : -3);
            var finalRotation = Quaternion.Euler(0, rotate ? 85 : 0, 0);
            SetSpaceCurve(parentClip, "m_LocalRotation.x", 0, finalRotation.x);
            SetSpaceCurve(parentClip, "m_LocalRotation.y", 0, finalRotation.y);
            SetSpaceCurve(parentClip, "m_LocalRotation.z", 0, finalRotation.z);
            SetSpaceCurve(parentClip, "m_LocalRotation.w", 1, finalRotation.w);
            var parentTrack = Own(timeline.CreateTrack<AnimationTrack>(null, "Animated parent"));
            parentTrack.trackOffset = TrackOffset.ApplyTransformOffsets;
            var parentTimelineClip = parentTrack.CreateClip(parentClip);
            Own(parentTimelineClip.asset); parentTimelineClip.duration = 3;
            setup.Director.SetGenericBinding(parentTrack, parentAnimator);

            Transform carrier = animatedParent;
            if (check == SpaceCheck.Constraint)
            {
                carrier = Own(new GameObject("ConstraintCarrier")).transform;
                AddRootConstraint(carrier.gameObject.AddComponent<Animator>(), animatedParent);
            }
            setup.Target.transform.SetParent(carrier, false);
            setup.Target.transform.localPosition = new Vector3(.4f, .2f, -.3f);
            setup.Target.transform.localRotation = Quaternion.Euler(0, 12, 0);
            setup.Target.transform.localScale = Vector3.one * .8f;
            foreach (var driverTrack in setup.AnimationTracks)
            {
                var animation = ((AnimationPlayableAsset)driverTrack.GetClips().Single().asset).clip;
                AnimationUtility.SetEditorCurve(animation, EditorCurveBinding.FloatCurve("", typeof(Animator), "RootT.x"), AnimationCurve.Linear(0, 0, 3, .4f));
                AnimationUtility.SetEditorCurve(animation, EditorCurveBinding.FloatCurve("", typeof(Animator), "RootT.y"), AnimationCurve.Constant(0, 3, 1));
            }
            var hair = new GameObject("SecondaryHair").transform;
            hair.SetParent(setup.Target.GetBoneTransform(HumanBodyBones.Head), false);
            hair.localPosition = new Vector3(.1f, .2f, .3f);
            hair.localRotation = Quaternion.Euler(12, 23, 34);
            var renderer = setup.Target.gameObject.AddComponent<MeshRenderer>();
            renderer.enabled = false;
            var inactive = Own(new GameObject("RestoredActivation"));
            var targetBefore = Snapshot(setup.Target);
            var aBefore = Snapshot(setup.A); var bBefore = Snapshot(setup.B);
            var parentBefore = (animatedParent.position, animatedParent.rotation, animatedParent.localScale);
            setup.Director.time = .2;
            var request = BakeRequest(setup);
            request.bakeSpace = space;
            var frames = new List<SpaceFrame>();
            request.progress = _ =>
            {
                frames.Add(new SpaceFrame(setup.Target));
                if (check == SpaceCheck.Constraint)
                    AssertWorld(carrier, animatedParent.position, animatedParent.rotation);
                if (check == SpaceCheck.Restore) { renderer.enabled = true; inactive.SetActive(false); }
            };
            bakedResult = null;
            try
            {
                if (check == SpaceCheck.Cancel)
                {
                    request.cancel = () => frames.Count == 2;
                    var operation = HumanoidGenericBaker.Bake(request, SetBakedResult);
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
                    Assert.That(failure, Is.TypeOf<OperationCanceledException>());
                    Assert.That(AssetDatabase.LoadMainAssetAtPath(request.outputPath), Is.Null);
                }
                else
                {
                    yield return HumanoidGenericBaker.Bake(request, SetBakedResult);
                    CheckSpaceCurves(setup, bakedResult, frames, space, root, check);
                    if (check == SpaceCheck.CompareLocal)
                    {
                        var localFrames = new List<SpaceFrame>();
                        AnimationClip localClip = null;
                        var localRequest = new GenericBakeRequest
                        {
                            director = setup.Director, track = relayTrack,
                            outputPath = bakeFolder + "/LocalComparison.anim",
                            progress = _ => localFrames.Add(new SpaceFrame(setup.Target))
                        };
                        yield return HumanoidGenericBaker.Bake(localRequest, value => localClip = value);
                        CheckSpaceCurves(setup, localClip, localFrames, BakeSpace.Local, false, SpaceCheck.Local);
                        Assert.That(localFrames.Count, Is.EqualTo(frames.Count));
                        for (int i = 0; i < frames.Count; i++)
                            Assert.That(Vector3.Distance(localFrames[i].worldPosition, frames[i].worldPosition), Is.LessThan(.001f), "Both modes sample the same final pose");
                        string hipsPath = AnimationUtility.CalculateTransformPath(setup.Target.GetBoneTransform(HumanBodyBones.Hips), setup.Target.transform);
                        float end = (frames.Count - 1) / bakedResult.frameRate;
                        Assert.That(Vector3.Distance(SpacePosition(bakedResult, hipsPath, end), SpacePosition(localClip, hipsPath, end)), Is.GreaterThan(1f), "Local vs Global parent translation");
                        Assert.That(Quaternion.Angle(SpaceRotation(bakedResult, hipsPath, end), SpaceRotation(localClip, hipsPath, end)), Is.GreaterThan(20f), "Local vs Global parent rotation");
                    }
                }
                Assert.That(setup.Director.time, Is.EqualTo(.2));
                Assert.That(setup.Director.timeUpdateMode, Is.EqualTo(DirectorUpdateMode.Manual));
                AssertSnapshot(setup.Target, targetBefore);
                AssertSnapshot(setup.A, aBefore); AssertSnapshot(setup.B, bBefore);
                AssertWorld(animatedParent, parentBefore.Item1, parentBefore.Item2);
                Assert.That(animatedParent.localScale, Is.EqualTo(parentBefore.Item3));
                Assert.That(renderer.enabled, Is.False);
                Assert.That(inactive.activeSelf && setup.Target.gameObject.activeSelf, Is.True);
                Assert.That(HumanoidGenericBaker.IsBaking, Is.False);
            }
            finally { TearDown(); CleanBakeAssets(); }
        }

        private static void SetSpaceCurve(AnimationClip clip, string property, float first, float last)
        {
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("", typeof(Transform), property), AnimationCurve.Linear(0, first, 3, last));
        }

        private static Vector3 SpacePosition(AnimationClip clip, string path, float time)
        {
            return new Vector3(SpaceValue(clip, path, "m_LocalPosition.x", time), SpaceValue(clip, path, "m_LocalPosition.y", time), SpaceValue(clip, path, "m_LocalPosition.z", time));
        }

        private static Quaternion SpaceRotation(AnimationClip clip, string path, float time)
        {
            return new Quaternion(SpaceValue(clip, path, "m_LocalRotation.x", time), SpaceValue(clip, path, "m_LocalRotation.y", time), SpaceValue(clip, path, "m_LocalRotation.z", time), SpaceValue(clip, path, "m_LocalRotation.w", time));
        }

        private static float SpaceValue(AnimationClip clip, string path, string property, float time)
        {
            return AnimationUtility.GetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, typeof(Transform), property)).Evaluate(time);
        }

        private static void CheckSpaceCurves(CutSetup setup, AnimationClip clip, List<SpaceFrame> frames, BakeSpace space, bool root, SpaceCheck check)
        {
            var bindings = AnimationUtility.GetCurveBindings(clip);
            string hipsPath = AnimationUtility.CalculateTransformPath(setup.Target.GetBoneTransform(HumanBodyBones.Hips), setup.Target.transform);
            Assert.That(hipsPath, Does.StartWith("Armature/"));
            Assert.That(bindings.Count(b => b.path == hipsPath), Is.EqualTo(7));
            Assert.That(bindings.Count(b => b.path == ""), Is.EqualTo(space == BakeSpace.Local && root ? 7 : 0));
            Assert.That(bindings.Any(b => b.propertyName.Contains("Scale") || b.propertyName.Contains("Euler") || b.path.Contains("SecondaryHair")), Is.False);
            foreach (var binding in bindings)
                Assert.That(binding.propertyName.StartsWith("m_LocalRotation.") ||
                    (binding.propertyName.StartsWith("m_LocalPosition.") &&
                     (binding.path == hipsPath || (space == BakeSpace.Local && root && binding.path == ""))), Is.True, "Only hips and optional Local root may have position curves");
            var reference = frames[0];
            var replay = Object.Instantiate(setup.Target.gameObject);
            var animator = replay.GetComponent<Animator>();
            animator.avatar = null; animator.Rebind(); animator.enabled = false;
            replay.transform.SetPositionAndRotation(reference.rootPosition, reference.rootRotation);
            replay.transform.localScale = reference.rootScale;
            var replayHips = replay.transform.Find(hipsPath);
            float localVsGlobalDistance = 0, localVsGlobalAngle = 0, localHipsTravel = 0;
            try
            {
                for (int i = 0; i < frames.Count; i++)
                {
                    float time = i / clip.frameRate;
                    var sample = frames[i];
                    var position = SpacePosition(clip, hipsPath, time);
                    var rotation = SpaceRotation(clip, hipsPath, time);
                    localHipsTravel = Mathf.Max(localHipsTravel, Vector3.Distance(reference.localPosition, sample.localPosition));
                    if (space == BakeSpace.Local)
                    {
                        Assert.That(Vector3.Distance(position, sample.localPosition), Is.LessThan(.001f), "Local hips position frame " + i);
                        Assert.That(Quaternion.Angle(rotation, sample.localRotation), Is.LessThan(.1f), "Local hips rotation frame " + i);
                        if (root)
                        {
                            Assert.That(Vector3.Distance(SpacePosition(clip, "", time), sample.rootLocalPosition), Is.LessThan(.001f));
                            Assert.That(Quaternion.Angle(SpaceRotation(clip, "", time), sample.rootLocalRotation), Is.LessThan(.1f));
                        }
                    }
                    else
                    {
                        clip.SampleAnimation(replay, time);
                        Assert.That(Vector3.Distance(replayHips.position, sample.worldPosition), Is.LessThan(.001f), "Frozen hierarchy replay position frame " + i);
                        Assert.That(Quaternion.Angle(replayHips.rotation, sample.worldRotation), Is.LessThan(.1f), "Frozen hierarchy replay rotation frame " + i);
                        localVsGlobalDistance = Mathf.Max(localVsGlobalDistance, Vector3.Distance(position, sample.localPosition));
                        localVsGlobalAngle = Mathf.Max(localVsGlobalAngle, Quaternion.Angle(rotation, sample.localRotation));
                    }
                    foreach (var bone in sample.otherRotations)
                        Assert.That(Quaternion.Angle(SpaceRotation(clip, bone.Key, time), bone.Value), Is.LessThan(.1f), "Other bone remains local: " + bone.Key);
                }
                if (check == SpaceCheck.Translation || check == SpaceCheck.CompareLocal || check == SpaceCheck.Constraint)
                    Assert.That(localVsGlobalDistance, Is.GreaterThan(1f), "Parent translation must not cancel back to local");
                if (check == SpaceCheck.Rotation || check == SpaceCheck.CompareLocal)
                    Assert.That(localVsGlobalAngle, Is.GreaterThan(20f), "Parent rotation must be integrated");
                if (check == SpaceCheck.Local || check == SpaceCheck.Hips || check == SpaceCheck.CompareLocal)
                    Assert.That(localHipsTravel, Is.GreaterThan(.01f), "Fixture also animates hips locally");
                if (check == SpaceCheck.Reference)
                    Assert.That(Vector3.Distance(reference.rootPosition, setup.Target.transform.position), Is.GreaterThan(1f), "Reference must be evaluated at first sample, not before bake");
                if (check == SpaceCheck.Cut)
                {
                    int cut = (int)clip.frameRate;
                    Assert.That(Vector3.Distance(frames[cut - 1].worldPosition, frames[cut].worldPosition), Is.GreaterThan(1f), "Fixture has an actual root cut");
                    clip.SampleAnimation(replay, 1f - .0001f);
                    Assert.That(Vector3.Distance(replayHips.position, frames[cut - 1].worldPosition), Is.LessThan(.001f), "Immediately before cut holds outgoing frame");
                    clip.SampleAnimation(replay, 1f);
                    Assert.That(Vector3.Distance(replayHips.position, frames[cut].worldPosition), Is.LessThan(.001f), "Cut frame uses incoming source");
                    clip.SampleAnimation(replay, 1f + 1f / clip.frameRate);
                    Assert.That(Vector3.Distance(replayHips.position, frames[cut + 1].worldPosition), Is.LessThan(.001f), "Frame after cut uses incoming source");
                }
            }
            finally { Object.DestroyImmediate(replay); }
        }
    }
}
