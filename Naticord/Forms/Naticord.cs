using System;
using System.Collections.Generic;
using System.Net;
using System.Windows.Forms;
using System.Drawing.Drawing2D;
using System.Drawing;
using System.IO;
using Naticord.UserControls;
using Naticord.Classes;
using System.Net.Http;
using System.Threading.Tasks;
using System.Diagnostics;

namespace Naticord
{
    public partial class Naticord : Form
    {
        private const string DiscordApiBaseUrl = "https://discord.com/api/v9/";
        private const string ProfilePicturePath = "user.png";
        private string AccessToken;
        private Login signin;
        private string userPFP;
        private Dictionary<string, long> groupChatIDs = new Dictionary<string, long>();
        private WebSocketClient websocketClient;

        public Naticord(string token, Login signinArg)
        {
            InitializeComponent();
            signin = signinArg;
            AccessToken = token;
            descriptionLabel = new Label();
            SetUserInfo();
            PopulateFriendsTabAsync();
            PopulateServersTabAsync();
            SetProfilePictureRegion();
            Settings settingsForm = new Settings();

            Application.EnableVisualStyles();
            WebSocketClient client = WebSocketClient.Instance(AccessToken, null, null);
        }

        private void SetProfilePictureRegion()
        {
            GraphicsPath path = new GraphicsPath();
            path.AddEllipse(0, 0, profilepicture.Width, profilepicture.Height);
            profilepicture.Region = new Region(path);
        }

        private void SetUserInfo()
        {
            try
            {
                dynamic userProfile = GetApiResponse("users/@me");
                string displayname = userProfile.global_name ?? userProfile.username;
                usernameLabel.Text = displayname;

                userPFP = $"https://cdn.discordapp.com/avatars/{userProfile.id}/{userProfile.avatar}.png";

                if (!File.Exists(ProfilePicturePath))
                {
                    using (var webClient = new WebClient())
                    {
                        webClient.DownloadFile(userPFP, ProfilePicturePath);
                    }
                }

                profilepicture.ImageLocation = ProfilePicturePath;
            }
            catch (WebException ex)
            {
                ShowErrorMessage("Failed to retrieve user profile", ex);
            }
        }

        public async Task PopulateFriendsTabAsync()
        {
            try
            {
                dynamic channels = await GetApiResponseAsync("users/@me/channels");
                dynamic relationships = await GetApiResponseAsync("users/@me/relationships");
                HashSet<long> blockedUsers = new HashSet<long>();

                foreach (var relationship in relationships)
                {
                    if ((int)relationship.type == 2)
                    {
                        blockedUsers.Add((long)relationship.id);
                    }
                }

                HashSet<long> channelIds = new HashSet<long>();
                friendsPanel.Controls.Clear();
                int yPosition = 0;

                List<Task> imageDownloadTasks = new List<Task>();

                await Task.Delay(2000);

                foreach (var channel in channels)
                {
                    if (channel == null || channel.id == null || channel.id == 0)
                    {
                        continue;
                    }

                    long channelId = (long)channel.id;

                    if (channelIds.Contains(channelId))
                    {
                        continue;
                    }

                    string channelType = "";
                    string namesOrName = "";
                    string statusContent = "";
                    string userId = "";

                    switch ((int)channel.type)
                    {
                        case 1:
                            if (channel.recipients != null && channel.recipients.Count > 0)
                            {
                                List<string> names = new List<string>();
                                foreach (var recipient in channel.recipients)
                                {
                                    string recipientName = (string)recipient.nickname ?? (string)recipient.global_name ?? (string)recipient.username;
                                    names.Add(recipientName);
                                    userId = recipient.id.ToString();
                                }
                                namesOrName = string.Join(", ", names);
                                channelType = "Direct Message";

                                string status = UserStatusManager.GetUserStatus(userId);
                                statusContent = string.IsNullOrEmpty(status) ? "Unknown" : status;
                            }
                            else
                            {
                                namesOrName = "Unknown User";
                                channelType = "Direct Message";
                            }
                            break;

                        case 3:
                            if (channel.name != null)
                            {
                                namesOrName = (string)channel.name;
                                channelType = "Group Message";

                                SaveGroupChatID(namesOrName, channelId);
                            }
                            else if (channel.recipients != null && channel.recipients.Count > 0)
                            {
                                List<string> names = new List<string>();
                                foreach (var recipient in channel.recipients)
                                {
                                    string recipientName = (string)recipient.nickname ?? (string)recipient.global_name ?? (string)recipient.username;
                                    names.Add(recipientName);
                                    userId = recipient.id.ToString();
                                }
                                namesOrName = string.Join(", ", names);
                                channelType = "Group Message";

                                SaveGroupChatID(namesOrName, channelId);
                            }

                            statusContent = $"{channel.recipients.Count} members";

                            if (!groupChatIDs.ContainsKey(namesOrName))
                            {
                                groupChatIDs[namesOrName] = channelId;
                            }
                            break;
                    }

                    if (string.IsNullOrEmpty(namesOrName))
                    {
                        continue;
                    }

                    var friendControl = new FriendControl
                    {
                        Username = namesOrName,
                        StatusContent = statusContent,
                        Tag = Tuple.Create(channelId, userId),
                        Location = new Point(0, yPosition)
                    };

                    if (channel.recipients != null && channel.recipients.Count > 0)
                    {
                        var recipient = channel.recipients[0];
                        string avatarHash = recipient.avatar;

                        if (!string.IsNullOrEmpty(avatarHash))
                        {
                            string avatarUrl = $"https://cdn.discordapp.com/avatars/{recipient.id}/{avatarHash}.png";

                            var downloadTask = DownloadProfileImageAsync(avatarUrl, friendControl);
                            imageDownloadTasks.Add(downloadTask);
                        }
                    }

                    if (channel.type == 1 && !blockedUsers.Contains((long)channel.recipients[0].id))
                    {
                        string status = UserStatusManager.GetUserStatus(userId);
                        friendControl.StatusContent = status;
                    }
                    yPosition += friendControl.Height;

                    friendsPanel.Controls.Add(friendControl);
                    channelIds.Add(channelId);

                    friendControl.MouseDoubleClick += FriendControl_MouseDoubleClick;
                }

                await Task.WhenAll(imageDownloadTasks);
                friendsPanel.PerformLayout();
            }
            catch (WebException ex)
            {
                ShowErrorMessage("Failed to retrieve channel list", ex);
            }
        }

