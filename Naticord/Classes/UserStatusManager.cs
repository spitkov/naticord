using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Naticord.Classes
{
    internal class UserStatusManager
    {
        private static Dictionary<string, string> userStatuses = new Dictionary<string, string>();

        public static void SetUserStatus(string userId, string status)
        {
            if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(status))
            {
                Debug.WriteLine("Error occurred during user status set process.");
                return;
            }

            if (userStatuses.ContainsKey(userId))
            {
                userStatuses[userId] = status;
            }
            else
            {
                userStatuses.Add(userId, status);
            }
        }

        public static string GetUserStatus(string userId)
        {
            return userStatuses.ContainsKey(userId) ? userStatuses[userId] : "Offline";
        }

        public static void ClearStatuses()
        {
            userStatuses.Clear();
        }

        public static Dictionary<string, string> GetAllUserStatuses()
        {
            return userStatuses;
        }
    }
}
