using System;
using System.Collections.Generic;
using UnityEngine;

namespace SillBill.PandaMotionRelay.Editor
{
    /// <summary>World output omits scale and helper curves; reject evaluated motion it cannot preserve.</summary>
    internal sealed class WorldBakeValidation
    {
        private const float ScaleTolerance = 0.0001f;
        private readonly Transform root, hips;
        private readonly HashSet<Transform> mapped = new HashSet<Transform>();
        private readonly HashSet<Transform> internalTransforms = new HashSet<Transform>();
        private readonly HashSet<Transform> scales = new HashSet<Transform>();
        private readonly Dictionary<Transform, State> initial = new Dictionary<Transform, State>();
        private Vector3 initialWorldScale;

        public WorldBakeValidation(Animator target)
        {
            root = target.transform;
            hips = target.GetBoneTransform(HumanBodyBones.Hips);
            for (int i = 0; i < (int)HumanBodyBones.LastBone; i++)
            {
                var bone = target.GetBoneTransform((HumanBodyBones)i);
                if (bone == null) continue;
                mapped.Add(bone);
                for (var t = bone; t != null && t != root; t = t.parent) internalTransforms.Add(t);
            }
            foreach (var t in internalTransforms) scales.Add(t);
            for (var t = root; t != null; t = t.parent) scales.Add(t);
        }

        public void ValidateStatic()
        {
            foreach (var t in scales)
            {
                if (t == null) throw new ArgumentException("World to Root: the hierarchy changed during validation.");
                var s = t.localScale;
                if (!Finite(s) || s.x <= 0 || s.y <= 0 || s.z <= 0 || !Uniform(s))
                    throw Unsupported(t, "non-uniform, negative/mirrored, zero or non-finite scale");
                var m = t.localToWorldMatrix;
                var x = (Vector3)m.GetColumn(0); var y = (Vector3)m.GetColumn(1); var z = (Vector3)m.GetColumn(2);
                if (!Finite(x) || !Finite(y) || !Finite(z) || !Uniform(new Vector3(x.magnitude, y.magnitude, z.magnitude)) ||
                    Mathf.Abs(Vector3.Dot(x.normalized, y.normalized)) > ScaleTolerance ||
                    Mathf.Abs(Vector3.Dot(x.normalized, z.normalized)) > ScaleTolerance ||
                    Mathf.Abs(Vector3.Dot(y.normalized, z.normalized)) > ScaleTolerance)
                    throw Unsupported(t, "non-uniform world scale or shear");
            }
        }

        public void Sample()
        {
            ValidateStatic();
            if (initial.Count == 0)
            {
                foreach (var t in scales) initial.Add(t, new State(t));
                initialWorldScale = root.lossyScale;
                return;
            }
            if (ChangedScale(root.lossyScale, initialWorldScale)) throw Unsupported(root, "animated world scale");
            foreach (var t in scales)
            {
                var state = initial[t];
                if (t.parent != state.parent) throw Unsupported(t, "animated hierarchy parenting");
                if (ChangedScale(t.localScale, state.scale)) throw Unsupported(t, "animated scale");
                if (!internalTransforms.Contains(t)) continue;
                var delta = t.localPosition - state.position;
                if (t != hips && (t.parent != null ? t.parent.TransformVector(delta) : delta).magnitude > 0.00001f)
                    throw Unsupported(t, "animated mapped-bone/helper translation (only Root and Hips position curves are supported)");
                if (!mapped.Contains(t) && Quaternion.Angle(t.localRotation, state.rotation) > 0.001f)
                    throw Unsupported(t, "animated internal helper rotation (no unmapped curves are generated)");
            }
        }

        private static bool Finite(Vector3 v) => Finite(v.x) && Finite(v.y) && Finite(v.z);
        private static bool Finite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);
        private static bool Uniform(Vector3 v) => Mathf.Abs(v.x - v.y) <= ScaleTolerance * Mathf.Max(0.00001f, v.x, v.y, v.z) &&
            Mathf.Abs(v.x - v.z) <= ScaleTolerance * Mathf.Max(0.00001f, v.x, v.y, v.z);
        private static bool ChangedScale(Vector3 a, Vector3 b) => Vector3.Distance(a, b) > ScaleTolerance * Mathf.Max(0.00001f, b.magnitude);
        private static ArgumentException Unsupported(Transform t, string reason) =>
            new ArgumentException("World to Root cannot bake " + reason + " at '" + t.name + "'. Use constant positive uniform scale and a fixed internal skeleton; no asset was saved.");

        private sealed class State
        {
            public readonly Transform parent;
            public readonly Vector3 position, scale;
            public readonly Quaternion rotation;
            public State(Transform t) { parent = t.parent; position = t.localPosition; rotation = t.localRotation; scale = t.localScale; }
        }
    }
}
