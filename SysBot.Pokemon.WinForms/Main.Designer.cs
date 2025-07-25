using SysBot.Pokemon.WinForms.Properties;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Linq;
using System.Windows.Forms;
using SysBot.Base;

#pragma warning disable CS8618
#pragma warning disable CS8625
#pragma warning disable CS8669

namespace SysBot.Pokemon.WinForms
{
    partial class Main
    {
        private System.ComponentModel.IContainer? components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            if (disposing && trayIcon != null)
            {
                trayIcon.Visible = false;
                trayIcon.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Main));

            animationTimer = new System.Windows.Forms.Timer(this.components);
            animationTimer.Interval = 16;
            animationTimer.Tick += AnimationTimer_Tick;
            animationTimer.Start();

            trayIcon = new NotifyIcon(this.components);
            trayContextMenu = new ContextMenuStrip(this.components);
            trayMenuShow = new ToolStripMenuItem();
            trayMenuExit = new ToolStripMenuItem();

            mainLayoutPanel = new TableLayoutPanel();
            sidebarPanel = new Panel();
            contentPanel = new Panel();
            headerPanel = new Panel();

            logoPanel = new Panel();
            navButtonsPanel = new FlowLayoutPanel();
            btnNavBots = new Button();
            btnNavHub = new Button();
            btnNavLogs = new Button();
            sidebarBottomPanel = new Panel();
            btnUpdate = new Button();
            statusIndicator = new Panel();

            titleLabel = new Label();
            controlButtonsPanel = new FlowLayoutPanel();
            btnStart = new Button();
            btnStop = new Button();
            btnReboot = new Button();

            botsPanel = new Panel();
            hubPanel = new Panel();
            logsPanel = new Panel();

            botHeaderPanel = new Panel();
            addBotPanel = new Panel();
            TB_IP = new TextBox();
            NUD_Port = new NumericUpDown();
            CB_Protocol = new ComboBox();
            CB_Routine = new ComboBox();
            B_New = new Button();
            FLP_Bots = new FlowLayoutPanel();

            PG_Hub = new PropertyGrid();

            RTB_Logs = new RichTextBox();
            logsHeaderPanel = new Panel();
            searchPanel = new Panel();
            logSearchBox = new TextBox();
            searchOptionsPanel = new FlowLayoutPanel();
            btnCaseSensitive = new CheckBox();
            btnRegex = new CheckBox();
            btnWholeWord = new CheckBox();
            btnClearLogs = new Button();
            searchStatusLabel = new Label();

            comboBox1 = new ComboBox();

