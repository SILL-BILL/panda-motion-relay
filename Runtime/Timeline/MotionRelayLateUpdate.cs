using UnityEngine;

namespace SillBill.PandaMotionRelay
{
    [DefaultExecutionOrder(32000)]
    [AddComponentMenu("")]
    public sealed class MotionRelayLateUpdate : MonoBehaviour
    {
        private void LateUpdate() { MotionRelayEvaluation.ApplyPendingPoses(); }
    }
}
