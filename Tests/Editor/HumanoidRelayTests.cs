using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace SillBill.PandaMotionRelay.Tests
{
    public sealed class HumanoidRelayTests
    {
        private readonly List<Object> owned = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var item in owned)
            {
                var go = item as GameObject;
                if (go != null && go.TryGetComponent<PlayableDirector>(out var director)) director.Stop();
            }
            for (int i = owned.Count - 1; i >= 0; i--)
                if (owned[i] != null) Object.DestroyImmediate(owned[i]);
            owned.Clear();
        }

        [Test]
        public void RetargetsMusclesBetweenDifferentSizeAvatars()
        {
            var source = Rig("Driver", 1f);
            var target = Rig("Target", 1.5f);
            Pose(source, 0.55f);
            using (var relay = new HumanoidPoseRelay(source, target)) Assert.That(relay.Apply(), Is.True);
            AssertPoseMatches(source, target);
        }

        [Test]
        public void PreservesRootScaleAndLiveSecondaryBonePose()
        {
            var source = Rig("Driver", 1f);
            var target = Rig("Target", 1f);
            target.transform.SetPositionAndRotation(new Vector3(3, 1, 2), Quaternion.Euler(0, 35, 0));
            var hair = new GameObject("Hair_Secondary").transform;
            hair.SetParent(target.GetBoneTransform(HumanBodyBones.Head), false);
            hair.localPosition = new Vector3(0, 0.1f, 0);
            var hips = target.GetBoneTransform(HumanBodyBones.Hips);
            hips.localScale = new Vector3(1.1f, 1f, 0.9f);
            using (var relay = new HumanoidPoseRelay(source, target))
            {
                Pose(source, 0.4f);
                relay.Apply();
                hair.localRotation = Quaternion.Euler(12, 23, 34);
                var hairRotation = hair.localRotation;
                relay.Apply();
                Assert.That(Quaternion.Angle(hair.localRotation, hairRotation), Is.LessThan(0.001f));
                Assert.That(hair.localPosition, Is.EqualTo(new Vector3(0, 0.1f, 0)));
            }
            Assert.That(target.transform.position, Is.EqualTo(new Vector3(3, 1, 2)));
            Assert.That(Quaternion.Angle(target.transform.rotation, Quaternion.Euler(0, 35, 0)), Is.LessThan(0.001f));
            Assert.That(hips.localScale, Is.EqualTo(new Vector3(1.1f, 1f, 0.9f)));
            Assert.That(target.gameObject.activeSelf, Is.True);
            Assert.That(source.gameObject.activeSelf, Is.True);
            Assert.That(target.enabled, Is.True);
        }

        [TestCase(1f, 1f)]
        [TestCase(2f, 0.8f)]
        public void DriverWorldPlacementDoesNotDisplaceTargetBody(float sourceScale, float targetScale)
        {
            var source = Rig("Driver", 1f);
            var target = Rig("Target", 1f);
            source.transform.localScale = Vector3.one * sourceScale;
            target.transform.localScale = Vector3.one * targetScale;
            source.transform.SetPositionAndRotation(new Vector3(10, 2, -5), Quaternion.Euler(0, 65, 0));
            target.transform.SetPositionAndRotation(new Vector3(-3, 1, 2), Quaternion.Euler(0, -30, 0));
            var hips = target.GetBoneTransform(HumanBodyBones.Hips);
            var before = hips.localPosition;
            using (var relay = new HumanoidPoseRelay(source, target)) relay.Apply();
            Assert.That(Vector3.Distance(hips.localPosition, before), Is.LessThan(.03f));
        }

        [Test]
        public void InactiveTargetIsNeverActivatedAndDisposedRelayIsSafe()
        {
            var source = Rig("Driver", 1f);
            var target = Rig("Target", 1f);
            var relay = new HumanoidPoseRelay(source, target);
            target.gameObject.SetActive(false);
            Assert.That(relay.Apply(), Is.False);
            Assert.That(target.gameObject.activeSelf, Is.False);
            relay.Dispose();
            relay.Dispose();
            Assert.That(relay.Apply(), Is.False);
        }

        [Test]
        public void RejectsMissingAvatarAndSelfRelay()
        {
            var source = Rig("Driver", 1f);
            Assert.Throws<ArgumentException>(() => new HumanoidPoseRelay(source, source));
            Assert.Throws<ArgumentException>(() => new HumanoidPoseRelay(null, source));
            var invalid = Own(new GameObject("Invalid")).AddComponent<Animator>();
            Assert.Throws<ArgumentException>(() => new HumanoidPoseRelay(source, invalid));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TimelineSeekReadsCurrentFrameRegardlessOfTrackOrder(bool relayFirst)
        {
            var director = TimelineRig(relayFirst, out var source, out var target);
            var muscle = Array.IndexOf(HumanTrait.MuscleName, "Left Arm Down-Up");
            try
            {
                foreach (double time in new[] { 0.1, 1.5, 0.4, 1.9 })
                {
                    director.time = time;
                    director.Evaluate();
                    MotionRelayEvaluation.ApplyPendingPoses();
                    var sourcePose = ReadPose(source);
                    Assert.That(sourcePose.muscles[muscle], Is.EqualTo(-0.4 + time * 0.5).Within(0.03));
                    AssertPoseMatches(source, target);
                    Assert.That(target.gameObject.activeSelf, Is.True);
                }
            }
            finally
            {
                director.Stop();
            }
        }

        [UnityTest]
        public IEnumerator EditorPreviewAutomaticallyAppliesAfterSeek()
        {
            var director = TimelineRig(true, out var source, out var target);
            foreach (double time in new[] { 0.1, 1.5, 0.4 })
            {
                director.time = time;
                director.Evaluate();
                // Allow the normal EditorApplication.update preview hook to run.
                yield return null;
                yield return null;
                AssertPoseMatches(source, target);
            }
        }

        [UnityTest]
        public IEnumerator PlayModeLateUpdateAutomaticallyAppliesAfterSeek()
        {
            yield return new EnterPlayMode();
            var director = TimelineRig(true, out var source, out var target);
            director.timeUpdateMode = DirectorUpdateMode.Manual;
            director.Play();
            foreach (double time in new[] { 0.1, 1.5, 0.4 })
            {
                director.time = time;
                director.Evaluate();
                yield return null;
                AssertPoseMatches(source, target);
                Assert.That(target.gameObject.activeSelf, Is.True);
            }
            TearDown();
            yield return new ExitPlayMode();
        }

        [Test]
        public void ClipDoesNotAdvertiseBlendingOrExtrapolation()
        {
            var clip = Own(ScriptableObject.CreateInstance<MotionRelayClip>());
            Assert.That(clip.clipCaps, Is.EqualTo(ClipCaps.None));
        }

        private static void AssertPoseMatches(Animator source, Animator target)
        {
            var a = ReadPose(source);
            var b = ReadPose(target);
            var muscle = Array.IndexOf(HumanTrait.MuscleName, "Left Arm Down-Up");
            Assert.That(b.muscles[muscle], Is.EqualTo(a.muscles[muscle]).Within(0.03), "Current-frame arm muscle");
        }

        private static HumanPose ReadPose(Animator animator)
        {
            using (var handler = new HumanPoseHandler(animator.avatar, animator.transform))
            {
                var pose = new HumanPose { muscles = new float[HumanTrait.MuscleCount] };
                handler.GetHumanPose(ref pose);
                return pose;
            }
        }

        private static void Pose(Animator animator, float arm)
        {
            var pose = ReadPose(animator);
            pose.muscles[Array.IndexOf(HumanTrait.MuscleName, "Left Arm Down-Up")] = arm;
            using (var handler = new HumanPoseHandler(animator.avatar, animator.transform))
                handler.SetHumanPose(ref pose);
        }

        private T Own<T>(T value) where T : Object { owned.Add(value); return value; }

        private PlayableDirector TimelineRig(bool relayFirst, out Animator source, out Animator target)
        {
            source = Rig("Driver", 1f);
            target = Rig("Target", 1.3f);
            var director = Own(new GameObject("Director")).AddComponent<PlayableDirector>();
            var timeline = Own(ScriptableObject.CreateInstance<TimelineAsset>());
            var animation = Own(new AnimationClip());
            var muscle = Array.IndexOf(HumanTrait.MuscleName, "Left Arm Down-Up");
            Assert.That(muscle, Is.GreaterThanOrEqualTo(0));
            AnimationUtility.SetEditorCurve(animation,
                EditorCurveBinding.FloatCurve("", typeof(Animator), HumanTrait.MuscleName[muscle]),
                AnimationCurve.Linear(0, -0.4f, 2, 0.6f));
            MotionRelayTrack relayTrack = null;
            if (relayFirst) relayTrack = Own(timeline.CreateTrack<MotionRelayTrack>(null, "Relay"));
            var animationTrack = Own(timeline.CreateTrack<AnimationTrack>(null, "Driver Animation"));
            animationTrack.trackOffset = TrackOffset.ApplySceneOffsets;
            var animationClip = animationTrack.CreateClip(animation);
            Own(animationClip.asset);
            animationClip.duration = 2;
            if (!relayFirst) relayTrack = Own(timeline.CreateTrack<MotionRelayTrack>(null, "Relay"));
            var clip = relayTrack.CreateClip<MotionRelayClip>();
            clip.duration = 2;
            var relayAsset = Own((MotionRelayClip)clip.asset);
            relayAsset.source.exposedName = new PropertyName("driver");
            director.playableAsset = timeline;
            director.SetGenericBinding(animationTrack, source);
            director.SetGenericBinding(relayTrack, target);
            director.SetReferenceValue(relayAsset.source.exposedName, source);
            return director;
        }

        private Animator Rig(string name, float size)
        {
            var root = Own(new GameObject(name));
            var bones = new Dictionary<string, Transform>();
            Action<string, string, Vector3> add = (boneName, parent, position) =>
            {
                var bone = new GameObject(boneName).transform;
                bone.SetParent(parent == null ? root.transform : bones[parent], false);
                bone.localPosition = position * size;
                bones.Add(boneName, bone);
            };
            add("Hips", null, new Vector3(0, 1, 0));
            add("Spine", "Hips", new Vector3(0, .2f, 0));
            add("Chest", "Spine", new Vector3(0, .2f, 0));
            add("Neck", "Chest", new Vector3(0, .2f, 0));
            add("Head", "Neck", new Vector3(0, .15f, 0));
            foreach (var side in new[] { "Left", "Right" })
            {
                float direction = side == "Left" ? -1f : 1f;
                add(side + "UpperLeg", "Hips", new Vector3(.1f * direction, -.05f, 0));
                add(side + "LowerLeg", side + "UpperLeg", new Vector3(0, -.4f, 0));
                add(side + "Foot", side + "LowerLeg", new Vector3(0, -.4f, .05f));
                add(side + "Toes", side + "Foot", new Vector3(0, 0, .15f));
                add(side + "Shoulder", "Chest", new Vector3(.1f * direction, .12f, 0));
                add(side + "UpperArm", side + "Shoulder", new Vector3(.15f * direction, 0, 0));
                add(side + "LowerArm", side + "UpperArm", new Vector3(.3f * direction, 0, 0));
                add(side + "Hand", side + "LowerArm", new Vector3(.25f * direction, 0, 0));
            }
            var human = new List<HumanBone>();
            foreach (var entry in bones)
            {
                human.Add(new HumanBone { boneName = entry.Key, humanName = entry.Key,
                    limit = new HumanLimit { useDefaultValues = true } });
            }
            var skeleton = new List<SkeletonBone>();
            foreach (var bone in root.GetComponentsInChildren<Transform>())
                skeleton.Add(new SkeletonBone { name = bone.name, position = bone.localPosition,
                    rotation = bone.localRotation, scale = bone.localScale });
            var description = new HumanDescription
            {
                human = human.ToArray(), skeleton = skeleton.ToArray(),
                upperArmTwist = .5f, lowerArmTwist = .5f, upperLegTwist = .5f, lowerLegTwist = .5f,
                armStretch = .05f, legStretch = .05f, feetSpacing = 0, hasTranslationDoF = false
            };
            var avatar = Own(AvatarBuilder.BuildHumanAvatar(root, description));
            Assert.That(avatar.isValid && avatar.isHuman, Is.True, "Synthetic Humanoid fixture");
            var animator = root.AddComponent<Animator>();
            animator.avatar = avatar;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            return animator;
        }
    }
}
