using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using NKDiscordChatWidget.General;
using NKDiscordChatWidget.Services;

namespace NKDiscordChatWidget.BackgroundService
{
    /// <summary>
    /// https://docs.microsoft.com/en-us/dotnet/api/system.io.filesystemwatcher?redirectedfrom=MSDN&view=netcore-2.1
    /// </summary>
    public class ResourceFileWatch : IHostedService
    {
        private readonly WebsocketClientSidePool Pool;
        private readonly ProgramOptions ProgramOptions;

        public ResourceFileWatch(
            WebsocketClientSidePool pool,
            ProgramOptions programOptions
        )
        {
            Pool = pool;
            ProgramOptions = programOptions;
        }

        #region IHostedService

        private readonly CancellationTokenSource CancellationSource = new CancellationTokenSource();
        private CancellationToken CancellationToken => CancellationSource.Token;
        private Task MainTask;

        [System.Diagnostics.CodeAnalysis.SuppressMessage("ReSharper", "MethodSupportsCancellation")]
        public Task StartAsync(CancellationToken cancellationToken)
        {
            MainTask = Task.Run(StartTask);
            return Task.CompletedTask;
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            CancellationSource.Cancel();
            await MainTask;
        }

        #endregion

        public void StartTask()
        {
            using (var watcher1 = new FileSystemWatcher())
            {
                watcher1.Path = ProgramOptions.WWWRoot;

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

                CancellationToken.WaitHandle.WaitOne();
            }
        }

        public static HashSet<string> GetWatchedFilenames()
        {
            // MUST BE IN LOWER CASE

            return new HashSet<string>() { "images/chat.js", "images/main.css", "chat.html" };
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

        public bool IfFileShouldBeWatched(string fullFilename)
        {
            return IfFileShouldBeWatched(fullFilename, ProgramOptions.WWWRoot);
        }

        // Define the event handlers.
        private void OnChanged(object source, FileSystemEventArgs e)
        {
            if (!IfFileShouldBeWatched(e.FullPath))
            {
                return;
            }

            // Specify what is done when a file is changed, created, or deleted.
            Console.WriteLine($"File: {e.FullPath} {e.ChangeType}");
            Pool.ChangeResource(e.Name);
        }

        private void OnRenamed(object source, RenamedEventArgs e)
        {
            if (!IfFileShouldBeWatched(e.FullPath) && !IfFileShouldBeWatched(e.OldFullPath))
            {
                return;
            }

            // Specify what is done when a file is renamed.
            Console.WriteLine($"File: {e.OldFullPath} renamed to {e.FullPath}");
            Pool.ChangeResource(e.Name);
        }
    }
}