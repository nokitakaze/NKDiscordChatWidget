'use strict';

$(document).ready(function () {
    let query = {};
    let last_server_answer_time = 0;
    for (let pair of document.location.search.substr(1).split('&')) {
        let a = pair.split('=');
        query[a[0]] = a[1];
    }

    let chatBlock = $('#chat');
    let queryData = {
        guild: query.guild,
        channel: query.channel,
    };
    let queryDataWebsocket = {
        guild: query.guild,
        channel: query.channel,
    };
    for (let key in query) {
        if (key.substr(0, 7) == 'option_') {
            queryData[key] = query[key];
            queryDataWebsocket[key.substr(7)] = query[key];
        }
    }
    startSignalRClient(queryDataWebsocket);

    chatBlock.load(function () {
        console.log('chatBlock.load');
    });

    $(window).on('resize', function () {
        console.log('window resize');
        windowScrollToBottom();
    });
});

function windowScrollToBottom() {
    window.scrollY = document.body.scrollHeight;
    window.scrollTo(0, document.body.scrollHeight);
}

function setLoadHandlersToMessage(message) {
    const maxErrorNumber = 100;

    message
        .find('img')
        .each(function () {
            const obj = this;
            let errorNumber = 0;
            console.debug('img load start', this);

            const interval = setInterval(function () {
                windowScrollToBottom();
            }, 50);

            $(obj).on('load', function () {
                // Доскроливаем чат до последнего пикселя
                console.debug('event load', this, errorNumber);
                clearInterval(interval);

                windowScrollToBottom();
            });

            $(obj).on('error', function (a, b, c) {
                // Перезагружаем картинку
                console.debug('event error', a, 'error number ', errorNumber, ' for', obj);
                errorNumber++;
                if (errorNumber >= maxErrorNumber) {
                    console.warn('event load error', this, errorNumber, a);
                    clearInterval(interval);
                    return;
                }

                const realSrc = obj.src;
                obj.src = '';
                obj.src = realSrc;
            });
        });

    message
        .find('video')
        .on('loadeddata', function () {
            // У видео загрузился первый кадр. Доскроливаем чат до последнего пикселя
            windowScrollToBottom();
        });
}

function startSignalRClient(queryData) {
    let url = "/websocketChat";
    const connection = new signalR
        .HubConnectionBuilder()
        .withUrl(url)
        .build();

    let chatBlock = $('#chat');
    let receiveFullMessageListGot = false;
    let receiveMessageQueue = [];

    let processReceiveMessageQueue = function () {
        for (let answer of receiveMessageQueue) {
            for (let message of answer.messages) {
                {
                    let existed = chatBlock.find('> [data-id="' + message.id + '"]');
                    if (existed.length > 0) {
                        // Такое сообщение уже существует, обновляем его
                        // hint: Это возможно, если пришло несколько event'ов ReceiveFullMessageList
                        let timeUpdate = parseFloat(existed.attr('data-time-update'));
                        if ((timeUpdate < message.time_update) || (existed.attr('data-hash') != message.hash)) {
                            existed.attr('data-time-update', message.time_update);
                            existed.attr('data-hash', message.hash);
                            existed.html(message.html);
                            setLoadHandlersToMessage(existed);
                        }

                        continue;
                    }
                }

                // Создание нового сообщения
                let d = document.createElement('div');
                $(d)
                    .attr({
                        'data-id': message.id,
                        'data-time': message.time,
                        'data-time-update': message.time_update,
                        'data-hash': message.hash,
                    })
                    .addClass('message')
                    .html(message.html);
                setLoadHandlersToMessage($(d));
                chatBlock[0].appendChild(d);
            }

            // hint: Мы не стираем сообщения в этом методе, для этого есть два других event'а:
            // ReceiveFullMessageList & RemoveMessage

            // Доскроливаем чат до последнего пикселя
            windowScrollToBottom();
        }
        receiveMessageQueue = [];
    };

    connection.on("ReceiveMessage", function (answer) {
        // Пришло одно (несколько) сообщение
        console.debug("signalR::ReceiveMessage", answer);

        receiveMessageQueue.push(answer);
        if (!receiveFullMessageListGot) {
            return;
        }

        processReceiveMessageQueue();
    });

    connection.on("ReceiveFullMessageList", function (answer) {
        // Обновление всего чата
        console.debug("signalR::ReceiveFullMessageList", answer);

        document.title = answer.channel_title;

        for (let message of answer.messages) {
            {
                let existed = chatBlock.find('> [data-id="' + message.id + '"]');
                if (existed.length > 0) {
                    // Такое сообщение уже существует, обновляем его
                    // hint: Это возможно, если пришло несколько event'ов ReceiveFullMessageList
                    let timeUpdate = parseFloat(existed.attr('data-time-update'));
                    if ((timeUpdate < message.time_update) || (existed.attr('data-hash') != message.hash)) {
                        existed.attr('data-time-update', message.time_update);
                        existed.attr('data-hash', message.hash);
                        existed.html(message.html);
                        setLoadHandlersToMessage(existed);
                    }

                    continue;
                }
            }

            // Создание нового сообщения
            let d = document.createElement('div');
            $(d)
                .attr({
                    'data-id': message.id,
                    'data-time': message.time,
                    'data-time-update': message.time_update,
                    'data-hash': message.hash,
                })
                .addClass('message')
                .html(message.html);
            setLoadHandlersToMessage($(d));
            chatBlock[0].appendChild(d);
        }

        // Стираем удалённые сообщения
        {
            let existed = new Set();
            for (let id of answer.existedID) {
                existed.add(id);
            }

            chatBlock.find('> .message').each(function () {
                let id = $(this).attr('data-id');
                if (!existed.has(id)) {
                    $(this).remove();
                }
            });

            // hint: Подсообщения удаляются сами, со стороны сервера
        }

        // Доскроливаем чат до последнего пикселя
        windowScrollToBottom();

        // Теперь запускаем обработку всех ReceiveMessage, если они были до этого
        receiveFullMessageListGot = true;
        processReceiveMessageQueue();
    });

    connection.on("RemoveMessage", function (messageId) {
        // Удаление сообщения
        console.debug("signalR::RemoveMessage", messageId);

        chatBlock.find('> .message[data-id="' + messageId + '"]').remove();
    });

    connection.start()
        .then(function () {
            console.debug("signalR::onStart", queryData);
            $('#error_block').hide();
            connection
                .invoke("ChangeDrawOptionAndGetAllMessages", JSON.stringify(queryData))
                .catch(function (err) {
                    // Выводим ошибку на экран и пытаемся зайти в чат ещё раз
                    $('#error_block').text('Error on ChangeDrawOptionAndGetAllMessages: ' + err.toString()).show();
                    setTimeout(function () {
                        startSignalRClient(queryData);
                    }, 5000);

                    console.debug("signalR::ChangeDrawOptionAndGetAllMessages::catch", user, message, err);
                    return console.error(err.toString());
                });
        })
        .catch(function (err) {
            // Выводим ошибку на экран и пытаемся зайти в чат ещё раз
            $('#error_block').text('Error connection: ' + err.toString()).show();
            setTimeout(function () {
                startSignalRClient(queryData);
            }, 5000);

            return console.error(err.toString());
        });

    connection.onclose(function () {
        console.warn('Chat connection has been closed');
        $('#error_block').text('chat has been disconnected').show();
        setTimeout(function () {
            startSignalRClient(queryData);
        }, 5000);
    });

    // @todo обрабатывать дисконнект, писать о нём в console.warn
}
