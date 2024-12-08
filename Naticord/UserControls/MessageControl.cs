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
                UpdateHeight(); // Update height whenever content changes
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
                    // Measure the height required for the messageContent
                    SizeF textSize = g.MeasureString(messageContent.Text, messageContent.Font, messageContent.Width);
                    int requiredHeight = (int)Math.Ceiling(textSize.Height);

                    // Adjust the height of the messageContent control
                    messageContent.Height = requiredHeight;

                    // Update the height of the MessageControl to fit the new content
                    Height = messageContent.Top + messageContent.Height + 10; // Add padding for aesthetics
                }
            }
        }
    }
}
