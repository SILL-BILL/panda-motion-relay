using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Timeline;

namespace SillBill.PandaMotionRelay.Tests
{
    public sealed partial class HumanoidRelayTests
    {
        [Test]
        public void MotionRelayTrack_NewTrackDefaultsRootOn()
        {
            var timeline = Own(ScriptableObject.CreateInstance<TimelineAsset>());
            Assert.That(Own(timeline.CreateTrack<MotionRelayTrack>()).relayMotionRoot, Is.True);
        }

        [Test] public void MotionRelayTrack_SerializedFalseRemainsFalse() { CheckSavedMotionRoot(false); }
        [Test] public void MotionRelayTrack_SerializedTrueRemainsTrue() { CheckSavedMotionRoot(true); }

        private void CheckSavedMotionRoot(bool enabled)
        {
            BakeRequest(CutTimeline()); // Creates a unique, generated-test-only output folder.
            string path = bakeFolder + "/SavedTimeline.playable";
            var timeline = ScriptableObject.CreateInstance<TimelineAsset>();
            AssetDatabase.CreateAsset(timeline, path);
            var track = timeline.CreateTrack<MotionRelayTrack>();
            track.relayMotionRoot = enabled;
            track.closeUnrepresentableMicroGaps = enabled;
            EditorUtility.SetDirty(track);
            AssetDatabase.SaveAssetIfDirty(track);
            Resources.UnloadAsset(track);
            Resources.UnloadAsset(timeline);
            var reloaded = AssetDatabase.LoadAssetAtPath<TimelineAsset>(path);
            Assert.That(reloaded.GetOutputTracks().OfType<MotionRelayTrack>().Single().relayMotionRoot, Is.EqualTo(enabled));
            Assert.That(reloaded.GetOutputTracks().OfType<MotionRelayTrack>().Single().closeUnrepresentableMicroGaps, Is.EqualTo(enabled));
        }
    }
}
