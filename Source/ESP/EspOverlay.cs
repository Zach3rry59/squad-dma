using SharpDX;
using SharpDX.Direct2D1;
using SharpDX.Mathematics.Interop;
using System.Runtime.InteropServices;
using System.Numerics;
using SharpDX.DirectWrite;
using System.Diagnostics;

namespace squad_dma
{
    public struct FTransform
    {
        public Quaternion Rotation;
        public Vector3 Translation;
        public Vector3 Scale3D;

        public Matrix4x4 ToMatrix()
        {
            return Matrix4x4.CreateFromQuaternion(Rotation) *
                   Matrix4x4.CreateScale(Scale3D) *
                   Matrix4x4.CreateTranslation(Translation);
        }
    }

    public static class Vector2Extensions
    {
        public static RawVector2 ToRawVector2(this Vector2 vector)
        {
            return new RawVector2(vector.X, vector.Y);
        }
    }

    public class EspOverlay : Form
    {
        private WindowRenderTarget renderTarget;
        private SolidColorBrush brush;
        private SolidColorBrush vehicleBrush;
        private SolidColorBrush boneBrush;
        private SolidColorBrush healthBrush;
        private SharpDX.DirectWrite.TextFormat textFormat;
        private bool running = true;
        private Game Game => Memory._game;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        private const int GWL_EXSTYLE = -20;

        private static readonly Dictionary<ActorType, string> ActorTypeNames = new Dictionary<ActorType, string>
        {
            { ActorType.FOBRadio, "FOB Radio" },
            { ActorType.Hab, "Hab" },
            { ActorType.AntiAir, "Anti-Air" },
            { ActorType.APC, "APC" },
            { ActorType.AttackHelicopter, "Attack Helicopter" },
            { ActorType.LoachCAS, "Loach CAS" },
            { ActorType.LoachScout, "Loach Scout" },
            { ActorType.Boat, "Boat" },
            { ActorType.BoatLogistics, "Boat Logistics" },
            { ActorType.DeployableAntiAir, "Deployable Anti-Air" },
            { ActorType.DeployableAntitank, "Deployable Antitank" },
            { ActorType.DeployableAntitankGun, "Deployable Antitank Gun" },
            { ActorType.DeployableGMG, "Deployable GMG" },
            { ActorType.DeployableHellCannon, "Deployable Hell Cannon" },
            { ActorType.DeployableHMG, "Deployable HMG" },
            { ActorType.DeployableMortars, "Deployable Mortars" },
            { ActorType.DeployableRockets, "Deployable Rockets" },
            { ActorType.Drone, "Drone" },
            { ActorType.IFV, "IFV" },
            { ActorType.JeepAntiAir, "Jeep Anti-Air" },
            { ActorType.JeepAntitank, "Jeep Antitank" },
            { ActorType.JeepArtillery, "Jeep Artillery" },
            { ActorType.JeepLogistics, "Jeep Logistics" },
            { ActorType.JeepTransport, "Jeep Transport" },
            { ActorType.JeepRWSTurret, "Jeep RWS Turret" },
            { ActorType.JeepTurret, "Jeep Turret" },
            { ActorType.Mine, "Mine" },
            { ActorType.Motorcycle, "Motorcycle" },
            { ActorType.RallyPoint, "Rally Point" },
            { ActorType.Tank, "Tank" },
            { ActorType.TankMGS, "Tank MGS" },
            { ActorType.TrackedAPC, "Tracked APC" },
            { ActorType.TrackedLogistics, "Tracked Logistics" },
            { ActorType.TrackedAPCArtillery, "Tracked APC Artillery" },
            { ActorType.TrackedIFV, "Tracked IFV" },
            { ActorType.TrackedJeep, "Tracked Jeep" },
            { ActorType.TransportHelicopter, "Transport Helicopter" },
            { ActorType.TruckAntiAir, "Truck Anti-Air" },
            { ActorType.TruckArtillery, "Truck Artillery" },
            { ActorType.TruckLogistics, "Truck Logistics" },
            { ActorType.TruckTransport, "Truck Transport" },
            { ActorType.TruckTransportArmed, "Truck Transport Armed" }
        };

