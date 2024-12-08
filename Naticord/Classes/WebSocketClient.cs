using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection.Emit;
using System.Security.Authentication;
using System.Threading.Tasks;
using System.Windows.Forms;
using Naticord.Classes;
using Newtonsoft.Json.Linq;
using WebSocketSharp;

namespace Naticord
{
    public class WebSocketClient
    {
        private static WebSocketClient _instance;
        public Naticord parentClientForm;
        public DM parentDMForm;
        public Group parentGroupForm;
        public Server parentServerForm;
        public WebSocket webSocket;
        private string accessToken;
        private string _chatId;
        private List<Tuple<string, string>> userStatuses = new List<Tuple<string, string>>();
        private const SslProtocols Tls12 = (SslProtocols)0x00000C00;
        private bool websocketClosed = false;

        private WebSocketClient(string accessToken, string chatId, Naticord parentClientForm = null, DM parentDMForm = null, Group parentGroupForm = null, Server parentServerForm = null)
        {
            ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072;
            this.accessToken = accessToken;
            this.parentClientForm = parentClientForm;
            this.parentDMForm = parentDMForm;
            this.parentGroupForm = parentGroupForm;
            this.parentServerForm = parentServerForm;
            this._chatId = chatId;
            InitializeWebSocket();
        }

        public static WebSocketClient Instance(string accessToken, string chatId, Naticord parentClientForm = null, DM parentDMForm = null, Group parentGroupForm = null, Server parentServerForm = null)
        {
            if (_instance == null)
            {
                _instance = new WebSocketClient(accessToken, chatId, parentClientForm, parentDMForm, parentGroupForm, parentServerForm);
            }
            return _instance;
        }

        private void InitializeWebSocket()
        {
            webSocket = new WebSocket($"wss://gateway.discord.gg/?v=9&encoding=json");
            webSocket.SslConfiguration.EnabledSslProtocols = Tls12;
            webSocket.OnMessage += async (sender, e) => await HandleWebSocketMessage(e.Data);
            webSocket.OnError += (sender, e) => HandleWebSocketError(e.Message);
            webSocket.OnClose += (sender, e) => HandleWebSocketClose();
            webSocket.Connect();
            SendIdentifyPayload();
        }

        private void SendIdentifyPayload()
        {
            if (webSocket.ReadyState == WebSocketState.Open)
            {
                var identifyPayload = new
                {
                    op = 2,
                    d = new
                    {
                        token = accessToken,
                        properties = new
                        {
                            os = "windows",
                            browser = "chrome",
                            device = "pc"
                        }
                    }
                };

                try
                {
                    string payloadJson = Newtonsoft.Json.JsonConvert.SerializeObject(identifyPayload);
                    webSocket.Send(payloadJson);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error sending identify payload: {ex.Message}");
                }
            }
            else
            {
                Console.WriteLine("WebSocket connection is not open. Unable to send identify payload.");
            }
        }

        private async Task HandleWebSocketMessage(string data)
        {
            Debug.WriteLine($"Received WebSocket Message: {data}");

            var json = JObject.Parse(data);
            int opCode = (int)json["op"];

            switch (opCode)
            {
                case 0:
                    string eventType = (string)json["t"];
                    switch (eventType)
                    {
                        case "READY":
                            await ParseReadyEventAsync(data);
                            break;
                        case "USER_SETTINGS_UPDATE":
                            // ParseCustomStatusText(data);
                            break;
                        case "TYPING_START":
                            HandleTypingStartEvent(json["d"]);
                            break;
                        case "MESSAGE_CREATE":
                            await HandleMessageCreateEventAsync(json["d"]);
                            HandleTypingStopEvent(json["d"]);
                            break;
                        case "PRESENCE_UPDATE":
                            Debug.WriteLine("Received status update");
                            break;
                        default:
                            Debug.WriteLine($"Unhandled event type: {eventType}");
                            break;
                    }
                    break;

                case 1:
                    Debug.WriteLine("Heartbeat event received");
                    break;

                case 10:
                    Debug.WriteLine("Hello! From Discord Gateway");
                    break;

                default:
                    Debug.WriteLine($"Unhandled OpCode: {opCode}");
                    break;
            }
        }

