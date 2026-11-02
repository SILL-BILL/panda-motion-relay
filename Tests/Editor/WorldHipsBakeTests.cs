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

        private IEnumerator AssertTimelineReplay(Animator animator, AnimationClip clip, List<WorldFrame> frames, bool hipsOutput, Action<int> beforeEvaluate = null)
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
                director.time = f / (double)clip.frameRate;
                director.Evaluate();
                yield return null;
                Assert.That(animator.isHuman, Is.False);
                AssertWorldFrames(transforms, frames[f], hipsOutput);
                if (hipsOutput) AssertWorld(animator.transform, Vector3.zero, Quaternion.identity);
            }
            director.Stop();
        }
    }
}
