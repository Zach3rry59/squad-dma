using Offsets;
using SkiaSharp;
using System.Diagnostics;
using System.Numerics;

namespace squad_dma
{
    /// <summary>
    /// Class containing Game Player Data.
    /// </summary>
    public class UActor
    {
        private readonly object _posLock = new();

        #region PlayerProperties
        public uint NameId { get; set; }
        public string Name { get; set; } // Class name ("BP_Soldier_UAF")
        public string Nickname { get; set; } // In-game player name ("Player123")
        public float Health { get; set; } = -1;
        public int TeamID { get; set; } = -1;
        public int SquadID { get; set; } = -1;
        public List<UActor> MySquadMembers { get; } = new List<UActor>();
        public Team Team { get; set; } = Team.Unknown;
        public int MissingCount { get; set; } = 0;
        public ulong Mesh { get; set; }
        public FTransform ComponentToWorld { get; set; }
        public Dictionary<int, FTransform> BoneTransforms { get; set; } = new Dictionary<int, FTransform>();
        public Vector2[] BoneScreenPositions { get; set; }
        public bool NeedsFullUpdate { get; set; } = true;

        public bool IsFriendly()
        {
            if (TeamID == -1) return false; 

            var localPlayer = Memory.LocalPlayer;
            if (localPlayer == null) return false;

            if (localPlayer.TeamID != -1 && this.TeamID != -1)
                return localPlayer.TeamID == this.TeamID;

            return localPlayer.Team == this.Team;
        }

        public bool IsInMySquad()
        {
            return IsFriendly() &&
                   SquadID != -1 &&
                   SquadID == Memory.LocalPlayer?.SquadID;
        }

        public bool bInThirdPersonView { get; set; } = false;
        public Vector3 CameraOffset { get; set; } = new Vector3(0, -300, 50); // Default third-person offset
        public float CameraDistance { get; set; } = 300.0f;

        // Add this method to control third-person view
        public void UpdateThirdPersonView(ulong pawnPtr)
        {
            try
            {
                if (pawnPtr == 0) return;

                // Read the current third-person status from memory
                bInThirdPersonView = Memory.ReadValue<bool>(pawnPtr + 0x1654);

                // If in third-person, read camera settings
                if (bInThirdPersonView)
                {
                    CameraOffset = Memory.ReadValue<Vector3>(pawnPtr + 0x21D0);
                    CameraDistance = Memory.ReadValue<float>(pawnPtr + 0x21DC);
                }
            }
            catch { /* Silently handle errors */ }
        }

        public ActorType ActorType { get; set; } = ActorType.Player;
        private Vector3 _pos = new Vector3(0, 0, 0);
        public Vector3 Position
        {
            get
            {
                lock (_posLock)
                {
                    return _pos;
                }
            }
            set
            {
                lock (_posLock)
                {
                    _pos = value;
                }
            }
        }
        public Vector2 ZoomedPosition { get; set; } = new();
        public Vector2 Rotation { get; set; } = new Vector2(0, 0);
        public Vector3 Rotation3D { get; set; } = new Vector3(0, 0, 0);
        public int ErrorCount { get; set; } = 0;

        public Vector3 DeathPosition { get; set; } = Vector3.Zero; 
        public DateTime TimeOfDeath { get; set; } = DateTime.MinValue;

        #endregion

        #region Getters
        public ulong Base { get; }
        public bool IsAlive => Health > 0.0;
        #endregion

        #region Constructor
        public UActor(ulong actorBase)
        {
            Debug.WriteLine("Actor Constructor: Initialization started.");
            this.Base = actorBase;
            this.BoneScreenPositions = new Vector2[19];
            this.Mesh = Memory.ReadValue<ulong>(actorBase + ASQSoldier.Mesh); // Offset Mesh
            //Program.Log($"Actor Mesh initialized: {this.Mesh:X}");
        }
        #endregion
    }
}
