using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Web;
using NKDiscordChatWidget.DiscordModel;
using NKDiscordChatWidget.Services.General;
using NKDiscordChatWidget.Services.Services;

namespace NKDiscordChatWidget;

public class SmallController
{
    private readonly DiscordRepository DiscordRepository;

    public SmallController(DiscordRepository repository)
    {
        DiscordRepository = repository;
    }

    #region low level HTTP responder

    public async Task MainPage(HttpContext httpContext)
    {
        // Главная
        var html = await File.ReadAllTextAsync(ProgramOptions.WWWRoot + "/index.html");
        html = replaceLinksInHTML(html);
        string guildsHTML = "";
        foreach (var (guildID, channels) in DiscordRepository.channels)
        {
            string guildHTML = "";
            var channelsByGroup = new Dictionary<string, List<EventGuildCreate.EventGuildCreate_Channel>>();
            // Каналы без групп отображаются выше всех
            var groupPositions = new Dictionary<string, int> { [""] = -2 };
            foreach (var channel in channels.Values)
            {
                // ReSharper disable once SwitchStatementMissingSomeCases
                switch (channel.type)
                {
                    case 0:
                    {
                        string parentId = channel.parent_id ?? "";
                        if (!channelsByGroup.ContainsKey(parentId))
                        {
                            channelsByGroup[parentId] =
                                new List<EventGuildCreate.EventGuildCreate_Channel>();
                        }

                        channelsByGroup[parentId].Add(channel);
                        break;
                    }

                    case 4:
                        groupPositions[channel.id] = channel.position ?? -1;
                        break;
                }
            }

            var channelIDs = channelsByGroup.Keys.ToList();
            channelIDs.Sort((a, b) => groupPositions[a].CompareTo(groupPositions[b]));

            foreach (var parentChannelId in channelIDs)
            {
                var localChannels = channelsByGroup[parentChannelId];
                if (parentChannelId != "")
                {
                    guildHTML += string.Format("<li class='item'>{0}</li>",
                        HttpUtility.HtmlEncode(channels[parentChannelId].name)
                    );
                }
                else
                {
                    guildHTML += "<li class='item'>-----------------</li>";
                }

                localChannels.Sort((a, b) =>
                {
                    if (a.position == null)
                    {
                        return -1;
                    }

                    // ReSharper disable once ConvertIfStatementToReturnStatement
                    if (b.position == null)
                    {
                        return 1;
                    }

                    return a.position.Value.CompareTo(b.position.Value);
                });

                guildHTML = localChannels.Aggregate(guildHTML, (current, realChannel) =>
                    current + string.Format(
                        "<li class='item-sub'><a href='/chat.cgi?guild={1}&channel={2}' " +
                        "data-guild-id='{1}' data-channel-id='{2}' target='_blank'>{0}</a></li>",
                        HttpUtility.HtmlEncode(realChannel.name), guildID, realChannel.id));
            }

            guildHTML = string.Format(
                "<div class='block-guild'><h2><img src='{2}'> {1}</h2><ul>{0}</ul></div>",
                guildHTML,
                HttpUtility.HtmlEncode(DiscordRepository.guilds[guildID].name),
                HttpUtility.HtmlEncode(DiscordRepository.guilds[guildID].GetIconURL)
            );
            guildsHTML += guildHTML;
        }

        html = html.Replace("{#wait:guilds#}", guildsHTML);

        httpContext.Response.ContentType = "text/html; charset=utf-8";
        await httpContext.Response.WriteAsync(html);
    }

    private static readonly Regex rHTMLIncludeLink =
        new Regex("{#include_link:(.+?)#}", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public async Task ChatHTML(HttpContext httpContext)
    {
        var html = await File.ReadAllTextAsync(ProgramOptions.WWWRoot + "/chat.html");
        html = replaceLinksInHTML(html);

        httpContext.Response.ContentType = "text/html; charset=utf-8";
        await httpContext.Response.WriteAsync(html);
    }

    private string replaceLinksInHTML(string html)
    {
        return rHTMLIncludeLink.Replace(html, (m) =>
        {
            var filename = m.Groups[1].Value;
            // hint: тут специально не проверяется путь до файла
            var bytes = File.ReadAllBytes(ProgramOptions.WWWRoot + filename);

            using var hashA = SHA1.Create();
            byte[] data = hashA.ComputeHash(bytes);
            var sha1hash = data.Aggregate("", (current, c) => current + c.ToString("x2"));

            return string.Format("{0}?hash={1}",
                filename,
                sha1hash[..6]
            );
        });
    }

    #endregion
}