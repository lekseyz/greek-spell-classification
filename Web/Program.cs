using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

/*var token = Environment.GetEnvironmentVariable("TELEGRAM_BOT_TOKEN");
if (string.IsNullOrWhiteSpace(token))
{
    Console.WriteLine("ОШИБКА: Токен не найден! (Проверьте переменные окружения)");
    return;
}*/

var botClient = new TelegramBotClient("8483840427:AAEkDox_niCsZ-8qV4CmzUW39s6VIP7oxn4");
using CancellationTokenSource cts = new();

ReceiverOptions receiverOptions = new()
{
    AllowedUpdates = Array.Empty<UpdateType>()
};

botClient.StartReceiving(
    HandleUpdateAsync,
    HandleErrorAsync,
    receiverOptions,
    cts.Token
);

var me = await botClient.GetMe();
Console.WriteLine($"Бот @{me.Username} запущен. Нажмите Enter для выхода.");
Console.ReadLine();

cts.Cancel();
async Task HandleUpdateAsync(ITelegramBotClient bot, Update update, CancellationToken cancellationToken)
{
    if (update.Message is not { } message)
        return;

    var chatId = message.Chat.Id;
    Console.WriteLine($"Пришло сообщение: {message.Type}");

    switch (message.Type)
    {
        case MessageType.Text:
            await bot.SendMessage(
                chatId: chatId,
                text: $"Эхо: {message.Text}",
                cancellationToken: cancellationToken
            );
            break;

        case MessageType.Photo:
            var photoId = message.Photo!.Last().FileId;

            await bot.SendMessage(
                chatId: chatId,
                text: $"ID фото: {photoId}",
                cancellationToken: cancellationToken
            );
            break;
            
        default:
             await bot.SendMessage(
                chatId: chatId,
                text: "Я понимаю только текст и фото",
                cancellationToken: cancellationToken
            );
            break;
    }
}

Task HandleErrorAsync(ITelegramBotClient bot, Exception exception, CancellationToken cancellationToken)
{
    var errorMessage = exception switch
    {
        ApiRequestException apiRequestException
            => $"Telegram API Error:\n[{apiRequestException.ErrorCode}]\n{apiRequestException.Message}",
        _ => exception.ToString()
    };

    Console.WriteLine(errorMessage);
    
    return Task.CompletedTask;
}