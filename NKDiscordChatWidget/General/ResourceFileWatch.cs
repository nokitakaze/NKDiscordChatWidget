using System;
using System.Collections.Generic;
using System.IO;
using NKDiscordChatWidget.WidgetServer;

namespace NKDiscordChatWidget.General
{
    /// <summary>
    /// https://docs.microsoft.com/en-us/dotnet/api/system.io.filesystemwatcher?redirectedfrom=MSDN&view=netcore-2.1
    /// </summary>
    public static class ResourceFileWatch
    {
        public static void StartTask()
        {
            using (FileSystemWatcher watcher1 = new FileSystemWatcher())
            {
                watcher1.Path = Options.WWWRoot;

                // Watch for changes in LastAccess and LastWrite times, and
                // the renaming of files or directories.
                watcher1.NotifyFilter = NotifyFilters.LastAccess
                                        | NotifyFilters.LastWrite
                                        | NotifyFilters.FileName
                                        | NotifyFilters.DirectoryName;

                watcher1.IncludeSubdirectories = true;

                watcher1.Filter = "*";

                // Add event handlers.
                watcher1.Changed += OnChanged;
                watcher1.Created += OnChanged;
                watcher1.Deleted += OnChanged;
                watcher1.Renamed += OnRenamed;

                // Begin watching.
                watcher1.EnableRaisingEvents = true;

                General.Global.globalCancellationToken.WaitHandle.WaitOne();
            }
        }

        public static HashSet<string> GetWatchedFilenames()
        {
            // MUST BE IN LOWER CASE

            return new HashSet<string>() {"images/chat.js", "images/main.css", "chat.html"};
        }

        public static bool IfFileShouldBeWatched(string fullFilename, string WWWRootFolder)
        {
            WWWRootFolder = WWWRootFolder.Replace('\\', '/').TrimEnd('/');

            if (fullFilename.Length <= WWWRootFolder.Length + 1)
            {
                // WAT?!
                return false;
            }

            var postfix = fullFilename
                .Substring(WWWRootFolder.Length + 1)
                .Replace('\\', '/')
                .ToLowerInvariant();
            return GetWatchedFilenames().Contains(postfix);
        }

        public static bool IfFileShouldBeWatched(string fullFilename)
        {
            return IfFileShouldBeWatched(fullFilename, Options.WWWRoot);
        }

        // Define the event handlers.
        private static void OnChanged(object source, FileSystemEventArgs e)
        {
            if (!IfFileShouldBeWatched(e.FullPath))
            {
                return;
            }

            // Specify what is done when a file is changed, created, or deleted.
            Console.WriteLine($"File: {e.FullPath} {e.ChangeType}");
            WebsocketClientSide.ChangeResource(e.Name);
        }

        private static void OnRenamed(object source, RenamedEventArgs e)
        {
            if (!IfFileShouldBeWatched(e.FullPath) && !IfFileShouldBeWatched(e.OldFullPath))
            {
                return;
            }

            // Specify what is done when a file is renamed.
            Console.WriteLine($"File: {e.OldFullPath} renamed to {e.FullPath}");
            WebsocketClientSide.ChangeResource(e.Name);
        }

        public struct S1
        {
            public int f1;
            public string s1;

            public int property1 => f1 + 1;

            public void HelloWorld()
            {
                
            }

            public override string ToString()
            {
                return "I am a struct";
            }
        }
    }
}