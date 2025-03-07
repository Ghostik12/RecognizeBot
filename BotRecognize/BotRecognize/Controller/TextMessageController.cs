using Microsoft.Extensions.DependencyInjection;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace BotRecognize.Controller
{
    public class TextMessageController
    {
        private ITelegramBotClient _client;
        public TextMessageController(ITelegramBotClient client)
        {
            _client = client;
        }

        internal async Task BotClient_OnCallbackQuery(CallbackQuery? callbackQuery)
        {
            throw new NotImplementedException();
        }

        internal async Task Handle(Update update, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }
    }
}