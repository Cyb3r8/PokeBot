using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace SysBot.Pokemon.WinForms
{
    partial class BotController
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            if (animationTimer != null)
            {
                animationTimer.Stop();
                animationTimer.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Component Designer generated code

        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.mainPanel = new System.Windows.Forms.Panel();
            this.topPanel = new System.Windows.Forms.Panel();
            this.statusIndicator = new System.Windows.Forms.PictureBox();
            this.lblBotName = new System.Windows.Forms.Label();
            this.lblRoutineType = new System.Windows.Forms.Label();
            this.lblConnectionInfo = new System.Windows.Forms.Label();
            this.bottomPanel = new System.Windows.Forms.Panel();
            this.statusPanel = new System.Windows.Forms.Panel();
            this.lblStatus = new System.Windows.Forms.Label();
            this.lblStatusValue = new System.Windows.Forms.Label();
            this.actionPanel = new System.Windows.Forms.Panel();
            this.btnActions = new System.Windows.Forms.Button();
            this.contextMenu = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.animationTimer = new System.Windows.Forms.Timer(this.components);
            this.mainPanel.SuspendLayout();
            this.topPanel.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.statusIndicator)).BeginInit();
            this.bottomPanel.SuspendLayout();
            this.statusPanel.SuspendLayout();
            this.actionPanel.SuspendLayout();
            this.SuspendLayout();

            // BotController
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.FromArgb(27, 40, 56);
            this.Controls.Add(this.mainPanel);
            this.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.Margin = new System.Windows.Forms.Padding(0, 0, 0, 5);
            this.Name = "BotController";
            this.Size = new System.Drawing.Size(900, 70);
            this.Paint += new System.Windows.Forms.PaintEventHandler(this.BotController_Paint);
            this.MouseEnter += new System.EventHandler(this.BotController_MouseEnter);
            this.MouseLeave += new System.EventHandler(this.BotController_MouseLeave);

            // Main Panel
            this.mainPanel.Anchor = System.Windows.Forms.AnchorStyles.Top |
                                     System.Windows.Forms.AnchorStyles.Bottom |
                                     System.Windows.Forms.AnchorStyles.Left |
                                     System.Windows.Forms.AnchorStyles.Right;
            this.mainPanel.BackColor = System.Drawing.Color.FromArgb(22, 32, 45);
            this.mainPanel.Controls.Add(this.topPanel);
            this.mainPanel.Controls.Add(this.bottomPanel);
            this.mainPanel.Location = new System.Drawing.Point(3, 3);
            this.mainPanel.Name = "mainPanel";
            this.mainPanel.Size = new System.Drawing.Size(894, 64);
            this.mainPanel.TabIndex = 0;
            this.mainPanel.Paint += new System.Windows.Forms.PaintEventHandler(this.MainPanel_Paint);
            this.mainPanel.MouseEnter += new System.EventHandler(this.BotController_MouseEnter);
            this.mainPanel.MouseLeave += new System.EventHandler(this.BotController_MouseLeave);

            // Top Panel
            this.topPanel.Anchor = System.Windows.Forms.AnchorStyles.Top |
                                   System.Windows.Forms.AnchorStyles.Left |
                                   System.Windows.Forms.AnchorStyles.Right;
            this.topPanel.BackColor = System.Drawing.Color.Transparent;
            this.topPanel.Controls.Add(this.statusIndicator);
            this.topPanel.Controls.Add(this.lblBotName);
            this.topPanel.Controls.Add(this.lblRoutineType);
            this.topPanel.Controls.Add(this.lblConnectionInfo);
            this.topPanel.Location = new System.Drawing.Point(0, 0);
            this.topPanel.Name = "topPanel";
            this.topPanel.Size = new System.Drawing.Size(894, 35);
            this.topPanel.TabIndex = 0;
            this.topPanel.MouseEnter += new System.EventHandler(this.BotController_MouseEnter);
            this.topPanel.MouseLeave += new System.EventHandler(this.BotController_MouseLeave);

            // Status Indicator
            this.statusIndicator.BackColor = System.Drawing.Color.FromArgb(90, 186, 71);
            this.statusIndicator.Location = new System.Drawing.Point(15, 13);
            this.statusIndicator.Name = "statusIndicator";
            this.statusIndicator.Size = new System.Drawing.Size(8, 8);
            this.statusIndicator.TabIndex = 0;
            this.statusIndicator.TabStop = false;
            this.statusIndicator.Paint += new System.Windows.Forms.PaintEventHandler(this.StatusIndicator_Paint);

            // Bot Name Label
            this.lblBotName.AutoSize = false;
            this.lblBotName.Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Bold);
            this.lblBotName.ForeColor = System.Drawing.Color.FromArgb(239, 239, 239);
            this.lblBotName.Location = new System.Drawing.Point(35, 5);
            this.lblBotName.Name = "lblBotName";
            this.lblBotName.Size = new System.Drawing.Size(180, 18);
            this.lblBotName.TabIndex = 1;
            this.lblBotName.Text = "192.168.1.100";
            this.lblBotName.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            this.lblBotName.MouseEnter += new System.EventHandler(this.BotController_MouseEnter);
            this.lblBotName.MouseLeave += new System.EventHandler(this.BotController_MouseLeave);

            // Routine Type Label
            this.lblRoutineType.AutoSize = false;
            this.lblRoutineType.Font = new System.Drawing.Font("Segoe UI", 8F);
            this.lblRoutineType.ForeColor = System.Drawing.Color.FromArgb(139, 179, 217);
            this.lblRoutineType.Location = new System.Drawing.Point(35, 20);
            this.lblRoutineType.Name = "lblRoutineType";
            this.lblRoutineType.Size = new System.Drawing.Size(180, 12);
            this.lblRoutineType.TabIndex = 2;
            this.lblRoutineType.Text = "FlexTrade";
            this.lblRoutineType.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            this.lblRoutineType.MouseEnter += new System.EventHandler(this.BotController_MouseEnter);
            this.lblRoutineType.MouseLeave += new System.EventHandler(this.BotController_MouseLeave);

            // Connection Info Label
            this.lblConnectionInfo.Anchor = System.Windows.Forms.AnchorStyles.Top |
                                           System.Windows.Forms.AnchorStyles.Left |
                                           System.Windows.Forms.AnchorStyles.Right;
            this.lblConnectionInfo.AutoEllipsis = true;
            this.lblConnectionInfo.Font = new System.Drawing.Font("Segoe UI", 8F);
            this.lblConnectionInfo.ForeColor = System.Drawing.Color.FromArgb(176, 176, 176);
            this.lblConnectionInfo.Location = new System.Drawing.Point(220, 10);
            this.lblConnectionInfo.Name = "lblConnectionInfo";
            this.lblConnectionInfo.Size = new System.Drawing.Size(450, 15);
            this.lblConnectionInfo.TabIndex = 3;
            this.lblConnectionInfo.Text = "Waiting for command...";
            this.lblConnectionInfo.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            this.lblConnectionInfo.MouseEnter += new System.EventHandler(this.BotController_MouseEnter);
            this.lblConnectionInfo.MouseLeave += new System.EventHandler(this.BotController_MouseLeave);

            // Bottom Panel
            this.bottomPanel.Anchor = System.Windows.Forms.AnchorStyles.Bottom |
                                      System.Windows.Forms.AnchorStyles.Left |
                                      System.Windows.Forms.AnchorStyles.Right;
            this.bottomPanel.BackColor = System.Drawing.Color.FromArgb(16, 24, 34);
            this.bottomPanel.Controls.Add(this.statusPanel);
            this.bottomPanel.Controls.Add(this.actionPanel);
            this.bottomPanel.Location = new System.Drawing.Point(0, 35);
            this.bottomPanel.Name = "bottomPanel";
            this.bottomPanel.Size = new System.Drawing.Size(894, 29);
            this.bottomPanel.TabIndex = 1;
            this.bottomPanel.Paint += new System.Windows.Forms.PaintEventHandler(this.BottomPanel_Paint);
            this.bottomPanel.MouseEnter += new System.EventHandler(this.BotController_MouseEnter);
            this.bottomPanel.MouseLeave += new System.EventHandler(this.BotController_MouseLeave);

            // Status Panel
            this.statusPanel.BackColor = System.Drawing.Color.Transparent;
            this.statusPanel.Controls.Add(this.lblStatus);
            this.statusPanel.Controls.Add(this.lblStatusValue);
            this.statusPanel.Location = new System.Drawing.Point(15, 0);
            this.statusPanel.Name = "statusPanel";
            this.statusPanel.Size = new System.Drawing.Size(250, 29);
            this.statusPanel.TabIndex = 0;
            this.statusPanel.MouseEnter += new System.EventHandler(this.BotController_MouseEnter);
            this.statusPanel.MouseLeave += new System.EventHandler(this.BotController_MouseLeave);

            // Status Label
            this.lblStatus.AutoSize = false;
            this.lblStatus.Font = new System.Drawing.Font("Segoe UI", 7F);
            this.lblStatus.ForeColor = System.Drawing.Color.FromArgb(139, 179, 217);
            this.lblStatus.Location = new System.Drawing.Point(0, 8);
            this.lblStatus.Name = "lblStatus";
            this.lblStatus.Size = new System.Drawing.Size(45, 12);
            this.lblStatus.TabIndex = 0;
            this.lblStatus.Text = "STATUS";
            this.lblStatus.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;

            // Status Value Label
            this.lblStatusValue.AutoSize = false;
            this.lblStatusValue.Font = new System.Drawing.Font("Segoe UI", 8F, System.Drawing.FontStyle.Bold);
            this.lblStatusValue.ForeColor = System.Drawing.Color.FromArgb(90, 186, 71);
            this.lblStatusValue.Location = new System.Drawing.Point(50, 7);
            this.lblStatusValue.Name = "lblStatusValue";
            this.lblStatusValue.Size = new System.Drawing.Size(180, 14);
            this.lblStatusValue.TabIndex = 1;
            this.lblStatusValue.Text = "RUNNING";
            this.lblStatusValue.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;

            // Action Panel
            this.actionPanel.Anchor = System.Windows.Forms.AnchorStyles.Top |
                                      System.Windows.Forms.AnchorStyles.Right;
            this.actionPanel.BackColor = System.Drawing.Color.Transparent;
            this.actionPanel.Controls.Add(this.btnActions);
            this.actionPanel.Location = new System.Drawing.Point(720, 0);
            this.actionPanel.Name = "actionPanel";
            this.actionPanel.Size = new System.Drawing.Size(160, 29);
            this.actionPanel.TabIndex = 1;

            // Actions Button
            this.btnActions.Anchor = System.Windows.Forms.AnchorStyles.Right;
            this.btnActions.BackColor = System.Drawing.Color.FromArgb(102, 192, 244);
            this.btnActions.FlatAppearance.BorderSize = 0;
            this.btnActions.FlatAppearance.MouseDownBackColor = System.Drawing.Color.FromArgb(92, 173, 220);
            this.btnActions.FlatAppearance.MouseOverBackColor = System.Drawing.Color.FromArgb(122, 207, 255);
            this.btnActions.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnActions.Font = new System.Drawing.Font("Segoe UI", 8F, System.Drawing.FontStyle.Bold);
            this.btnActions.ForeColor = System.Drawing.Color.FromArgb(22, 32, 45);
            this.btnActions.Location = new System.Drawing.Point(50, 3);
            this.btnActions.Name = "btnActions";
            this.btnActions.Size = new System.Drawing.Size(100, 23);
            this.btnActions.TabIndex = 0;
            this.btnActions.Text = "▼ ACTIONS";
            this.btnActions.UseVisualStyleBackColor = false;
            this.btnActions.Click += new System.EventHandler(this.BtnActions_Click);
            this.btnActions.Paint += new System.Windows.Forms.PaintEventHandler(this.BtnActions_Paint);
            this.btnActions.MouseEnter += new System.EventHandler(this.BtnActions_MouseEnter);
            this.btnActions.MouseLeave += new System.EventHandler(this.BtnActions_MouseLeave);

            // Context Menu
            this.contextMenu.BackColor = System.Drawing.Color.FromArgb(27, 40, 56);
            this.contextMenu.Font = new System.Drawing.Font("Segoe UI", 9F);
            this.contextMenu.Name = "contextMenu";
            this.contextMenu.RenderMode = System.Windows.Forms.ToolStripRenderMode.Professional;
            this.contextMenu.ShowImageMargin = false;
            this.contextMenu.Size = new System.Drawing.Size(150, 4);

            // Animation Timer
            this.animationTimer.Interval = 50;
            this.animationTimer.Tick += new System.EventHandler(this.AnimationTimer_Tick);
            this.animationTimer.Enabled = true;

            // Component initialization
            this.L_Description = this.lblConnectionInfo;
            this.L_Left = this.lblBotName;
            this.PB_Lamp = this.statusIndicator;
            this.RCMenu = this.contextMenu;
            this.statusPanel = this.statusPanel;
            this.progressBar = new System.Windows.Forms.Panel();
            this.actionButton = this.btnActions;

            this.mainPanel.ResumeLayout(false);
            this.topPanel.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.statusIndicator)).EndInit();
            this.bottomPanel.ResumeLayout(false);
            this.statusPanel.ResumeLayout(false);
            this.actionPanel.ResumeLayout(false);
            this.ResumeLayout(false);
        }

        #endregion

        // Legacy controls for compatibility
        private System.Windows.Forms.Label L_Description;
        private System.Windows.Forms.Label L_Left;
        private System.Windows.Forms.PictureBox PB_Lamp;
        private System.Windows.Forms.ContextMenuStrip RCMenu;
        private System.Windows.Forms.Panel statusPanel;
        private System.Windows.Forms.Panel progressBar;
        private System.Windows.Forms.Button actionButton;
        private System.Windows.Forms.Timer animationTimer;

        // New controls
        private System.Windows.Forms.Panel mainPanel;
        private System.Windows.Forms.Panel topPanel;
        private System.Windows.Forms.Panel bottomPanel;
        private System.Windows.Forms.PictureBox statusIndicator;
        private System.Windows.Forms.Label lblBotName;
        private System.Windows.Forms.Label lblRoutineType;
        private System.Windows.Forms.Label lblConnectionInfo;
        private System.Windows.Forms.Label lblStatus;
        private System.Windows.Forms.Label lblStatusValue;
        private System.Windows.Forms.Panel actionPanel;
        private System.Windows.Forms.Button btnActions;
        private System.Windows.Forms.ContextMenuStrip contextMenu;
    }
}
