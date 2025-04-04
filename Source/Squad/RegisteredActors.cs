using Offsets;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Numerics;

namespace squad_dma
{
    public class RegistredActors
    {
        private readonly ulong _persistentLevel;
        private ulong _actorsArray;
        private readonly Stopwatch _regSw = new();
        private readonly ConcurrentDictionary<ulong, UActor> _actors = new();
        private Dictionary<ulong, int> _squadCache = new();
        private DateTime _lastSquadUpdate = DateTime.MinValue;
        private const int SquadUpdateInterval = 1000; // Update every 1 second

        public IEnumerable<uint> GetActorNameIds()
        {
            return _actors.Values.Select(actor => actor.NameId).Where(id => id != 0);
        }

        #region Getters
        public ReadOnlyDictionary<ulong, UActor> Actors { get; }

        public int ActorCount
        {
            get
            {
                const int maxAttempts = 6;
                for (int attempt = 0; attempt < maxAttempts; attempt++)
                {
                    try
                    {
                        var count = Memory.ReadValue<int>(_persistentLevel + Offsets.Level.MaxPacket);
                        if (count < 1)
                        {
                            this._actors.Clear();
                            return -1;
                        }
                        return count;
                    }
                    catch (DMAShutdown)
                    {
                        throw;
                    }
                    catch (Exception ex) when (attempt < maxAttempts - 1)
                    {
                        Program.Log($"ERROR - PlayerCount attempt {attempt + 1} failed: {ex}");
                        Thread.Sleep(1000);
                    }
                }
                Program.Log("ERROR - ActorCount failed after all attempts");
                return -1;
            }
        }
        #endregion

        public RegistredActors(ulong persistentLevelAddr)
        {
            this._persistentLevel = persistentLevelAddr;
            this.Actors = new ReadOnlyDictionary<ulong, UActor>(_actors);
            this._actorsArray = Memory.ReadPtr(_persistentLevel + Offsets.Level.Actors);
            this._regSw.Start();
            Program.Log($"RegistredActors initialized with persistentLevelAddr: 0x{persistentLevelAddr:X}");
        }

        #region Update List/Player Functions
        public Dictionary<ulong, uint> GetActorBaseWithName()
        {
            var count = this.ActorCount;
            if (count < 1)
            {
                Program.Log("GetActorBaseWithName: ActorCount < 1, returning empty dictionary");
                return new Dictionary<ulong, uint>();
            }

            var scatterMap = new ScatterReadMap(count);
            var actorRound = scatterMap.AddRound();
            var idRound = scatterMap.AddRound();

            for (int i = 0; i < count; i++)
            {
                var actorAddr = actorRound.AddEntry<ulong>(i, 0, _actorsArray + (uint)(i * 0x8));
                idRound.AddEntry<uint>(i, 1, actorAddr, null, Offsets.Actor.ID);
            }

            scatterMap.Execute();

            var actorBaseWithName = new Dictionary<ulong, uint>();
            for (int i = 0; i < count; i++)
            {
                if (!scatterMap.Results[i][0].TryGetResult<ulong>(out var addr) || addr == 0)
                    continue;
                if (!scatterMap.Results[i][1].TryGetResult<uint>(out var id) || id == 0)
                    continue;
                actorBaseWithName[addr] = id;
            }

            //Program.Log($"GetActorBaseWithName: Found {actorBaseWithName.Count} valid actors");
            return actorBaseWithName;
        }

