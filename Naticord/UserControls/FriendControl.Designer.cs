namespace Naticord.UserControls
{
    partial class FriendControl
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

        #region Component Designer generated code

        /// <summary> 
        /// Required method for Designer support - do not modify 
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.statusLabel = new System.Windows.Forms.Label();
            this.usernameLabel = new System.Windows.Forms.Label();
            this.profilepicture = new System.Windows.Forms.PictureBox();
            ((System.ComponentModel.ISupportInitialize)(this.profilepicture)).BeginInit();
            this.SuspendLayout();
            // 
            // statusLabel
            // 
            this.statusLabel.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.statusLabel.Location = new System.Drawing.Point(59, 28);
            this.statusLabel.Name = "statusLabel";
            this.statusLabel.Size = new System.Drawing.Size(160, 19);
            this.statusLabel.TabIndex = 5;
            this.statusLabel.Text = "customStatus";
            this.statusLabel.UseCompatibleTextRendering = true;
            // 
            // usernameLabel
            // 
            this.usernameLabel.Font = new System.Drawing.Font("Segoe UI Semibold", 11.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.usernameLabel.Location = new System.Drawing.Point(57, 9);
            this.usernameLabel.Name = "usernameLabel";
            this.usernameLabel.Size = new System.Drawing.Size(162, 20);
            this.usernameLabel.TabIndex = 4;
            this.usernameLabel.Text = "usernameaaaaaaaaaaaa\r\n";
            // 
            // profilepicture
            // 
            this.profilepicture.Image = global::Naticord.Properties.Resources.defaultpfp;
            this.profilepicture.Location = new System.Drawing.Point(6, 5);
            this.profilepicture.Name = "profilepicture";
            this.profilepicture.Size = new System.Drawing.Size(45, 45);
            this.profilepicture.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom;
            this.profilepicture.TabIndex = 3;
            this.profilepicture.TabStop = false;
            // 
            // FriendControl
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.statusLabel);
            this.Controls.Add(this.usernameLabel);
            this.Controls.Add(this.profilepicture);
            this.Name = "FriendControl";
            this.Size = new System.Drawing.Size(234, 55);
            ((System.ComponentModel.ISupportInitialize)(this.profilepicture)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Label statusLabel;
        private System.Windows.Forms.Label usernameLabel;
        private System.Windows.Forms.PictureBox profilepicture;
    }
}
