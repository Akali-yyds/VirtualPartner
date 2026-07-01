using System;
using System.Collections.Generic;
using UnityEngine;

namespace VirtualPartner.Runtime.PhoneOS
{
    [Serializable]
    public sealed class MomotalkMessageData
    {
        [SerializeField] private string senderName;
        [SerializeField] private string messageText;
        [SerializeField] private string timeText;
        [SerializeField] private bool isUser;

        public MomotalkMessageData(string senderName, string messageText, string timeText, bool isUser)
        {
            this.senderName = senderName;
            this.messageText = messageText;
            this.timeText = timeText;
            this.isUser = isUser;
        }

        public string SenderName => senderName;
        public string MessageText => messageText;
        public string TimeText => timeText;
        public bool IsUser => isUser;

        public static List<MomotalkMessageData> CreateStage5Preview(string contactName)
        {
            var safeName = string.IsNullOrWhiteSpace(contactName) ? "Toki" : contactName;
            return new List<MomotalkMessageData>
            {
                new MomotalkMessageData(safeName, "Hello, Sensei.", "18:20", false),
                new MomotalkMessageData("You", "Hi " + safeName + ".", "18:21", true),
                new MomotalkMessageData(safeName, "This is the new PhoneOS Momotalk preview.", "18:22", false),
                new MomotalkMessageData("You", "The UI is being rebuilt inside PhoneOS.", "18:23", true),
                new MomotalkMessageData(safeName, "I will wait here until the real chat system is connected.", "18:24", false),
                new MomotalkMessageData(safeName, "This is a longer message used to verify wrapping and bubble layout inside the scroll view.", "18:25", false)
            };
        }
    }
}