        public void UpdateList()
        {
        if (this._regSw.ElapsedMilliseconds < 800)
        {
            //Program.Log("UpdateList: Skipped due to timing constraint");
            return;
        }

        try
        {
            var count = this.ActorCount;
            if (count < 10)
                throw new GameEnded();

            var scatterMap = new ScatterReadMap(count);
            var actorRound = scatterMap.AddRound();
            var idRound = scatterMap.AddRound();

            for (int i = 0; i < count; i++)
            {
                var actorAddr = actorRound.AddEntry<ulong>(i, 0, _actorsArray + (uint)(i * 0x8));
                idRound.AddEntry<uint>(i, 1, actorAddr, null, Offsets.Actor.ID);
            }

            scatterMap.Execute();

            var actorBaseWithName = new Dictionary<ulong, uint>();
            for (int i = 0; i < count; i++)
            {
                if (!scatterMap.Results[i][0].TryGetResult<ulong>(out var addr) || addr == 0)
                    continue;
                if (!scatterMap.Results[i][1].TryGetResult<uint>(out var id) || id == 0)
                    continue;
                actorBaseWithName[addr] = id;
            }

            var notUpdated = new HashSet<ulong>(_actors.Keys);
            foreach (var item in actorBaseWithName.ToList())
            {
                if (_actors.ContainsKey(item.Key) && _actors[item.Key].NameId == item.Value)
                {
                    notUpdated.Remove(item.Key);
                    actorBaseWithName.Remove(item.Key);
                }
            }

            var names = Memory.GetNamesById([.. actorBaseWithName.Values.Distinct()]);
            foreach (var item in names)
            {
                if (item.Value.StartsWith("BP_UAF"))
                    names[item.Key] = item.Value.Replace("BP_UAF", "BP_Soldier_UAF");
            }
            var playersNameIDs = names.Where(x => x.Value.StartsWith("BP_Soldier") || Names.TechNames.ContainsKey(x.Value))
                                      .ToDictionary(x => x.Key, x => x.Value);
            var filteredActors = actorBaseWithName.Where(actor => playersNameIDs.ContainsKey(actor.Value))
                                                  .Select(actor => actor.Key)
                                                  .ToList();
            count = filteredActors.Count;

            for (int i = 0; i < count; i++)
            {
                var actorAddr = filteredActors[i];
                var nameId = actorBaseWithName[actorAddr];
                var actorName = playersNameIDs[nameId];
                var team = Team.Unknown;
                var actorType = Names.TechNames.GetValueOrDefault(actorName, ActorType.Player);
                if (actorType == ActorType.Player)
                    team = Names.Teams.GetValueOrDefault(actorName[..14], Team.Unknown);

                if (_actors.TryGetValue(actorAddr, out var actor))
                {
                    if (actor.ErrorCount > 60)
                    {
                        Program.Log($"Existing player '{actor.Base}' being reallocated due to excessive errors...");
                        actor = reallocateActor(actorAddr, team, actorType, nameId, actorName);
                    }
                    else
                    {
                        actor.Name = actorName;
                        actor.MissingCount = 0;
                    }
                }
                else
                {
                    actor = reallocateActor(actorAddr, team, actorType, nameId, actorName);
                }
                notUpdated.Remove(actorAddr);
            }

            foreach (var actorId in notUpdated)
            {
                if (_actors.TryGetValue(actorId, out var actor))
                {
                    actor.MissingCount++;
                    if (actor.MissingCount > 20)
                    {
                        _actors.TryRemove(actorId, out _);
                        //Program.Log($"Actor 0x{actorId:X} removed due to MissingCount > 20");
                    }
                }
            }

            //Program.Log($"UpdateList: Processed {count} actors, {notUpdated.Count} not updated");
        }
        catch (DMAShutdown)
        {
            throw;
        }
        catch (GameEnded)
        {
            throw;
        }
        catch (Exception ex)
        {
            Program.Log($"CRITICAL ERROR - UpdateList Loop FAILED: {ex}");
        }
        finally
        {
            this._regSw.Restart();
        }
        }

        private UActor reallocateActor(ulong actorBase, Team team, ActorType actorType, uint nameId, string name)
        {
            var newActor = new UActor(actorBase)
            {
                Team = team,
                ActorType = actorType,
                NameId = nameId,
                Name = name,
                MissingCount = 0
            };
            _actors[actorBase] = newActor;
            //Program.Log($"Actor reallocated: 0x{actorBase:X}, Name: {name}");
            return newActor;
        }

