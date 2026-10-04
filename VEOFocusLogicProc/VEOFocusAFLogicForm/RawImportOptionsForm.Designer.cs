namespace VIEWORKS_AFTS
{
    partial class RawImportOptionsForm
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        private void InitializeComponent()
        {
            this.labelFileInfo = new System.Windows.Forms.Label();
            this.labelPrefixInfo = new System.Windows.Forms.Label();
            this.labelWidth = new System.Windows.Forms.Label();
            this.TextBox_Width = new System.Windows.Forms.TextBox();
            this.labelHeight = new System.Windows.Forms.Label();
            this.TextBox_Height = new System.Windows.Forms.TextBox();
            this.labelPreset = new System.Windows.Forms.Label();
            this.comboPreset = new System.Windows.Forms.ComboBox();
            this.labelValidation = new System.Windows.Forms.Label();
            this.btnOK = new System.Windows.Forms.Button();
            this.btnCancel = new System.Windows.Forms.Button();
            this.SuspendLayout();
            //
            // labelFileInfo
            //
            this.labelFileInfo.AutoSize = true;
            this.labelFileInfo.Font = new System.Drawing.Font("Century Gothic", 9F, System.Drawing.FontStyle.Bold);
            this.labelFileInfo.Location = new System.Drawing.Point(16, 15);
            this.labelFileInfo.Name = "labelFileInfo";
            this.labelFileInfo.Size = new System.Drawing.Size(200, 17);
            this.labelFileInfo.TabIndex = 0;
            this.labelFileInfo.Text = "File size :        Frames :";
            //
            // labelPrefixInfo
            //
            this.labelPrefixInfo.AutoSize = true;
            this.labelPrefixInfo.Font = new System.Drawing.Font("Century Gothic", 8F);
            this.labelPrefixInfo.ForeColor = System.Drawing.Color.DimGray;
            this.labelPrefixInfo.Location = new System.Drawing.Point(16, 37);
            this.labelPrefixInfo.Name = "labelPrefixInfo";
            this.labelPrefixInfo.Size = new System.Drawing.Size(120, 15);
            this.labelPrefixInfo.TabIndex = 1;
            this.labelPrefixInfo.Text = "Pattern :";
            //
            // labelWidth
            //
            this.labelWidth.AutoSize = true;
            this.labelWidth.Font = new System.Drawing.Font("Century Gothic", 9F);
            this.labelWidth.Location = new System.Drawing.Point(16, 74);
            this.labelWidth.Name = "labelWidth";
            this.labelWidth.Size = new System.Drawing.Size(45, 17);
            this.labelWidth.TabIndex = 2;
            this.labelWidth.Text = "Width";
            //
            // TextBox_Width
            //
            this.TextBox_Width.Font = new System.Drawing.Font("Century Gothic", 10F, System.Drawing.FontStyle.Bold);
            this.TextBox_Width.Location = new System.Drawing.Point(70, 70);
            this.TextBox_Width.Name = "TextBox_Width";
            this.TextBox_Width.Size = new System.Drawing.Size(75, 24);
            this.TextBox_Width.TabIndex = 3;
            this.TextBox_Width.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            this.TextBox_Width.TextChanged += new System.EventHandler(this.Input_TextChanged);
            //
            // labelHeight
            //
            this.labelHeight.AutoSize = true;
            this.labelHeight.Font = new System.Drawing.Font("Century Gothic", 9F);
            this.labelHeight.Location = new System.Drawing.Point(163, 74);
            this.labelHeight.Name = "labelHeight";
            this.labelHeight.Size = new System.Drawing.Size(48, 17);
            this.labelHeight.TabIndex = 4;
            this.labelHeight.Text = "Height";
            //
            // TextBox_Height
            //
            this.TextBox_Height.Font = new System.Drawing.Font("Century Gothic", 10F, System.Drawing.FontStyle.Bold);
            this.TextBox_Height.Location = new System.Drawing.Point(220, 70);
            this.TextBox_Height.Name = "TextBox_Height";
            this.TextBox_Height.Size = new System.Drawing.Size(75, 24);
            this.TextBox_Height.TabIndex = 5;
            this.TextBox_Height.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            this.TextBox_Height.TextChanged += new System.EventHandler(this.Input_TextChanged);
            //
            // labelPreset
            //
            this.labelPreset.AutoSize = true;
            this.labelPreset.Font = new System.Drawing.Font("Century Gothic", 9F);
            this.labelPreset.Location = new System.Drawing.Point(16, 111);
            this.labelPreset.Name = "labelPreset";
            this.labelPreset.Size = new System.Drawing.Size(46, 17);
            this.labelPreset.TabIndex = 6;
            this.labelPreset.Text = "Preset";
            //
            // comboPreset
            //
            this.comboPreset.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboPreset.Font = new System.Drawing.Font("Century Gothic", 9F);
            this.comboPreset.FormattingEnabled = true;
            this.comboPreset.Location = new System.Drawing.Point(70, 107);
            this.comboPreset.Name = "comboPreset";
            this.comboPreset.Size = new System.Drawing.Size(225, 25);
            this.comboPreset.TabIndex = 7;
            this.comboPreset.SelectedIndexChanged += new System.EventHandler(this.comboPreset_SelectedIndexChanged);
            //
            // labelValidation
            //
            this.labelValidation.AutoSize = true;
            this.labelValidation.Font = new System.Drawing.Font("Century Gothic", 9F);
            this.labelValidation.Location = new System.Drawing.Point(16, 148);
            this.labelValidation.Name = "labelValidation";
            this.labelValidation.Size = new System.Drawing.Size(0, 17);
            this.labelValidation.TabIndex = 8;
            //
            // btnOK
            //
            this.btnOK.Font = new System.Drawing.Font("Century Gothic", 9F);
            this.btnOK.Location = new System.Drawing.Point(133, 180);
            this.btnOK.Name = "btnOK";
            this.btnOK.Size = new System.Drawing.Size(78, 30);
            this.btnOK.TabIndex = 9;
            this.btnOK.Text = "OK";
            this.btnOK.UseVisualStyleBackColor = true;
            this.btnOK.Click += new System.EventHandler(this.btnOK_Click);
            //
            // btnCancel
            //
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Font = new System.Drawing.Font("Century Gothic", 9F);
            this.btnCancel.Location = new System.Drawing.Point(217, 180);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(78, 30);
            this.btnCancel.TabIndex = 10;
            this.btnCancel.Text = "Cancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            //
            // RawImportOptionsForm
            //
            this.AcceptButton = this.btnOK;
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.White;
            this.CancelButton = this.btnCancel;
            this.ClientSize = new System.Drawing.Size(311, 224);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnOK);
            this.Controls.Add(this.labelValidation);
            this.Controls.Add(this.comboPreset);
            this.Controls.Add(this.labelPreset);
            this.Controls.Add(this.TextBox_Height);
            this.Controls.Add(this.labelHeight);
            this.Controls.Add(this.TextBox_Width);
            this.Controls.Add(this.labelWidth);
            this.Controls.Add(this.labelPrefixInfo);
            this.Controls.Add(this.labelFileInfo);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "RawImportOptionsForm";
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "RAW Import Options";
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        #endregion

        private System.Windows.Forms.Label labelFileInfo;
        private System.Windows.Forms.Label labelPrefixInfo;
        private System.Windows.Forms.Label labelWidth;
        private System.Windows.Forms.TextBox TextBox_Width;
        private System.Windows.Forms.Label labelHeight;
        private System.Windows.Forms.TextBox TextBox_Height;
        private System.Windows.Forms.Label labelPreset;
        private System.Windows.Forms.ComboBox comboPreset;
        private System.Windows.Forms.Label labelValidation;
        private System.Windows.Forms.Button btnOK;
        private System.Windows.Forms.Button btnCancel;
    }
}