        private async Task HandleMessageCreateEventAsync(JToken data)
        {
            string channelId = (string)data["channel_id"];
            string userId = (string)data["author"]["id"];
            string avatarHash = (string)data["author"]["avatar"];
            string author = (string)data["author"]?["global_name"] ?? (string)data["author"]?["username"];
            string content = (string)data["content"];

            if (parentDMForm == null)
            {
                Debug.WriteLine("parentDMForm is null. Skipping message handling.");
                return;
            }

            string chatIdString = parentDMForm.ChatID.ToString();
            bool isChannelIdValid = !string.IsNullOrEmpty(channelId);
            bool isChatIdMatch = chatIdString == channelId;

            if (isChannelIdValid && isChatIdMatch)
            {
                parentDMForm.Invoke((MethodInvoker)(() =>
                {
                    parentDMForm.AddMessage(author, content, userId, avatarHash);
                    parentDMForm.ScrollToBottom();
                }));
            }
            else
            {
                // chat id invalid, dont add message
            }

            if (parentGroupForm != null && channelId == parentGroupForm.ChatID.ToString())
            {
                // parentGroupForm.AddMessage(author, content, "said", null, null, true, true);
                parentGroupForm.Invoke((MethodInvoker)(() => parentGroupForm.ScrollToBottom()));
            }

            if (parentServerForm != null && channelId == parentServerForm.ChatID.ToString())
            {
                // parentServerForm.AddMessage(author, content, "said", null, null, true, true);
                parentServerForm.Invoke((MethodInvoker)(() => parentServerForm.ScrollToBottom()));
            }
        }
        private async Task ParseReadyEventAsync(string data)
        {
            try
            {
                var json = JObject.Parse(data);
                var presences = json["d"]?["presences"];

                var statusTranslations = new Dictionary<string, string>
                {
                    { "dnd", "Do not disturb" },
                    { "online", "Online" },
                    { "idle", "Idle" },
                    { "offline", "Offline" },
                    { "invisible", "Invisible" }
                };

                UserStatusManager.ClearStatuses();

                List<Task> statusTasks = new List<Task>();

                foreach (var presence in presences)
                {
                    string userId = (string)presence["user"]?["id"];
                    string userStatus = (string)presence["status"] ?? "Offline";

                    // Translate status if applicable
                    if (statusTranslations.ContainsKey(userStatus))
                    {
                        userStatus = statusTranslations[userStatus];
                    }
                    else
                    {
                        userStatus = "Offline";
                    }

                    var activities = presence["activities"] as JArray;
                    string statusMessage = userStatus;

                    var customStatus = activities?.FirstOrDefault(a => (int?)a["type"] == 4);
                    bool hasEmoji = customStatus != null && (customStatus["state"]?.ToString().Contains("🙂") == true || customStatus["emoji"] != null);

                    if (hasEmoji)
                    {
                        statusMessage = userStatus;
                    }
                    else if (customStatus != null)
                    {
                        string customState = (string)customStatus["state"];
                        if (!string.IsNullOrEmpty(customState))
                        {
                            statusMessage = customState;
                        }
                    }

                    if (activities != null)
                    {
                        foreach (var activity in activities)
                        {
                            int activityType = (int?)activity["type"] ?? -1;
                            string activityStatus = (string)activity["name"];

                            switch (activityType)
                            {
                                case 0: // Playing (Game)
                                    statusMessage = $"Playing {activityStatus}";
                                    break;

                                case 1: // Streaming (e.g., watching a live stream)
                                    statusMessage = $"Streaming {activityStatus}";
                                    break;

                                case 2: // Listening (e.g., Music, Spotify)
                                    statusMessage = $"Listening to {activityStatus}";
                                    break;

                                case 3: // Watching (e.g., YouTube or a video)
                                    statusMessage = $"Watching {activityStatus}";
                                    break;

                                default:
                                    // Fallback to custom status if no activity type matched
                                    if (string.IsNullOrEmpty(statusMessage))
                                    {
                                        statusMessage = customStatus != null ? (string)customStatus["state"] : "No current activity";
                                    }
                                    break;
                            }

                            // After processing the first valid activity, break out of the loop
                            break;
                        }
                    }

                    // Set the status message (either custom status or activity-based)
                    var task = Task.Run(() => UserStatusManager.SetUserStatus(userId, statusMessage));
                    statusTasks.Add(task);

                    Debug.WriteLine($"User ID: {userId}");
                    Debug.WriteLine($"Status: {statusMessage}");
                }

                await Task.WhenAll(statusTasks);

                if (parentClientForm != null)
                {
                    await parentClientForm.PopulateFriendsTabAsync();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error parsing READY event: {ex.Message}");
            }
        }

        private void HandleTypingStartEvent(JToken jToken)
        {
            if (parentDMForm != null)
            {
                string channelId = (string)jToken["channel_id"];
                if (long.TryParse(channelId, out long parsedChannelId) && parsedChannelId == parentDMForm.ChatID)
                {
                    string userId = (string)jToken["user_id"];
                }
            }

            if (parentGroupForm != null)
            {
                string channelId = (string)jToken["channel_id"];
                if (long.TryParse(channelId, out long parsedChannelId) && parsedChannelId == parentGroupForm.ChatID)
                {
                    string userId = (string)jToken["user_id"];
                }
            }

            if (parentServerForm != null)
            {
                string channelId = (string)jToken["channel_id"];
                if (long.TryParse(channelId, out long parsedChannelId) && parsedChannelId == parentServerForm.ChatID)
                {
                    string userId = (string)jToken["user_id"];
                }
            }
        }

        private void HandleTypingStopEvent(JToken jToken)
        {
            if (parentDMForm != null)
            {
                string channelId = (string)jToken["channel_id"];
                if (long.TryParse(channelId, out long parsedChannelId) && parsedChannelId == parentDMForm.ChatID)
                {
                    UpdateFormLabel(parentDMForm.typingStatus, string.Empty);
                }
            }

            if (parentGroupForm != null)
            {
                string channelId = (string)jToken["channel_id"];
                if (long.TryParse(channelId, out long parsedChannelId) && parsedChannelId == parentGroupForm.ChatID)
                {
                    // Do nothing
                }
            }

            if (parentServerForm != null)
            {
                string channelId = (string)jToken["channel_id"];
                if (long.TryParse(channelId, out long parsedChannelId) && parsedChannelId == parentServerForm.ChatID)
                {
                    // Do nothing
                }
            }
        }

        private void UpdateFormLabel(Control label, string text)
        {
            if (label.InvokeRequired)
            {
                label.Invoke((Action)(() => label.Text = text));
            }
            else
            {
                label.Text = text;
            }
        }

        private void HandleWebSocketError(string message)
        {
            Console.WriteLine($"WebSocket error: {message}");
            ReconnectWebSocket();
        }

        private void HandleWebSocketClose()
        {
            Console.WriteLine("WebSocket connection closed");
            if (!websocketClosed)
            {
                ReconnectWebSocket();
            }
        }

        public void CloseWebSocket()
        {
            if (webSocket?.ReadyState != WebSocketState.Closed)
            {
                Console.WriteLine("Closing WebSocket...");
                websocketClosed = true;
                webSocket?.Close();
            }
            else
            {
                Console.WriteLine("WebSocket is already closed.");
            }
        }

        private void ReconnectWebSocket()
        {
            Console.WriteLine("Reconnecting WebSocket...");
            CloseWebSocket();
            InitializeWebSocket();
        }
    }
}
