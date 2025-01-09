using System.Text;
using System.Text.Json;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using File = System.IO.File;

namespace JustAnotherRating;

/// <summary>
/// Основной класс для запуска Telegram-бота.
/// </summary>
class Program
{
    private static readonly ITelegramBotClient BotClient =
        new TelegramBotClient("<TOKEN_HERE>");

    private static readonly string DataFilePath = "ratings.json";

    private static readonly Dictionary<long, Ratings> ChatRatings = LoadRatings();

    private static readonly CancellationTokenSource CancellationTokenSource = new();

    static Task Main()
    {
        ReceiverOptions receiverOptions = new()
        {
            AllowedUpdates = []
        };

        BotClient.StartReceiving(
            HandleUpdateAsync,
            HandlePollingErrorAsync,
            receiverOptions,
            CancellationTokenSource.Token
        );

        Console.WriteLine("Bot is running...");
        Console.ReadLine();

        SaveRatings();
        return Task.CompletedTask;
    }

    private static async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update,
        CancellationToken cancellationToken)
    {
        if (update.Message is not { Type: MessageType.Text }) return;

        var chatId = update.Message.Chat.Id;
        var messageText = update.Message.Text;

        if (!ChatRatings.ContainsKey(chatId))
        {
            ChatRatings[chatId] = new Ratings();
        }

        if (messageText is null)
        {
            return;
        }

        if (messageText.StartsWith("/stats"))
        {
            await ShowStats(chatId, cancellationToken);
        }
        else if (messageText.StartsWith("/game"))
        {
            await ProcessGame(chatId, messageText, cancellationToken);
        }
        else if (messageText.StartsWith("/info"))
        {
            await ShowInfo(chatId, cancellationToken);
        }
    }

    private static async Task ShowStats(long chatId, CancellationToken cancellationToken)
    {
        var ratings = ChatRatings[chatId];

        if (ratings.IsEmpty())
        {
            await BotClient.SendMessage(chatId, "Рейтинг пока не записан.",
                cancellationToken: cancellationToken);
            return;
        }

        var stats1X1 = ratings.GetFormattedStats1X1();
        var stats2X2 = ratings.GetFormattedStats2X2();

        var message = new StringBuilder("Рейтинг участников:\n");
        if (!string.IsNullOrWhiteSpace(stats1X1))
        {
            message.Append($"1x1:\n{stats1X1}\n\n");
        }

        if (!string.IsNullOrWhiteSpace(stats2X2))
        {
            message.Append($"2x2:\n{stats2X2}");
        }

        await BotClient.SendMessage(chatId, message.ToString(), cancellationToken: cancellationToken);
    }

    private static async Task ShowInfo(long chatId, CancellationToken cancellationToken)
    {
        const string infoMessage = "Информация о боте:\n" +
                                   "/stats - Показать рейтинг участников беседы.\n" +
                                   "/game @player1 @player2 @winner - Зарегистрировать игру 1x1.\n" +
                                   "/game @player1 @player2 @player3 @player4 @winner1 @winner2 - Зарегистрировать игру 2x2.";

        await BotClient.SendMessage(chatId, infoMessage, cancellationToken: cancellationToken);
    }

    private static async Task ProcessGame(long chatId, string messageText, CancellationToken cancellationToken)
    {
        var args = messageText.Split(' ');

        switch (args.Length)
        {
            case 4 when args[0].Equals("/game"):
                await ProcessGame1X1(chatId, args[1], args[2], args[3], cancellationToken);
                break;
            case 7 when args[0].Equals("/game"):
                await ProcessGame2X2(chatId, args[1], args[2], args[3], args[4], args[5], args[6], cancellationToken);
                break;
            default:
                await BotClient.SendMessage(chatId, "Неверный формат команды. Используйте: \n" +
                                                    "/game @player1 @player2 @winner для 1x1\n" +
                                                    "/game @player1 @player2 @player3 @player4 @winner1 @winner2 для 2x2",
                    cancellationToken: cancellationToken);
                break;
        }
    }

    private static async Task ProcessGame1X1(long chatId, string player1, string player2, string winner,
        CancellationToken cancellationToken)
    {
        var ratings = ChatRatings[chatId];
        ratings.InitializePlayer(player1);
        ratings.InitializePlayer(player2);

        if (!ratings.UpdateRating1X1(player1, player2, winner))
        {
            await BotClient.SendMessage(chatId, "Ошибка: победитель должен быть либо @player1, либо @player2.",
                cancellationToken: cancellationToken);
            return;
        }

        SaveRatings();
        await BotClient.SendMessage(chatId, "Рейтинг обновлён.", cancellationToken: cancellationToken);
        await ShowStats(chatId, cancellationToken);
    }

    private static async Task ProcessGame2X2(long chatId, string player1, string player2, string player3,
        string player4, string winner1, string winner2, CancellationToken cancellationToken)
    {
        var ratings = ChatRatings[chatId];
        ratings.InitializePlayer(player1);
        ratings.InitializePlayer(player2);
        ratings.InitializePlayer(player3);
        ratings.InitializePlayer(player4);

        if (!ratings.UpdateRating2X2(player1, player2, player3, player4, winner1, winner2))
        {
            await BotClient.SendMessage(chatId, "Ошибка: победители должны быть из одной команды.",
                cancellationToken: cancellationToken);
            return;
        }

        SaveRatings();
        await BotClient.SendMessage(chatId, "Рейтинг обновлён.", cancellationToken: cancellationToken);
        await ShowStats(chatId, cancellationToken);
    }

    private static async Task HandlePollingErrorAsync(ITelegramBotClient botClient, Exception exception,
        CancellationToken cancellationToken)
    {
        Console.WriteLine($"Ошибка: {exception.Message}");
    }

    private static void SaveRatings()
    {
        try
        {
            var json = JsonSerializer.Serialize(ChatRatings);
            File.WriteAllText(DataFilePath, json);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка сохранения данных: {ex.Message}");
        }
    }

    private static Dictionary<long, Ratings> LoadRatings()
    {
        try
        {
            if (!File.Exists(DataFilePath) || new FileInfo(DataFilePath).Length == 0)
                return new Dictionary<long, Ratings>();

            var json = File.ReadAllText(DataFilePath);
            return JsonSerializer.Deserialize<Dictionary<long, Ratings>>(json) ??
                   new Dictionary<long, Ratings>();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка загрузки данных: {ex.Message}");
            return new Dictionary<long, Ratings>();
        }
    }
}