        private static readonly HashSet<ActorType> VehicleTypes = new HashSet<ActorType>
        {
            ActorType.APC, ActorType.AttackHelicopter, ActorType.Boat, ActorType.BoatLogistics,
            ActorType.IFV, ActorType.JeepAntiAir, ActorType.JeepAntitank, ActorType.JeepArtillery,
            ActorType.JeepLogistics, ActorType.JeepTransport, ActorType.JeepRWSTurret, ActorType.JeepTurret,
            ActorType.LoachCAS, ActorType.LoachScout, ActorType.Motorcycle, ActorType.Tank, ActorType.TankMGS,
            ActorType.TrackedAPC, ActorType.TrackedLogistics, ActorType.TrackedAPCArtillery, ActorType.TrackedIFV,
            ActorType.TrackedJeep, ActorType.TransportHelicopter, ActorType.TruckAntiAir, ActorType.TruckArtillery,
            ActorType.TruckLogistics, ActorType.TruckTransport, ActorType.TruckTransportArmed
        };

        private static readonly List<(UActor actor, Vector2 screenPos, float distance)> visibleActors = new List<(UActor, Vector2, float)>();

        public EspOverlay()
        {
            FormBorderStyle = FormBorderStyle.None;
            TopMost = true;
            ShowInTaskbar = false;
            Width = Screen.PrimaryScreen.Bounds.Width;
            Height = Screen.PrimaryScreen.Bounds.Height;
            Location = new System.Drawing.Point(0, 0);
            BackColor = System.Drawing.Color.Black;

            int exStyle = GetWindowLong(Handle, GWL_EXSTYLE);
            SetWindowLong(Handle, GWL_EXSTYLE, exStyle);

            InitializeDirect2D();
            StartRenderLoop();
        }

        private void InitializeDirect2D()
        {
            var factory = new SharpDX.Direct2D1.Factory();
            var renderProperties = new HwndRenderTargetProperties
            {
                Hwnd = Handle,
                PixelSize = new Size2(Width, Height),
                PresentOptions = PresentOptions.Immediately
            };
            renderTarget = new WindowRenderTarget(factory, new RenderTargetProperties(), renderProperties);
            brush = new SolidColorBrush(renderTarget, new RawColor4(
                Program.Config.EspTextColor.R / 255f,
                Program.Config.EspTextColor.G / 255f,
                Program.Config.EspTextColor.B / 255f,
                Program.Config.EspTextColor.A / 255f
            ));
            vehicleBrush = new SolidColorBrush(renderTarget, new RawColor4(1.0f, 0.0f, 0.0f, 1.0f));
            boneBrush = brush;
            healthBrush = new SolidColorBrush(renderTarget, new RawColor4(0.0f, 1.0f, 0.0f, 1.0f));
            textFormat = new SharpDX.DirectWrite.TextFormat(new SharpDX.DirectWrite.Factory(), "Verdana", Program.Config.ESPFontSize);
        }

        private void StartRenderLoop()
        {
            Thread renderThread = new Thread(() =>
            {
                Program.Log("Render thread started.");
                bool wasReadyLastFrame = false;
                var stopwatch = Stopwatch.StartNew();
                while (running)
                {
                    stopwatch.Restart();
                    var memoryStart = Stopwatch.StartNew();
                    bool isMemoryReady = Memory.Ready;
                    //Program.Log($"Memory.Ready time: {memoryStart.ElapsedMilliseconds}ms"); //performance
                    if (!isMemoryReady)
                    {
                        renderTarget.BeginDraw();
                        renderTarget.Clear(new RawColor4(0, 0, 0, 1));
                        renderTarget.EndDraw();
                        this.Invalidate();
                        Thread.Sleep(wasReadyLastFrame ? 500 : 1500);
                        wasReadyLastFrame = false;
                        continue;
                    }

                    RenderFrame();
                    int elapsedMs = (int)stopwatch.ElapsedMilliseconds;
                    int targetMs = 8; // 16 = ~60 FPS 6 = 144 FPS
                    int sleepMs = Math.Max(1, targetMs - elapsedMs);
                    //Program.Log($"Frame time: {elapsedMs}ms, Sleeping: {sleepMs}ms"); //performance
                    Thread.Sleep(sleepMs);
                    wasReadyLastFrame = true;
                }
                Program.Log("Render thread stopped.");
            });
            renderThread.Priority = ThreadPriority.AboveNormal;
            renderThread.Start();
        }

