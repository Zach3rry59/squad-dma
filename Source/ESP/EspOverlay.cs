using SharpDX;
using SharpDX.Direct2D1;
using SharpDX.Mathematics.Interop;
using System.Runtime.InteropServices;
using System.Numerics;
using SharpDX.DirectWrite;

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

        // HashSet for vehicle types (excluding deployables like FOBRadio, Hab, etc.)
        private static readonly HashSet<ActorType> VehicleTypes = new HashSet<ActorType>
        {
            ActorType.APC,
            ActorType.AttackHelicopter,
            ActorType.Boat,
            ActorType.BoatLogistics,
            ActorType.IFV,
            ActorType.JeepAntiAir,
            ActorType.JeepAntitank,
            ActorType.JeepArtillery,
            ActorType.JeepLogistics,
            ActorType.JeepTransport,
            ActorType.JeepRWSTurret,
            ActorType.JeepTurret,
            ActorType.LoachCAS,
            ActorType.LoachScout,
            ActorType.Motorcycle,
            ActorType.Tank,
            ActorType.TankMGS,
            ActorType.TrackedAPC,
            ActorType.TrackedLogistics,
            ActorType.TrackedAPCArtillery,
            ActorType.TrackedIFV,
            ActorType.TrackedJeep,
            ActorType.TransportHelicopter,
            ActorType.TruckAntiAir,
            ActorType.TruckArtillery,
            ActorType.TruckLogistics,
            ActorType.TruckTransport,
            ActorType.TruckTransportArmed
        };


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
                while (running)
                {
                    if (!Memory.Ready)
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
                    Thread.Sleep(8); //  16 = ~60 FPS
                    wasReadyLastFrame = true;
                }
                Program.Log("Render thread stopped.");
            });
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
            renderTarget.BeginDraw();
            renderTarget.Clear(new RawColor4(0, 0, 0, 0));

            Dictionary<ulong, UActor> actorsCopy;

                if (!IsReadyToRender())
                {
                    renderTarget.EndDraw();
                    this.Invalidate();
                    return;
                }
                actorsCopy = new Dictionary<ulong, UActor>(Game.Actors);

            DrawEsp(actorsCopy);
            renderTarget.EndDraw();
        }

        private void DrawEsp(Dictionary<ulong, UActor> actors)
        {
            if (Game == null || Game.LocalPlayer == null || actors == null || actors.Count < 1)
            {
                Program.Log("DrawEsp: Game, LocalPlayer, or actors not initialized.");
                return;
            }

            var viewInfo = new MinimalViewInfo
            {
                Location = Game.LocalPlayer.Position,
                Rotation = Game.LocalPlayer.Rotation3D,
                FOV = Game.CurrentFOV
            };

            Vector3 camPos = viewInfo.Location;

            float maxDistance = Program.Config.EspMaxDistance;
            float vehicleMaxDistance = maxDistance + 1000f;
            bool showAllies = Program.Config.EspShowAllies;
            var playerColor = brush.Color;

            var visibleActors = new List<(UActor actor, Vector2 screenPos, float distance)>(actors.Count);
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

                Vector2 screenPos = Camera.WorldToScreen(viewInfo, actor.Position);
                if (screenPos == Vector2.Zero)
                    continue;

                visibleActors.Add((actor, screenPos, distance));
            }

            foreach (var (actor, screenPos, distance) in visibleActors)
            {
                if (actor.ActorType == ActorType.Player)
                {
                    if (Program.Config.EspBones && actor.BoneScreenPositions != null)
                    {
                        // Always draw the bone lines if enabled
                        DrawBoneLines(actor.BoneScreenPositions);

                        // Calculate the box dimensions (even if we don't draw it)
                        RawRectangleF boxRect = GetBoxFromBones(actor.BoneScreenPositions);
                        if (boxRect.Left != 0 || boxRect.Top != 0 || boxRect.Right != 0 || boxRect.Bottom != 0) // Check if the box is valid
                        {
                            // Draw the box only if EspShowBox is enabled
                            if (Program.Config.EspShowBox)
                            {
                                renderTarget.DrawRectangle(boxRect, boneBrush);
                            }

                            // Draw the name above the box area
                            string nameText = GetNameText(actor);
                            RawRectangleF nameRect = new RawRectangleF(boxRect.Left, boxRect.Top - 20f, boxRect.Left + 200f, boxRect.Top);
                            brush.Color = playerColor;
                            renderTarget.DrawText(nameText, textFormat, nameRect, brush);

                            // Draw the distance below the box area
                            string distanceText = Program.Config.EspShowDistance ? $"[{(int)distance}m]" : "";
                            RawRectangleF distanceRect = new RawRectangleF(boxRect.Left, boxRect.Bottom, boxRect.Left + 200f, boxRect.Bottom + 20f);
                            renderTarget.DrawText(distanceText, textFormat, distanceRect, brush);

                            // Draw the health bar on the right side of the box area
                            if (Program.Config.EspShowHealth && actor.Health >= 0)
                            {
                                DrawHealthBar(boxRect, actor.Health);
                            }
                        }
                    }
                }
                else if (IsVehicle(actor))
                {
                    DrawVehicleBox(actor, screenPos, distance);
                }
            }
        }

        private string GetEspText(UActor actor, float distance)
        {
            string name;
            if (actor.ActorType == ActorType.Player)
            {
                name = Program.Config.ShowNames ? actor.Name : "";
            }
            else
            {
                name = ActorTypeNames.TryGetValue(actor.ActorType, out var typeName)
                    ? typeName
                    : "";
            }
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

            vehicleBrush.Color = isUnclaimed ? new RawColor4(1.0f, 1.0f, 0.0f, 1.0f) : // Yellow
                                isEnemy ? new RawColor4(1.0f, 0.5f, 0.5f, 1.0f) :       // Light red
                                new RawColor4(0.0f, 1.0f, 0.0f, 1.0f);                  // Green

            float boxSize = 500f / (distance + 1f);
            float halfSize = boxSize / 2f;
            RawRectangleF vehicleRect = new RawRectangleF(
                screenPos.X - halfSize,
                screenPos.Y - halfSize,
                screenPos.X + halfSize,
                screenPos.Y + halfSize
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

            Vector2 head = screenPositions[0]; // Head position
            Vector2 rightFoot = screenPositions[15]; // Right foot
            Vector2 leftFoot = screenPositions[18]; // Left foot

            if (head == Vector2.Zero || rightFoot == Vector2.Zero || leftFoot == Vector2.Zero)
                return new RawRectangleF(0, 0, 0, 0);

            // Determine the top and bottom Y-coordinates
            float topY = head.Y;
            float bottomY = Math.Max(rightFoot.Y, leftFoot.Y); 

            // Determine the left and right X-coordinates
            float leftX = Math.Min(head.X, Math.Min(rightFoot.X, leftFoot.X));
            float rightX = Math.Max(head.X, Math.Max(rightFoot.X, leftFoot.X));

            // Add some padding to the box
            const float padding = 6f;
            leftX -= padding;
            rightX += padding;
            topY -= padding;
            bottomY += padding;

            return new RawRectangleF(leftX, topY, rightX, bottomY);
        }

        private void DrawHealthBar(RawRectangleF boxRect, float health)
        {
            if (health < 0) return;

            // Health bar dimensions
            const float barWidth = 5f;
            float barHeight = boxRect.Bottom - boxRect.Top; // Height of the box
            float healthHeight = (health / 100f) * barHeight; // Scale height based on health (0-100)

            // Position the health bar on the right side of the box
            float barX = boxRect.Right + 2f; // Small offset from the box
            float barY = boxRect.Top + (barHeight - healthHeight); 

            // Adjust the color based on health (green to red)
            float red = 1.0f - (health / 100f); // More red as health decreases
            float green = health / 100f; // More green as health increases
            healthBrush.Color = new RawColor4(red, green, 0.0f, 1.0f);

            // Draw the health bar
            renderTarget.FillRectangle(new RawRectangleF(barX, barY, barX + barWidth, barY + healthHeight), healthBrush);

            // Draw a border around the health bar
            renderTarget.DrawRectangle(new RawRectangleF(barX, boxRect.Top, barX + barWidth, boxRect.Bottom), boneBrush);
        }

        private string GetNameText(UActor actor)
        {
            string name;
            if (actor.ActorType == ActorType.Player)
            {
                name = Program.Config.ShowNames ? actor.Name : "";
            }
            else
            {
                name = ActorTypeNames.TryGetValue(actor.ActorType, out var typeName)
                    ? typeName
                    : "";
            }
            return name;
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