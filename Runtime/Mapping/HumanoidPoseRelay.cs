using System;
using System.Collections.Generic;
using UnityEngine;

namespace SillBill.PandaMotionRelay
{
    /// <summary>Retargets a Humanoid pose while preserving target root, scale and unmapped bones.</summary>
    public sealed class HumanoidPoseRelay : IDisposable
    {
        private readonly Animator source;
        private readonly Animator target;
        private readonly Avatar sourceAvatar;
        private readonly Avatar targetAvatar;
        private HumanPoseHandler sourceHandler;
        private HumanPoseHandler targetHandler;
        private HumanPose pose;
        private readonly List<TransformState> protectedBones = new List<TransformState>();
        private readonly List<TransformState> mappedBones = new List<TransformState>();
        private readonly TransformState root;
        private bool disposed;

        public static bool IsValid(Animator animator)
        {
            return animator != null && animator.avatar != null && animator.avatar.isValid &&
                animator.avatar.isHuman;
        }

        public HumanoidPoseRelay(Animator source, Animator target)
        {
            if (!IsValid(source) || !IsValid(target))
                throw new ArgumentException("Source and target must have valid Humanoid Avatars.");
            if (source == target || source.transform.IsChildOf(target.transform) ||
                target.transform.IsChildOf(source.transform))
                throw new ArgumentException("Source and target must have independent skeletons.");

            this.source = source;
            this.target = target;
            sourceAvatar = source.avatar;
            targetAvatar = target.avatar;
            root = new TransformState(target.transform);
            var mapped = new HashSet<Transform>();
            for (int i = 0; i < (int)HumanBodyBones.LastBone; i++)
            {
                var bone = target.GetBoneTransform((HumanBodyBones)i);
                if (bone != null && bone != target.transform) mapped.Add(bone);
            }
            foreach (var bone in target.GetComponentsInChildren<Transform>(true))
            {
                if (bone == target.transform) continue;
                var state = new TransformState(bone);
                if (mapped.Contains(bone)) mappedBones.Add(state);
                else protectedBones.Add(state);
            }
            sourceHandler = new HumanPoseHandler(sourceAvatar, source.transform);
            try
            {
                targetHandler = new HumanPoseHandler(targetAvatar, target.transform);
                pose.muscles = new float[HumanTrait.MuscleCount];
            }
            catch
            {
                sourceHandler.Dispose();
                throw;
            }
        }

        /// <summary>Transfers root-relative body position/rotation and muscles; root motion is not copied.</summary>
        public bool Apply()
        {
            if (disposed || !IsValid(source) || !IsValid(target) ||
                source.avatar != sourceAvatar || target.avatar != targetAvatar ||
                !source.gameObject.activeInHierarchy || !target.gameObject.activeInHierarchy)
                return false;

            // Snapshot each evaluation, so live secondary simulation is never reset to a rest pose.
            root.Capture();
            foreach (var state in protectedBones) state.Capture();
            foreach (var state in mappedBones) state.CaptureScale();
            sourceHandler.GetHumanPose(ref pose);
            // GetHumanPose reports world-space COM normalized by humanScale, while
            // SetHumanPose expects root-relative normalized COM. Remove driver placement.
            pose.bodyPosition = Quaternion.Inverse(source.transform.rotation) *
                (pose.bodyPosition - source.transform.position / source.humanScale);
            pose.bodyRotation = Quaternion.Inverse(source.transform.rotation) * pose.bodyRotation;
            try
            {
                targetHandler.SetHumanPose(ref pose);
            }
            finally
            {
                root.Restore();
                foreach (var state in protectedBones) state.Restore();
                foreach (var state in mappedBones) state.RestoreScale();
            }
            return true;
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            sourceHandler?.Dispose();
            targetHandler?.Dispose();
            sourceHandler = null;
            targetHandler = null;
        }

        private sealed class TransformState
        {
            private readonly Transform transform;
            private Vector3 position;
            private Quaternion rotation;
            private Vector3 scale;
            public TransformState(Transform transform) { this.transform = transform; Capture(); }
            public void Capture()
            {
                if (transform == null) return;
                position = transform.localPosition;
                rotation = transform.localRotation;
                scale = transform.localScale;
            }
            public void CaptureScale() { if (transform != null) scale = transform.localScale; }
            public void Restore()
            {
                if (transform == null) return;
                transform.localPosition = position;
                transform.localRotation = rotation;
                transform.localScale = scale;
            }
            public void RestoreScale() { if (transform != null) transform.localScale = scale; }
        }
    }
}
