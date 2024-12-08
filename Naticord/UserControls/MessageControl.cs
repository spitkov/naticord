using System;
using System.Drawing;
using System.Windows.Forms;

namespace Naticord.UserControls
{
    public partial class MessageControl : UserControl
    {
        public MessageControl()
        {
            InitializeComponent();
            SetProfilePictureShape(profilepicture);
        }

        public string Username
        {
            get => usernameLabel.Text;
            set => usernameLabel.Text = value;
        }

        public string MessageContent
        {
            get => messageContent.Text;
            set
            {
                messageContent.Text = value;
                UpdateHeight();
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

        public void UpdateHeight()
        {
            if (messageContent != null)
            {
                using (Graphics g = CreateGraphics())
                {
                    SizeF textSize = g.MeasureString(
                        messageContent.Text,
                        messageContent.Font,
                        messageContent.MaximumSize.Width
                    );
                    int requiredHeight = (int)Math.Ceiling(textSize.Height);

                    messageContent.Height = requiredHeight;

                    Height = messageContent.Top + messageContent.Height + 5;
                }
            }
        }
    }
}
