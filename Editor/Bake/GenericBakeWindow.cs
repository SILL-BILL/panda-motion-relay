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
        private PlayableDirector evaluationDirector;
        private int trackIndex;
        [SerializeField] private BakeSpace bakeSpace = BakeSpace.Local;

        [MenuItem("Tools/Panda Motion Relay/Bake Humanoid to Generic")]
        public static void Open()
        {
            var window = GetWindow<GenericBakeWindow>("Panda Motion Relay Bake");
            if (Selection.activeGameObject != null) window.director = Selection.activeGameObject.GetComponent<PlayableDirector>();
        }

        private void OnGUI()
        {
            director = (PlayableDirector)EditorGUILayout.ObjectField(new GUIContent("Relay Director", "転送設定がある側：Motion Relay Trackを持つTimelineのDirector"), director, typeof(PlayableDirector), true);
            evaluationDirector = (PlayableDirector)EditorGUILayout.ObjectField(new GUIContent("Evaluation Director", "全体をまとめて動かす側：Character / Propを評価するMaster。単一Timelineなら空欄"), evaluationDirector, typeof(PlayableDirector), true);
            if (evaluationDirector == null)
                EditorGUILayout.HelpBox("転送設定がある側をRelay Directorへ指定します。単一TimelineならEvaluation Directorは空欄。Character / Propが別Timelineなら全体を動かすMasterを指定します。", MessageType.Info);
            var modes = new[] { BakeSpace.Local, BakeSpace.World, BakeSpace.Global };
            var labels = new[] { new GUIContent("Local — Use Existing Parent", "元の親階層・Root Motionを再生時にも使用します。"),
                new GUIContent("World — Record Motion to Root", "親階層やConstraint評価後のTarget最終World動作を、独立モデルのAnimator Rootへ記録します。"),
                new GUIContent("World — Consolidate Motion to Hips", "親階層やRoot MotionをHipsへ集約します。従来のGlobalと同じ方式です。") };
            int selected = Mathf.Max(0, Array.IndexOf(modes, bakeSpace));
            selected = EditorGUILayout.Popup(new GUIContent("Bake Mode", "再生先の階層と、全体移動の記録先を選びます。"), selected, labels);
            bakeSpace = modes[selected];
            EditorGUILayout.HelpBox(labels[selected].tooltip + (bakeSpace == BakeSpace.World ? " 再生先の親は位置0・回転0・Scale 1。Characterの一定Scaleと内部Skeletonを開始Frameに合わせ、元の親やConstraintを重ねません。全範囲を先に検証するため2回評価します。" : ""), MessageType.Info);
            var timeline = director != null ? director.playableAsset as TimelineAsset : null;
            var tracks = timeline != null ? timeline.GetOutputTracks().OfType<MotionRelayTrack>().ToArray() : Array.Empty<MotionRelayTrack>();
            if (tracks.Length == 0) { EditorGUILayout.HelpBox("Select a Director with a Motion Relay Track.", MessageType.Info); return; }
            trackIndex = Mathf.Clamp(trackIndex, 0, tracks.Length - 1);
            trackIndex = EditorGUILayout.Popup(new GUIContent("Relay Track", "Humanoid TargetへBindingした身体側Trackを選びます。Root-only Animator Trackは対象外です。"), trackIndex, tracks.Select(t => new GUIContent(t.name)).ToArray());
            EditorGUILayout.HelpBox("全ClipのDriverは、使用区間外も本体・親・Animatorを有効にします。Activation Trackをミュートし、隠す場合はRendererのみOFFにしてください。", MessageType.Info);
            EditorGUILayout.HelpBox("Raw per-frame Generic bake for this Humanoid target. Mapped bones only; no scale or secondary-bone curves. Uses the Timeline frame rate and the first-to-last Relay Clip range.", MessageType.Info);
            if (!Application.isPlaying || EditorApplication.isPaused)
                EditorGUILayout.HelpBox("Enter unpaused Play Mode before baking so animation and native constraints can evaluate. The Director and scene transforms are restored after baking.", MessageType.Info);
            using (new EditorGUI.DisabledScope(!Application.isPlaying || EditorApplication.isPaused || HumanoidGenericBaker.IsBaking))
                if (GUILayout.Button("Bake to New Animation Clip"))
                {
                    string path = EditorUtility.SaveFilePanelInProject("Save Generic Bake", "PandaMotionRelay_Baked", "anim", "Choose a new asset path.");
                    if (string.IsNullOrEmpty(path)) return;
                    var request = new GenericBakeRequest { director = director, evaluationDirector = evaluationDirector, track = tracks[trackIndex], outputPath = path, bakeSpace = bakeSpace };
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
            request.validationProgress = value => runner.canceled |= EditorUtility.DisplayCancelableProgressBar("Panda Motion Relay Bake", "World → Root: 全範囲のScale / Translationを検証中", value);
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
