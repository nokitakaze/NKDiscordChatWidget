'use strict';

$(document).ready(function () {
    let query = {};
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
            },
        });
    }, 100);
});