        private bool IsReadyToRender()
        {
            bool inGame = Game?.InGame ?? false;
            bool localPlayerExists = Game?.LocalPlayer != null;
            bool actorsExist = Game?.Actors != null && Game.Actors.Count > 0;
            return inGame && localPlayerExists && actorsExist;
        }

        private void RenderFrame()
        {
            var frameStart = Stopwatch.StartNew();
            renderTarget.BeginDraw();
            renderTarget.Clear(new RawColor4(0, 0, 0, 0));

            Dictionary<ulong, UActor> actorsCopy;
            if (!IsReadyToRender())
            {
                renderTarget.EndDraw();
                this.Invalidate();
                return;
            }

            var copyStart = Stopwatch.StartNew();
            actorsCopy = new Dictionary<ulong, UActor>(Game.Actors);
            //Program.Log($"Actor copy time: {copyStart.ElapsedMilliseconds}ms"); //performance

            DrawEsp(actorsCopy);
            renderTarget.EndDraw();
            //Program.Log($"Total frame render time: {frameStart.ElapsedMilliseconds}ms"); //performance
        }

        private void DrawEsp(Dictionary<ulong, UActor> actors)
        {
            var espStart = Stopwatch.StartNew();
            if (Game == null || Game.LocalPlayer == null || actors == null || actors.Count < 1)
            {
                Program.Log("DrawEsp: Game, LocalPlayer, or actors not initialized.");
                return;
            }

            var viewInfoStart = Stopwatch.StartNew();
            var viewInfo = new MinimalViewInfo
            {
                Location = Game.LocalPlayer.Position,
                Rotation = Game.LocalPlayer.Rotation3D,
                FOV = Game.CurrentFOV
            };
            //Program.Log($"ViewInfo fetch time: {viewInfoStart.ElapsedMilliseconds}ms"); //performance

            Vector3 camPos = viewInfo.Location;
            float maxDistance = Program.Config.EspMaxDistance;
            float vehicleMaxDistance = maxDistance + 1000f;
            bool showAllies = Program.Config.EspShowAllies;
            var playerColor = brush.Color;

            visibleActors.Clear();
            long totalWtsTime = 0;
            int wtsCalls = 0;
            foreach (var actor in actors.Values)
            {
                if (actor == null || actor.Position == Vector3.Zero || !actor.IsAlive)
                    continue;

                float distance = Vector3.Distance(camPos, actor.Position) / 100f;
                bool isPlayer = actor.ActorType == ActorType.Player;
                if (distance > (isPlayer ? maxDistance : vehicleMaxDistance))
                    continue;

                if (isPlayer && Vector3.Distance(Memory.LocalPlayer.Position, actor.Position) < 1.0f)
                    continue;

                if (!showAllies && actor.IsFriendly())
                    continue;

                var wtsStart = Stopwatch.StartNew();
                Vector2 screenPos = Camera.WorldToScreen(viewInfo, actor.Position);
                totalWtsTime += wtsStart.ElapsedMilliseconds;
                wtsCalls++;
                if (screenPos == Vector2.Zero)
                    continue;

                visibleActors.Add((actor, screenPos, distance));
            }
            if (wtsCalls > 0)
                //Program.Log($"WorldToScreen total time: {totalWtsTime}ms, Calls: {wtsCalls}, Avg: {totalWtsTime / wtsCalls}ms"); //performance

                foreach (var (actor, screenPos, distance) in visibleActors)
                {
                    if (actor.ActorType == ActorType.Player)
                    {
                        if (Program.Config.EspBones && actor.BoneScreenPositions != null)
                        {
                            DrawBoneLines(actor.BoneScreenPositions);
                            RawRectangleF boxRect = GetBoxFromBones(actor.BoneScreenPositions);
                            if (boxRect.Left != 0 || boxRect.Top != 0 || boxRect.Right != 0 || boxRect.Bottom != 0)
                            {
                                if (Program.Config.EspShowBox)
                                    renderTarget.DrawRectangle(boxRect, boneBrush);

                                string nameText = GetNameText(actor);
                                RawRectangleF nameRect = new RawRectangleF(boxRect.Left, boxRect.Top - 20f, boxRect.Left + 200f, boxRect.Top);
                                brush.Color = playerColor;
                                renderTarget.DrawText(nameText, textFormat, nameRect, brush);

                                string distanceText = Program.Config.EspShowDistance ? $"[{(int)distance}m]" : "";
                                RawRectangleF distanceRect = new RawRectangleF(boxRect.Left, boxRect.Bottom, boxRect.Left + 200f, boxRect.Bottom + 20f);
                                renderTarget.DrawText(distanceText, textFormat, distanceRect, brush);

                                if (Program.Config.EspShowHealth && actor.Health >= 0)
                                    DrawHealthBar(boxRect, actor.Health);
                            }
                        }
                    }
                    else if (IsVehicle(actor))
                    {
                        DrawVehicleBox(actor, screenPos, distance);
                    }
                }
            //Program.Log($"DrawEsp time: {espStart.ElapsedMilliseconds}ms, Actors processed: {visibleActors.Count}"); //performance
        }

