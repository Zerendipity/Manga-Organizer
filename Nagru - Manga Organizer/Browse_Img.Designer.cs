namespace Nagru___Manga_Organizer
{
    partial class Browse_Img
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
            if (disposing && (components != null))
            {
                components.Dispose();
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
            this.picBx = new System.Windows.Forms.PictureBox();
            ((System.ComponentModel.ISupportInitialize)(this.picBx)).BeginInit();
            this.SuspendLayout();
            // 
            // picBx
            // 
            this.picBx.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(39)))), ((int)(((byte)(40)))), ((int)(((byte)(34)))));
            this.picBx.Dock = System.Windows.Forms.DockStyle.Fill;
            this.picBx.Location = new System.Drawing.Point(0, 0);
            this.picBx.Name = "picBx";
            this.picBx.Size = new System.Drawing.Size(784, 562);
            this.picBx.TabIndex = 1;
            this.picBx.TabStop = false;
            this.picBx.MouseUp += new System.Windows.Forms.MouseEventHandler(this.Browse_MouseUp);
            // 
            // Browse_Img
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.DarkGray;
            this.ClientSize = new System.Drawing.Size(784, 562);
            this.Controls.Add(this.picBx);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = true;
            this.MinimizeBox = false;
            this.Name = "Browse_Img";
            this.ShowIcon = false;
            this.ShowInTaskbar = false;
            this.Text = "Browse";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.Browse_FormClosing);
            this.Load += new System.EventHandler(this.Browse_Load);
            this.Shown += new System.EventHandler(this.Browse_Shown);
            this.KeyDown += new System.Windows.Forms.KeyEventHandler(this.Browse_KeyDown);
            this.MouseUp += new System.Windows.Forms.MouseEventHandler(this.Browse_MouseUp);
            ((System.ComponentModel.ISupportInitialize)(this.picBx)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.PictureBox picBx;
    }
}