        private void FriendControl_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            FriendControl friendControl = sender as FriendControl;
            if (friendControl != null)
            {
                var tag = friendControl.Tag as Tuple<long, string>;
                if (tag != null)
                {
                    long channelId = tag.Item1;
                    string userId = tag.Item2;

                    DM dm = new DM(channelId, userId, AccessToken, userPFP);
                    dm.Show();
                    Console.WriteLine($"ChatID: {channelId}, UserID: {userId}");
                }
            }
        }

        private async Task DownloadProfileImageAsync(string avatarUrl, FriendControl friendControl)
        {
            try
            {
                using (var httpClient = new HttpClient())
                {
                    var imageBytes = await httpClient.GetByteArrayAsync(avatarUrl);
                    using (var memoryStream = new MemoryStream(imageBytes))
                    {
                        Image profileImage = Image.FromStream(memoryStream);
                        friendControl.SetProfilePicture(profileImage);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Failed to load profile picture: " + ex.Message);
            }
        }

        private void SaveGroupChatID(string groupName, long chatID)
        {
            groupChatIDs[groupName] = chatID;
        }

        private long GetChatID(string name)
        {
            try
            {
                dynamic channels = GetApiResponse("users/@me/channels");
                foreach (var channel in channels)
                {
                    if (channel.type == 1)
                    {
                        foreach (var recipient in channel.recipients)
                        {
                            string recipientName = recipient.global_name ?? recipient.username;
                            if (recipientName == name)
                            {
                                return (long)channel.id;
                            }
                        }
                    }
                }
                return -1;
            }
            catch (WebException ex)
            {
                ShowErrorMessage("Failed to retrieve chat ID", ex);
                return -1;
            }
        }

        private long GetFriendID(string name)
        {
            try
            {
                dynamic friends = GetApiResponse("users/@me/relationships");
                foreach (var friend in friends)
                {
                    if (friend.type == 1)
                    {
                        string friendName = friend.user.global_name ?? friend.user.username;
                        if (friendName == name)
                        {
                            return (long)friend.id;
                        }
                    }
                }
                return -1;
            }
            catch (WebException ex)
            {
                ShowErrorMessage("Failed to retrieve friend ID", ex);
                return -1;
            }
        }

        private long GetGroupID(string name)
        {
            if (groupChatIDs.ContainsKey(name))
            {
                return groupChatIDs[name];
            }
            else
            {
                return -1;
            }
        }

        private long GetServerID(string name)
        {
            try
            {
                dynamic guilds = GetApiResponse("users/@me/guilds");
                foreach (var guild in guilds)
                {
                    if (guild.name.ToString() == name) return (long)guild.id;
                }
                return -1;
            }
            catch (WebException ex)
            {
                ShowErrorMessage("Failed to retrieve server list", ex);
                return -1;
            }
        }

        private async Task PopulateServersTabAsync()
        {
            try
            {
                dynamic guilds = await GetApiResponseAsync("users/@me/guilds");

                serversPanel.Controls.Clear();
                int yPosition = 0;

                List<Task> imageDownloadTasks = new List<Task>();
                HashSet<string> processedGuilds = new HashSet<string>();

                foreach (var guild in guilds)
                {
                    if (guild == null || guild.id == null || guild.name == null)
                    {
                        continue;
                    }

                    string guildName = guild.name.ToString();
                    string guildId = guild.id.ToString();

                    if (processedGuilds.Contains(guildId))
                    {
                        continue;
                    }

                    processedGuilds.Add(guildId);

                    string iconHash = guild.icon;
                    dynamic guildDetails = await GetApiResponseAsync($"guilds/{guildId}");

                    string description = string.IsNullOrEmpty(guildDetails?.description.ToString()) ? "No description found." : guildDetails.description.ToString();

                    var serverControl = new FriendControl
                    {
                        Username = guildName,
                        StatusContent = description,
                        Tag = new Tuple<string, string>(guildId, guildName),
                        Location = new Point(0, yPosition)
                    };

                    if (!string.IsNullOrEmpty(iconHash))
                    {
                        string iconUrl = $"https://cdn.discordapp.com/icons/{guildId}/{iconHash}.png";

                        var downloadTask = DownloadProfileImageAsync(iconUrl, serverControl);
                        imageDownloadTasks.Add(downloadTask);
                    }

                    yPosition += serverControl.Height;
                    serversPanel.Controls.Add(serverControl);
                    serverControl.MouseDoubleClick += ServerControl_DoubleClick;
                }

                await Task.WhenAll(imageDownloadTasks);
            }
            catch (WebException ex)
            {
                ShowErrorMessage("Failed to retrieve server list", ex);
            }
        }

        private void ServerControl_DoubleClick(object sender, MouseEventArgs e)
        {
            FriendControl friendControl = sender as FriendControl;
            if (friendControl != null)
            {
                var tag = friendControl.Tag as Tuple<string, string>;

                if (tag != null)
                {
                    string guildIdString = tag.Item1;
                    string guildName = tag.Item2;

                    if (long.TryParse(guildIdString, out long guildId))
                    {
                        Console.WriteLine($"Opening server: {guildName} (ID: {guildId})");

                        Server server = new Server(guildId, AccessToken);
                        server.Show();
                    }
                    else
                    {
                        Console.WriteLine("Invalid guild ID format.");
                    }
                }
            }
        }

        private async Task<int> GetMemberCountAsync(string guildId)
        {
            int memberCount = 0;
            string lastUserId = null;

            while (true)
            {
                string url = $"guilds/{guildId}/members";
                if (!string.IsNullOrEmpty(lastUserId))
                {
                    url += $"?before={lastUserId}";
                }

                dynamic guildMembers = await GetApiResponseAsync(url);

                if (guildMembers.Count == 0)
                {
                    break;
                }

                memberCount += guildMembers.Count;
                lastUserId = guildMembers[guildMembers.Count - 1].user.id.ToString();
            }

            return memberCount;
        }

        private dynamic GetApiResponse(string endpoint)
        {
            using (var webClient = new WebClient())
            {
                webClient.Headers[HttpRequestHeader.Authorization] = AccessToken;
                string jsonResponse = webClient.DownloadString(DiscordApiBaseUrl + endpoint);
                return Newtonsoft.Json.JsonConvert.DeserializeObject(jsonResponse);
            }
        }

        private async Task<dynamic> GetApiResponseAsync(string endpoint)
        {
            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Add("Authorization", AccessToken);
                string jsonResponse = await client.GetStringAsync(DiscordApiBaseUrl + endpoint);
                return Newtonsoft.Json.JsonConvert.DeserializeObject(jsonResponse);
            }
        }

        private void ShowErrorMessage(string message, Exception ex)
        {
            MessageBox.Show($"{message}: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            signin.Hide();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            base.OnFormClosing(e);
            signin.Close();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            Settings settingsForm = new Settings();
            settingsForm.Show();
        }
    }
}