        private string GetEspText(UActor actor, float distance)
        {
            string name = actor.ActorType == ActorType.Player
                ? (Program.Config.ShowNames ? actor.Name : "")
                : (ActorTypeNames.TryGetValue(actor.ActorType, out var typeName) ? typeName : "");
            string wdistance = Program.Config.EspShowDistance ? $"[{(int)distance}m]" : "";
            string whealth = Program.Config.EspShowHealth && actor.Health >= 0 ? $"[{(int)actor.Health}❤]" : "";
            return $"{name}{(string.IsNullOrEmpty(name) ? "" : " ")}{wdistance}{(string.IsNullOrEmpty(wdistance) ? "" : " ")}{whealth}";
        }

        private bool IsVehicle(UActor actor)
        {
            return VehicleTypes.Contains(actor.ActorType);
        }

        private void DrawVehicleBox(UActor actor, Vector2 screenPos, float distance)
        {
            bool isEnemy = actor.TeamID != -1 && actor.TeamID != Memory.LocalPlayer.TeamID;
            bool isFriendly = actor.TeamID != -1 && actor.TeamID == Memory.LocalPlayer.TeamID;
            bool isUnclaimed = actor.TeamID == -1;

            if (!Program.Config.EspShowAllies && !isEnemy)
                return;

            vehicleBrush.Color = isUnclaimed ? new RawColor4(1.0f, 1.0f, 0.0f, 1.0f) :
                                isEnemy ? new RawColor4(1.0f, 0.5f, 0.5f, 1.0f) :
                                new RawColor4(0.0f, 1.0f, 0.0f, 1.0f);

            float boxSize = 500f / (distance + 1f);
            float halfSize = boxSize / 2f;
            RawRectangleF vehicleRect = new RawRectangleF(
                screenPos.X - halfSize, screenPos.Y - halfSize,
                screenPos.X + halfSize, screenPos.Y + halfSize
            );

            renderTarget.DrawRectangle(vehicleRect, vehicleBrush);

            string vehicleText = GetEspText(actor, distance);
            renderTarget.DrawText(vehicleText, textFormat, new RawRectangleF(
                screenPos.X - 50, screenPos.Y - halfSize - 20, screenPos.X + 50, screenPos.Y - halfSize), vehicleBrush);
        }

