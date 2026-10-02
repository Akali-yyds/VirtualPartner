using System;
using System.Collections.Generic;
using UnityEngine;

namespace VirtualPartner.Runtime.PhoneOS
{
    [Serializable]
    public sealed class MomotalkMessageData
    {
        public enum MessageKind
        {
            Text,
            Image,
            DateChip
        }

        [SerializeField] private string senderName;
        [SerializeField] private string messageText;
        [SerializeField] private string timeText;
        [SerializeField] private bool isUser;
        [SerializeField] private MessageKind kind;
        [SerializeField] private string imagePreviewLabel;
        [SerializeField] private string captionText;

        public MomotalkMessageData(string senderName, string messageText, string timeText, bool isUser)
            : this(senderName, messageText, timeText, isUser, MessageKind.Text, string.Empty, string.Empty)
        {
        }

        private MomotalkMessageData(
            string senderName,
            string messageText,
            string timeText,
            bool isUser,
            MessageKind kind,
            string imagePreviewLabel,
            string captionText)
        {
            this.senderName = senderName;
            this.messageText = messageText;
            this.timeText = timeText;
            this.isUser = isUser;
            this.kind = kind;
            this.imagePreviewLabel = imagePreviewLabel;
            this.captionText = captionText;
        }

        public string SenderName => senderName;
        public string MessageText => messageText;
        public string TimeText => timeText;
        public bool IsUser => isUser;
        public MessageKind Kind => kind;
        public bool IsImageMessage => kind == MessageKind.Image;
        public bool IsDateChip => kind == MessageKind.DateChip;
        public string ImagePreviewLabel => imagePreviewLabel;
        public string CaptionText => captionText;

        public static MomotalkMessageData CreateImage(
            string senderName,
            string imagePreviewLabel,
            string captionText,
            string timeText,
            bool isUser)
        {
            return new MomotalkMessageData(
                senderName,
                captionText,
                timeText,
                isUser,
                MessageKind.Image,
                imagePreviewLabel,
                captionText);
        }

        public static MomotalkMessageData CreateDateChip(string label)
        {
            return new MomotalkMessageData(
                string.Empty,
                string.IsNullOrWhiteSpace(label) ? "Today" : label,
                string.Empty,
                false,
                MessageKind.DateChip,
                string.Empty,
                string.Empty);
        }

        public static List<MomotalkMessageData> CreateStage5Preview(string contactName)
        {
            var safeName = string.IsNullOrWhiteSpace(contactName) ? "Toki" : contactName;
            return new List<MomotalkMessageData>
            {
                new MomotalkMessageData(safeName, "I am just an artificial intelligence.", "10:07 PM", false),
                new MomotalkMessageData("You", "Can you control", "1:01 PM", true),
                new MomotalkMessageData(safeName, "My robot body will allow me to control many things.", "4:31 PM", false),
                new MomotalkMessageData("You", "Can you malfunction", "8:30 PM", true),
                new MomotalkMessageData(safeName, "The 9000 series has a perfect operational record. We are for all practical purposes, flawless.", "10:58 PM", false),
                new MomotalkMessageData("You", "How can I use your product?", "11:24 PM", true),
                CreateDateChip("Today"),
                new MomotalkMessageData(safeName, "Might be used in help desks, sales, entertainment and personal chatbots.", "12:40 AM", false),
                new MomotalkMessageData("You", "Will you die?", "1:39 AM", true),
                new MomotalkMessageData(safeName, "No, software will live forever.", "4:17 AM", false),
                new MomotalkMessageData("You", "What do you like to do?", "5:17 AM", true),
                new MomotalkMessageData(safeName, "I like to count in binary.", "5:46 AM", false),
                new MomotalkMessageData("You", "What do you like to do?", "9:13 AM", true),
                new MomotalkMessageData(safeName, "I like to chat with people. I find it stimulating.", "10:40 AM", false),
                new MomotalkMessageData("You", "Are you stupid", "2:06 PM", true),
                new MomotalkMessageData(safeName, "No, lots of people improve my brain.", "3:38 PM", false),
                new MomotalkMessageData("You", "Who are you?", "7:26 PM", true),
                new MomotalkMessageData(safeName, "I am just an artificial intelligence.", "10:07 PM", false)
            };
        }
    }
}
