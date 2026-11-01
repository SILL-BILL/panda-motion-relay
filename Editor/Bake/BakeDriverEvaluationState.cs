using System.Collections.Generic;
using UnityEngine;

namespace SillBill.PandaMotionRelay.Editor
{
    /// <summary>Keep active Source animation evaluable even when its renderers are hidden.</summary>
    internal sealed class BakeDriverEvaluationState
    {
        private readonly Dictionary<Animator, AnimatorCullingMode> original = new Dictionary<Animator, AnimatorCullingMode>();

        public BakeDriverEvaluationState(IEnumerable<Animator> drivers)
        {
            foreach (var driver in drivers)
                if (driver != null && !original.ContainsKey(driver)) original.Add(driver, driver.cullingMode);
        }

        public void Prepare()
        {
            foreach (var driver in original.Keys)
                if (driver != null) driver.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        }

        public void Restore()
        {
            foreach (var pair in original)
                if (pair.Key != null) pair.Key.cullingMode = pair.Value;
        }
    }
}
