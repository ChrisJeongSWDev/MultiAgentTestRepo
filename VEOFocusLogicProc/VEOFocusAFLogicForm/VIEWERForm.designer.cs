namespace VIEWORKS_AFTS
{
    partial class VIEWERForm
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _renderDriver?.Dispose();
                _renderDriver = null;
                _uiPollingDriver?.Dispose();
                _uiPollingDriver = null;
                if (ImageViewThread != null && ImageViewThread.IsBusy)
                {
                    ImageViewThread.CancelAsync();
                }
                if (components != null)
                {
                    components.Dispose();
                }
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.Grabber_ImageViewer = new CHLib.Controls.CHImageViewer();
            this.panel1 = new System.Windows.Forms.Panel();
            this.FPSText = new System.Windows.Forms.Label();
            this.FrameText = new System.Windows.Forms.Label();
            this.ZoomRatioLabel = new System.Windows.Forms.Label();
            this.IntensityLabel = new System.Windows.Forms.Label();
            this.YPoSLabel = new System.Windows.Forms.Label();
            this.XPoSLabel = new System.Windows.Forms.Label();
            this.ImageViewThread = new System.ComponentModel.BackgroundWorker();
            this.panel1.SuspendLayout();
            this.SuspendLayout();
            // 
            // Grabber_ImageViewer
            // 
            this.Grabber_ImageViewer.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.Grabber_ImageViewer.BackColor = System.Drawing.Color.DarkGray;
            this.Grabber_ImageViewer.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
            this.Grabber_ImageViewer.Location = new System.Drawing.Point(0, 0);
            this.Grabber_ImageViewer.Name = "Grabber_ImageViewer";
            this.Grabber_ImageViewer.Size = new System.Drawing.Size(1120, 540);
            this.Grabber_ImageViewer.TabIndex = 5;
            this.Grabber_ImageViewer.Text = "chImageViewer1";
            this.Grabber_ImageViewer.ZoomMode = CHLib.Controls.ImageZoomMode.None;
            this.Grabber_ImageViewer.ZoomPoint = new System.Drawing.Point(0, 0);
            this.Grabber_ImageViewer.MouseMove += new System.Windows.Forms.MouseEventHandler(this.Grabber_ImageViewer_MouseMove);
            // 
            // panel1
            // 
            this.panel1.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.panel1.BackColor = System.Drawing.Color.Gray;
            this.panel1.Controls.Add(this.FPSText);
            this.panel1.Controls.Add(this.FrameText);
            this.panel1.Controls.Add(this.ZoomRatioLabel);
            this.panel1.Controls.Add(this.IntensityLabel);
            this.panel1.Controls.Add(this.YPoSLabel);
            this.panel1.Controls.Add(this.XPoSLabel);
            this.panel1.Location = new System.Drawing.Point(0, 540);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(1120, 40);
            this.panel1.TabIndex = 6;
            // 
            // FPSText
            // 
            this.FPSText.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.FPSText.AutoSize = true;
            this.FPSText.Font = new System.Drawing.Font("Century Gothic", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.FPSText.ForeColor = System.Drawing.Color.White;
            this.FPSText.Location = new System.Drawing.Point(988, 13);
            this.FPSText.Name = "FPSText";
            this.FPSText.Size = new System.Drawing.Size(29, 16);
            this.FPSText.TabIndex = 129;
            this.FPSText.Text = "FPS:";
            // 
            // FrameText
            // 
            this.FrameText.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.FrameText.AutoSize = true;
            this.FrameText.Font = new System.Drawing.Font("Century Gothic", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.FrameText.ForeColor = System.Drawing.Color.White;
            this.FrameText.Location = new System.Drawing.Point(867, 13);
            this.FrameText.Name = "FrameText";
            this.FrameText.Size = new System.Drawing.Size(47, 16);
            this.FrameText.TabIndex = 128;
            this.FrameText.Text = "Frame:";
            // 
            // ZoomRatioLabel
            // 
            this.ZoomRatioLabel.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.ZoomRatioLabel.AutoSize = true;
            this.ZoomRatioLabel.Font = new System.Drawing.Font("Century Gothic", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.ZoomRatioLabel.ForeColor = System.Drawing.Color.White;
            this.ZoomRatioLabel.Location = new System.Drawing.Point(195, 12);
            this.ZoomRatioLabel.Name = "ZoomRatioLabel";
            this.ZoomRatioLabel.Size = new System.Drawing.Size(75, 16);
            this.ZoomRatioLabel.TabIndex = 127;
            this.ZoomRatioLabel.Text = "Zoom Ratio:";
            // 
            // IntensityLabel
            // 
            this.IntensityLabel.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.IntensityLabel.AutoSize = true;
            this.IntensityLabel.Font = new System.Drawing.Font("Century Gothic", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.IntensityLabel.ForeColor = System.Drawing.Color.White;
            this.IntensityLabel.Location = new System.Drawing.Point(103, 12);
            this.IntensityLabel.Name = "IntensityLabel";
            this.IntensityLabel.Size = new System.Drawing.Size(57, 16);
            this.IntensityLabel.TabIndex = 124;
            this.IntensityLabel.Text = "Intensity:";
            // 
            // YPoSLabel
            // 
            this.YPoSLabel.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.YPoSLabel.AutoSize = true;
            this.YPoSLabel.Font = new System.Drawing.Font("Century Gothic", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.YPoSLabel.ForeColor = System.Drawing.Color.White;
            this.YPoSLabel.Location = new System.Drawing.Point(59, 12);
            this.YPoSLabel.Name = "YPoSLabel";
            this.YPoSLabel.Size = new System.Drawing.Size(17, 16);
            this.YPoSLabel.TabIndex = 123;
            this.YPoSLabel.Text = "Y:";
            // 
            // XPoSLabel
            // 
            this.XPoSLabel.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.XPoSLabel.AutoSize = true;
            this.XPoSLabel.Font = new System.Drawing.Font("Century Gothic", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.XPoSLabel.ForeColor = System.Drawing.Color.White;
            this.XPoSLabel.Location = new System.Drawing.Point(6, 12);
            this.XPoSLabel.Name = "XPoSLabel";
            this.XPoSLabel.Size = new System.Drawing.Size(18, 16);
            this.XPoSLabel.TabIndex = 122;
            this.XPoSLabel.Text = "X:";
            // 
            // ImageViewThread
            // 
            this.ImageViewThread.WorkerSupportsCancellation = true;
            this.ImageViewThread.DoWork += new System.ComponentModel.DoWorkEventHandler(this.ImageViewThread_DoWork);
            this.ImageViewThread.RunWorkerCompleted += new System.ComponentModel.RunWorkerCompletedEventHandler(this.ImageViewThread_RunWorkerCompleted);
            // 
            // VIEWERForm
            // 
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
            this.ClientSize = new System.Drawing.Size(1120, 580);
            this.Controls.Add(this.panel1);
            this.Controls.Add(this.Grabber_ImageViewer);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
            this.Name = "VIEWERForm";
            this.Text = "VIEWERForm";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.GRABBER_IMAGEVIEW_FormClosing);
            this.panel1.ResumeLayout(false);
            this.panel1.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private CHLib.Controls.CHImageViewer Grabber_ImageViewer;
        private System.Windows.Forms.Panel panel1;
        private System.Windows.Forms.Label FPSText;
        private System.Windows.Forms.Label FrameText;
        private System.Windows.Forms.Label ZoomRatioLabel;
        private System.Windows.Forms.Label IntensityLabel;
        private System.Windows.Forms.Label YPoSLabel;
        private System.Windows.Forms.Label XPoSLabel;
        private System.ComponentModel.BackgroundWorker ImageViewThread;
    }
}