using UnityEditor;

namespace SillBill.PandaMotionRelay.Editor
{
    [InitializeOnLoad]
    internal static class MotionRelayPreview
    {
        static MotionRelayPreview()
        {
            EditorApplication.update += Apply;
        }

        private static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (MotionRelayEvaluation.ApplyPendingPoses()) SceneView.RepaintAll();
        }
    }
}
