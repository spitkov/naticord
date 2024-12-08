using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using Newtonsoft.Json;
using System.Drawing;
using System.Net;
using System.IO;
using System.Linq;

namespace Naticord
{
    public partial class Group : Form
    {
        private const string DiscordApiBaseUrl = "https://discord.com/api/v9/";
        private WebSocketClient websocketClient;
        private const string htmlStart = "<!DOCTYPE html><html><head><meta http-equiv=\"X-UA-Compatible\" content=\"edge\" ><style>* {background-color: transparent; font-family: \"Segoe UI\", sans-serif; font-size: 10pt; overflow-x: hidden;} p,strong,b,i,em,mark,small,del,ins,sub,sup,h1,h2,h3,h4,h5,h6 {display: inline;} img {width: auto; height: auto; max-width: 60% !important; max-height: 60% !important;} .spoiler {background-color: black; color: black; border-radius: 5px;} .spoiler:hover {background-color: black; color: white; border-radius: 5px;} .ping {background-color: #e6e8fd; color: #5865f3; border-radius: 5px;} .rich {width: 60%; border-style: solid; border-radius: 5px; border-width: 2px; border-color: black; padding: 10px;}</style></head><body>";
        private string htmlMiddle = "";
        private const string htmlEnd = "</body></html>";
        private readonly string AccessToken;
        public long ChatID { get; }
        private readonly string userPFP;
        private string lastMessageAuthor = "";
        private Image _lastUploadedImage = null;

        public Group(long chatid, string token, string userpfp)
        {
            InitializeComponent();
            AccessToken = token;
            ChatID = chatid;
            userPFP = userpfp;
            LoadGroupName();
            SetProfilePictureShape(profilepicturefriend);
        }

        private void SetProfilePictureShape(PictureBox pictureBox)
        {
            var path = new System.Drawing.Drawing2D.GraphicsPath();
            path.AddEllipse(0, 0, pictureBox.Width, pictureBox.Height);
            pictureBox.Region = new Region(path);
        }

        private void Group_Load(object sender, EventArgs e)
        {
            chatBox.DocumentText = "";
        }

        private async Task SendMessage()
        {
            string message = messageBox.Text.Trim();
            if (!string.IsNullOrEmpty(message))
            {
                try
                {
                    var postData = new
                    {
                        content = message
                    };
                    string jsonPostData = Newtonsoft.Json.JsonConvert.SerializeObject(postData);

                    using (var client = new HttpClient())
                    {
                        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(AccessToken);
                        HttpContent content = new StringContent(jsonPostData, Encoding.UTF8, "application/json");

                        HttpResponseMessage response = await client.PostAsync($"{DiscordApiBaseUrl}channels/{ChatID}/messages", content);
                        response.EnsureSuccessStatusCode();

                        if (Clipboard.ContainsImage())
                        {
                            Image currentImage = Clipboard.GetImage();

                            if (_lastUploadedImage == null || !ImagesAreEqual(_lastUploadedImage, currentImage))
                            {
                                byte[] imageBytes = ImageToBytes(currentImage);
                                await UploadImage(imageBytes);
                                _lastUploadedImage = currentImage;
                            }
                        }
                    }

                    messageBox.Clear();
                }
                catch (Exception ex)
                {
                    ShowErrorMessage("Failed to send message", ex);
                }
            }
        }

        private bool ImagesAreEqual(Image img1, Image img2)
        {
            if (img1 == null || img2 == null)
                return false;

            using (MemoryStream ms1 = new MemoryStream(), ms2 = new MemoryStream())
            {
                img1.Save(ms1, System.Drawing.Imaging.ImageFormat.Png);
                img2.Save(ms2, System.Drawing.Imaging.ImageFormat.Png);
                byte[] bytes1 = ms1.ToArray();
                byte[] bytes2 = ms2.ToArray();
                return bytes1.SequenceEqual(bytes2);
            }
        }


        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == (Keys.Control | Keys.V))
            {
                if (Clipboard.ContainsImage())
                {
                    try
                    {
                        Image clipboardImage = Clipboard.GetImage();
                        byte[] imageBytes = ImageToBytes(clipboardImage);

                        Task.Run(() => UploadImage(imageBytes)).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        ShowErrorMessage("Failed to upload image", ex);
                    }
                }
            }

            return base.ProcessCmdKey(ref msg, keyData);
        }

        private async Task UploadImage(byte[] imageBytes)
        {
            try
            {
                using (var client = new HttpClient())
                {
                    client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(AccessToken);

                    MultipartFormDataContent formData = new MultipartFormDataContent();
                    formData.Add(new ByteArrayContent(imageBytes), "file", "image.png");

                    HttpResponseMessage response = await client.PostAsync($"{DiscordApiBaseUrl}channels/{ChatID}/messages", formData);
                    response.EnsureSuccessStatusCode();

                    string responseContent = await response.Content.ReadAsStringAsync();
                }
            }
            catch (Exception ex)
            {
                ShowErrorMessage("Failed to upload image", ex);
            }
        }

        private byte[] ImageToBytes(Image image)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                image.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                return ms.ToArray();
            }
        }

        public async Task<dynamic> GetApiResponse(string endpoint)
        {
            using (var client = new HttpClient { BaseAddress = new Uri(DiscordApiBaseUrl) })
            {
                client.DefaultRequestHeaders.Add("Authorization", AccessToken);
                var response = await client.GetAsync(endpoint);
                response.EnsureSuccessStatusCode();
                var content = await response.Content.ReadAsStringAsync();
                return JsonConvert.DeserializeObject(content);
            }
        }

        // this can be easily replaced by something more simple but it works sooooo
        private async void LoadGroupName()
        {
            try
            {
                dynamic channels = await GetApiResponse("users/@me/channels");
                string groupName = "";

                foreach (var channel in channels)
                {
                    if (channel.type == 3 && (long)channel.id == ChatID)
                    {
                        groupName = channel.name ?? "Group Chat"; // group chat is for "we couldnt find out the name go fuck yourself"
                        break;
                    }
                }

                usernameLabel.Text = groupName;
                this.Text = $"{groupName} - Naticord";
            }
            catch (Exception ex)
            {
                ShowErrorMessage("Failed to retrieve group name", ex);
            }
        }


        private void ShowErrorMessage(string message, Exception ex)
        {
            MessageBox.Show($"{message}\n\nError: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        public void ScrollToBottom()
        {
            try
            {
                if (chatBox.Document != null && chatBox.Document.Body != null)
                {
                    chatBox.Document.OpenNew(true);
                    chatBox.Document.Write(htmlStart + htmlMiddle + htmlEnd);
                    chatBox.Document.Window.ScrollTo(0, chatBox.Document.Body.ScrollRectangle.Bottom);
                }
            }
            catch (Exception)
            {
                // who tf cares bro it works
            }
        }

        private void messageBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter && !e.Shift)
            {
                e.SuppressKeyPress = true;
                SendMessage();
            }
        }
    }
}