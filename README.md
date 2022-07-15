# NK Discord Chat Widget
[![Build status](https://ci.appveyor.com/api/projects/status/pe2pjukqf3dv97rb?svg=true)](https://ci.appveyor.com/project/nokitakaze/nkdiscordchatwidget)
[![Test status](https://img.shields.io/appveyor/tests/nokitakaze/nkdiscordchatwidget.svg)](https://ci.appveyor.com/project/nokitakaze/nkdiscordchatwidget/branch/master)
[![Docker pulls](https://badgen.net/docker/pulls/nokitakaze/discord-chat-widget)](https://hub.docker.com/r/nokitakaze/discord-chat-widget)
[![Docker stars](https://badgen.net/docker/stars/nokitakaze/discord-chat-widget?icon=docker&label=stars)](https://hub.docker.com/r/nokitakaze/discord-chat-widget)
[![Docker image size](https://badgen.net/docker/size/nokitakaze/discord-chat-widget)](https://hub.docker.com/r/nokitakaze/discord-chat-widget)

Widget (in the form of an HTML page) for Open Broadcaster and other streaming software.

## Creating bot

Briefly, it is done like this:
1. https://discordapp.com/developers/applications/ Create an application
2. Go to the Bot tab in your application, create a bot. The bot has zero permissions,
   it doesn't need anything other than reading messages and a list of channels.
3. Invite the bot to the servers where it should work. You can invite the bot only to the server
   on which you have administrative privileges. Therefore, it is better to make the bot public (public bot),

## Start the server
### As separate application
```bash
dotnet NKDiscordChatWidget.dll -p 5050 -t "LONG_DISCORD_BOT_TOKEN"
```

Open the link http://127.0.0.1:5050/ in a browser (the port number is from the command line).
Modify settings and pick the desired channel, copy the link to it into the "browser" plugin in your OBS.

## As docker image
Your `docker-compose.yml`:
```yml
version: "3"
services:
    widget:
        build: .
        ports:
            - "5051:5050"
        environment:
            - DISCORD_BOT_TOKEN=NTXXXXXXXXXXXXXXXXXXXXXX.XXXXXX.XXXXXXXXXXXXXXXXXXXXXXXXXBI
```
Than:
```sh
docker-compose up -d
```

## License
Licensed under the Apache License.

Emoji packs belong to [Joypixels](https://www.joypixels.com/) and [Twitter](https://twemoji.twitter.com/).