            mainLayoutPanel.SuspendLayout();
            sidebarPanel.SuspendLayout();
            navButtonsPanel.SuspendLayout();
            sidebarBottomPanel.SuspendLayout();
            headerPanel.SuspendLayout();
            controlButtonsPanel.SuspendLayout();
            contentPanel.SuspendLayout();
            botsPanel.SuspendLayout();
            botHeaderPanel.SuspendLayout();
            addBotPanel.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)NUD_Port).BeginInit();
            hubPanel.SuspendLayout();
            logsPanel.SuspendLayout();
            logsHeaderPanel.SuspendLayout();
            searchPanel.SuspendLayout();
            searchOptionsPanel.SuspendLayout();
            SuspendLayout();

            SetStyle(ControlStyles.AllPaintingInWmPaint |
                    ControlStyles.UserPaint |
                    ControlStyles.DoubleBuffer |
                    ControlStyles.ResizeRedraw |
                    ControlStyles.OptimizedDoubleBuffer, true);
            UpdateStyles();

            // Main Form
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(1000, 400);
            MinimumSize = new Size(900, 400);
            BackColor = Color.FromArgb(27, 40, 56);
            Font = new Font("Segoe UI", 9F, FontStyle.Regular);
            Icon = Resources.icon;
            Name = "Main";
            StartPosition = FormStartPosition.CenterScreen;
            Text = "PokéBot Control Center";
            FormClosing += Main_FormClosing;
            DoubleBuffered = true;
            Resize += Main_Resize;

            // Main Layout Panel
            mainLayoutPanel.ColumnCount = 2;
            mainLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 240F));
            mainLayoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            mainLayoutPanel.Controls.Add(sidebarPanel, 0, 0);
            mainLayoutPanel.Controls.Add(contentPanel, 1, 0);
            mainLayoutPanel.Dock = DockStyle.Fill;
            mainLayoutPanel.Location = new Point(0, 0);
            mainLayoutPanel.Margin = new Padding(0);
            mainLayoutPanel.Name = "mainLayoutPanel";
            mainLayoutPanel.RowCount = 1;
            mainLayoutPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            mainLayoutPanel.TabIndex = 0;
            mainLayoutPanel.BackColor = Color.Transparent;
            EnableDoubleBuffering(mainLayoutPanel);

            // Sidebar Panel - Cuztom style
            sidebarPanel.BackColor = Color.FromArgb(23, 29, 37);
            sidebarPanel.Controls.Add(navButtonsPanel);
            sidebarPanel.Controls.Add(sidebarBottomPanel);
            sidebarPanel.Controls.Add(logoPanel);
            sidebarPanel.Dock = DockStyle.Fill;
            sidebarPanel.Location = new Point(0, 0);
            sidebarPanel.Margin = new Padding(0);
            sidebarPanel.Name = "sidebarPanel";
            sidebarPanel.Size = new Size(240, 600);
            sidebarPanel.TabIndex = 0;
            EnableDoubleBuffering(sidebarPanel);

            // Logo Panel - Cuztom gradient
            logoPanel.BackColor = Color.FromArgb(23, 29, 37);
            logoPanel.Dock = DockStyle.Top;
            logoPanel.Height = 60;
            logoPanel.Location = new Point(0, 0);
            logoPanel.Name = "logoPanel";
            logoPanel.Size = new Size(240, 60);
            logoPanel.TabIndex = 2;
            logoPanel.Paint += LogoPanel_Paint;
            EnableDoubleBuffering(logoPanel);

            // Navigation Buttons Panel
            navButtonsPanel.AutoSize = false;
            navButtonsPanel.Controls.Add(btnNavBots);
            navButtonsPanel.Controls.Add(btnNavHub);
            navButtonsPanel.Controls.Add(btnNavLogs);
            navButtonsPanel.Dock = DockStyle.Fill;
            navButtonsPanel.FlowDirection = FlowDirection.TopDown;
            navButtonsPanel.Location = new Point(0, 60);
            navButtonsPanel.Margin = new Padding(0);
            navButtonsPanel.Name = "navButtonsPanel";
            navButtonsPanel.Padding = new Padding(0, 10, 0, 0);
            navButtonsPanel.Size = new Size(240, 460);
            navButtonsPanel.TabIndex = 1;
            navButtonsPanel.BackColor = Color.Transparent;
            EnableDoubleBuffering(navButtonsPanel);

            // Configure Cuztom-style nav buttons with neon accents
            ConfigureNavButton(btnNavBots, "BOTS", 0, "Manage bot connections", Color.FromArgb(57, 255, 221)); // Neon cyan
            ConfigureNavButton(btnNavHub, "CONFIGURATION", 1, "System settings", Color.FromArgb(255, 0, 255)); // Neon magenta
            ConfigureNavButton(btnNavLogs, "SYSTEM LOGS", 2, "View activity logs", Color.FromArgb(255, 165, 0)); // Neon orange

            var separator = new Panel();
            separator.BackColor = Color.FromArgb(32, 38, 48);
            separator.Size = new Size(200, 1);
            separator.Margin = new Padding(20, 20, 20, 20);
            navButtonsPanel.Controls.Add(separator);

            var btnExit = new Button();
            ConfigureNavButton(btnExit, "EXIT", 3, "Exit application", Color.FromArgb(236, 98, 95));
            btnExit.Click += BtnExit_Click;
            navButtonsPanel.Controls.Add(btnExit);

            // Sidebar Bottom Panel
            sidebarBottomPanel.Controls.Add(btnUpdate);
            sidebarBottomPanel.Controls.Add(comboBox1);
            sidebarBottomPanel.Dock = DockStyle.Bottom;
            sidebarBottomPanel.Height = 80;
            sidebarBottomPanel.Location = new Point(0, 520);
            sidebarBottomPanel.Name = "sidebarBottomPanel";
            sidebarBottomPanel.Padding = new Padding(20, 10, 20, 10);
            sidebarBottomPanel.TabIndex = 0;
            sidebarBottomPanel.BackColor = Color.FromArgb(19, 23, 30);
            sidebarBottomPanel.MaximumSize = new Size(240, 80);
            EnableDoubleBuffering(sidebarBottomPanel);

            // Mode Selector ComboBox - Cuztom style
            comboBox1.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            comboBox1.BackColor = Color.FromArgb(32, 38, 48);
            comboBox1.DropDownStyle = ComboBoxStyle.DropDownList;
            comboBox1.FlatStyle = FlatStyle.Flat;
            comboBox1.Font = new Font("Segoe UI", 8F);
            comboBox1.ForeColor = Color.FromArgb(239, 239, 239);
            comboBox1.Location = new Point(10, 10);
            comboBox1.Name = "comboBox1";
            comboBox1.Size = new Size(200, 21);
            comboBox1.TabIndex = 0;
            comboBox1.SelectedIndexChanged += ComboBox1_SelectedIndexChanged;
            comboBox1.MaximumSize = new Size(220, 21);

            // Update Button - Cuztom style
            btnUpdate.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            btnUpdate.BackColor = Color.FromArgb(32, 38, 48);
            btnUpdate.FlatAppearance.BorderSize = 0;
            btnUpdate.FlatAppearance.MouseOverBackColor = Color.FromArgb(45, 51, 61);
            btnUpdate.FlatStyle = FlatStyle.Flat;
            btnUpdate.Font = new Font("Segoe UI", 8F, FontStyle.Regular);
            btnUpdate.ForeColor = Color.FromArgb(139, 179, 217);
            btnUpdate.Location = new Point(10, 35);
            btnUpdate.Name = "btnUpdate";
            btnUpdate.Size = new Size(200, 35);
            btnUpdate.TabIndex = 1;
            btnUpdate.Text = "";
            btnUpdate.UseVisualStyleBackColor = false;
            btnUpdate.Click += Updater_Click;
            btnUpdate.Cursor = Cursors.Hand;
            btnUpdate.Tag = new ButtonAnimationState();
            btnUpdate.MaximumSize = new Size(220, 35);
            ConfigureHoverAnimation(btnUpdate);
            ConfigureUpdateButton();

            // Content Panel
            contentPanel.BackColor = Color.FromArgb(30, 35, 44);
            contentPanel.Controls.Add(botsPanel);
            contentPanel.Controls.Add(hubPanel);
            contentPanel.Controls.Add(logsPanel);
            contentPanel.Controls.Add(headerPanel);
            contentPanel.Dock = DockStyle.Fill;
            contentPanel.Location = new Point(240, 0);
            contentPanel.Margin = new Padding(0);
            contentPanel.Name = "contentPanel";
            contentPanel.Size = new Size(860, 600);
            contentPanel.TabIndex = 1;
            EnableDoubleBuffering(contentPanel);

            // Header Panel - Cuztom style
            headerPanel.BackColor = Color.FromArgb(30, 35, 44);
            headerPanel.Controls.Add(controlButtonsPanel);
            headerPanel.Controls.Add(titleLabel);
            headerPanel.Dock = DockStyle.Top;
            headerPanel.Height = 60;
            headerPanel.Location = new Point(0, 0);
            headerPanel.Name = "headerPanel";
            headerPanel.Size = new Size(860, 60);
            headerPanel.TabIndex = 3;
            headerPanel.Paint += HeaderPanel_Paint;
            headerPanel.Resize += HeaderPanel_Resize;
            EnableDoubleBuffering(headerPanel);

            // Title Label
            titleLabel.AutoSize = true;
            titleLabel.Font = ScaleFont(new Font("Segoe UI", 16F, FontStyle.Bold));
            titleLabel.ForeColor = Color.FromArgb(239, 239, 239);
            titleLabel.Location = new Point(20, 18);
            titleLabel.Name = "titleLabel";
            titleLabel.TabIndex = 0;
            titleLabel.Text = "Bot Management";
            titleLabel.MaximumSize = new Size(350, 35);
            titleLabel.AutoEllipsis = true;

            // Control Buttons Panel
            controlButtonsPanel.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            controlButtonsPanel.AutoSize = true;
            controlButtonsPanel.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            controlButtonsPanel.Controls.Add(btnStart);
            controlButtonsPanel.Controls.Add(btnStop);
            controlButtonsPanel.Controls.Add(btnReboot);
            controlButtonsPanel.FlowDirection = FlowDirection.LeftToRight;
            controlButtonsPanel.Location = new Point(contentPanel.Width - 350, 16);
            controlButtonsPanel.Name = "controlButtonsPanel";
            controlButtonsPanel.TabIndex = 1;
            controlButtonsPanel.BackColor = Color.Transparent;
            controlButtonsPanel.WrapContents = false;
            controlButtonsPanel.MaximumSize = new Size(400, 32);

            // Enhanced Cuztom-style control buttons
            ConfigureEnhancedControlButton(btnStart, "START ALL", Color.FromArgb(90, 186, 71), "\uE768");
            ConfigureEnhancedControlButton(btnStop, "STOP ALL", Color.FromArgb(236, 98, 95), "\uE71A");
            ConfigureEnhancedControlButton(btnReboot, "REBOOT", Color.FromArgb(102, 192, 244), "\uE777");

            btnStart.Click += B_Start_Click;
            btnStop.Click += B_Stop_Click;
            btnReboot.Click += B_RebootStop_Click;

            // Bots Panel
            botsPanel.BackColor = Color.Transparent;
            botsPanel.Controls.Add(FLP_Bots);
            botsPanel.Controls.Add(botHeaderPanel);
            botsPanel.Dock = DockStyle.Fill;
            botsPanel.Location = new Point(0, 60);
            botsPanel.Name = "botsPanel";
            botsPanel.Padding = new Padding(10);
            botsPanel.Size = new Size(860, 540);
            botsPanel.TabIndex = 0;
            botsPanel.Visible = true;
            EnableDoubleBuffering(botsPanel);

            // Bot Header Panel - Cuztom style
            botHeaderPanel.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            botHeaderPanel.BackColor = Color.FromArgb(22, 32, 45);
            botHeaderPanel.Controls.Add(addBotPanel);
            botHeaderPanel.Height = 60;
            botHeaderPanel.Location = new Point(10, 10);
            botHeaderPanel.Name = "botHeaderPanel";
            botHeaderPanel.Size = new Size(840, 60);
            botHeaderPanel.TabIndex = 1;
            CreateRoundedPanel(botHeaderPanel);
            EnableDoubleBuffering(botHeaderPanel);

            // Add Bot Panel
            addBotPanel.Controls.Add(B_New);
            addBotPanel.Controls.Add(CB_Routine);
            addBotPanel.Controls.Add(CB_Protocol);
            addBotPanel.Controls.Add(NUD_Port);
            addBotPanel.Controls.Add(TB_IP);
            addBotPanel.Dock = DockStyle.Fill;
            addBotPanel.Location = new Point(0, 0);
            addBotPanel.Name = "addBotPanel";
            addBotPanel.Size = new Size(840, 60);
            addBotPanel.TabIndex = 0;
            addBotPanel.BackColor = Color.Transparent;
            addBotPanel.Layout += AddBotPanel_Layout;

            // Cuztom-style input controls
            TB_IP.BackColor = Color.FromArgb(32, 38, 48);
            TB_IP.BorderStyle = BorderStyle.FixedSingle;
            TB_IP.Font = ScaleFont(new Font("Segoe UI", 9F));
            TB_IP.ForeColor = Color.FromArgb(239, 239, 239);
            TB_IP.Location = new Point(15, 18);
            TB_IP.Name = "TB_IP";
            TB_IP.PlaceholderText = "IP Address";
            TB_IP.Size = new Size(110, 23);
            TB_IP.TabIndex = 0;
            TB_IP.Text = "192.168.0.1";

            ConfigureNumericUpDown(NUD_Port, 135, 18, 60);
            NUD_Port.Maximum = new decimal(new int[] { 65535, 0, 0, 0 });
            NUD_Port.Value = new decimal(new int[] { 6000, 0, 0, 0 });

            CB_Protocol.SuspendLayout();
            ConfigureComboBox(CB_Protocol, 205, 18, 70);
            CB_Protocol.SelectedIndexChanged += CB_Protocol_SelectedIndexChanged;
            CB_Protocol.ResumeLayout();

            ConfigureComboBox(CB_Routine, 285, 18, 130);

            CB_Routine.SizeChanged += CB_Routine_SizeChanged;
            CB_Routine.LocationChanged += CB_Routine_LocationChanged;

            // Add Bot Button - Cuztom accent - Responsive positioning
            B_New.Anchor = AnchorStyles.Top | AnchorStyles.Left;
            B_New.BackColor = Color.FromArgb(102, 192, 244);
            B_New.FlatAppearance.BorderSize = 0;
            B_New.FlatStyle = FlatStyle.Flat;
            B_New.Font = ScaleFont(new Font("Segoe UI", 8.5F, FontStyle.Bold));
            B_New.ForeColor = Color.FromArgb(22, 32, 45);
            B_New.Location = new Point(430, 16);
            B_New.Name = "B_New";
            B_New.Size = new Size(85, 28);
            B_New.TabIndex = 4;
            B_New.Text = "ADD BOT";
            B_New.UseVisualStyleBackColor = false;
            B_New.Click += B_New_Click;
            B_New.Cursor = Cursors.Hand;
            ConfigureGlowButton(B_New);
            CreateRoundedButton(B_New);

            // Bots Flow Layout Panel
            FLP_Bots.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            FLP_Bots.AutoScroll = true;
            FLP_Bots.BackColor = Color.Transparent;
            FLP_Bots.FlowDirection = FlowDirection.TopDown;
            FLP_Bots.Location = new Point(10, 75);
            FLP_Bots.Margin = new Padding(0, 5, 0, 0);
            FLP_Bots.Name = "FLP_Bots";
            FLP_Bots.Padding = new Padding(0);
            FLP_Bots.Size = new Size(840, 455);
            FLP_Bots.TabIndex = 0;
            FLP_Bots.WrapContents = false;
            FLP_Bots.Resize += FLP_Bots_Resize;
            FLP_Bots.Paint += FLP_Bots_Paint;
            FLP_Bots.Scroll += FLP_Bots_Scroll;
            FLP_Bots.ControlAdded += FLP_Bots_ControlAdded;
            FLP_Bots.ControlRemoved += FLP_Bots_ControlRemoved;
            EnableDoubleBuffering(FLP_Bots);

            // Hub Panel
            hubPanel.BackColor = Color.Transparent;
            hubPanel.Controls.Add(PG_Hub);
            hubPanel.Dock = DockStyle.Fill;
            hubPanel.Location = new Point(0, 60);
            hubPanel.Name = "hubPanel";
            hubPanel.Padding = new Padding(10);
            hubPanel.Size = new Size(860, 540);
            hubPanel.TabIndex = 1;
            hubPanel.Visible = false;
            EnableDoubleBuffering(hubPanel);

            // Property Grid Container - Cuztom style
            var pgContainer = new Panel();
            pgContainer.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            pgContainer.BackColor = Color.FromArgb(22, 32, 45);
            pgContainer.Location = new Point(10, 10);
            pgContainer.Name = "pgContainer";
            pgContainer.Padding = new Padding(2);
            pgContainer.Size = new Size(840, 520);
            CreateRoundedPanel(pgContainer);
            EnableDoubleBuffering(pgContainer);
            hubPanel.Controls.Add(pgContainer);

            // Property Grid - Cuztom colors
            PG_Hub.BackColor = Color.FromArgb(22, 32, 45);
            PG_Hub.CategoryForeColor = Color.FromArgb(239, 239, 239);
            PG_Hub.CategorySplitterColor = Color.FromArgb(32, 38, 48);
            PG_Hub.CommandsBackColor = Color.FromArgb(22, 32, 45);
            PG_Hub.CommandsForeColor = Color.FromArgb(239, 239, 239);
            PG_Hub.Dock = DockStyle.Fill;
            PG_Hub.Font = ScaleFont(new Font("Segoe UI", 9F));
            PG_Hub.HelpBackColor = Color.FromArgb(22, 32, 45);
            PG_Hub.HelpForeColor = Color.FromArgb(139, 179, 217);
            PG_Hub.LineColor = Color.FromArgb(32, 38, 48);
            PG_Hub.Location = new Point(2, 2);
            PG_Hub.Name = "PG_Hub";
            PG_Hub.PropertySort = PropertySort.Categorized;
            PG_Hub.Size = new Size(836, 516);
            PG_Hub.TabIndex = 0;
            PG_Hub.ToolbarVisible = false;
            PG_Hub.ViewBackColor = Color.FromArgb(32, 38, 48);
            PG_Hub.ViewForeColor = Color.FromArgb(239, 239, 239);
            pgContainer.Controls.Add(PG_Hub);
            PG_Hub.CreateControl();

            // Logs Panel
            logsPanel.BackColor = Color.Transparent;
            logsPanel.Dock = DockStyle.Fill;
            logsPanel.Location = new Point(0, 60);
            logsPanel.Name = "logsPanel";
            logsPanel.Padding = new Padding(10);
            logsPanel.Size = new Size(860, 540);
            logsPanel.TabIndex = 2;
            logsPanel.Visible = false;
            EnableDoubleBuffering(logsPanel);

            // Logs Container - Cuztom style
            var logsContainer = new Panel();
            logsContainer.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            logsContainer.BackColor = Color.FromArgb(22, 32, 45);
            logsContainer.Location = new Point(10, 60);
            logsContainer.Margin = new Padding(0, 5, 0, 0);
            logsContainer.Name = "logsContainer";
            logsContainer.Padding = new Padding(2);
            logsContainer.Size = new Size(840, 470);
            CreateRoundedPanel(logsContainer);
            EnableDoubleBuffering(logsContainer);
            logsPanel.Controls.Add(logsContainer);
            logsPanel.Controls.Add(logsHeaderPanel);

            // Logs Header Panel - Cuztom style
            logsHeaderPanel.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            logsHeaderPanel.BackColor = Color.FromArgb(22, 32, 45);
            logsHeaderPanel.Height = 45;
            logsHeaderPanel.Location = new Point(10, 10);
            logsHeaderPanel.Name = "logsHeaderPanel";
            logsHeaderPanel.Padding = new Padding(15, 8, 15, 8);
            logsHeaderPanel.Size = new Size(840, 45);
            logsHeaderPanel.TabIndex = 1;
            CreateRoundedPanel(logsHeaderPanel);
            EnableDoubleBuffering(logsHeaderPanel);

            // Search Panel
            searchPanel.Anchor = AnchorStyles.Top | AnchorStyles.Left;
            searchPanel.Controls.Add(logSearchBox);
            searchPanel.Height = 23;
            searchPanel.Location = new Point(15, 11);
            searchPanel.Name = "searchPanel";
            searchPanel.Size = new Size(380, 23);
            searchPanel.TabIndex = 0;
            searchPanel.BackColor = Color.FromArgb(22, 32, 45);

            // Log Search Box - Cuztom style
            logSearchBox.BackColor = Color.FromArgb(32, 38, 48);
            logSearchBox.BorderStyle = BorderStyle.FixedSingle;
            logSearchBox.Dock = DockStyle.Fill;
            logSearchBox.Font = ScaleFont(new Font("Segoe UI", 8.5F));
            logSearchBox.ForeColor = Color.FromArgb(239, 239, 239);
            logSearchBox.Location = new Point(0, 0);
            logSearchBox.Name = "logSearchBox";
            logSearchBox.PlaceholderText = "Search logs (Enter = next, Shift+Enter = previous, Esc = clear)...";
            logSearchBox.Size = new Size(380, 23);
            logSearchBox.TabIndex = 0;
            logSearchBox.TextChanged += LogSearchBox_TextChanged;
            logSearchBox.KeyDown += LogSearchBox_KeyDown;

            // Search Options Panel
            searchOptionsPanel.Anchor = AnchorStyles.Top | AnchorStyles.Left;
            searchOptionsPanel.AutoSize = true;
            searchOptionsPanel.Controls.Add(btnCaseSensitive);
            searchOptionsPanel.Controls.Add(btnRegex);
            searchOptionsPanel.Controls.Add(btnWholeWord);
            searchOptionsPanel.FlowDirection = FlowDirection.LeftToRight;
            searchOptionsPanel.Height = 18;
            searchOptionsPanel.Location = new Point(400, 13);
            searchOptionsPanel.Name = "searchOptionsPanel";
            searchOptionsPanel.Size = new Size(100, 18);
            searchOptionsPanel.TabIndex = 1;
            searchOptionsPanel.BackColor = Color.FromArgb(22, 32, 45);
            searchOptionsPanel.WrapContents = false;

            ConfigureSearchOption(btnCaseSensitive, "Aa", "Case sensitive search");
            ConfigureSearchOption(btnRegex, ".*", "Regular expression search");
            ConfigureSearchOption(btnWholeWord, "Ab", "Whole word search");

            // Search Status Label
            searchStatusLabel.AutoSize = true;
            searchStatusLabel.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            searchStatusLabel.Font = ScaleFont(new Font("Segoe UI", 7.5F));
            searchStatusLabel.ForeColor = Color.FromArgb(139, 179, 217);
            searchStatusLabel.Location = new Point(660, 14);
            searchStatusLabel.Name = "searchStatusLabel";
            searchStatusLabel.Size = new Size(80, 12);
            searchStatusLabel.TabIndex = 2;
            searchStatusLabel.Text = "";
            searchStatusLabel.TextAlign = ContentAlignment.MiddleRight;

            // Clear Logs Button - Cuztom style
            btnClearLogs.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnClearLogs.BackColor = Color.FromArgb(236, 98, 95);
            btnClearLogs.FlatAppearance.BorderSize = 0;
            btnClearLogs.FlatStyle = FlatStyle.Flat;
            btnClearLogs.Font = ScaleFont(new Font("Segoe UI", 7.5F, FontStyle.Bold));
            btnClearLogs.ForeColor = Color.White;
            btnClearLogs.Location = new Point(750, 10);
            btnClearLogs.Name = "btnClearLogs";
            btnClearLogs.Size = new Size(75, 23);
            btnClearLogs.TabIndex = 3;
            btnClearLogs.Text = "CLEAR";
            btnClearLogs.UseVisualStyleBackColor = false;
            btnClearLogs.Cursor = Cursors.Hand;
            btnClearLogs.Click += BtnClearLogs_Click;
            ConfigureGlowButton(btnClearLogs);
            CreateRoundedButton(btnClearLogs);

            // Rich Text Box - Cuztom style
            RTB_Logs.BackColor = Color.FromArgb(32, 38, 48);
            RTB_Logs.BorderStyle = BorderStyle.None;
            RTB_Logs.Dock = DockStyle.Fill;
            RTB_Logs.Font = ScaleFont(new Font("Consolas", 9F));
            RTB_Logs.ForeColor = Color.FromArgb(239, 239, 239);
            RTB_Logs.Location = new Point(2, 2);
            RTB_Logs.Name = "RTB_Logs";
            RTB_Logs.ReadOnly = true;
            RTB_Logs.Size = new Size(836, 466);
            RTB_Logs.TabIndex = 0;
            RTB_Logs.Text = "";
            RTB_Logs.HideSelection = false;
            RTB_Logs.KeyDown += RTB_Logs_KeyDown;
            logsContainer.Controls.Add(RTB_Logs);

            // Add controls to logsHeaderPanel
            logsHeaderPanel.Controls.Add(searchPanel);
            logsHeaderPanel.Controls.Add(searchOptionsPanel);
            logsHeaderPanel.Controls.Add(searchStatusLabel);
            logsHeaderPanel.Controls.Add(btnClearLogs);

            // Hidden tab control for compatibility
            TC_Main = new TabControl { Visible = false };
            Tab_Bots = new TabPage();
            Tab_Hub = new TabPage();
            Tab_Logs = new TabPage();
            TC_Main.TabPages.Add(Tab_Bots);
            TC_Main.TabPages.Add(Tab_Hub);
            TC_Main.TabPages.Add(Tab_Logs);
            TC_Main.SendToBack();

            Controls.Add(mainLayoutPanel);

            // Resume layouts
            mainLayoutPanel.ResumeLayout(false);
            sidebarPanel.ResumeLayout(false);
            navButtonsPanel.ResumeLayout(false);
            sidebarBottomPanel.ResumeLayout(false);
            headerPanel.ResumeLayout(false);
            headerPanel.PerformLayout();
            controlButtonsPanel.ResumeLayout(false);
            contentPanel.ResumeLayout(false);
            botsPanel.ResumeLayout(false);
            botHeaderPanel.ResumeLayout(false);
            addBotPanel.ResumeLayout(false);
            addBotPanel.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)NUD_Port).EndInit();
            hubPanel.ResumeLayout(false);
            logsPanel.ResumeLayout(false);
            logsHeaderPanel.ResumeLayout(false);
            logsHeaderPanel.PerformLayout();
            searchPanel.ResumeLayout(false);
            searchOptionsPanel.ResumeLayout(false);
            ResumeLayout(false);

            ConfigureSystemTray();
            animationTimer.Start();
        }

        #endregion

        #region Font Scaling

        private Font ScaleFont(Font baseFont)
        {
            using (Graphics g = CreateGraphics())
            {
                float dpiScale = g.DpiX / 96f;
                float scaledSize = baseFont.Size * dpiScale;

                if (ClientSize.Width < 900)
                {
                    scaledSize *= 0.85f;
                }
                else if (ClientSize.Width < 1100)
                {
                    scaledSize *= 0.92f;
                }

                scaledSize = Math.Max(7f, scaledSize);

                if (ClientSize.Width < 800)
                {
                    if (baseFont.Size >= 24)
                        scaledSize = Math.Min(scaledSize, 16f);
                    else if (baseFont.Size >= 11)
                        scaledSize = Math.Min(scaledSize, 9f);
                    else
                        scaledSize = Math.Min(scaledSize, 8f);
                }

                return new Font(baseFont.FontFamily, scaledSize, baseFont.Style);
            }
        }

        #endregion

        #region UI Helper Methods

        private void EnableDoubleBuffering(Control control)
        {
            if (control == null) return;

            typeof(Control).InvokeMember("DoubleBuffered",
                System.Reflection.BindingFlags.SetProperty |
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic,
                null, control, new object[] { true });
        }

        private void HeaderPanel_Resize(object sender, EventArgs e)
        {
            if (controlButtonsPanel != null && headerPanel != null)
            {
                int rightMargin = 20;
                int minLeftPosition = titleLabel.Right + 20;

                int availableWidth = headerPanel.Width - minLeftPosition - rightMargin;

                controlButtonsPanel.MaximumSize = new Size(400, 32);
                controlButtonsPanel.WrapContents = false;

                int desiredX = headerPanel.Width - controlButtonsPanel.Width - rightMargin;
                controlButtonsPanel.Location = new Point(Math.Max(minLeftPosition, desiredX), 16);
            }
        }

        private void ConfigureSearchOption(CheckBox checkBox, string text, string tooltip)
        {
            checkBox.Appearance = Appearance.Button;
            checkBox.BackColor = Color.FromArgb(45, 51, 61);
            checkBox.FlatAppearance.BorderSize = 1;
            checkBox.FlatAppearance.BorderColor = Color.FromArgb(32, 38, 48);
            checkBox.FlatAppearance.CheckedBackColor = Color.FromArgb(102, 192, 244);
            checkBox.FlatStyle = FlatStyle.Flat;
            checkBox.Font = ScaleFont(new Font("Segoe UI", 6.5F, FontStyle.Bold));
            checkBox.ForeColor = Color.FromArgb(200, 200, 200);
            checkBox.Margin = new Padding(0, 0, 3, 0);
            checkBox.Size = new Size(22, 16);
            checkBox.Text = text;
            checkBox.TextAlign = ContentAlignment.MiddleCenter;
            checkBox.UseVisualStyleBackColor = false;
            checkBox.Cursor = Cursors.Hand;

            var toolTip = new ToolTip();
            toolTip.SetToolTip(checkBox, tooltip);
        }

        private void ConfigureNavButton(Button btn, string text, int index, string tooltip, Color neonColor)
        {
            btn.BackColor = Color.FromArgb(23, 29, 37);
            btn.Cursor = Cursors.Hand;
            btn.FlatAppearance.BorderSize = 0;
            btn.FlatAppearance.MouseOverBackColor = Color.FromArgb(32, 38, 48);
            btn.FlatStyle = FlatStyle.Flat;
            btn.Font = ScaleFont(new Font("Segoe UI", 10F, FontStyle.Regular));
            btn.ForeColor = Color.FromArgb(139, 179, 217);
            btn.Location = new Point(0, 10 + (index * 45));
            btn.Margin = new Padding(0, 0, 0, 5);
            btn.Name = $"btnNav{text.Replace(" ", "")}";
            btn.Padding = new Padding(50, 0, 0, 0);
            btn.Size = new Size(240, 40);
            btn.TabIndex = index;
            btn.Text = text;
            btn.TextAlign = ContentAlignment.MiddleLeft;
            btn.UseVisualStyleBackColor = false;
            btn.Tag = new NavButtonState { NeonColor = neonColor, Index = index };

            btn.Paint += (s, e) => {
                var navState = btn.Tag as NavButtonState;
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;

                // Draw background
                using (var bgBrush = new SolidBrush(btn.BackColor))
                {
                    g.FillRectangle(bgBrush, btn.ClientRectangle);
                }

                // Draw left accent bar when selected
                if (navState.IsSelected)
                {
                    using (var accentBrush = new SolidBrush(navState.NeonColor))
                    {
                        g.FillRectangle(accentBrush, 0, 0, 3, btn.Height);
                    }

                    // Draw neon glow effect
                    for (int i = 1; i <= 2; i++)
                    {
                        using (var glowBrush = new SolidBrush(Color.FromArgb(20 / i, navState.NeonColor)))
                        {
                            g.FillRectangle(glowBrush, 0, 0, 3 + i * 2, btn.Height);
                        }
                    }

                    // Update text color to match neon
                    btn.ForeColor = navState.NeonColor;
                }
                else
                {
                    btn.ForeColor = Color.FromArgb(139, 179, 217);
                }

                // Draw icon
                int iconSize = 18;
                var iconRect = new Rectangle(15, (btn.Height - iconSize) / 2, iconSize, iconSize);
                using var iconFont = new Font("Segoe MDL2 Assets", 13F);
                string iconText = index switch
                {
                    0 => "\uE77B", // Bots icon
                    1 => "\uE713", // Settings icon
                    2 => "\uE7C3", // Logs icon
                    3 => "\uE7E8", // Exit icon
                    _ => "\uE700"
                };

                var iconColor = navState.IsSelected ? navState.NeonColor : Color.FromArgb(139, 179, 217);
                using var iconBrush = new SolidBrush(iconColor);
                var textSize = g.MeasureString(iconText, iconFont);
                var textX = iconRect.X + (iconRect.Width - textSize.Width) / 2;
                var textY = iconRect.Y + (iconRect.Height - textSize.Height) / 2;
                g.DrawString(iconText, iconFont, iconBrush, textX, textY);

                // Draw text with proper font
                var textRect = new Rectangle(50, 0, btn.Width - 50, btn.Height);
                TextRenderer.DrawText(g, btn.Text, btn.Font, textRect, btn.ForeColor,
                    TextFormatFlags.VerticalCenter | TextFormatFlags.Left);
            };

            btn.Click += (s, e) => {
                if (index >= 3) return; // Don't select exit button

                // Update all nav buttons
                foreach (Button navBtn in navButtonsPanel.Controls.OfType<Button>())
                {
                    if (navBtn.Tag is NavButtonState state)
                    {
                        state.IsSelected = false;
                        navBtn.Invalidate();
                    }
                }

                // Select this button
                var navState = btn.Tag as NavButtonState;
                navState.IsSelected = true;
                btn.Invalidate();

                TransitionPanels(index);

                titleLabel.Text = index switch
                {
                    0 => "Bot Management",
                    1 => "Configuration",
                    2 => "System Logs",
                    _ => "PokéBot"
                };
            };

            ConfigureHoverAnimation(btn);

            // Select first button by default
            if (index == 0)
            {
                var navState = btn.Tag as NavButtonState;
                navState.IsSelected = true;
            }
        }

        private void ConfigureEnhancedControlButton(Button btn, string text, Color baseColor, string iconText)
        {
            btn.BackColor = baseColor;
            btn.Cursor = Cursors.Hand;
            btn.FlatAppearance.BorderSize = 0;
            btn.FlatStyle = FlatStyle.Flat;
            btn.Font = ScaleFont(new Font("Segoe UI", 8.5F, FontStyle.Bold));
            btn.ForeColor = Color.FromArgb(22, 32, 45);
            btn.Margin = new Padding(3, 0, 3, 0);
            btn.Name = $"btn{text.Replace(" ", "")}";
            btn.Padding = new Padding(10, 5, 10, 5);
            btn.TabIndex = 0;
            btn.Text = text;
            btn.UseVisualStyleBackColor = false;
            btn.AutoSize = true;
            btn.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            btn.MinimumSize = new Size(75, 28);
            btn.MaximumSize = new Size(120, 32);

            var animState = new EnhancedButtonAnimationState
            {
                BaseColor = baseColor,
                IconText = iconText,
                IsActive = false
            };
            btn.Tag = animState;

            CreateRoundedButton(btn);
            ConfigureEnhancedHoverAnimation(btn);

            btn.Paint += (s, e) => {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;

                var state = btn.Tag as EnhancedButtonAnimationState;
                var rect = btn.ClientRectangle;

                // Draw background gradient
                using (var bgBrush = new LinearGradientBrush(
                    rect,
                    Color.FromArgb(255, Math.Min(255, baseColor.R + 20), Math.Min(255, baseColor.G + 20), Math.Min(255, baseColor.B + 20)),
                    baseColor,
                    LinearGradientMode.Vertical))
                {
                    g.FillRectangle(bgBrush, rect);
                }

                // Draw glow effect
                if (state.HoverProgress > 0 || state.PulseIntensity > 0)
                {
                    var glowIntensity = Math.Max(state.HoverProgress, state.PulseIntensity * 0.3f);
                    var glowAlpha = (int)(60 * glowIntensity);

                    for (int i = 1; i <= 3; i++)
                    {
                        var glowRect = new Rectangle(
                            rect.X - i * 2,
                            rect.Y - i * 2,
                            rect.Width + i * 4,
                            rect.Height + i * 4
                        );

                        using (var glowBrush = new SolidBrush(Color.FromArgb(glowAlpha / i, baseColor)))
                        {
                            using (var glowPath = new GraphicsPath())
                            {
                                GraphicsExtensions.AddRoundedRectangle(glowPath, glowRect, 4 + i);
                                g.FillPath(glowBrush, glowPath);
                            }
                        }
                    }
                }

                // Draw pulse effect when active
                if (state.IsActive && state.PulseIntensity > 0)
                {
                    var pulseAlpha = (int)(40 * state.PulseIntensity);
                    using (var pulseBrush = new SolidBrush(Color.FromArgb(pulseAlpha, 255, 255, 255)))
                    {
                        g.FillRectangle(pulseBrush, rect);
                    }
                }

                // Draw icon and text
                var iconSize = 10;
                var iconX = 8;
                var iconY = (rect.Height - iconSize) / 2;

                using (var iconFont = new Font("Segoe MDL2 Assets", iconSize))
                {
                    var iconColor = state.HoverProgress > 0.5f
                        ? Color.FromArgb(255, 255, 255)
                        : btn.ForeColor;

                    using (var iconBrush = new SolidBrush(iconColor))
                    {
                        var iconBounds = g.MeasureString(iconText, iconFont);
                        var centeredIconY = (rect.Height - iconBounds.Height) / 2;
                        g.DrawString(iconText, iconFont, iconBrush, iconX, centeredIconY);
                    }
                }

                var textX = iconX + iconSize + 8;
                var textY = (rect.Height - g.MeasureString(text, btn.Font).Height) / 2;

                var textColor = state.HoverProgress > 0.5f
                    ? Color.FromArgb(255, 255, 255)
                    : btn.ForeColor;

                using (var textBrush = new SolidBrush(textColor))
                {
                    g.DrawString(text, btn.Font, textBrush, textX, textY);
                }

                // Draw top highlight
                using (var highlightPen = new Pen(Color.FromArgb(60, 255, 255, 255), 1))
                {
                    g.DrawLine(highlightPen, 3, 1, rect.Width - 3, 1);
                }
            };

            btn.Resize += (s, e) => btn.Invalidate();
        }

        private void ConfigureEnhancedHoverAnimation(Button btn)
        {
            var animState = btn.Tag as EnhancedButtonAnimationState;

            btn.MouseEnter += (s, e) => {
                animState.IsHovering = true;
                animState.AnimationStart = DateTime.Now;
            };

            btn.MouseLeave += (s, e) => {
                animState.IsHovering = false;
                animState.AnimationStart = DateTime.Now;
            };

            btn.MouseDown += (s, e) => {
                animState.IsPressed = true;
                btn.BackColor = Color.FromArgb(
                    Math.Max(0, animState.BaseColor.R - 30),
                    Math.Max(0, animState.BaseColor.G - 30),
                    Math.Max(0, animState.BaseColor.B - 30)
                );
            };

            btn.MouseUp += (s, e) => {
                animState.IsPressed = false;
                btn.BackColor = animState.BaseColor;
            };
        }

        private void ConfigureNumericUpDown(NumericUpDown nud, int x, int y, int width)
        {
            nud.BackColor = Color.FromArgb(32, 38, 48);
            nud.BorderStyle = BorderStyle.None;
            nud.Font = ScaleFont(new Font("Segoe UI", 9F));
            nud.ForeColor = Color.FromArgb(239, 239, 239);
            nud.Location = new Point(x, y);
            nud.Name = nud.Name;
            nud.Size = new Size(width, 23);
            nud.TabIndex = 1;
        }

        private void ConfigureComboBox(ComboBox cb, int x, int y, int width)
        {
            cb.BackColor = Color.FromArgb(32, 38, 48);
            cb.DropDownStyle = ComboBoxStyle.DropDownList;
            cb.FlatStyle = FlatStyle.Flat;
            cb.Font = ScaleFont(new Font("Segoe UI", 9F));
            cb.ForeColor = Color.FromArgb(239, 239, 239);
            cb.Location = new Point(x, y);
            cb.Name = cb.Name;
            cb.Size = new Size(width, 23);
            cb.TabIndex = 2;
        }

        private void ConfigureHoverAnimation(Control control)
        {
            var animState = control.Tag as ButtonAnimationState ?? new ButtonAnimationState();
            control.Tag = animState;

            control.MouseEnter += (s, e) => {
                animState.IsHovering = true;
                animState.AnimationStart = DateTime.Now;
            };

            control.MouseLeave += (s, e) => {
                animState.IsHovering = false;
                animState.AnimationStart = DateTime.Now;
            };
        }

        private void ConfigureGlowButton(Button btn)
        {
            ConfigureHoverAnimation(btn);

            btn.Paint += (s, e) => {
                var animState = btn.Tag as ButtonAnimationState;
                if (animState != null && animState.HoverProgress > 0)
                {
                    e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

                    var glowAlpha = (int)(40 * animState.HoverProgress);
                    using (var glowBrush = new SolidBrush(Color.FromArgb(glowAlpha, btn.BackColor)))
                    {
                        for (int i = 1; i <= 2; i++)
                        {
                            var rect = new Rectangle(-i * 2, -i * 2, btn.Width + i * 4, btn.Height + i * 4);
                            e.Graphics.FillRectangle(glowBrush, rect);
                        }
                    }
                }
            };
        }

        private void CreateRoundedPanel(Panel panel)
        {
            panel.Paint += (s, e) => {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using var path = new GraphicsPath();
                var rect = panel.ClientRectangle;
                rect.Inflate(-1, -1);
                GraphicsExtensions.AddRoundedRectangle(path, rect, 4);
                panel.Region = new Region(path);
            };
        }

        private void CreateRoundedButton(Button btn)
        {
            btn.Paint += (s, e) => {
                if (btn.Region != null) btn.Region.Dispose();

                using var path = new GraphicsPath();
                var rect = btn.ClientRectangle;
                int radius = Math.Min(3, Math.Min(rect.Width, rect.Height) / 4);
                GraphicsExtensions.AddRoundedRectangle(path, rect, radius);
                btn.Region = new Region(path);
            };

            btn.Invalidate();
        }

        private void CreateCircularRegion(Control control)
        {
            using var path = new GraphicsPath();
            path.AddEllipse(0, 0, control.Width, control.Height);
            control.Region = new Region(path);
        }

        private void ConfigureUpdateButton()
        {
            statusIndicator.BackColor = Color.FromArgb(100, 100, 100);
            statusIndicator.Size = new Size(8, 8);
            statusIndicator.Location = new Point(btnUpdate.ClientSize.Width - 18, 13);
            statusIndicator.Name = "statusIndicator";
            statusIndicator.Enabled = false;
            statusIndicator.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            CreateCircularRegion(statusIndicator);
            btnUpdate.Controls.Add(statusIndicator);
            statusIndicator.BringToFront();

            statusIndicator.Paint += (s, e) => {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                var rect = statusIndicator.ClientRectangle;
                rect.Inflate(-1, -1);

                using var brush = new SolidBrush(statusIndicator.BackColor);
                e.Graphics.FillEllipse(brush, rect);

                var mainForm = (Main)statusIndicator.FindForm();
                if (mainForm != null && mainForm.hasUpdate)
                {
                    var highlightRect = new Rectangle(1, 1, 3, 3);
                    using var highlightBrush = new SolidBrush(Color.FromArgb(200, 255, 255, 255));
                    e.Graphics.FillEllipse(highlightBrush, highlightRect);
                }
            };

            var updateTooltip = new ToolTip();
            updateTooltip.SetToolTip(btnUpdate, "Check for updates");
            btnUpdate.MouseEnter += (s, e) => {
                var mainForm = (Main)btnUpdate.FindForm();
                if (mainForm != null && mainForm.hasUpdate)
                {
                    updateTooltip.SetToolTip(btnUpdate, "Update available! Click to download.");
                }
                else
                {
                    updateTooltip.SetToolTip(btnUpdate, "No updates available");
                }
            };

            btnUpdate.Resize += (s, e) => {
                if (statusIndicator != null && btnUpdate.Controls.Contains(statusIndicator))
                {
                    statusIndicator.Location = new Point(btnUpdate.ClientSize.Width - 18, 13);
                }
            };

            btnUpdate.Paint += (s, e) => {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                e.Graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;

                var animState = btnUpdate.Tag as ButtonAnimationState;

                if (animState != null && animState.HoverProgress > 0 && animState.IsHovering)
                {
                    using var glowBrush = new SolidBrush(Color.FromArgb((int)(20 * animState.HoverProgress), 102, 192, 244));
                    e.Graphics.FillRectangle(glowBrush, btnUpdate.ClientRectangle);
                }

                var iconColor = btnUpdate.ForeColor;
                if (animState != null && animState.HoverProgress > 0)
                {
                    iconColor = Color.FromArgb(
                        (int)(139 + (239 - 139) * animState.HoverProgress),
                        (int)(179 + (239 - 179) * animState.HoverProgress),
                        (int)(217 + (239 - 217) * animState.HoverProgress)
                    );
                }

                using var iconFont = new Font("Segoe MDL2 Assets", 11F);
                var iconText = "\uE895";

                using var iconBrush = new SolidBrush(iconColor);
                var iconSize = e.Graphics.MeasureString(iconText, iconFont);

                var iconX = 10;
                var iconY = (btnUpdate.Height - iconSize.Height) / 2;
                e.Graphics.DrawString(iconText, iconFont, iconBrush, iconX, iconY);

                using var textFont = ScaleFont(new Font("Segoe UI", 7.5F, FontStyle.Regular));
                var text = "CHECK FOR UPDATES";

                var textSize = e.Graphics.MeasureString(text, textFont);
                var textX = iconX + iconSize.Width + 5;
                var textY = (btnUpdate.Height - textSize.Height) / 2;
                e.Graphics.DrawString(text, textFont, iconBrush, textX, textY);

                var mainForm = (Main)btnUpdate.FindForm();
                if (mainForm != null && mainForm.hasUpdate && statusIndicator != null)
                {
                    var indicatorBounds = new Rectangle(
                        statusIndicator.Left - 2,
                        statusIndicator.Top - 2,
                        statusIndicator.Width + 4,
                        statusIndicator.Height + 4
                    );

                    for (int i = 2; i > 0; i--)
                    {
                        var glowAlpha = (int)(15 / i * (0.5 + 0.5 * Math.Sin(mainForm.pulsePhase)));
                        using var glowBrush = new SolidBrush(Color.FromArgb(glowAlpha, 102, 192, 244));
                        var glowRect = new Rectangle(
                            indicatorBounds.X - i * 2,
                            indicatorBounds.Y - i * 2,
                            indicatorBounds.Width + i * 4,
                            indicatorBounds.Height + i * 4
                        );
                        e.Graphics.FillEllipse(glowBrush, glowRect);
                    }
                }
            };
        }

        #endregion

        #region Paint Event Handlers

        private void LogoPanel_Paint(object sender, PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
            e.Graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;

            var rect = logoPanel.ClientRectangle;

            // Draw metallic gradient background
            using (var bgPath = new GraphicsPath())
            {
                bgPath.AddRectangle(rect);
                using (var pgBrush = new PathGradientBrush(bgPath))
                {
                    pgBrush.CenterColor = Color.FromArgb(35, 42, 54);
                    pgBrush.SurroundColors = new[] { Color.FromArgb(23, 29, 37) };
                    pgBrush.FocusScales = new PointF(0.8f, 0.5f);
                    e.Graphics.FillRectangle(pgBrush, rect);
                }
            }

            // Draw brass/copper accent lines
            using (var pen = new Pen(Color.FromArgb(40, 184, 115, 51), 1))
            {
                e.Graphics.DrawLine(pen, 0, 0, rect.Width, 0);
                e.Graphics.DrawLine(pen, 0, rect.Height - 1, rect.Width, rect.Height - 1);
            }

            // Calculate bot states
            _runningBotCount = FLP_Bots.Controls.OfType<BotController>().Count(c => c.GetBot()?.IsRunning ?? false);
            _idlingBotCount = FLP_Bots.Controls.OfType<BotController>().Count(c => c.GetBot()?.IsPaused ?? false);
            _totalBotCount = FLP_Bots.Controls.OfType<BotController>().Count();

            float activityLevel = _totalBotCount > 0 ? (float)_runningBotCount / _totalBotCount : 0f;
            float idleLevel = _totalBotCount > 0 ? (float)_idlingBotCount / _totalBotCount : 0f;

            // Draw gears behind text
            DrawGears(e.Graphics, rect, activityLevel, idleLevel);

            // Draw electric arcs for active bots
            if (activityLevel > 0)
            {
                UpdateAndDrawElectricArcs(e.Graphics, rect, activityLevel);
            }

            // Draw main logo text with metallic effect
            DrawMetallicText(e.Graphics, rect, activityLevel, idleLevel);
        }

        private void UpdateAndDrawElectricArcs(Graphics g, Rectangle rect, float activityLevel)
        {
            // Spawn new arcs occasionally
            if (_random.NextDouble() < activityLevel * 0.05)
            {
                var start = new PointF(rect.Width / 2 + _random.Next(-30, 30), rect.Height / 2);
                var end = new PointF(
                    _random.Next(10, rect.Width - 10),
                    _random.Next(10, rect.Height - 10)
                );

                var arc = new ElectricArc
                {
                    Start = start,
                    End = end,
                    Intensity = activityLevel,
                    Life = 0.5f
                };

                // Generate arc points
                int segments = 8;
                for (int i = 0; i <= segments; i++)
                {
                    float t = i / (float)segments;
                    float x = start.X + (end.X - start.X) * t;
                    float y = start.Y + (end.Y - start.Y) * t;

                    if (i > 0 && i < segments)
                    {
                        x += (float)(_random.NextDouble() * 10 - 5) * activityLevel;
                        y += (float)(_random.NextDouble() * 10 - 5) * activityLevel;
                    }

                    arc.Points.Add(new PointF(x, y));
                }

                _electricArcs.Add(arc);
            }

            // Update and draw arcs
            for (int i = _electricArcs.Count - 1; i >= 0; i--)
            {
                var arc = _electricArcs[i];
                arc.Life -= 0.05f;

                if (arc.Life <= 0)
                {
                    _electricArcs.RemoveAt(i);
                    continue;
                }

                int alpha = (int)(arc.Life * 255);
                using (var pen = new Pen(Color.FromArgb(alpha, 57, 255, 221), 2))
                {
                    pen.EndCap = LineCap.Round;
                    pen.StartCap = LineCap.Round;

                    for (int j = 0; j < arc.Points.Count - 1; j++)
                    {
                        g.DrawLine(pen, arc.Points[j], arc.Points[j + 1]);
                    }
                }

                // Draw glow
                using (var glowPen = new Pen(Color.FromArgb(alpha / 3, 57, 255, 221), 6))
                {
                    glowPen.EndCap = LineCap.Round;
                    glowPen.StartCap = LineCap.Round;

                    for (int j = 0; j < arc.Points.Count - 1; j++)
                    {
                        g.DrawLine(glowPen, arc.Points[j], arc.Points[j + 1]);
                    }
                }
            }
        }

        private void DrawMetallicText(Graphics g, Rectangle rect, float activityLevel, float idleLevel)
        {
            using var font = ScaleFont(new Font("Segoe UI", 14F, FontStyle.Bold));
            var text = "POKÉBOT";
            var textSize = g.MeasureString(text, font);
            var x = (rect.Width - textSize.Width) / 2;
            var y = (rect.Height - textSize.Height) / 2;

            // Create metallic gradient
            var textRect = new RectangleF(x, y, textSize.Width, textSize.Height);
            using (var metalBrush = new LinearGradientBrush(
                textRect,
                Color.FromArgb(255, 220, 180, 140), // Light brass
                Color.FromArgb(255, 139, 69, 19),   // Dark brass
                LinearGradientMode.Vertical))
            {
                metalBrush.SetBlendTriangularShape(0.5f);

                // Shadow
                using (var shadowBrush = new SolidBrush(Color.FromArgb(80, 0, 0, 0)))
                {
                    g.DrawString(text, font, shadowBrush, x + 2, y + 2);
                }

                // Main text
                g.DrawString(text, font, metalBrush, x, y);

                // Different glow effects based on state
                if (activityLevel > 0)
                {
                    // Yellow glow for active
                    float pulseValue = (float)(Math.Sin(DateTime.Now.Millisecond / 500.0 * Math.PI) + 1) / 2;
                    int glowAlpha = (int)(30 + pulseValue * 50 + activityLevel * 50);
                    using (var glowBrush = new SolidBrush(Color.FromArgb(glowAlpha, 255, 255, 0)))
                    {
                        g.DrawString(text, font, glowBrush, x - 1, y - 1);
                    }
                }
                else if (idleLevel > 0)
                {
                    // Orange breathing glow for idle
                    float breathe = (float)(Math.Sin(DateTime.Now.Millisecond / 1000.0 * Math.PI) + 1) / 2;
                    int glowAlpha = (int)(40 + breathe * 60 * idleLevel);
                    using (var glowBrush = new SolidBrush(Color.FromArgb(glowAlpha, 255, 165, 0)))
                    {
                        g.DrawString(text, font, glowBrush, x - 1, y - 1);
                    }
                }
                else
                {
                    // Subtle pulse when no bots
                    float pulseValue = (float)(Math.Sin(DateTime.Now.Millisecond / 1500.0 * Math.PI) + 1) / 2;
                    int glowAlpha = (int)(20 + pulseValue * 30);
                    using (var glowBrush = new SolidBrush(Color.FromArgb(glowAlpha, 102, 192, 244)))
                    {
                        g.DrawString(text, font, glowBrush, x - 1, y - 1);
                    }
                }
            }
        }

        private void DrawGears(Graphics g, Rectangle rect, float activityLevel, float idleLevel)
        {
            var centerX = rect.Width / 2;
            var centerY = rect.Height / 2;

            // Calculate gear positions for proper meshing
            float gear1Radius = 20;
            float gear2Radius = 12;
            int gear1Teeth = 16;
            int gear2Teeth = 10;

            // Position gears so they mesh properly
            float gearDistance = gear1Radius + gear2Radius - 2; // Slight overlap for meshing
            float gear1X = centerX - 10;
            float gear1Y = centerY;
            float gear2X = gear1X + gearDistance;
            float gear2Y = gear1Y;

            // Draw shadow first
            DrawGearShadow(g, gear1X + 2, gear1Y + 2, gear1Radius, 0.2f);
            DrawGearShadow(g, gear2X + 2, gear2Y + 2, gear2Radius, 0.2f);

            // Large gear (behind, left)
            DrawSingleGear(g, gear1X, gear1Y, gear1Radius, gear1Teeth, _gearRotation1, 0.4f, true);

            // Small gear (behind, right) - rotation adjusted for proper meshing
            float meshOffset = 360f / gear2Teeth / 2; // Half tooth offset for proper meshing
            DrawSingleGear(g, gear2X, gear2Y, gear2Radius, gear2Teeth, _gearRotation2 + meshOffset, 0.4f, false);
        }

        private void DrawGearShadow(Graphics g, float centerX, float centerY, float radius, float opacity)
        {
            using (var shadowBrush = new SolidBrush(Color.FromArgb((int)(255 * opacity), 0, 0, 0)))
            {
                g.FillEllipse(shadowBrush, centerX - radius, centerY - radius, radius * 2, radius * 2);
            }
        }

        private void DrawSingleGear(Graphics g, float centerX, float centerY, float outerRadius, int teethCount, float rotation, float opacity, bool isLargeGear)
        {
            // 3D color scheme
            var baseColor = Color.FromArgb((int)(255 * opacity), 70, 75, 85);
            var darkColor = Color.FromArgb((int)(255 * opacity), 40, 45, 55);
            var lightColor = Color.FromArgb((int)(255 * opacity), 90, 95, 105);
            var highlightColor = Color.FromArgb((int)(255 * opacity), 110, 115, 125);

            using (var gearPath = new GraphicsPath())
            {
                // Create gear teeth with better proportions
                float toothDepth = outerRadius * 0.25f;
                float innerRadius = outerRadius - toothDepth;
                float angleStep = 360f / teethCount;
                float toothAngle = angleStep * 0.4f; // Wider teeth for better mesh

                // Create the gear outline
                List<PointF> gearPoints = new List<PointF>();

                for (int i = 0; i < teethCount; i++)
                {
                    float angle = i * angleStep + rotation;

                    // Calculate tooth points with beveled edges
                    float toothStart = angle - toothAngle / 2;
                    float toothEnd = angle + toothAngle / 2;
                    float valleyStart = angle + toothAngle / 2 + angleStep * 0.1f;
                    float valleyEnd = angle + angleStep - toothAngle / 2 - angleStep * 0.1f;

                    // Tooth tip (with slight bevel)
                    gearPoints.Add(PolarToCartesian(centerX, centerY, outerRadius, toothStart));
                    gearPoints.Add(PolarToCartesian(centerX, centerY, outerRadius, angle - toothAngle * 0.1f));
                    gearPoints.Add(PolarToCartesian(centerX, centerY, outerRadius, angle + toothAngle * 0.1f));
                    gearPoints.Add(PolarToCartesian(centerX, centerY, outerRadius, toothEnd));

                    // Valley
                    gearPoints.Add(PolarToCartesian(centerX, centerY, innerRadius, valleyStart));
                    gearPoints.Add(PolarToCartesian(centerX, centerY, innerRadius, valleyEnd));
                }

                gearPath.AddPolygon(gearPoints.ToArray());

                // Create 3D effect with gradients
                var bounds = new RectangleF(centerX - outerRadius, centerY - outerRadius, outerRadius * 2, outerRadius * 2);

                // Base gradient for 3D depth
                using (var gradientBrush = new LinearGradientBrush(bounds, lightColor, darkColor, 45f))
                {
                    gradientBrush.SetBlendTriangularShape(0.5f, 1.0f);
                    g.FillPath(gradientBrush, gearPath);
                }

                // Draw beveled edges on teeth
                for (int i = 0; i < teethCount; i++)
                {
                    float angle = i * angleStep + rotation;
                    DrawToothBevel(g, centerX, centerY, outerRadius, innerRadius, angle, toothAngle, angleStep, lightColor, darkColor);
                }

                // Inner hub with 3D effect
                float hubRadius = innerRadius * 0.6f;
                var hubBounds = new RectangleF(centerX - hubRadius, centerY - hubRadius, hubRadius * 2, hubRadius * 2);

                using (var hubPath = new GraphicsPath())
                {
                    hubPath.AddEllipse(hubBounds);
                    using (var hubGradient = new PathGradientBrush(hubPath))
                    {
                        hubGradient.CenterColor = lightColor;
                        hubGradient.SurroundColors = new[] { darkColor };
                        hubGradient.FocusScales = new PointF(0.3f, 0.3f);
                        g.FillPath(hubGradient, hubPath);
                    }
                }

                // Raised rim around hub
                float rimRadius = hubRadius * 1.1f;
                using (var rimPen = new Pen(lightColor, 1.5f))
                {
                    g.DrawEllipse(rimPen, centerX - rimRadius, centerY - rimRadius, rimRadius * 2, rimRadius * 2);
                }

                // Center hole with depth
                float holeRadius = hubRadius * 0.3f;
                var holeBounds = new RectangleF(centerX - holeRadius, centerY - holeRadius, holeRadius * 2, holeRadius * 2);

                using (var holePath = new GraphicsPath())
                {
                    holePath.AddEllipse(holeBounds);
                    using (var holeGradient = new PathGradientBrush(holePath))
                    {
                        holeGradient.CenterColor = Color.FromArgb((int)(255 * opacity), 10, 15, 25);
                        holeGradient.SurroundColors = new[] { Color.FromArgb((int)(255 * opacity), 20, 25, 35) };
                        g.FillPath(holeGradient, holePath);
                    }
                }

                // Specular highlight for metallic look
                using (var highlightPath = new GraphicsPath())
                {
                    var highlightBounds = new RectangleF(centerX - outerRadius * 0.7f, centerY - outerRadius * 0.7f, outerRadius * 1.4f, outerRadius * 1.4f);
                    highlightPath.AddArc(highlightBounds, -60 + rotation * 0.3f, 120);

                    using (var highlightBrush = new LinearGradientBrush(
                        highlightBounds,
                        Color.FromArgb((int)(80 * opacity), 150, 155, 165),
                        Color.Transparent,
                        -30f + rotation * 0.3f))
                    {
                        g.FillPath(highlightBrush, highlightPath);
                    }
                }
            }
        }

        private PointF PolarToCartesian(float centerX, float centerY, float radius, float angleDegrees)
        {
            float angleRadians = angleDegrees * (float)Math.PI / 180f;
            return new PointF(
                centerX + radius * (float)Math.Cos(angleRadians),
                centerY + radius * (float)Math.Sin(angleRadians)
            );
        }

        private void DrawToothBevel(Graphics g, float centerX, float centerY, float outerRadius, float innerRadius, float angle, float toothAngle, float angleStep, Color lightColor, Color darkColor)
        {
            // Draw highlights on the leading edge of each tooth
            using (var bevelPath = new GraphicsPath())
            {
                float toothStart = angle - toothAngle / 2;
                float toothMid = angle;

                var p1 = PolarToCartesian(centerX, centerY, outerRadius, toothStart);
                var p2 = PolarToCartesian(centerX, centerY, outerRadius, toothMid);
                var p3 = PolarToCartesian(centerX, centerY, innerRadius * 1.1f, toothStart);

                bevelPath.AddPolygon(new[] { p1, p2, p3 });

                using (var bevelBrush = new SolidBrush(Color.FromArgb(40, lightColor)))
                {
                    g.FillPath(bevelBrush, bevelPath);
                }
            }

            // Draw shadow on trailing edge
            using (var shadowPath = new GraphicsPath())
            {
                float toothEnd = angle + toothAngle / 2;
                float toothMid = angle;

                var p1 = PolarToCartesian(centerX, centerY, outerRadius, toothMid);
                var p2 = PolarToCartesian(centerX, centerY, outerRadius, toothEnd);
                var p3 = PolarToCartesian(centerX, centerY, innerRadius * 1.1f, toothEnd);

                shadowPath.AddPolygon(new[] { p1, p2, p3 });

                using (var shadowBrush = new SolidBrush(Color.FromArgb(40, darkColor)))
                {
                    g.FillPath(shadowBrush, shadowPath);
                }
            }
        }

        private void HeaderPanel_Paint(object sender, PaintEventArgs e)
        {
            // Cuztom-style bottom border
            using var pen = new Pen(Color.FromArgb(22, 26, 32), 1);
            e.Graphics.DrawLine(pen, 0, headerPanel.Height - 1, headerPanel.Width, headerPanel.Height - 1);
        }

        private void FLP_Bots_Paint(object sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;

            if (_currentModeImage != null && FLP_Bots.Controls.Count == 0)
            {
                var image = _currentModeImage;
                var panelWidth = FLP_Bots.ClientSize.Width;
                var panelHeight = FLP_Bots.ClientSize.Height;

                float scale = 0.35f;
                int imageWidth = (int)(image.Width * scale);
                int imageHeight = (int)(image.Height * scale);

                int x = (panelWidth - imageWidth) / 2;
                int y = 30;

                using (var attributes = new ImageAttributes())
                {
                    float[][] matrixItems = {
                        new float[] {1, 0, 0, 0, 0},
                        new float[] {0, 1, 0, 0, 0},
                        new float[] {0, 0, 1, 0, 0},
                        new float[] {0, 0, 0, 0.1f, 0},
                        new float[] {0, 0, 0, 0, 1}
                    };
                    var colorMatrix = new ColorMatrix(matrixItems);
                    attributes.SetColorMatrix(colorMatrix);

                    g.DrawImage(image,
                        new Rectangle(x, y, imageWidth, imageHeight),
                        0, 0, image.Width, image.Height,
                        GraphicsUnit.Pixel, attributes);
                }

                using var font = ScaleFont(new Font("Segoe UI", 11F, FontStyle.Regular));
                using var brush = new SolidBrush(Color.FromArgb(139, 179, 217));
                var text = "No bots configured. Add a bot using the form above.";
                var size = g.MeasureString(text, font);
                g.DrawString(text, font, brush,
                    (panelWidth - size.Width) / 2,
                    y + imageHeight + 10);
            }
        }

        private void AnimationTimer_Tick(object sender, EventArgs e)
        {
            // Handle button hover animations
            foreach (Control control in GetAllControls(this))
            {
                if (control.Tag is ButtonAnimationState animState)
                {
                    var oldProgress = animState.HoverProgress;
                    var elapsed = (DateTime.Now - animState.AnimationStart).TotalMilliseconds;
                    var duration = 150.0;

                    if (animState.IsHovering)
                    {
                        animState.HoverProgress = Math.Min(1.0, elapsed / duration);
                    }
                    else
                    {
                        animState.HoverProgress = Math.Max(0.0, 1.0 - (elapsed / duration));
                    }

                    if (Math.Abs(animState.HoverProgress - oldProgress) > 0.01)
                    {
                        control.Invalidate();
                    }
                }

                if (control.Tag is EnhancedButtonAnimationState enhancedState)
                {
                    var oldProgress = enhancedState.HoverProgress;
                    var elapsed = (DateTime.Now - enhancedState.AnimationStart).TotalMilliseconds;
                    var duration = 200.0;

                    if (enhancedState.IsHovering)
                    {
                        enhancedState.HoverProgress = Math.Min(1.0f, (float)(elapsed / duration));
                    }
                    else
                    {
                        enhancedState.HoverProgress = Math.Max(0.0f, 1.0f - (float)(elapsed / duration));
                    }

                    enhancedState.PulsePhase += 0.08f;
                    if (enhancedState.PulsePhase > Math.PI * 2)
                        enhancedState.PulsePhase -= (float)(Math.PI * 2);

                    enhancedState.PulseIntensity = (float)((Math.Sin(enhancedState.PulsePhase) + 1) / 2);

                    if (Math.Abs(enhancedState.HoverProgress - oldProgress) > 0.01 || enhancedState.IsActive)
                    {
                        control.Invalidate();
                    }
                }
            }

            // Update status indicator pulse
            UpdateStatusIndicatorPulse();

            // Update gear rotations with proper gear ratio
            float baseSpeed = 0.5f; // Slow speed when stopped
            float activeSpeed = 3.0f; // Fast speed when running

            float activityLevel = _totalBotCount > 0 ? (float)_runningBotCount / _totalBotCount : 0f;
            float rotationSpeed = baseSpeed + (activeSpeed - baseSpeed) * activityLevel;

            // Gear ratio based on teeth count (16:10 = 1.6:1)
            float gearRatio = 16f / 10f;

            _gearRotation1 += rotationSpeed;
            _gearRotation2 -= rotationSpeed * gearRatio; // Smaller gear rotates faster

            if (_gearRotation1 > 360) _gearRotation1 -= 360;
            if (_gearRotation2 < -360) _gearRotation2 += 360;

            // ALWAYS redraw logo panel to show continuous animations
            logoPanel.Invalidate();
        }

        private void TransitionPanels(int index)
        {
            botsPanel.Visible = false;
            hubPanel.Visible = false;
            logsPanel.Visible = false;

            contentPanel.Refresh();

            switch (index)
            {
                case 0:
                    botsPanel.Visible = true;
                    break;
                case 1:
                    hubPanel.Visible = true;
                    break;
                case 2:
                    logsPanel.Visible = true;
                    logsPanel.Refresh();
                    break;
            }
        }

        private IEnumerable<Control> GetAllControls(Control container)
        {
            var controls = container.Controls.Cast<Control>();
            return controls.SelectMany(ctrl => GetAllControls(ctrl)).Concat(controls);
        }

        #endregion

        #region System Tray

        private void ConfigureSystemTray()
        {
            trayIcon.Icon = Icon;
            trayIcon.Text = "PokéBot Control Center";
            trayIcon.Visible = false;
            trayIcon.DoubleClick += TrayIcon_DoubleClick;

            trayContextMenu.BackColor = Color.FromArgb(27, 40, 56);
            trayContextMenu.Font = ScaleFont(new Font("Segoe UI", 9F));
            trayContextMenu.Renderer = new CuztomMenuRenderer();

            trayMenuShow.Text = "Show Window";
            trayMenuShow.ForeColor = Color.FromArgb(239, 239, 239);
            trayMenuShow.Click += TrayMenuShow_Click;

            var separator = new ToolStripSeparator();

            var trayMenuStart = new ToolStripMenuItem("Start All Bots");
            trayMenuStart.ForeColor = Color.FromArgb(90, 186, 71);
            trayMenuStart.Click += (s, e) => {
                RunningEnvironment.InitializeStart();
                foreach (var c in FLP_Bots.Controls.OfType<BotController>())
                    c.SendCommand(BotControlCommand.Start, false);
                LogUtil.LogInfo("All bots started from tray", "Tray");
            };

            var trayMenuStop = new ToolStripMenuItem("Stop All Bots");
            trayMenuStop.ForeColor = Color.FromArgb(236, 98, 95);
            trayMenuStop.Click += (s, e) => {
                RunningEnvironment.StopAll();
                LogUtil.LogInfo("All bots stopped from tray", "Tray");
            };

            var separator2 = new ToolStripSeparator();

            trayMenuExit.Text = "Exit";
            trayMenuExit.ForeColor = Color.FromArgb(236, 98, 95);
            trayMenuExit.Click += TrayMenuExit_Click;

            trayContextMenu.Items.AddRange(new ToolStripItem[] {
                trayMenuShow,
                separator,
                trayMenuStart,
                trayMenuStop,
                separator2,
                trayMenuExit
            });
            trayIcon.ContextMenuStrip = trayContextMenu;
        }

        #endregion

        #region Custom Classes

        private class CuztomMenuRenderer : ToolStripProfessionalRenderer
        {
            public CuztomMenuRenderer() : base(new CuztomColorTable()) { }

            protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
            {
                var rc = new Rectangle(Point.Empty, e.Item.Size);
                var c = e.Item.Selected ? Color.FromArgb(45, 51, 61) : Color.FromArgb(27, 40, 56);
                using (var brush = new SolidBrush(c))
                    e.Graphics.FillRectangle(brush, rc);
            }

            protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
            {
                e.TextColor = e.Item.Enabled ? e.Item.ForeColor : Color.FromArgb(100, 100, 100);
                base.OnRenderItemText(e);
            }
        }

        private class CuztomColorTable : ProfessionalColorTable
        {
            public override Color MenuItemSelected => Color.FromArgb(45, 51, 61);
            public override Color MenuItemBorder => Color.FromArgb(102, 192, 244);
            public override Color MenuBorder => Color.FromArgb(32, 38, 48);
            public override Color ToolStripDropDownBackground => Color.FromArgb(27, 40, 56);
            public override Color ImageMarginGradientBegin => Color.FromArgb(27, 40, 56);
            public override Color ImageMarginGradientMiddle => Color.FromArgb(27, 40, 56);
            public override Color ImageMarginGradientEnd => Color.FromArgb(27, 40, 56);
            public override Color SeparatorDark => Color.FromArgb(32, 38, 48);
            public override Color SeparatorLight => Color.FromArgb(45, 51, 61);
        }

        private class ButtonAnimationState
        {
            public bool IsHovering { get; set; }
            public DateTime AnimationStart { get; set; }
            public double HoverProgress { get; set; }
            public Color BaseColor { get; set; }
        }

        private class EnhancedButtonAnimationState
        {
            public bool IsHovering { get; set; }
            public bool IsPressed { get; set; }
            public bool IsActive { get; set; }
            public DateTime AnimationStart { get; set; }
            public float HoverProgress { get; set; }
            public float PulsePhase { get; set; }
            public float PulseIntensity { get; set; }
            public Color BaseColor { get; set; }
            public string IconText { get; set; } = "";
        }

        private class NavButtonState : ButtonAnimationState
        {
            public Color NeonColor { get; set; }
            public bool IsSelected { get; set; }
            public int Index { get; set; }
        }

        #endregion

        #region Controls Declaration

        private TableLayoutPanel mainLayoutPanel;
        private Panel sidebarPanel;
        private Panel contentPanel;
        private Panel headerPanel;
        private Panel logoPanel;
        private FlowLayoutPanel navButtonsPanel;
        private Button btnNavBots;
        private Button btnNavHub;
        private Button btnNavLogs;
        private Panel sidebarBottomPanel;
        private Button btnUpdate;
        private Button btnExit;
        private Label titleLabel;
        private FlowLayoutPanel controlButtonsPanel;
        private Button btnStart;
        private Button btnStop;
        private Button btnReboot;
        private Panel botsPanel;
        private Panel hubPanel;
        private Panel logsPanel;
        private Panel botHeaderPanel;
        private Panel addBotPanel;
        private TextBox TB_IP;
        private NumericUpDown NUD_Port;
        private ComboBox CB_Protocol;
        private ComboBox CB_Routine;
        private Button B_New;
        private FlowLayoutPanel FLP_Bots;
        private PropertyGrid PG_Hub;
        private RichTextBox RTB_Logs;
        private Panel logsHeaderPanel;
        private Panel searchPanel;
        private TextBox logSearchBox;
        private FlowLayoutPanel searchOptionsPanel;
        private CheckBox btnCaseSensitive;
        private CheckBox btnRegex;
        private CheckBox btnWholeWord;
        private Button btnClearLogs;
        private Label searchStatusLabel;
        private Panel statusIndicator;
        private System.Windows.Forms.Timer animationTimer;
        private ComboBox comboBox1;

        private NotifyIcon trayIcon;
        private ContextMenuStrip trayContextMenu;
        private ToolStripMenuItem trayMenuShow;
        private ToolStripMenuItem trayMenuExit;

        private Button updater => btnUpdate;
        private Button B_Start => btnStart;
        private Button B_Stop => btnStop;
        private Button B_RebootStop => btnReboot;
        private TabControl TC_Main;
        private TabPage Tab_Bots;
        private TabPage Tab_Hub;
        private TabPage Tab_Logs;
        private Panel ButtonPanel => controlButtonsPanel;

        private int _idlingBotCount = 0;
        private readonly List<ElectricArc> _electricArcs = new List<ElectricArc>();
        private readonly Random _random = new Random();
        private int _runningBotCount = 0;
        private int _totalBotCount = 0;
        private float _gearRotation1 = 0f;
        private float _gearRotation2 = 0f;
        #endregion
    }

    public class ElectricArc
    {
        public PointF Start { get; set; }
        public PointF End { get; set; }
        public float Intensity { get; set; }
        public float Life { get; set; }
        public List<PointF> Points { get; set; } = new List<PointF>();
    }

    public static class GraphicsExtensions
    {
        public static void AddRoundedRectangle(this GraphicsPath path, Rectangle rect, int radius)
        {
            path.AddArc(rect.X, rect.Y, radius * 2, radius * 2, 180, 90);
            path.AddArc(rect.Right - radius * 2, rect.Y, radius * 2, radius * 2, 270, 90);
            path.AddArc(rect.Right - radius * 2, rect.Bottom - radius * 2, radius * 2, radius * 2, 0, 90);
            path.AddArc(rect.X, rect.Bottom - radius * 2, radius * 2, radius * 2, 90, 90);
            path.CloseFigure();
        }
    }
}

#pragma warning restore CS8618
#pragma warning restore CS8625
#pragma warning restore CS8669
