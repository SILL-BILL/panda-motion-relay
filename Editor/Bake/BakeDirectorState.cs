using System.Collections.Generic;
using UnityEngine.Playables;

namespace SillBill.PandaMotionRelay.Editor
{
    /// <summary>Restore parents before children: parent evaluation can change child graphs and clocks.</summary>
    internal sealed class BakeDirectorState
    {
        private readonly List<State> states = new List<State>();

        public BakeDirectorState(IEnumerable<PlayableDirector> directors)
        {
            foreach (var director in directors) states.Add(new State(director));
        }

        public void Prepare()
        {
            foreach (var state in states)
            {
                state.director.Pause();
                state.director.timeUpdateMode = DirectorUpdateMode.Manual;
            }
        }

        public void Restore()
        {
            foreach (var state in states)
            {
                var director = state.director;
                if (director == null) continue;
                if (!state.valid) director.Stop();
                else
                {
                    if (!director.playableGraph.IsValid()) director.RebuildGraph();
                    director.time = state.time;
                    director.Evaluate();
                    // A destroyed parent Control playable may Stop its child during this
                    // evaluation. Once that teardown is drained, restore the original live graph.
                    if (!director.playableGraph.IsValid())
                    {
                        director.RebuildGraph();
                        director.time = state.time;
                        director.Evaluate();
                    }
                    if (state.playState == PlayState.Playing) director.Play();
                    else director.Pause();
                    var graph = director.playableGraph;
                    for (int i = 0; graph.IsValid() && i < graph.GetRootPlayableCount() && i < state.speeds.Count; i++)
                        graph.GetRootPlayable(i).SetSpeed(state.speeds[i]);
                }
                director.time = state.time;
                director.timeUpdateMode = state.mode;
            }
            // All parent/child evaluation is complete before discarding deferred pose writes.
            foreach (var state in states)
                if (state.director != null) MotionRelayEvaluation.DiscardPendingPoses(state.director);
        }

        private sealed class State
        {
            public readonly PlayableDirector director;
            public readonly double time;
            public readonly DirectorUpdateMode mode;
            public readonly PlayState playState;
            public readonly bool valid;
            public readonly List<double> speeds = new List<double>();
            public State(PlayableDirector director)
            {
                this.director = director; time = director.time; mode = director.timeUpdateMode;
                playState = director.state; valid = director.playableGraph.IsValid();
                if (valid)
                    for (int i = 0; i < director.playableGraph.GetRootPlayableCount(); i++)
                        speeds.Add(director.playableGraph.GetRootPlayable(i).GetSpeed());
            }
        }
    }
}
