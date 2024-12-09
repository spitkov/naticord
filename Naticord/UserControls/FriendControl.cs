using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Naticord.UserControls
{
    public partial class FriendControl : UserControl
    {
        public FriendControl()
        {
            InitializeComponent();
            SetProfilePictureShape(profilepicture);
            this.Margin = new Padding(0);
            this.Padding = new Padding(0);
        }

        public string Username
        {
            get => usernameLabel.Text;
            set
            {
                if (value.Length > 17)
                {
                    usernameLabel.Text = value.Substring(0, 17) + "...";
                }
                else
                {
                    usernameLabel.Text = value;
                }
            }
        }

        public string StatusContent
        {
            get => statusLabel.Text;
            set
            {
                if (value.Length > 24)
                {
                    statusLabel.Text = value.Substring(0, 24).TrimEnd() + "...";
                }
                else
                {
                    statusLabel.Text = value;
                }
            }
        }

        public void SetProfilePicture(Image image)
        {
            profilepicture.Image = image;
        }

        private void SetProfilePictureShape(PictureBox pictureBox)
        {
            var path = new System.Drawing.Drawing2D.GraphicsPath();
            path.AddEllipse(0, 0, pictureBox.Width, pictureBox.Height);
            pictureBox.Region = new Region(path);
        }
    }
}
