using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SillBill.PandaMotionRelay.Editor
{
    /// <summary>Transient Play Mode scene state; contains no asset files or private model data.</summary>
    internal sealed class BakeSceneState
    {
        private readonly List<TransformState> transforms = new List<TransformState>();
        private readonly List<RendererState> renderers = new List<RendererState>();
        private readonly List<AnimatorState> animators = new List<AnimatorState>();

        public BakeSceneState()
        {
            for (int sceneIndex = 0; sceneIndex < SceneManager.sceneCount; sceneIndex++)
                foreach (var root in SceneManager.GetSceneAt(sceneIndex).GetRootGameObjects())
                {
                    if ((root.hideFlags & HideFlags.DontSave) != 0) continue;
                    foreach (var transform in root.GetComponentsInChildren<Transform>(true)) transforms.Add(new TransformState(transform));
                    foreach (var renderer in root.GetComponentsInChildren<Renderer>(true)) renderers.Add(new RendererState { target = renderer, enabled = renderer.enabled });
                    foreach (var animator in root.GetComponentsInChildren<Animator>(true)) animators.Add(new AnimatorState { target = animator, enabled = animator.enabled });
                }
        }

        public void Restore()
        {
            foreach (var state in transforms)
                if (state.target != null && state.target.gameObject.activeSelf != state.active) state.target.gameObject.SetActive(state.active);
            foreach (var state in transforms)
            {
                if (state.target == null) continue;
                if (state.target.parent != state.parent) state.target.SetParent(state.parent, false);
                state.target.localPosition = state.position; state.target.localRotation = state.rotation; state.target.localScale = state.scale;
            }
            foreach (var state in renderers) if (state.target != null) state.target.enabled = state.enabled;
            foreach (var state in animators) if (state.target != null) state.target.enabled = state.enabled;
        }

        private sealed class TransformState
        {
            public readonly Transform target, parent;
            public readonly Vector3 position, scale;
            public readonly Quaternion rotation;
            public readonly bool active;
            public TransformState(Transform target)
            {
                this.target = target; parent = target.parent; position = target.localPosition;
                rotation = target.localRotation; scale = target.localScale; active = target.gameObject.activeSelf;
            }
        }
        private sealed class RendererState { public Renderer target; public bool enabled; }
        private sealed class AnimatorState { public Animator target; public bool enabled; }
    }
}
