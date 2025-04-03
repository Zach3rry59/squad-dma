using SharpDX;
using SharpDX.Direct2D1;
using SharpDX.Mathematics.Interop;
using System.Runtime.InteropServices;
using System.Numerics;

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
        private bool running = true;
        private Game Game => Memory._game;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        private const int GWL_EXSTYLE = -20;

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
        }

        private void StartRenderLoop()
        {
            Thread renderThread = new Thread(() =>
            {
                Program.Log("Render thread started.");
                bool wasReadyLastFrame = false; 
                while (running)
                {
                    if (Game == null)
                    {
                        Program.Log("Game instance not yet initialized, waiting...");
                        renderTarget.BeginDraw();
                        renderTarget.Clear(new RawColor4(0, 0, 0, 1));
                        renderTarget.EndDraw();
                        this.Invalidate();
                        Thread.Sleep(1500);
                        wasReadyLastFrame = false;
                        continue;
                    }

                    bool isReady = IsReadyToRender();
                    if (!isReady)
                    {
                        if (wasReadyLastFrame) 
                        {
                            Program.Log("Not ready to render, waiting...");
                            renderTarget.BeginDraw();
                            renderTarget.Clear(new RawColor4(0, 0, 0, 1));
                            renderTarget.EndDraw();
                            this.Invalidate();
                        }
                        Thread.Sleep(500); 
                        wasReadyLastFrame = false;
                        continue;
                    }

                    RenderFrame();
                    Thread.Sleep(12); // ~60 FPS
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

            foreach (var actor in actors.Values)
            {

                if (actor == null || actor.Position == Vector3.Zero || !actor.IsAlive || actor.ActorType != ActorType.Player)
                    continue;

                if (Vector3.Distance(Game.LocalPlayer.Position, actor.Position) < 1.0f)
                    continue;

                if (!Program.Config.EspShowAllies && actor.IsFriendly())
                    continue;

                Vector2 screenPos = Camera.WorldToScreen(viewInfo, actor.Position);
                if (screenPos == Vector2.Zero)
                    continue;

                var distance = Vector3.Distance(camPos, actor.Position) / 100f;
                if (distance > Program.Config.EspMaxDistance)
                    continue;

                string espText = GetEspText(actor, distance);
                RawRectangleF textRect = GetEspTextRect(screenPos, Game.IsAimingDownSights, Game.HasPipScope);

                brush.Color = new RawColor4(
                    Program.Config.EspTextColor.R / 255f,
                    Program.Config.EspTextColor.G / 255f,
                    Program.Config.EspTextColor.B / 255f,
                    Program.Config.EspTextColor.A / 255f
                );
                renderTarget.DrawText(
                    espText,
                    new SharpDX.DirectWrite.TextFormat(new SharpDX.DirectWrite.Factory(), "Verdana", Program.Config.ESPFontSize),
                    textRect,
                    brush
                );

                if (Program.Config.EspBones)
                {
                    DrawBoneEsp(actor, viewInfo);
                }
            }
        }

        private string GetEspText(UActor actor, float distance)
        {
            string wdistance = Program.Config.EspShowDistance ? $"[{(int)distance}m]" : "";
            string whealth = Program.Config.EspShowHealth ? $"[{(int)actor.Health}❤]" : "";
            string name = Program.Config.ShowNames ? actor.Name : "";
            return $"{name}{(string.IsNullOrEmpty(name) ? "" : " ")}{wdistance}{(string.IsNullOrEmpty(wdistance) ? "" : " ")}{whealth}";
        }

        private RawRectangleF GetEspTextRect(Vector2 screenPos, bool isAiming, bool hasPip)
        {
            float x = screenPos.X;
            float y = screenPos.Y;
            float width = 200f;
            float height = 20f;

            return new RawRectangleF(x, y, x + width, y + height);
        }

        private void DrawBoneEsp(UActor actor, MinimalViewInfo viewInfo)
        {
            if (actor.Mesh == 0 || actor.BoneScreenPositions == null)
            {
                Program.Log("Mesh pointer or bone data is null for actor.");
                return;
            }

            brush.Color = brush.Color = new RawColor4(
                    Program.Config.EspTextColor.R / 255f,
                    Program.Config.EspTextColor.G / 255f,
                    Program.Config.EspTextColor.B / 255f,
                    Program.Config.EspTextColor.A / 255f
                );
            DrawBoneLines(actor.BoneScreenPositions);
        }
        private void DrawBoneLines(Vector2[] screenPositions)
        {
            // Head -> Neck -> Torso
            DrawLine(screenPositions[0], screenPositions[1]); // Head -> Neck
            DrawLine(screenPositions[1], screenPositions[2]); // Neck -> Torso

            // Spine - pelvis
            DrawLine(screenPositions[2], screenPositions[3]); // Torso -> Spine
            DrawLine(screenPositions[3], screenPositions[4]); // Spine -> Pelvis

            // Right arm
            DrawLine(screenPositions[2], screenPositions[5]); 
            DrawLine(screenPositions[5], screenPositions[6]); 
            DrawLine(screenPositions[6], screenPositions[7]);
            DrawLine(screenPositions[7], screenPositions[8]);

            // Left arm
            DrawLine(screenPositions[2], screenPositions[9]); 
            DrawLine(screenPositions[9], screenPositions[10]); 
            DrawLine(screenPositions[10], screenPositions[11]); 
            DrawLine(screenPositions[11], screenPositions[12]); 

            // Right leg
            DrawLine(screenPositions[4], screenPositions[13]); 
            DrawLine(screenPositions[13], screenPositions[14]); 
            DrawLine(screenPositions[14], screenPositions[15]); 

            // Left leg
            DrawLine(screenPositions[4], screenPositions[16]); 
            DrawLine(screenPositions[16], screenPositions[17]); 
            DrawLine(screenPositions[17], screenPositions[18]); 
        }

        private void DrawLine(Vector2 start, Vector2 end)
        {
            if (start != Vector2.Zero && end != Vector2.Zero)
                renderTarget.DrawLine(start.ToRawVector2(), end.ToRawVector2(), brush);
        }

        protected override void OnClosed(EventArgs e)
        {
            running = false;
            brush.Dispose();
            renderTarget.Dispose();
            base.OnClosed(e);
        }
    }
}