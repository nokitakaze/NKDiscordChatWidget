using System;
using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

namespace NKDiscordChatWidget.WidgetServer
{
    public class SentEventsChat : Hub
    {
        private static SentEventsChat _instance = null;

        public SentEventsChat()
        {
            _instance = this;

            Console.WriteLine(this);
        }

        public async Task SendMessage(string user, string message)
        {
            await this.Clients.All.SendAsync("ReceiveMessage", user, message);
        }

        public async Task TestMethod(string user, string message)
        {
            await this.Clients.All.SendAsync("TestMethod", user, message);
        }
    }
}