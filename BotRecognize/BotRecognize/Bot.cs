using BotRecognize.Controller;
using Microsoft.Extensions.Hosting;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types.Enums;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace BotRecognize
{
    public class Bot : BackgroundService
    {
        private ITelegramBotClient _telegramClient;
        private TextMessageController _textMessageController;
        private DocumentMessageController _documentMessageController;


        public Bot(ITelegramBotClient telegramClient, TextMessageController textMessageController, DocumentMessageController documentMessageController)
        {
            _telegramClient = telegramClient;
            _textMessageController = textMessageController;
            _documentMessageController = documentMessageController;
        }

        protected override async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            _telegramClient.StartReceiving(HandleUpdateAsync, HandleErrorAsync, new ReceiverOptions() { AllowedUpdates = { } }, cancellationToken: cancellationToken);

            Console.WriteLine("Бот запущен");
        }

        async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
        {
            if (update.Type == UpdateType.CallbackQuery)
            {
                await _textMessageController.BotClient_OnCallbackQuery(update.CallbackQuery);
                return;
            }

            if (update.Type == UpdateType.Message)
            {
                switch (update.Message!.Type)
                {
                    case MessageType.Text:
                        if (update.Message.Text == "/start" || update.Message.Text == "/menu")
                        {
                            //await _textMessageController.CheckUserOrAdmin(update);
                        }
                        else if (update.Message.Text.StartsWith("/admin"))
                        {
                            //await _textMessageController.HandleAdminCommand(update.Message.Chat.Id, update.Message.Text);
                        }
                        else
                        {
                            await _textMessageController.Handle(update, cancellationToken);
                        }
                        break;
                    case MessageType.Document:
                        await _documentMessageController.Handle(update, cancellationToken);
                        break;
                    default:
                        return;
                }
            }
        }

        Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
        {
            var errorMessage = exception switch
            {
                ApiRequestException apiRequestException => $"Telegram API Error:\n[{apiRequestException}]\n{apiRequestException}",
                _ => exception.ToString()
            };

            Console.WriteLine(errorMessage);

            Console.WriteLine("Ожидаем 10 секунд перед повторным подключением");
            Thread.Sleep(10000);

            return Task.CompletedTask;
        }
    }
}