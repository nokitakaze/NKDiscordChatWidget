using System.Diagnostics.CodeAnalysis;
using CommandLine;
using Microsoft.Extensions.FileProviders;
using NKDiscordChatWidget.BackgroundService;
using NKDiscordChatWidget.Services;
using NKDiscordChatWidget.Services.General;
using NKDiscordChatWidget.Services.Services;
using NKDiscordChatWidget.Services.Util;

namespace NKDiscordChatWidget;

internal static class Program
{
    private static string[] ARGS;
    private static readonly DiscordRepository DiscordRepository = new();

    private static void Main(string[] args)
    {
        ARGS = args;
        Parser.Default.ParseArguments<ProgramOptions>(args).WithParsed(Startup);
    }

    [SuppressMessage("ReSharper", "RedundantTypeArgumentsOfMethod")]
    private static void Startup(ProgramOptions ProgramOptions)
    {
        var token = Environment.GetEnvironmentVariable("DISCORD_BOT_TOKEN");
        if (!string.IsNullOrEmpty(token) && string.IsNullOrEmpty(ProgramOptions.DiscordBotToken))
        {
            ProgramOptions.DiscordBotToken = token!;
        }

        if (string.IsNullOrEmpty(ProgramOptions.DiscordBotToken))
        {
            throw new Exception("DiscordBotToken is empty");
        }

        UnicodeEmojiEngine.LoadAllEmojiPacks(ProgramOptions.WWWRoot);

        var builder = WebApplication.CreateBuilder(ARGS);

        builder.Services.AddSignalR();
        // builder.Services.AddControllers();
        builder.Services.AddSingleton<SmallController>();

        builder.Services.AddSingleton<WebsocketClientSidePool>();
        builder.Services.AddSingleton<ProgramOptions>(_ => ProgramOptions);
        builder.Services.AddSingleton(_ => DiscordRepository);
        builder.Services.AddSingleton<MessageMarkdownParser>();
        builder.Services.AddSingleton<MessageArtist>();

        // ResourceFileWatch отключён, потому что не функционирует корректно под Linux
        // builder.Services.AddHostedService<ResourceFileWatch>();
        builder.Services.AddHostedService<ClearChatTimer>();
        builder.Services.AddHostedService<Bot>();

        var listenUrl = string.Format(
            "http://{0}:{1}",
            ProgramOptions.ListenGlobal ? "0.0.0.0" : "localhost",
            ProgramOptions.HttpPort
        );

        // Чтобы эта строка работала, необходимо отсутствие ASPNETCORE_URLS в переменных
        builder.WebHost
            .PreferHostingUrls(false)
            .UseUrls(listenUrl);

        // Собираем готовое приложение из сервисов
        var app = builder.Build();

        if (!app.Environment.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }

        // https://docs.microsoft.com/ru-ru/aspnet/core/tutorials/signalr?view=aspnetcore-6.0&tabs=visual-studio
        app.MapHub<WebsocketClientSide>("/websocketChat");

        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = new PhysicalFileProvider(Path.Combine(ProgramOptions.WWWRoot, "images")),
            RequestPath = "/images"
        });

        var defaultController = app.Services.GetService<SmallController>()!;

        app.MapGet("/", defaultController.MainPage);
        app.MapGet("/chat.cgi", defaultController.ChatHTML);

        app.Run();
    }
}