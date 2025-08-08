using SysBot.Base;
using SysBot.Pokemon.Discord;
using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SysBot.Pokemon.WinForms
{
    public partial class BotController : UserControl
    {
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public PokeBotState State { get; private set; } = new();
        private IPokeBotRunner? Runner;
        public EventHandler? Remove;

        private float hoverProgress = 0f;
        private bool isHovering = false;
        private DateTime animationStart = DateTime.Now;
        private Color currentStatusColor = Color.FromArgb(90, 186, 71);
        private Color targetStatusColor = Color.FromArgb(90, 186, 71);
        private DateTime LastUpdateStatus = DateTime.Now;
        private bool buttonHovering = false;
        private float pulsePhase = 0f;
        private bool shouldPulse = false;
        private float glowIntensity = 0f;

        private readonly Color CuztomBackground = Color.FromArgb(27, 40, 56);
        private readonly Color CuztomDarkBackground = Color.FromArgb(22, 32, 45);
        private readonly Color CuztomDarkerBackground = Color.FromArgb(16, 24, 34);
        private readonly Color CuztomAccent = Color.FromArgb(102, 192, 244);
        private readonly Color CuztomText = Color.FromArgb(239, 239, 239);
        private readonly Color CuztomSubText = Color.FromArgb(139, 179, 217);
        private readonly Color CuztomGreen = Color.FromArgb(90, 186, 71);
        private readonly Color CuztomRed = Color.FromArgb(236, 98, 95);
        private readonly Color CuztomYellow = Color.FromArgb(245, 197, 92);

        public BotController()
        {
            InitializeComponent();
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                    ControlStyles.DoubleBuffer | ControlStyles.ResizeRedraw |
                    ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);

            ConfigureContextMenu();
            ConfigureChildControls();
            ModernizeStatusIndicator();
        }

        private void ModernizeStatusIndicator()
        {
            statusIndicator.Size = new Size(24, 24);
            statusIndicator.Location = new Point(12, 6);
            statusIndicator.BackColor = Color.Transparent;
        }

        private void ConfigureContextMenu()
        {
            var opt = Enum.GetValues<BotControlCommand>();

            contextMenu.Renderer = new CuztomMenuRenderer();

            for (int i = 1; i < opt.Length; i++)
            {
                var cmd = opt[i];
                var item = new ToolStripMenuItem(cmd.ToString())
                {
                    ForeColor = CuztomText,
                    BackColor = CuztomDarkBackground,
                    Font = new Font("Segoe UI", 8.5F)
                };
                item.Click += (_, __) => SendCommand(cmd);

                switch (cmd)
                {
                    case BotControlCommand.Start:
                        item.Text = "▶  Start";
                        break;
                    case BotControlCommand.Stop:
                        item.Text = "■  Stop";
                        break;
                    case BotControlCommand.Idle:
                        item.Text = "❚❚  Idle";
                        break;
                    case BotControlCommand.Resume:
                        item.Text = "⏵  Resume";
                        break;
                    case BotControlCommand.Restart:
                        item.Text = "↻  Restart";
                        break;
                    case BotControlCommand.RebootAndStop:
                        item.Text = "⚡  Reboot & Stop";
                        break;
                    case BotControlCommand.ScreenOnAll:
                        item.Text = "☀  Screen On";
                        break;
                    case BotControlCommand.ScreenOffAll:
                        item.Text = "🌙  Screen Off";
                        break;
                }

                contextMenu.Items.Add(item);
            }

            contextMenu.Items.Add(new ToolStripSeparator());

            var remove = new ToolStripMenuItem("╳  Remove Bot")
            {
                ForeColor = CuztomRed,
                BackColor = CuztomDarkBackground,
                Font = new Font("Segoe UI", 8.5F)
            };
            remove.Click += (_, __) => TryRemove();
            contextMenu.Items.Add(remove);
            contextMenu.Opening += RcMenuOnOpening;

            RCMenu = contextMenu;
        }

        private void ConfigureChildControls()
        {
            foreach (var c in Controls.OfType<Control>())
            {
                if (c != btnActions)
                {
                    c.MouseEnter += BotController_MouseEnter;
                    c.MouseLeave += BotController_MouseLeave;
                }
            }

            foreach (var c in mainPanel.Controls.OfType<Control>())
            {
                c.MouseEnter += BotController_MouseEnter;
                c.MouseLeave += BotController_MouseLeave;
            }
        }

        private void RcMenuOnOpening(object? sender, CancelEventArgs? e)
        {
            if (Runner == null)
                return;

            var bot = Runner.GetBot(State);
            if (bot is null)
                return;

            foreach (var tsi in contextMenu.Items.OfType<ToolStripMenuItem>())
            {
                var text = tsi.Text?[3..].Trim() ?? string.Empty;
                tsi.Enabled = Enum.TryParse(text.Replace(" ", "").Replace("&", "And"), out BotControlCommand cmd)
                    ? cmd.IsUsable(bot.IsRunning, bot.IsPaused)
                    : !bot.IsRunning;
            }
        }

        public void Initialize(IPokeBotRunner runner, PokeBotState cfg)
        {
            Runner = runner;
            State = cfg;
            ReloadStatus();
            lblConnectionInfo.Text = "Initializing...";
        }

        public void ReloadStatus()
        {
            var botSource = GetBot();
            if (botSource == null)
                return;
            var bot = botSource.Bot;
            lblBotName.Text = bot.Connection.Name;
            lblRoutineType.Visible = false;
            L_Left.Text = $"{bot.Connection.Name}\n{State.InitialRoutine}";
        }

        public void ReloadStatus(BotSource<PokeBotState> b)
        {
            ReloadStatus();
            var bot = b.Bot;
            lblConnectionInfo.Text = $"[{bot.LastTime:HH:mm:ss}] {bot.Connection.Label}: {bot.LastLogged}";

            var botState = ReadBotState();
            lblStatusValue.Text = botState;

            shouldPulse = false;

            switch (botState)
            {
                case "STOPPED":
                    targetStatusColor = Color.FromArgb(100, 100, 100);
                    lblStatusValue.ForeColor = Color.FromArgb(100, 100, 100);
                    break;
                case "IDLE":
                case "IDLING":
                    targetStatusColor = CuztomYellow;
                    lblStatusValue.ForeColor = CuztomYellow;
                    shouldPulse = true;
                    break;
                case "ERROR":
                    targetStatusColor = CuztomRed;
                    lblStatusValue.ForeColor = CuztomRed;
                    shouldPulse = true;
                    break;
                case "REBOOTING":
                    targetStatusColor = CuztomAccent;
                    lblStatusValue.ForeColor = CuztomAccent;
                    shouldPulse = true;
                    break;
                default:
                    targetStatusColor = CuztomGreen;
                    lblStatusValue.ForeColor = CuztomGreen;
                    shouldPulse = true;
                    break;
            }

            var lastTime = bot.LastTime;
            if (!b.IsRunning)
            {
                targetStatusColor = Color.FromArgb(100, 100, 100);
                shouldPulse = false;
                return;
            }

            if (!b.Bot.Connection.Connected)
            {
                targetStatusColor = CuztomAccent;
                shouldPulse = true;
                return;
            }

            var cfg = bot.Config;
            if (cfg.CurrentRoutineType == PokeRoutineType.Idle && cfg.NextRoutineType == PokeRoutineType.Idle)
            {
                targetStatusColor = CuztomYellow;
                shouldPulse = true;
                return;
            }

            if (LastUpdateStatus == lastTime)
                return;

            const int threshold = 100;
            Color good = cfg.Connection.Protocol == SwitchProtocol.USB ? CuztomAccent : CuztomGreen;
            Color bad = CuztomRed;

            var delta = DateTime.Now - lastTime;
            var seconds = delta.Seconds;

            LastUpdateStatus = lastTime;
            if (seconds > 2 * threshold)
                return;

            if (seconds > threshold)
            {
                targetStatusColor = bad;
                shouldPulse = true;
            }
            else
            {
                var factor = seconds / (double)threshold;
                targetStatusColor = Blend(bad, good, factor * factor);
                shouldPulse = true;
            }
        }

        private static Color Blend(Color color, Color backColor, double amount)
        {
            byte r = (byte)((color.R * amount) + (backColor.R * (1 - amount)));
            byte g = (byte)((color.G * amount) + (backColor.G * (1 - amount)));
            byte b = (byte)((color.B * amount) + (backColor.B * (1 - amount)));
            return Color.FromArgb(r, g, b);
        }

        public void TryRemove()
        {
            var bot = GetBot();
            if (!Runner!.Config.SkipConsoleBotCreation && bot != null)
                bot.Stop();

            Remove?.Invoke(this, EventArgs.Empty);
        }

        public void SendCommand(BotControlCommand cmd, bool _ = true)
        {
            if (Runner?.Config.SkipConsoleBotCreation != false)
            {
                LogUtil.LogError("No bots were created because SkipConsoleBotCreation is on!", "Hub");
                return;
            }
            var bot = GetBot();
            if (bot == null)
            {
                LogUtil.LogError("Bot is null!", "BotController");
                return;
            }

            switch (cmd)
            {
                case BotControlCommand.Idle:
                    bot.Pause();
                    break;
                case BotControlCommand.Start:
                    Runner.InitializeStart();
                    bot.Start();
                    break;
                case BotControlCommand.Stop:
                    bot.Stop();
                    break;
                case BotControlCommand.Resume:
                    bot.Resume();
                    break;
                case BotControlCommand.RebootAndStop:
                    bot.RebootAndStop();
                    break;
                case BotControlCommand.Restart:
                    {
                        var prompt = WinFormsUtil.Prompt(MessageBoxButtons.YesNo, "Are you sure you want to restart the connection?");
                        if (prompt != DialogResult.Yes)
                            return;

                        Runner.InitializeStart();
                        bot.Bot.Connection.Reset();
                        bot.Start();
                        break;
                    }
                case BotControlCommand.ScreenOnAll:
                    ExecuteScreenCommand(true);
                    break;
                case BotControlCommand.ScreenOffAll:
                    ExecuteScreenCommand(false);
                    break;
                default:
                    WinFormsUtil.Alert($"{cmd} is not a command that can be sent to the Bot.");
                    return;
            }
        }

        private void ExecuteScreenCommand(bool screenOn)
        {
            if (Runner == null)
            {
                LogUtil.LogError("Runner is null - cannot execute screen command", "BotController");
                return;
            }

            _ = Task.Run(async () =>
            {
                try
                {
                    var bots = Runner.Bots;
                    if (bots == null || bots.Count == 0)
                    {
                        LogUtil.LogError("No bots available to execute screen command", "BotController");
                        return;
                    }

                    int successCount = 0;
                    int totalCount = bots.Count;

                    foreach (var botSource in bots)
                    {
                        try
                        {
                            var bot = botSource.Bot;
                            if (bot?.Connection != null && bot.Connection.Connected)
                            {
                                var crlf = bot is SwitchRoutineExecutor<PokeBotState> { UseCRLF: true };
                                await bot.Connection.SendAsync(SwitchCommand.SetScreen(screenOn ? ScreenState.On : ScreenState.Off, crlf), CancellationToken.None);
                                successCount++;
                                LogUtil.LogInfo($"Screen turned {(screenOn ? "ON" : "OFF")} for {bot.Connection.Name}", "BotController");
                            }
                            else
                            {
                                LogUtil.LogError($"Cannot send screen command - bot {bot?.Connection?.Name ?? "unknown"} is not connected", "BotController");
                            }
                        }
                        catch (Exception ex)
                        {
                            LogUtil.LogError($"Failed to send screen command to bot: {ex.Message}", "BotController");
                        }
                    }

                    LogUtil.LogInfo($"Screen command sent to {successCount} of {totalCount} bots", "BotController");
                }
                catch (Exception ex)
                {
                    LogUtil.LogError($"Failed to execute screen command for all bots: {ex.Message}", "BotController");
                }
            });
        }

        public string ReadBotState()
        {
            try
            {
                var botSource = GetBot();
                if (botSource == null)
                    return "ERROR";

                var bot = botSource.Bot;
                if (bot == null)
                    return "ERROR";

                if (!botSource.IsRunning)
                    return "STOPPED";

                if (botSource.IsStopping)
                    return "STOPPING";

                if (botSource.IsPaused)
                {
                    if (bot.Config?.CurrentRoutineType != PokeRoutineType.Idle)
                        return "IDLING";
                    else
                        return "IDLE";
                }

                if (botSource.IsRunning && !bot.Connection.Connected)
                    return "REBOOTING";

                var cfg = bot.Config;
                if (cfg == null)
                    return "UNKNOWN";

                if (cfg.CurrentRoutineType == PokeRoutineType.Idle)
                    return "IDLE";

                if (botSource.IsRunning && bot.Connection.Connected)
                    return cfg.CurrentRoutineType.ToString();

                return "UNKNOWN";
            }
            catch (Exception ex)
            {
                LogUtil.LogError($"Error reading bot state: {ex.Message}", "BotController");
                return "ERROR";
            }
        }

        public BotSource<PokeBotState>? GetBot()
        {
            try
            {
                if (Runner == null)
                    return null;

                var bot = Runner.GetBot(State);
                if (bot == null)
                    return null;

                return bot;
            }
            catch (Exception ex)
            {
                LogUtil.LogError($"Error getting bot: {ex.Message}", "BotController");
                return null;
            }
        }

        private void BotController_MouseEnter(object? sender, EventArgs e)
        {
            if (!isHovering)
            {
                isHovering = true;
                animationStart = DateTime.Now;
            }
        }

        private void BotController_MouseLeave(object? sender, EventArgs e)
        {
            var pos = PointToClient(Cursor.Position);
            if (!ClientRectangle.Contains(pos))
            {
                isHovering = false;
                animationStart = DateTime.Now;
            }
        }

        private void BtnActions_MouseEnter(object? sender, EventArgs e)
        {
            buttonHovering = true;
        }

        private void BtnActions_MouseLeave(object? sender, EventArgs e)
        {
            buttonHovering = false;
        }

        public void ReadState()
        {
            var bot = GetBot();
            if (bot == null)
                return;

            if (InvokeRequired)
            {
                Invoke((MethodInvoker)(() => ReloadStatus(bot)));
            }
            else
            {
                ReloadStatus(bot);
            }
        }

        private void BotController_Paint(object sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;

            var rect = ClientRectangle;

            using var brush = new SolidBrush(CuztomBackground);
            g.FillRectangle(brush, rect);
        }

        private void MainPanel_Paint(object sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            var rect = mainPanel.ClientRectangle;
            using var path = new GraphicsPath();
            GraphicsExtensions.AddRoundedRectangle(path, rect, 4);

            using var brush = new SolidBrush(CuztomDarkBackground);
            g.FillPath(brush, path);

            if (hoverProgress > 0)
            {
                using var pen = new Pen(Color.FromArgb((int)(80 * hoverProgress), CuztomAccent), 2);
                g.DrawPath(pen, path);
            }
            else
            {
                using var pen = new Pen(Color.FromArgb(12, 20, 28), 1);
                g.DrawPath(pen, path);
            }
        }

        private void BottomPanel_Paint(object sender, PaintEventArgs e)
        {
            var g = e.Graphics;

            using var pen = new Pen(Color.FromArgb(12, 20, 28), 1);
            g.DrawLine(pen, 0, 0, bottomPanel.Width, 0);
        }

        private void StatusIndicator_Paint(object sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.CompositingQuality = CompositingQuality.HighQuality;

            if (sender is not PictureBox control)
                return;
            var fullRect = control.ClientRectangle;

            var centerX = fullRect.Width / 2f;
            var centerY = fullRect.Height / 2f;
            var coreSize = 10f;
            var coreRect = new RectangleF(centerX - coreSize / 2, centerY - coreSize / 2, coreSize, coreSize);

            if (shouldPulse && pulsePhase > 0)
            {
                var basePulse = (float)((Math.Sin(pulsePhase) + 1) / 2);
                var pulseScale = 0.7f + (basePulse * 0.4f);

                for (int ring = 3; ring >= 1; ring--)
                {
                    var ringSize = coreSize + (ring * 4f * pulseScale);
                    var ringAlpha = (int)(40 + (30 * basePulse)) / ring;

                    if (ringAlpha > 8)
                    {
                        var ringRect = new RectangleF(
                            centerX - ringSize / 2,
                            centerY - ringSize / 2,
                            ringSize,
                            ringSize
                        );

                        using var glowPath = new GraphicsPath();
                        glowPath.AddEllipse(ringRect);
                        using var glowBrush = new PathGradientBrush(glowPath);
                        glowBrush.CenterColor = Color.FromArgb(ringAlpha, currentStatusColor);
                        glowBrush.SurroundColors = [Color.FromArgb(0, currentStatusColor)];
                        glowBrush.CenterPoint = new PointF(centerX, centerY);
                        g.FillEllipse(glowBrush, ringRect);
                    }
                }

                var innerGlowSize = coreSize + (3f * pulseScale);
                var innerGlowRect = new RectangleF(
                    centerX - innerGlowSize / 2,
                    centerY - innerGlowSize / 2,
                    innerGlowSize,
                    innerGlowSize
                );
                var innerAlpha = (int)(70 + (50 * basePulse));

                using var innerGlowPath = new GraphicsPath();
                innerGlowPath.AddEllipse(innerGlowRect);
                using var innerGlowBrush = new PathGradientBrush(innerGlowPath);
                innerGlowBrush.CenterColor = Color.FromArgb(innerAlpha, currentStatusColor);
                innerGlowBrush.SurroundColors = [Color.FromArgb(0, currentStatusColor)];
                innerGlowBrush.CenterPoint = new PointF(centerX, centerY);
                g.FillEllipse(innerGlowBrush, innerGlowRect);
            }

            using var corePath = new GraphicsPath();
            corePath.AddEllipse(coreRect);

            using var coreGradient = new LinearGradientBrush(
                coreRect,
                Color.FromArgb(255, Math.Min(255, currentStatusColor.R + 40), Math.Min(255, currentStatusColor.G + 40), Math.Min(255, currentStatusColor.B + 40)),
                currentStatusColor,
                LinearGradientMode.Vertical);
            g.FillEllipse(coreGradient, coreRect);

            using var borderPen = new Pen(Color.FromArgb(180, 255, 255, 255), 0.5f);
            g.DrawEllipse(borderPen, coreRect);

            var highlightSize = 3f;
            var highlightRect = new RectangleF(
                centerX - coreSize / 3,
                centerY - coreSize / 3,
                highlightSize,
                highlightSize
            );

            using var highlightBrush = new SolidBrush(Color.FromArgb(160, 255, 255, 255));
            {
                g.FillEllipse(highlightBrush, highlightRect);
            }
        }

        private void BtnActions_Paint(object sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            if (sender is not Button btn)
                return;
            var rect = btn.ClientRectangle;

            using var path = new GraphicsPath();
            GraphicsExtensions.AddRoundedRectangle(path, rect, 3);
            btn.Region = new Region(path);
        }

        private void BtnActions_Click(object sender, EventArgs e)
        {
            contextMenu.Show(btnActions, new Point(0, btnActions.Height + 1));
        }

        private void AnimationTimer_Tick(object sender, EventArgs e)
        {
            var elapsed = (DateTime.Now - animationStart).TotalMilliseconds;
            var duration = 200.0;

            if (isHovering)
            {
                hoverProgress = Math.Min(1.0f, (float)(elapsed / duration));
            }
            else
            {
                hoverProgress = Math.Max(0.0f, 1.0f - (float)(elapsed / duration));
            }

            if (currentStatusColor != targetStatusColor)
            {
                currentStatusColor = Blend(targetStatusColor, currentStatusColor, 0.15);
            }

            if (shouldPulse)
            {
                pulsePhase += 0.08f;
                if (pulsePhase > Math.PI * 2)
                    pulsePhase -= (float)(Math.PI * 2);

                glowIntensity = (float)((Math.Sin(pulsePhase) + 1) / 2);
            }
            else
            {
                pulsePhase = 0f;
                glowIntensity = 0f;
            }

            if (hoverProgress > 0 && hoverProgress < 1)
            {
                mainPanel.Invalidate();
            }

            if (buttonHovering)
            {
                btnActions.BackColor = Color.FromArgb(122, 207, 255);
            }
            else
            {
                btnActions.BackColor = CuztomAccent;
            }

            statusIndicator?.Invalidate();
        }

        private class CuztomMenuRenderer : ToolStripProfessionalRenderer
        {
            public CuztomMenuRenderer() : base(new CuztomColorTable()) { }

            protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
            {
                var rc = new Rectangle(Point.Empty, e.Item.Size);
                var c = e.Item.Selected ? Color.FromArgb(46, 61, 83) : Color.FromArgb(27, 40, 56);
                using var brush = new SolidBrush(c);
                e.Graphics.FillRectangle(brush, rc);
            }

            protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
            {
                if (!e.Item.Enabled)
                    e.TextColor = Color.FromArgb(100, 100, 100);
                base.OnRenderItemText(e);
            }
        }

        private class CuztomColorTable : ProfessionalColorTable
        {
            public override Color MenuItemSelected => Color.FromArgb(46, 61, 83);
            public override Color MenuItemBorder => Color.FromArgb(102, 192, 244);
            public override Color MenuBorder => Color.FromArgb(12, 20, 28);
            public override Color ToolStripDropDownBackground => Color.FromArgb(27, 40, 56);
            public override Color ImageMarginGradientBegin => Color.FromArgb(27, 40, 56);
            public override Color ImageMarginGradientMiddle => Color.FromArgb(27, 40, 56);
            public override Color ImageMarginGradientEnd => Color.FromArgb(27, 40, 56);
            public override Color SeparatorDark => Color.FromArgb(12, 20, 28);
            public override Color SeparatorLight => Color.FromArgb(46, 61, 83);
        }
    }

    public enum BotControlCommand
    {
        None,
        Start,
        Stop,
        Idle,
        Resume,
        Restart,
        RebootAndStop,
        ScreenOnAll,
        ScreenOffAll,
    }

    public static class BotControlCommandExtensions
    {
        public static bool IsUsable(this BotControlCommand cmd, bool running, bool paused)
        {
            return cmd switch
            {
                BotControlCommand.Start => !running,
                BotControlCommand.Stop => running,
                BotControlCommand.Idle => running && !paused,
                BotControlCommand.Resume => paused,
                BotControlCommand.Restart => true,
                BotControlCommand.ScreenOnAll => running,
                BotControlCommand.ScreenOffAll => running,
                _ => false,
            };
        }
    }
}
