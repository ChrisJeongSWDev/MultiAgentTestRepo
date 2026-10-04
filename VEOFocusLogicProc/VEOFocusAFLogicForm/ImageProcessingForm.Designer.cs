namespace VIEWORKS_AFTS
{
    partial class Vieworks_AFTS
    {
        /// <summary>
        /// 필수 디자이너 변수입니다.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// 사용 중인 모든 리소스를 정리합니다.
        /// </summary>
        /// <param name="disposing">관리되는 리소스를 삭제해야 하면 true이고, 그렇지 않으면 false입니다.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form 디자이너에서 생성한 코드

        /// <summary>
        /// 디자이너 지원에 필요한 메서드입니다. 
        /// 이 메서드의 내용을 코드 편집기로 수정하지 마세요.
        /// </summary>
        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Vieworks_AFTS));
            this.PanelMainBar = new System.Windows.Forms.Panel();
            this.ProgramMinimizeButton = new System.Windows.Forms.Button();
            this.ProgramExitButton = new System.Windows.Forms.Button();
            this.ProgramMaximizeButton = new System.Windows.Forms.Button();
            this.label3 = new System.Windows.Forms.Label();
            this.btnAFLogicTest = new System.Windows.Forms.Button();
            this.PanelLogo = new System.Windows.Forms.Panel();
            this.label1 = new System.Windows.Forms.Label();
            this.PanelMenu = new System.Windows.Forms.Panel();
            this.PanelMainForm = new System.Windows.Forms.Panel();
            this.CameraSimulator = new System.ComponentModel.BackgroundWorker();
            this.ImageProcessingThread = new System.ComponentModel.BackgroundWorker();
            this.PanelMainBar.SuspendLayout();
            this.PanelLogo.SuspendLayout();
            this.PanelMenu.SuspendLayout();
            this.SuspendLayout();
            // 
            // PanelMainBar
            // 
            this.PanelMainBar.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.PanelMainBar.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(51)))), ((int)(((byte)(51)))), ((int)(((byte)(120)))));
            this.PanelMainBar.Controls.Add(this.ProgramMinimizeButton);
            this.PanelMainBar.Controls.Add(this.ProgramExitButton);
            this.PanelMainBar.Controls.Add(this.ProgramMaximizeButton);
            this.PanelMainBar.Controls.Add(this.label3);
            this.PanelMainBar.Location = new System.Drawing.Point(0, 0);
            this.PanelMainBar.Name = "PanelMainBar";
            this.PanelMainBar.Size = new System.Drawing.Size(1920, 62);
            this.PanelMainBar.TabIndex = 0;
            this.PanelMainBar.MouseDown += new System.Windows.Forms.MouseEventHandler(this.PanelMainBar_MouseDown);
            this.PanelMainBar.MouseMove += new System.Windows.Forms.MouseEventHandler(this.PanelMainBar_MouseMove);
            // 
            // ProgramMinimizeButton
            // 
            this.ProgramMinimizeButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.ProgramMinimizeButton.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(51)))), ((int)(((byte)(51)))), ((int)(((byte)(120)))));
            this.ProgramMinimizeButton.FlatAppearance.BorderColor = System.Drawing.Color.White;
            this.ProgramMinimizeButton.FlatAppearance.BorderSize = 0;
            this.ProgramMinimizeButton.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.ProgramMinimizeButton.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.ProgramMinimizeButton.ForeColor = System.Drawing.Color.White;
            this.ProgramMinimizeButton.Image = ((System.Drawing.Image)(resources.GetObject("ProgramMinimizeButton.Image")));
            this.ProgramMinimizeButton.ImageAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.ProgramMinimizeButton.Location = new System.Drawing.Point(1738, 0);
            this.ProgramMinimizeButton.Name = "ProgramMinimizeButton";
            this.ProgramMinimizeButton.Padding = new System.Windows.Forms.Padding(12, 0, 0, 0);
            this.ProgramMinimizeButton.Size = new System.Drawing.Size(57, 50);
            this.ProgramMinimizeButton.TabIndex = 11;
            this.ProgramMinimizeButton.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            this.ProgramMinimizeButton.TextImageRelation = System.Windows.Forms.TextImageRelation.TextBeforeImage;
            this.ProgramMinimizeButton.UseVisualStyleBackColor = false;
            this.ProgramMinimizeButton.Click += new System.EventHandler(this.ProgramMinimizeButton_Click);
            // 
            // ProgramExitButton
            // 
            this.ProgramExitButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.ProgramExitButton.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(51)))), ((int)(((byte)(51)))), ((int)(((byte)(120)))));
            this.ProgramExitButton.FlatAppearance.BorderColor = System.Drawing.Color.White;
            this.ProgramExitButton.FlatAppearance.BorderSize = 0;
            this.ProgramExitButton.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.ProgramExitButton.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.ProgramExitButton.ForeColor = System.Drawing.Color.White;
            this.ProgramExitButton.Image = ((System.Drawing.Image)(resources.GetObject("ProgramExitButton.Image")));
            this.ProgramExitButton.ImageAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.ProgramExitButton.Location = new System.Drawing.Point(1858, 1);
            this.ProgramExitButton.Name = "ProgramExitButton";
            this.ProgramExitButton.Padding = new System.Windows.Forms.Padding(12, 0, 0, 0);
            this.ProgramExitButton.Size = new System.Drawing.Size(57, 50);
            this.ProgramExitButton.TabIndex = 10;
            this.ProgramExitButton.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            this.ProgramExitButton.TextImageRelation = System.Windows.Forms.TextImageRelation.TextBeforeImage;
            this.ProgramExitButton.UseVisualStyleBackColor = false;
            this.ProgramExitButton.Click += new System.EventHandler(this.ProgramExitButton_Click);
            // 
            // ProgramMaximizeButton
            // 
            this.ProgramMaximizeButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.ProgramMaximizeButton.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(51)))), ((int)(((byte)(51)))), ((int)(((byte)(120)))));
            this.ProgramMaximizeButton.FlatAppearance.BorderColor = System.Drawing.Color.White;
            this.ProgramMaximizeButton.FlatAppearance.BorderSize = 0;
            this.ProgramMaximizeButton.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.ProgramMaximizeButton.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.ProgramMaximizeButton.ForeColor = System.Drawing.Color.White;
            this.ProgramMaximizeButton.Image = ((System.Drawing.Image)(resources.GetObject("ProgramMaximizeButton.Image")));
            this.ProgramMaximizeButton.ImageAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.ProgramMaximizeButton.Location = new System.Drawing.Point(1797, 1);
            this.ProgramMaximizeButton.Name = "ProgramMaximizeButton";
            this.ProgramMaximizeButton.Padding = new System.Windows.Forms.Padding(12, 0, 0, 0);
            this.ProgramMaximizeButton.Size = new System.Drawing.Size(57, 50);
            this.ProgramMaximizeButton.TabIndex = 9;
            this.ProgramMaximizeButton.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            this.ProgramMaximizeButton.TextImageRelation = System.Windows.Forms.TextImageRelation.TextBeforeImage;
            this.ProgramMaximizeButton.UseVisualStyleBackColor = false;
            this.ProgramMaximizeButton.Click += new System.EventHandler(this.ProgramMaximizeButton_Click);
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Font = new System.Drawing.Font("Century Gothic", 24F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label3.ForeColor = System.Drawing.Color.White;
            this.label3.Location = new System.Drawing.Point(7, 8);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(573, 38);
            this.label3.TabIndex = 2;
            this.label3.Text = "IMAGE PROCESSING TEST PROGRAM";
            // 
            // btnAFLogicTest
            // 
            this.btnAFLogicTest.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(130)))), ((int)(((byte)(50)))), ((int)(((byte)(50)))));
            this.btnAFLogicTest.FlatAppearance.BorderSize = 0;
            this.btnAFLogicTest.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnAFLogicTest.Font = new System.Drawing.Font("Century Gothic", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.btnAFLogicTest.ForeColor = System.Drawing.Color.White;
            this.btnAFLogicTest.Location = new System.Drawing.Point(0, 46);
            this.btnAFLogicTest.Name = "btnAFLogicTest";
            this.btnAFLogicTest.Size = new System.Drawing.Size(140, 56);
            this.btnAFLogicTest.TabIndex = 1;
            this.btnAFLogicTest.Text = "AF LOGIC TEST";
            this.btnAFLogicTest.UseVisualStyleBackColor = false;
            // 
            // PanelLogo
            // 
            this.PanelLogo.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(31)))), ((int)(((byte)(51)))), ((int)(((byte)(100)))));
            this.PanelLogo.Controls.Add(this.label1);
            this.PanelLogo.Location = new System.Drawing.Point(0, 1);
            this.PanelLogo.Name = "PanelLogo";
            this.PanelLogo.Size = new System.Drawing.Size(145, 45);
            this.PanelLogo.TabIndex = 0;
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Font = new System.Drawing.Font("Century Gothic", 18F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label1.ForeColor = System.Drawing.Color.White;
            this.label1.Location = new System.Drawing.Point(32, 9);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(79, 28);
            this.label1.TabIndex = 0;
            this.label1.Text = "MENU";
            // 
            // PanelMenu
            // 
            this.PanelMenu.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left)));
            this.PanelMenu.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(51)))), ((int)(((byte)(51)))), ((int)(((byte)(120)))));
            this.PanelMenu.Controls.Add(this.btnAFLogicTest);
            this.PanelMenu.Controls.Add(this.PanelLogo);
            this.PanelMenu.Location = new System.Drawing.Point(0, 53);
            this.PanelMenu.Name = "PanelMenu";
            this.PanelMenu.Size = new System.Drawing.Size(148, 1027);
            this.PanelMenu.TabIndex = 1;
            // 
            // PanelMainForm
            // 
            this.PanelMainForm.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.PanelMainForm.Location = new System.Drawing.Point(146, 61);
            this.PanelMainForm.Name = "PanelMainForm";
            this.PanelMainForm.Size = new System.Drawing.Size(1773, 1019);
            this.PanelMainForm.TabIndex = 3;
            // 
            // CameraSimulator
            // 
            this.CameraSimulator.RunWorkerCompleted += new System.ComponentModel.RunWorkerCompletedEventHandler(this.CameraSimulator_RunWorkerCompleted);
            // 
            // ImageProcessingThread
            // 
            this.ImageProcessingThread.DoWork += new System.ComponentModel.DoWorkEventHandler(this.ImageProcessingThread_DoWork);
            // 
            // Vieworks_AFTS
            // 
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
            this.BackColor = System.Drawing.Color.White;
            this.ClientSize = new System.Drawing.Size(1920, 1080);
            this.Controls.Add(this.PanelMainForm);
            this.Controls.Add(this.PanelMenu);
            this.Controls.Add(this.PanelMainBar);
            this.DoubleBuffered = true;
            this.ForeColor = System.Drawing.Color.White;
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
            this.Name = "Vieworks_AFTS";
            this.Text = "Vieworks_AFTS";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.Vieworks_AFTS_FormClosing);
            this.Load += new System.EventHandler(this.Vieworks_AFTS_Load);
            this.PanelMainBar.ResumeLayout(false);
            this.PanelMainBar.PerformLayout();
            this.PanelLogo.ResumeLayout(false);
            this.PanelLogo.PerformLayout();
            this.PanelMenu.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Panel PanelMainBar;
        private System.Windows.Forms.Panel PanelLogo;
        private System.Windows.Forms.Panel PanelMenu;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.Button ProgramMaximizeButton;
        private System.Windows.Forms.Button ProgramMinimizeButton;
        private System.Windows.Forms.Button ProgramExitButton;
        private System.Windows.Forms.Panel PanelMainForm;
        private System.Windows.Forms.Button btnAFLogicTest;
        private System.ComponentModel.BackgroundWorker CameraSimulator;
        private System.ComponentModel.BackgroundWorker ImageProcessingThread;
    }
}
