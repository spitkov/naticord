using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Authentication;
using System.Threading.Tasks;
using System.Windows.Forms;
using Naticord.Classes;
using Newtonsoft.Json.Linq;
using WebSocketSharp;

namespace Naticord
{
    public class WebSocketClient : IDisposable
    {
        private static WebSocketClient _instance;
        private Naticord parentClientForm;
        private DM parentDMForm;
        private Group parentGroupForm;
        private Server parentServerForm;
        private WebSocket webSocket;
        private string accessToken;
        private bool isDMFormActive = false;
        private string _chatId;
        private const SslProtocols Tls12 = (SslProtocols)0x00000C00;
        private bool websocketClosed = false;
        private bool disposed = false;
        private Timer memoryTrackingTimer;

        private WebSocketClient(string accessToken, string chatId, Naticord parentClientForm = null, DM parentDMForm = null, Group parentGroupForm = null, Server parentServerForm = null)
        {
            this.accessToken = accessToken;
            this.parentClientForm = parentClientForm;
            this.parentDMForm = parentDMForm;
            this.parentGroupForm = parentGroupForm;
            this.parentServerForm = parentServerForm;
            this._chatId = chatId;

            memoryTrackingTimer = new Timer();
            memoryTrackingTimer.Interval = 5000;
            memoryTrackingTimer.Tick += (sender, e) => LogMemoryUsage("Periodic Memory Check");

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

        public void SetDMFormActive(bool isActive)
        {
            isDMFormActive = isActive;
        }

        public void UpdateParentDMForm(DM dmForm)
        {
            parentDMForm = dmForm;
            Console.WriteLine($"parentDMForm updated: {parentDMForm?.ChatID}");
        }

        private void InitializeWebSocket()
        {
            LogMemoryUsage("Before WebSocket Initialization");

            GC.Collect();
            GC.WaitForPendingFinalizers();

            webSocket = new WebSocket($"wss://gateway.discord.gg/?v=9&encoding=json");
            webSocket.SslConfiguration.EnabledSslProtocols = Tls12;
            webSocket.OnMessage += async (sender, e) => await HandleWebSocketMessage(e.Data);
            webSocket.OnError += (sender, e) => HandleWebSocketError(e.Message);
            webSocket.OnClose += (sender, e) => HandleWebSocketClose();

            LogMemoryUsage("After WebSocket Initialization");

            webSocket.Connect();
            SendIdentifyPayload();

            memoryTrackingTimer.Start();
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
                        case "MESSAGE_CREATE":
                            await HandleMessageCreateEventAsync(json["d"]);
                            break;
                        case "PRESENCE_UPDATE":
                            Console.WriteLine("Received status update");
                            break;
                        default:
                            Console.WriteLine($"Unhandled event type: {eventType}");
                            break;
                    }
                    break;

                case 1:
                    Console.WriteLine("Heartbeat event received");
                    break;

                case 10:
                    Console.WriteLine("Hello! From Discord Gateway");
                    break;

                default:
                    Console.WriteLine($"Unhandled OpCode: {opCode}");
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

            if (!isDMFormActive || parentDMForm == null) return;

            if (parentDMForm.ChatID.ToString() == channelId)
            {
                parentDMForm.Invoke((MethodInvoker)(() =>
                {
                    parentDMForm.AddMessage(author, content, userId, avatarHash);
                    parentDMForm.ScrollToBottom();
                }));
            }

            if (parentGroupForm != null && channelId == parentGroupForm.ChatID.ToString())
            {
                parentGroupForm.Invoke((MethodInvoker)(() => parentGroupForm.ScrollToBottom()));
            }

            if (parentServerForm != null && channelId == parentServerForm.ChatID.ToString())
            {
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

                List<Task> statusTasks = new List<Task>();

                foreach (var presence in presences)
                {
                    string userId = (string)presence["user"]?["id"];
                    string userStatus = (string)presence["status"] ?? "Offline";

                    userStatus = statusTranslations.ContainsKey(userStatus) ? statusTranslations[userStatus] : "Offline";

                    var activities = presence["activities"] as JArray;
                    string statusMessage = userStatus;

                    var customStatus = activities?.FirstOrDefault(a => (int?)a["type"] == 4);
                    bool hasEmoji = customStatus != null && (customStatus["state"]?.ToString().Contains("🙂") == true || customStatus["emoji"] != null);

                    if (hasEmoji) statusMessage = userStatus;
                    else if (customStatus != null) statusMessage = customStatus["state"]?.ToString() ?? "No current activity";

                    var task = Task.Run(() => UserStatusManager.SetUserStatus(userId, statusMessage));
                    statusTasks.Add(task);
                }

                await Task.WhenAll(statusTasks);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error parsing READY event: {ex.Message}");
            }
        }

        private void HandleWebSocketError(string message)
        {
            Console.WriteLine($"WebSocket error: {message}");
            ReconnectWebSocket();
        }

        private void HandleWebSocketClose()
        {
            LogMemoryUsage("Before WebSocket Close");

            Console.WriteLine("WebSocket connection closed");
            if (!websocketClosed)
            {
                ReconnectWebSocket();
            }

            LogMemoryUsage("After WebSocket Close");
        }

        public void CloseWebSocket()
        {
            if (webSocket?.ReadyState != WebSocketState.Closed)
            {
                LogMemoryUsage("Before WebSocket Close");

                Console.WriteLine("Closing WebSocket...");
                websocketClosed = true;
                webSocket?.Close();

                LogMemoryUsage("After WebSocket Close");
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

        private void LogMemoryUsage(string message)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();

            long memoryUsed = GC.GetTotalMemory(false);
            Console.WriteLine($"{message}: {memoryUsed / (1024 * 1024)} MB");
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposed) return;
            if (disposing)
            {
                if (webSocket != null && webSocket.ReadyState != WebSocketState.Closed)
                {
                    webSocket.Close();
                }
                webSocket = null;

                memoryTrackingTimer?.Stop();
                memoryTrackingTimer = null;
            }
            disposed = true;
        }
    }
}
