using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Naticord.Classes
{
    internal class UserStatusManager
    {
        private static Dictionary<string, string> userStatuses = new Dictionary<string, string>();
        private static readonly object userStatusesLock = new object();

        public static void SetUserStatus(string userId, string status)
        {
            if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(status))
            {
                Debug.WriteLine("Error occurred during user status set process.");
                return;
            }

            lock (userStatusesLock)
            {
                if (userStatuses.ContainsKey(userId))
                {
                    userStatuses[userId] = status;
                }
                else
                {
                    userStatuses.Add(userId, status);
                }
            }
        }

        public static string GetUserStatus(string userId)
        {
            lock (userStatusesLock)
            {
                return userStatuses.ContainsKey(userId) ? userStatuses[userId] : "Offline";
            }
        }

        public static void ClearStatuses()
        {
            lock (userStatusesLock)
            {
                userStatuses.Clear();
            }
        }

        public static Dictionary<string, string> GetAllUserStatuses()
        {
            lock (userStatusesLock)
            {
                return new Dictionary<string, string>(userStatuses);
            }
        }
    }
}
