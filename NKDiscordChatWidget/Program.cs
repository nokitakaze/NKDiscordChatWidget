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
    private static ProgramOptions Options;
    private static readonly DiscordRepository DiscordRepository = new();

    private static void Main(string[] args)
    {
        ARGS = args;
        try
        {
            Parser.Default.ParseArguments<ProgramOptions>(args).WithParsed(Startup);
        }
        catch (Exception)
        {
            throw;
        }
    }

    [SuppressMessage("ReSharper", "RedundantTypeArgumentsOfMethod")]
    private static void Startup(ProgramOptions ProgramOptions)
    {
        Options = ProgramOptions;
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

        builder.Services.AddHostedService<ResourceFileWatch>();
        builder.Services.AddHostedService<ClearChatTimer>();
        builder.Services.AddHostedService<Bot>();

        // Чтобы эта строка работала, надо отсутствие ASPNETCORE_URLS в переменных
        builder.WebHost
            .PreferHostingUrls(false)
            .UseUrls(string.Format("http://localhost:{0}", ProgramOptions.HttpPort));

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