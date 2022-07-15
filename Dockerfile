FROM sitespeedio/node:ubuntu-20.04-nodejs-16.14.2 as npm_emoji
WORKDIR /app

COPY wwwroot/images/package_emoji.json package.json
RUN npm i

FROM sitespeedio/node:ubuntu-20.04-nodejs-16.14.2 as npm
WORKDIR /app

COPY wwwroot/images/package.json .
RUN npm i

# https://docs.microsoft.com/en-us/aspnet/core/host-and-deploy/docker/building-net-docker-images?view=aspnetcore-6.0
FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /source

# copy csproj and restore as distinct layers
COPY *.sln .
COPY MarkdownTest/*.csproj ./MarkdownTest/
COPY DiscordModel/*.csproj ./DiscordModel/
COPY NKDiscordChatWidget/*.csproj ./NKDiscordChatWidget/
COPY WidgetServices/*.csproj ./WidgetServices/
RUN dotnet restore NKDiscordChatWidget

# copy everything else and build app
COPY . .
WORKDIR /source/NKDiscordChatWidget
RUN dotnet publish -c Release -o /app

# final stage/image
FROM mcr.microsoft.com/dotnet/aspnet:6.0
WORKDIR /app
COPY ./wwwroot ./wwwroot/

COPY --from=npm_emoji /app/node_modules/twemoji-emojis/vendor/svg/*.svg ./wwwroot/images/emoji/twemoji/
COPY --from=npm_emoji /app/node_modules/emoji-assets/png/128/*.png ./wwwroot/images/emoji/joypixels/
COPY --from=npm /app/node_modules ./wwwroot/images/node_modules/

COPY --from=build /app ./
ENV ASPNETCORE_ENVIRONMENT=Production
ENTRYPOINT ["dotnet", "NKDiscordChatWidget.dll", "-p", "5050", "--global"]
