'use strict';

$(document).ready(function () {
    let query = {};
    for (let pair of document.location.search.substr(1).split('&')) {
        let a = pair.split('=');
        query[a[0]] = a[1];
    }

    let chatBlock = $('#chat');
    setInterval(function () {
        $.ajax({
            url: '/chat_ajax.cgi',
            dataType: 'json',
            data: {
                guild: query.guild,
                channel: query.channel,
            },
            success: function (answer, status, c) {
                for (let message of answer.messages) {
                    {
                        let existed = chatBlock.find('> [data-id="' + message.id + '"]');
                        if (existed.length > 0) {
                            let timeUpdate = parseFloat(existed.attr('data-time-update'));
                            if (timeUpdate < message.time_update) {
                                existed.attr('data-time-update', message.time_update);
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
                        })
                        .addClass('message')
                        .html(message.html);
                    chatBlock[0].appendChild(d);
                }
            },
        });
    }, 100);
});



