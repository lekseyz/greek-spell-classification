using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using MessageOperator;
using Ml;

BotService mlBot = new BotService();
CustomNeuralNetwork greekPredictor = CustomNeuralNetwork.Load(Path.Combine(AppContext.BaseDirectory, "custom_model.json"));

var token = Environment.GetEnvironmentVariable("TELEGRAM_BOT_TOKEN");
if (string.IsNullOrWhiteSpace(token))
{
    Console.WriteLine("ОШИБКА: Токен не найден! (Проверьте переменные окружения)");
    return;
}
var botClient = new TelegramBotClient(token);
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
            var messageText = message.Text;
            if (string.IsNullOrEmpty(messageText))
            {
                await bot.SendMessage(
                    chatId: chatId,
                    text: $"Пиши нормально!!!",
                    cancellationToken: cancellationToken
                );
                return;
            }
            
            if (messageText.StartsWith("/"))
            {
                switch (messageText)
                {
                    case "/start":
                        mlBot.AddUser(chatId);
                        await bot.SendMessage(
                            chatId: chatId,
                            text: $"Начат новый чат",
                            cancellationToken: cancellationToken
                        );
                        return;
                    case "/help":
                        await bot.SendMessage(
                            chatId: chatId,
                            text: "С ботом ты можешь просто дружески пообщаться или прислать твою любимую букву греческого алфавита и бот ответит что это за буква. \nЧтоб очистить/начать заново чат используй команду /start",
                            cancellationToken: cancellationToken
                        );
                        return;
                    default:
                        await bot.SendMessage(
                            chatId: chatId,
                            text: "Команда не распознана. \nЧтоб получить справку по использованию бота используй команду /help",
                            cancellationToken: cancellationToken
                        );
                        return;
                }
            }
            
            await bot.SendMessage(
                chatId: chatId,
                text: mlBot.Talk(messageText, chatId),
                cancellationToken: cancellationToken
            );
            break;

        case MessageType.Photo:
            if (message.Photo is null)
            {
                await bot.SendMessage(
                    chatId: chatId,
                    text: "Нормальные фотки кидай!!!",
                    cancellationToken: cancellationToken
                );
                return;
            }
            
            var photoId = message.Photo!.Last().FileId;
            var photo = message.Photo.First();
            
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