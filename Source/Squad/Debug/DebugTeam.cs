using System.Collections.ObjectModel;

namespace squad_dma.Source.Squad.Debug
{
    public class DebugTeam
    {
        private readonly bool _inGame;
        private readonly UActor _localUPlayer;
        private readonly ReadOnlyDictionary<ulong, UActor> _actors;

        public DebugTeam(bool inGame, UActor localUPlayer, ReadOnlyDictionary<ulong, UActor> actors)
        {
            _inGame = inGame;
            _localUPlayer = localUPlayer;
            _actors = actors;
        }

        public void LogTeamInfo()
        {
            if (!_inGame) return;

            Program.Log($"=== TEAM INFO DUMP ===");
            Program.Log($"Local Player TeamID: {_localUPlayer.TeamID}");

            if (_actors == null) return;

            foreach (var actor in _actors.Values)
            {
                if (actor.ActorType == ActorType.Player)
                {
                    Program.Log($"Actor: {actor.Name} | TeamID: {actor.TeamID} | Health: {actor.Health} | Position: {actor.Position}");
                }
            }
            Program.Log($"======================");
        }
    }
} 