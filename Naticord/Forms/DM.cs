using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Newtonsoft.Json;
using System.Drawing;
using System.Net;
using System.IO;
using System.Linq;
using Naticord.UserControls;
using System.Diagnostics;

namespace Naticord
{
    public partial class DM : Form
    {
        private const string DiscordApiBaseUrl = "https://discord.com/api/v9/";
        private WebSocketClient websocketClient;
        private readonly string AccessToken;
        public long ChatID { get; }
        public string FriendID { get; }
        private readonly string userPFP;
        private string lastMessageAuthor = "";
        private Image _lastUploadedImage = null;

        public DM(long chatid, string friendid, string token, string userpfp)
        {
            websocketClient = WebSocketClient.Instance(token, chatid.ToString(), parentDMForm: this);
            if (websocketClient == null)
            {
                throw new InvalidOperationException("WebSocketClient instance could not be initialized.");
            }
            AccessToken = token;
            ChatID = chatid;
            FriendID = friendid;
            userPFP = userpfp;
            InitializeComponent();
            SetFriendInfo();
            SetProfilePictureShape(profilepicturefriend);
            LoadMessages();
            websocketClient.SetDMFormActive(true);
        }

        private async void LoadMessages()
        {
            try
            {
                dynamic messages = await GetApiResponse($"channels/{ChatID}/messages");

                chatBox.AutoScroll = true;
                chatBox.FlowDirection = FlowDirection.TopDown;
                chatBox.WrapContents = false;
                chatBox.Controls.Clear();

                string lastAuthor = null;
                MessageControl lastMessageControl = null;
                Dictionary<string, Image> avatarCache = new();

                List<Task> imageLoadTasks = new();
                Dictionary<int, Task<Image>> imageTasksByIndex = new();

                for (int i = messages.Count - 1; i >= 0; i--)
                {
                    string authorId = Convert.ToString(messages[i].author.id);
                    string avatarHash = Convert.ToString(messages[i].author.avatar);
                    string avatarUrl = $"https://cdn.discordapp.com/avatars/{authorId}/{avatarHash}.png";

                    if (!avatarCache.ContainsKey(authorId))
                    {
                        imageTasksByIndex[i] = LoadImageFromUrlAsync(avatarUrl);
                    }
                }

                foreach (var kvp in imageTasksByIndex)
                {
                    try
                    {
                        avatarCache[Convert.ToString(messages[kvp.Key].author.id)] = await kvp.Value;
                    }
                    catch
                    {
                        avatarCache[Convert.ToString(messages[kvp.Key].author.id)] = Properties.Resources.defaultpfp;
                    }
                }

                chatBox.SuspendLayout();

                for (int i = messages.Count - 1; i >= 0; i--)
                {
                    string author = Convert.ToString(messages[i].author.global_name) ?? Convert.ToString(messages[i].author.username);
                    string content = Convert.ToString(messages[i].content);
                    string authorId = Convert.ToString(messages[i].author.id);

                    if (!string.IsNullOrEmpty(author) && !string.IsNullOrEmpty(content))
                    {
                        if (author == lastAuthor && lastMessageControl != null)
                        {
                            lastMessageControl.MessageContent += Environment.NewLine + content;
                            lastMessageControl.UpdateHeight();
                        }
                        else
                        {
                            lastMessageControl = new MessageControl
                            {
                                Username = author,
                                MessageContent = content
                            };

                            lastMessageControl.SetProfilePicture(avatarCache.TryGetValue(authorId, out var avatar) ? avatar : Properties.Resources.defaultpfp);
                            lastMessageControl.UpdateHeight();
                            chatBox.Controls.Add(lastMessageControl);
                            lastAuthor = author;
                        }
                    }
                }
                chatBox.ResumeLayout();
                ScrollToBottom();
            }
            catch (Exception ex)
            {
                ShowErrorMessage("Failed to retrieve messages", ex);
            }
        }

        public async Task AddMessage(string author, string content, string userId, string avatarHash)
        {
            try
            {
                string avatarUrl = $"https://cdn.discordapp.com/avatars/{userId}/{avatarHash}.png";

                Dictionary<string, Image> avatarCache = new();

                if (!string.IsNullOrEmpty(author) && !string.IsNullOrEmpty(content))
                {
                    if (!avatarCache.ContainsKey(userId))
                    {
                        avatarCache[userId] = await LoadImageFromUrlAsync(avatarUrl);
                    }

                    MessageControl lastMessageControl = chatBox.Controls.Count > 0
                        ? chatBox.Controls[chatBox.Controls.Count - 1] as MessageControl
                        : null;

                    if (lastMessageControl != null && lastMessageControl.Username == author)
                    {
                        lastMessageControl.MessageContent += Environment.NewLine + content;
                        lastMessageControl.UpdateHeight();
                    }
                    else
                    {
                        MessageControl newMessageControl = new MessageControl
                        {
                            Username = author,
                            MessageContent = content
                        };

                        newMessageControl.SetProfilePicture(avatarCache.ContainsKey(userId) ? avatarCache[userId] : Properties.Resources.defaultpfp);
                        newMessageControl.UpdateHeight();
                        chatBox.Controls.Add(newMessageControl);

                        ScrollToBottom();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to add message: {ex.Message}");
            }
        }

        private async Task<string> GetAvatarHashForUserAsync(string userId)
        {
            try
            {
                dynamic userData = await GetApiResponse($"users/{userId}");
                return userData.avatar ?? string.Empty;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to fetch avatar hash for user {userId}: {ex.Message}");
                return string.Empty;
            }
        }

        private async Task<Image> LoadImageFromUrlAsync(string url)
        {
            try
            {
                using (var httpClient = new System.Net.Http.HttpClient())
                {
                    byte[] imageBytes = await httpClient.GetByteArrayAsync(url);
                    using (var ms = new System.IO.MemoryStream(imageBytes))
                    {
                        return Image.FromStream(ms);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading image from URL: {url} - {ex.Message}");

                return Properties.Resources.defaultpfp;
            }
        }

        public void ScrollToBottom()
        {
            if (chatBox.Controls.Count > 0)
            {
                var lastControl = chatBox.Controls[chatBox.Controls.Count - 1];
                chatBox.ScrollControlIntoView(lastControl);
            }
        }


        private void SetProfilePictureShape(PictureBox pictureBox)
        {
            var path = new System.Drawing.Drawing2D.GraphicsPath();
            path.AddEllipse(0, 0, pictureBox.Width, pictureBox.Height);
            pictureBox.Region = new Region(path);
        }

        private async void SetFriendInfo()
        {
            try
            {
                dynamic userProfile = await GetApiResponse($"users/{FriendID}/profile");
                string displayname = userProfile.user.global_name ?? userProfile.user.username;
                string bio = userProfile.user.bio;
                usernameLabel.Text = displayname;
                profilepicturefriend.ImageLocation = $"https://cdn.discordapp.com/avatars/{userProfile.user.id}/{userProfile.user.avatar}.png";
            }
            catch (Exception ex)
            {
                ShowErrorMessage($"Failed to retrieve user profile Debug Information: {FriendID}", ex);
            }
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

        private void ShowErrorMessage(string message, Exception ex)
        {
            MessageBox.Show($"{message}\n\nError: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        private void messageBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter && !e.Shift)
            {
                e.SuppressKeyPress = true;
                SendMessage();
            }
        }

        private void DM_Load(object sender, EventArgs e)
        {
            websocketClient.UpdateParentDMForm(this);
            Debug.WriteLine($"DM Form assigned to WebSocketClient: {this.ChatID}");
        }
    }
}