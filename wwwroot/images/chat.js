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
    for (let key in query) {
        if (key.substr(0, 7) == 'option_') {
            queryData[key] = query[key];
        }
    }
    setInterval(function () {
        $.ajax({
            url: '/chat_ajax.cgi',
            dataType: 'json',
            data: queryData,
            success: function (answer, status, c) {
                if (answer.time_answer < last_server_answer_time) {
                    // Это более старый ответ от сервера, пропускаем его
                    return;
                }
                last_server_answer_time = parseFloat(answer.time_answer);

                for (let message of answer.messages) {
                    {
                        let existed = chatBlock.find('> [data-id="' + message.id + '"]');
                        if (existed.length > 0) {
                            let timeUpdate = parseFloat(existed.attr('data-time-update'));
                            if ((timeUpdate < message.time_update) || (existed.attr('data-hash') != message.hash)) {
                                existed.attr('data-time-update', message.time_update);
                                existed.attr('data-hash', message.hash);
                                existed.html(message.html);
                            }

                            continue
                        }
                    }

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

                //
                window.scrollY = document.body.scrollHeight;
                window.scrollTo(0, document.body.scrollHeight);
            },
        });
    }, 100);
});



