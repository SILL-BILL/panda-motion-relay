using System;
using System.Collections;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace SillBill.PandaMotionRelay.Editor
{
    public sealed class GenericBakeWindow : EditorWindow
    {
        private PlayableDirector director;
        private int trackIndex;

        [MenuItem("Tools/Panda Motion Relay/Bake Humanoid to Generic")]
        public static void Open()
        {
            var window = GetWindow<GenericBakeWindow>("Panda Motion Relay Bake");
            if (Selection.activeGameObject != null) window.director = Selection.activeGameObject.GetComponent<PlayableDirector>();
        }

        private void OnGUI()
        {
            director = (PlayableDirector)EditorGUILayout.ObjectField("Director", director, typeof(PlayableDirector), true);
            var timeline = director != null ? director.playableAsset as TimelineAsset : null;
            var tracks = timeline != null ? timeline.GetOutputTracks().OfType<MotionRelayTrack>().ToArray() : Array.Empty<MotionRelayTrack>();
            if (tracks.Length == 0) { EditorGUILayout.HelpBox("Select a Director with a Motion Relay Track.", MessageType.Info); return; }
            trackIndex = Mathf.Clamp(trackIndex, 0, tracks.Length - 1);
            trackIndex = EditorGUILayout.Popup("Relay Track", trackIndex, tracks.Select(t => t.name).ToArray());
            EditorGUILayout.HelpBox("Raw per-frame Generic bake for this Humanoid target. Mapped bones only; no scale or secondary-bone curves. Uses the Timeline frame rate and the first-to-last Relay Clip range.", MessageType.Info);
            if (!Application.isPlaying || EditorApplication.isPaused)
                EditorGUILayout.HelpBox("Enter unpaused Play Mode before baking so animation and native constraints can evaluate. The Director and scene transforms are restored after baking.", MessageType.Info);
            using (new EditorGUI.DisabledScope(!Application.isPlaying || EditorApplication.isPaused || HumanoidGenericBaker.IsBaking))
                if (GUILayout.Button("Bake to New Animation Clip"))
                {
                    string path = EditorUtility.SaveFilePanelInProject("Save Generic Bake", "PandaMotionRelay_Baked", "anim", "Choose a new asset path.");
                    if (string.IsNullOrEmpty(path)) return;
                    var request = new GenericBakeRequest { director = director, track = tracks[trackIndex], outputPath = path };
                    try { GenericBakeRunner.StartBake(request); }
                    catch (Exception exception) { EditorUtility.DisplayDialog("Bake could not start", exception.Message, "OK"); }
                }
        }
    }

    [AddComponentMenu("")]
    public sealed class GenericBakeRunner : MonoBehaviour
    {
        private IEnumerator operation;
        private bool canceled;

        public static void StartBake(GenericBakeRequest request)
        {
            HumanoidGenericBaker.Validate(request);
            if (!Application.isPlaying || EditorApplication.isPaused || HumanoidGenericBaker.IsBaking)
                throw new InvalidOperationException("Enter unpaused Play Mode and wait for any current bake to finish.");
            var host = new GameObject("Panda Motion Relay Bake") { hideFlags = HideFlags.HideAndDontSave };
            var runner = host.AddComponent<GenericBakeRunner>();
            request.cancel = () => runner.canceled;
            request.progress = value => runner.canceled |= EditorUtility.DisplayCancelableProgressBar("Panda Motion Relay Bake", "Sampling evaluated target pose", value);
            runner.operation = HumanoidGenericBaker.Bake(request, clip => { Selection.activeObject = clip; EditorGUIUtility.PingObject(clip); });
            runner.StartCoroutine(runner.Run());
        }

        private IEnumerator Run()
        {
            while (operation != null)
            {
                bool more;
                try { more = operation.MoveNext(); }
                catch (Exception exception)
                {
                    if (!(exception is OperationCanceledException)) Debug.LogException(exception);
                    break;
                }
                if (!more) break;
                yield return operation.Current;
            }
            Cleanup();
            Destroy(gameObject);
        }

        private void OnDisable() { Cleanup(); }
        private void Cleanup()
        {
            (operation as IDisposable)?.Dispose();
            operation = null;
            EditorUtility.ClearProgressBar();
        }
    }
}