        public void UpdateAllPlayers()
        {
        try
        {
            var count = _actors.Count;
            if (count < 10)
                throw new GameEnded();

            var actorBases = _actors.Values.Select(actor => actor.Base).ToArray();
            var playerInfoScatterMap = new ScatterReadMap(count);
            var playerInstanceInfoRound = playerInfoScatterMap.AddRound();
            var instigatorAndRootRound = playerInfoScatterMap.AddRound();
            var teamInfoRound = playerInfoScatterMap.AddRound();
            var meshRound = playerInfoScatterMap.AddRound();
            var boneInfoRound = playerInfoScatterMap.AddRound();

            int[] boneIds = { 7, 6, 5, 3, 2, 65, 66, 67, 68, 92, 93, 94, 95, 130, 131, 132, 125, 126, 127 };

            for (int i = 0; i < count; i++)
            {
                var actorAddr = actorBases[i];
                var actorType = _actors[actorAddr].ActorType;
                var rootComponent = playerInstanceInfoRound.AddEntry<ulong>(i, 1, actorAddr + Offsets.Actor.RootComponent);

                if (actorType == ActorType.Player)
                {
                    playerInstanceInfoRound.AddEntry<float>(i, 2, actorAddr + Offsets.ASQSoldier.Health);
                    var pawnPlayerState = playerInstanceInfoRound.AddEntry<ulong>(i, 6, actorAddr + Offsets.Pawn.PlayerState);
                    var controller = playerInstanceInfoRound.AddEntry<ulong>(i, 7, actorAddr + Offsets.Pawn.Controller);
                    var controllerPlayerState = teamInfoRound.AddEntry<ulong>(i, 8, controller, null, Offsets.Controller.PlayerState);
                    teamInfoRound.AddEntry<int>(i, 9, pawnPlayerState, null, Offsets.ASQPlayerState.TeamID);
                    teamInfoRound.AddEntry<int>(i, 10, controllerPlayerState, null, Offsets.ASQPlayerState.TeamID);

                    var meshPtr = playerInstanceInfoRound.AddEntry<ulong>(i, 11, actorAddr + Offsets.ASQSoldier.Mesh);
                    meshRound.AddEntry<FTransform>(i, 12, meshPtr, null, 0x1C0); // ComponentToWorld
                    var boneArrayPtr = meshRound.AddEntry<ulong>(i, 13, meshPtr, null, 0x4B0);

                    for (int j = 0; j < boneIds.Length; j++)
                        boneInfoRound.AddEntry<FTransform>(i, 14 + j, boneArrayPtr, null, (uint)(boneIds[j] * 0x30));
                }
                else if (Names.Deployables.Contains(actorType))
                {
                    playerInstanceInfoRound.AddEntry<float>(i, 2, actorAddr + Offsets.SQDeployable.Health);
                    playerInstanceInfoRound.AddEntry<float>(i, 3, actorAddr + Offsets.SQDeployable.MaxHealth);
                }
                else
                {
                    playerInstanceInfoRound.AddEntry<float>(i, 2, actorAddr + Offsets.SQVehicle.Health);
                    playerInstanceInfoRound.AddEntry<float>(i, 3, actorAddr + Offsets.SQVehicle.MaxHealth);
                }

                instigatorAndRootRound.AddEntry<Vector3>(i, 4, rootComponent, null, Offsets.USceneComponent.RelativeLocation);
                instigatorAndRootRound.AddEntry<Vector3>(i, 5, rootComponent, null, Offsets.USceneComponent.RelativeRotation);
            }

            playerInfoScatterMap.Execute();

            bool updateSquads = (DateTime.Now - _lastSquadUpdate).TotalMilliseconds > SquadUpdateInterval;

            for (int i = 0; i < count; i++)
            {
                var actor = _actors[actorBases[i]];
                var results = playerInfoScatterMap.Results[i];
                float hp = 0;

                if (results.TryGetValue(2, out var healthResult) && healthResult.TryGetResult<float>(out hp))
                {
                    if (actor.ActorType == ActorType.Player && actor.Health > 0 && hp <= 0)
                    {
                        actor.DeathPosition = actor.Position;
                        actor.TimeOfDeath = DateTime.Now;
                    }
                    actor.Health = hp;
                }

                if (results.TryGetValue(3, out var maxHpResult) &&
                    maxHpResult.TryGetResult<float>(out var maxHp) &&
                    maxHp > 0)
                {
                    actor.Health = (hp / maxHp) * 100;
                }

                if (actor.ActorType == ActorType.Player)
                {
                    if (results.TryGetValue(9, out var pawnTeamResult) &&
                        pawnTeamResult.TryGetResult<int>(out var pawnTeamId))
                    {
                        actor.TeamID = pawnTeamId;
                    }
                    else if (results.TryGetValue(10, out var controllerTeamResult) &&
                             controllerTeamResult.TryGetResult<int>(out var controllerTeamId))
                    {
                        actor.TeamID = controllerTeamId;
                    }
                    else if (actor.TeamID == -1)
                    {
                        continue;
                    }

                    if (actor.IsFriendly())
                    {
                        if (_squadCache.TryGetValue(actor.Base, out var cachedSquadId))
                        {
                            actor.SquadID = cachedSquadId;
                        }
                        else if (updateSquads)
                        {
                            try
                            {
                                ulong playerState = 0;
                                if (results.TryGetValue(6, out var psResult))
                                    psResult.TryGetResult<ulong>(out playerState);

                                if (playerState != 0)
                                {
                                    var squadState = Memory.ReadPtr(playerState + Offsets.ASQPlayerState.SquadState);
                                    if (squadState != 0)
                                    {
                                        var squadId = Memory.ReadValue<int>(squadState + Offsets.ASQSquadState.SquadId);
                                        if (squadId > 0 && squadId < 1000)
                                        {
                                            actor.SquadID = squadId;
                                            _squadCache[actor.Base] = squadId;
                                        }
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                //Program.Log($"Squad update failed for actor 0x{actor.Base:X}: {ex}");
                            }
                        }
                    }

                    if (results.TryGetValue(11, out var meshResult) && meshResult.TryGetResult<ulong>(out var meshAddr))
                    {
                        actor.Mesh = meshAddr;
                        if (results.TryGetValue(12, out var ctwResult) && ctwResult.TryGetResult<FTransform>(out var ctw))
                        {
                            actor.ComponentToWorld = ctw;
                        }

                        if (results.TryGetValue(13, out var boneArrayResult) && boneArrayResult.TryGetResult<ulong>(out var boneArrayPtr))
                        {
                            actor.BoneTransforms.Clear();
                            var viewInfo = new MinimalViewInfo
                            {
                                Location = Memory._game.LocalPlayer.Position,
                                Rotation = Memory._game.LocalPlayer.Rotation3D,
                                FOV = Memory._game.CurrentFOV
                            };
                            if (actor.BoneScreenPositions == null || actor.BoneScreenPositions.Length != boneIds.Length)
                                actor.BoneScreenPositions = new Vector2[boneIds.Length];

                            for (int j = 0; j < boneIds.Length; j++)
                            {
                                if (results.TryGetValue(14 + j, out var boneResult) &&
                                    boneResult.TryGetResult<FTransform>(out var boneTransform))
                                {
                                    actor.BoneTransforms[boneIds[j]] = boneTransform;
                                    Vector3 boneWorldPos = TransformToWorld(boneTransform, actor.ComponentToWorld);
                                    actor.BoneScreenPositions[j] = Camera.WorldToScreen(viewInfo, boneWorldPos);
                                }
                                else
                                {
                                    actor.BoneScreenPositions[j] = Vector2.Zero;
                                }
                            }
                        }
                        else
                        {
                            if (actor.BoneScreenPositions == null || actor.BoneScreenPositions.Length != boneIds.Length)
                                actor.BoneScreenPositions = new Vector2[boneIds.Length];
                            Array.Clear(actor.BoneScreenPositions, 0, actor.BoneScreenPositions.Length);
                        }
                    }
                    else
                    {
                        actor.Mesh = 0;
                        if (actor.BoneScreenPositions == null || actor.BoneScreenPositions.Length != boneIds.Length)
                            actor.BoneScreenPositions = new Vector2[boneIds.Length];
                        Array.Clear(actor.BoneScreenPositions, 0, actor.BoneScreenPositions.Length);
                    }
                }

                if (results.TryGetValue(4, out var locResult) &&
                    locResult.TryGetResult<Vector3>(out var location))
                {
                    actor.Position = location;
                }

                if (results.TryGetValue(5, out var rotResult) &&
                    rotResult.TryGetResult<Vector3>(out var rotation))
                {
                    actor.Rotation = new Vector2(rotation.Y, rotation.X);
                    actor.Rotation3D = rotation;
                }
                //UpdatePlayerArrayActors();
            }

            if (updateSquads)
            {
                _lastSquadUpdate = DateTime.Now;
                _squadCache = _squadCache.Where(kv => _actors.ContainsKey(kv.Key))
                                         .ToDictionary(kv => kv.Key, kv => kv.Value);
            }

            //Program.Log($"UpdateAllPlayers: Updated {count} actors");
        }
        catch (GameEnded)
        {
            throw;
        }
        catch (Exception ex)
        {
            Program.Log($"CRITICAL ERROR - UpdateAllPlayers Loop FAILED: {ex}");
        }
        }

        private Vector3 TransformToWorld(FTransform boneTransform, FTransform componentToWorld)
        {
            boneTransform.Scale3D = new Vector3(1, 1, 1);
            componentToWorld.Scale3D = new Vector3(1, 1, 1);
            Matrix4x4 boneMatrix = boneTransform.ToMatrix();
            Matrix4x4 worldMatrix = componentToWorld.ToMatrix();
            Matrix4x4 finalMatrix = boneMatrix * worldMatrix;
            return new Vector3(finalMatrix.M41, finalMatrix.M42, finalMatrix.M43);
        }

        /* public void UpdatePlayerArrayActors()
        {
            try
            {
                if (!Memory.Ready || Memory._squadBase == 0)
                {
                    Program.Log("Memory not ready or Squad base not found.");
                    return;
                }

                ulong gWorldPtrAddr = Memory._squadBase + GameObjects.GWorld;
                ulong gWorldAddr = Memory.ReadPtr(gWorldPtrAddr);
                if (gWorldAddr == 0)
                {
                    Program.Log("Failed to read GWorld pointer.");
                    return;
                }

                ulong gameStateAddr = Memory.ReadPtr(gWorldAddr + World.GameState);
                if (gameStateAddr == 0)
                {
                    Program.Log("Failed to read GameState pointer.");
                    return;
                }

                ulong playerArrayAddr = gameStateAddr + AGameStateBase.PlayerArray;
                ulong playerArrayData = Memory.ReadPtr(playerArrayAddr);
                int playerArraySize = Memory.ReadValue<int>(playerArrayAddr + 0x8);
                if (playerArraySize <= 0)
                {
                    Program.Log("Invalid PlayerArray size read.");
                    return;
                }

                Program.Log($"Found {playerArraySize} players in PlayerArray.");

                // Set up scatter read
                var scatterMap = new ScatterReadMap(playerArraySize);
                var playerStateRound = scatterMap.AddRound();           // Read PlayerState addresses
                var rootComponentRound = scatterMap.AddRound();        // Read RootComponent from PlayerState
                var positionRound = scatterMap.AddRound();             // Read position from RootComponent

                // Define offsets (adjust these based on your game)
                var offsets = new { RootComponent = 0x138, RelativeLocation = 0x11C };

                // Build scatter read entries
                for (int i = 0; i < playerArraySize; i++)
                {
                    var playerStateAddr = playerStateRound.AddEntry<ulong>(i, 0, playerArrayData + (uint)(i * 0x8));
                    var rootComponentAddr = rootComponentRound.AddEntry<ulong>(i, 1, playerStateAddr, null, Offsets.Actor.RootComponent);
                    positionRound.AddEntry<Vector3>(i, 2, rootComponentAddr, null, Offsets.USceneComponent.RelativeLocation);
                }

                // Execute the scatter read
                scatterMap.Execute();

                // Process results
                for (int i = 0; i < playerArraySize; i++)
                {
                    // Get PlayerState address
                    if (!scatterMap.Results[i][0].TryGetResult<ulong>(out var playerStateAddr) || playerStateAddr == 0)
                    {
                        Console.WriteLine($"Player {i}: Invalid PlayerState address.");
                        continue;
                    }

                    // Get RootComponent from PlayerState
                    if (!scatterMap.Results[i][1].TryGetResult<ulong>(out var rootComponentAddr) || rootComponentAddr == 0)
                    {
                        Console.WriteLine($"Player {i}: No valid RootComponent in PlayerState.");
                        continue; // Skip or handle as needed
                    }

                    // Get position from RootComponent
                    if (scatterMap.Results[i][2].TryGetResult<Vector3>(out var position))
                    {
                        Console.WriteLine($"Player {i}: Position = ({position.X}, {position.Y}, {position.Z})");
                    }
                    else
                    {
                        Console.WriteLine($"Player {i}: Failed to read position from RootComponent.");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
        } */
        #endregion
    }
}