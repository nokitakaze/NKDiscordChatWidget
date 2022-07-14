using System;
using System.IO;
using CommandLine;

namespace NKDiscordChatWidget.Services.General
{
    public class ProgramOptions
    {
        [Option('t', "discord-token", Required = true, HelpText = "Discord bot token")]
        public string DiscordBotToken { get; set; }

        [Option('p', "port", Required = false, HelpText = "Port for local HTTP server", Default = 5050)]
        public int HttpPort { get; set; }

        public static string WWWRoot
        {
            get
            {
                string folder = AppDomain
                    .CurrentDomain
                    .BaseDirectory
                    .Replace('\\', '/')
                    .TrimEnd('/');
                do
                {
                    if (Directory.Exists(folder + "/wwwroot"))
                    {
                        return folder + "/wwwroot";
                    }

                    folder = folder.Substring(0, folder.LastIndexOf('/')).TrimEnd('/');
                } while (folder.Length > 3); // @todo поменять на правильный

                return null;
            }
        }
    }
}