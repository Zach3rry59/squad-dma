using System;
using Offsets;

namespace squad_dma.Source.Squad.Features
{
    /// <summary>
    /// Class responsible for handling player statistics
    /// </summary>
    public class PlayerStats
    {
        private readonly ulong _playerController;

        /// <summary>
        /// Constructor for GameStats
        /// </summary>
        /// <param name="playerController">Pointer to the player controller</param>
        public PlayerStats(ulong playerController)
        {
            _playerController = playerController;
        }
        
        /// <summary>
        /// Gets the player's kill count
        /// </summary>
        public int Kills
        {
            get
            {
                try
                {
                    var ptrScatter = new ScatterReadMap(1);
                    var ptrRound = ptrScatter.AddRound();

                    ptrRound.AddEntry<ulong>(0, 0, _playerController + Controller.PlayerState);
                    ptrScatter.Execute();

                    if (!ptrScatter.Results[0][0].TryGetResult<ulong>(out var playerState) || playerState == 0)
                        return 0;

                    var dataScatter = new ScatterReadMap(1);
                    var dataRound = dataScatter.AddRound();

                    var playerStateData = playerState + ASQPlayerState.PlayerStateData;

                    dataRound.AddEntry<int>(0, 0, playerStateData + FPlayerStateDataObject.NumKills);

                    dataScatter.Execute();

                    if (!dataScatter.Results[0][0].TryGetResult<int>(out var kills))
                    {
                        return 0;
                    }

                    return kills;
                }
                catch
                {
                    return 0;
                }
            }
        }
        
        /// <summary>
        /// Gets the player's wounded count
        /// </summary>
        public int Woundeds
        {
            get
            {
                try
                {
                    var ptrScatter = new ScatterReadMap(1);
                    var ptrRound = ptrScatter.AddRound();

                    ptrRound.AddEntry<ulong>(0, 0, _playerController + Controller.PlayerState);
                    ptrScatter.Execute();

                    if (!ptrScatter.Results[0][0].TryGetResult<ulong>(out var playerState) || playerState == 0)
                        return 0;

                    var dataScatter = new ScatterReadMap(1);
                    var dataRound = dataScatter.AddRound();

                    var playerStateData = playerState + ASQPlayerState.PlayerStateData;

                    dataRound.AddEntry<int>(0, 0, playerStateData + FPlayerStateDataObject.NumWoundeds);

                    dataScatter.Execute();

                    if (!dataScatter.Results[0][0].TryGetResult<int>(out var woundeds))
                    {
                        return 0;
                    }

                    return woundeds;
                }
                catch
                {
                    return 0;
                }
            }
        }
    }
} 