        private void DrawBoneLines(Vector2[] screenPositions)
        {
            DrawLine(screenPositions[0], screenPositions[1], boneBrush); // Head -> Neck
            DrawLine(screenPositions[1], screenPositions[2], boneBrush); // Neck -> Torso
            DrawLine(screenPositions[2], screenPositions[3], boneBrush); // Torso -> Spine
            DrawLine(screenPositions[3], screenPositions[4], boneBrush); // Spine -> Pelvis
            DrawLine(screenPositions[2], screenPositions[5], boneBrush); // Torso -> Right arm
            DrawLine(screenPositions[5], screenPositions[6], boneBrush);
            DrawLine(screenPositions[6], screenPositions[7], boneBrush);
            DrawLine(screenPositions[7], screenPositions[8], boneBrush);
            DrawLine(screenPositions[2], screenPositions[9], boneBrush); // Torso -> Left arm
            DrawLine(screenPositions[9], screenPositions[10], boneBrush);
            DrawLine(screenPositions[10], screenPositions[11], boneBrush);
            DrawLine(screenPositions[11], screenPositions[12], boneBrush);
            DrawLine(screenPositions[4], screenPositions[13], boneBrush); // Pelvis -> Right leg
            DrawLine(screenPositions[13], screenPositions[14], boneBrush);
            DrawLine(screenPositions[14], screenPositions[15], boneBrush);
            DrawLine(screenPositions[4], screenPositions[16], boneBrush); // Pelvis -> Left leg
            DrawLine(screenPositions[16], screenPositions[17], boneBrush);
            DrawLine(screenPositions[17], screenPositions[18], boneBrush);
        }

        private RawRectangleF GetBoxFromBones(Vector2[] screenPositions)
        {
            if (screenPositions == null || screenPositions.Length < 19)
                return new RawRectangleF(0, 0, 0, 0);

            Vector2 head = screenPositions[0];
            Vector2 rightFoot = screenPositions[15];
            Vector2 leftFoot = screenPositions[18];

            if (head == Vector2.Zero || rightFoot == Vector2.Zero || leftFoot == Vector2.Zero)
                return new RawRectangleF(0, 0, 0, 0);

            float topY = head.Y;
            float bottomY = Math.Max(rightFoot.Y, leftFoot.Y);
            float leftX = Math.Min(head.X, Math.Min(rightFoot.X, leftFoot.X));
            float rightX = Math.Max(head.X, Math.Max(rightFoot.X, leftFoot.X));

            const float padding = 6f;
            return new RawRectangleF(leftX - padding, topY - padding, rightX + padding, bottomY + padding);
        }

        private void DrawHealthBar(RawRectangleF boxRect, float health)
        {
            if (health < 0) return;

            const float barWidth = 5f;
            float barHeight = boxRect.Bottom - boxRect.Top;
            float healthHeight = (health / 100f) * barHeight;
            float barX = boxRect.Right + 2f;
            float barY = boxRect.Top + (barHeight - healthHeight);

            healthBrush.Color = new RawColor4(1.0f - (health / 100f), health / 100f, 0.0f, 1.0f);
            renderTarget.FillRectangle(new RawRectangleF(barX, barY, barX + barWidth, barY + healthHeight), healthBrush);
            renderTarget.DrawRectangle(new RawRectangleF(barX, boxRect.Top, barX + barWidth, boxRect.Bottom), boneBrush);
        }

        private string GetNameText(UActor actor)
        {
            return actor.ActorType == ActorType.Player
                ? (Program.Config.ShowNames ? actor.Name : "")
                : (ActorTypeNames.TryGetValue(actor.ActorType, out var typeName) ? typeName : "");
        }

        private void DrawLine(Vector2 start, Vector2 end, SolidColorBrush lineBrush)
        {
            if (start != Vector2.Zero && end != Vector2.Zero)
                renderTarget.DrawLine(start.ToRawVector2(), end.ToRawVector2(), lineBrush);
        }

        protected override void OnClosed(EventArgs e)
        {
            running = false;
            brush.Dispose();
            vehicleBrush.Dispose();
            boneBrush.Dispose();
            healthBrush.Dispose();
            textFormat.Dispose();
            renderTarget.Dispose();
            base.OnClosed(e);
        }
    }
}