# NK Discord Chat Widget
[![Build status](https://ci.appveyor.com/api/projects/status/pe2pjukqf3dv97rb?svg=true)](https://ci.appveyor.com/project/nokitakaze/nkdiscordchatwidget)
[![Build status](https://ci.appveyor.com/api/projects/status/pe2pjukqf3dv97rb/branch/master?svg=true)](https://ci.appveyor.com/project/nokitakaze/nkdiscordchatwidget/branch/master)

Виджет (в виде HTML-страницы) для OBS и других программ для стриминга.

Выкладываю временно без описания настройки.

Коротко это делается так:
1. https://discordapp.com/developers/applications/ Создаёте приложение
2. Переходите во вкладку Bot в вашем приложении, создаёте бота. Доступы (permissions) у бота нулевые,
ему не нужно ничего кроме чтения сообщений и списка комнат.
3. Приглашаете бота на сервера, в которых он должен работать. Можно пригласить бота только на тот сервер,
на котором вы обладаете администраторскими привилегиями. Поэтому бота лучше сделать публчичным (public bot),


## Запуск
```bash
dotnet NKDiscordChatWidget.dll -p 5050 -t "LONG_DISCORD_BOT_TOKEN"
```

Открываете в браузере ссылку http://127.0.0.1:5050/ (номер порта — из командной строки). Выбираете необходимый канал,
копируете ссылку на него в плагин "браузер" в вашем OBS. 
