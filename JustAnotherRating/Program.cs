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

        var stats1x1 = ratings.GetFormattedStats1x1();
        var stats2x2 = ratings.GetFormattedStats2x2();

        var message = "Рейтинг участников:\n";
        message += !string.IsNullOrWhiteSpace(stats1x1) ? $"1x1:\n{stats1x1}\n\n" : "";
        message += !string.IsNullOrWhiteSpace(stats2x2) ? $"2x2:\n{stats2x2}" : "";

        await BotClient.SendMessage(chatId, message, cancellationToken: cancellationToken);
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

        if (args.Length == 4 && args[0].Equals("/game"))
        {
            await ProcessGame1X1(chatId, args[1], args[2], args[3], cancellationToken);
        }
        else if (args.Length == 7 && args[0].Equals("/game"))
        {
            await ProcessGame2X2(chatId, args[1], args[2], args[3], args[4], args[5], args[6], cancellationToken);
        }
        else
        {
            await BotClient.SendMessage(chatId, "Неверный формат команды. Используйте: \n" +
                                                "/game @player1 @player2 @winner для 1x1\n" +
                                                "/game @player1 @player2 @player3 @player4 @winner1 @winner2 для 2x2",
                cancellationToken: cancellationToken);
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

/// <summary>
/// Класс Ratings отвечает за управление рейтингами пользователей.
/// </summary>
class Ratings
{
    private readonly Dictionary<string, int> _rating1x1 = new();
    private readonly Dictionary<string, int> _rating2x2 = new();
    private const double KFactor = 32.0;

    public void InitializePlayer(string player)
    {
        if (!_rating1x1.ContainsKey(player)) _rating1x1[player] = 1500;
        if (!_rating2x2.ContainsKey(player)) _rating2x2[player] = 1500;
    }

    public bool UpdateRating1X1(string player1, string player2, string winner)
    {
        if (winner != player1 && winner != player2)
        {
            return false;
        }

        var loser = winner == player1 ? player2 : player1;

        var ratingWinner = _rating1x1[winner];
        var ratingLoser = _rating1x1[loser];

        double expectedWinner = 1.0 / (1.0 + Math.Pow(10, (ratingLoser - ratingWinner) / 400.0));
        double expectedLoser = 1.0 / (1.0 + Math.Pow(10, (ratingWinner - ratingLoser) / 400.0));

        _rating1x1[winner] += (int)(KFactor * (1 - expectedWinner));
        _rating1x1[loser] += (int)(KFactor * (0 - expectedLoser));

        return true;
    }

    public bool UpdateRating2X2(string player1, string player2, string player3, string player4, string winner1,
        string winner2)
    {
        var team1 = new HashSet<string> { player1, player2 };
        var team2 = new HashSet<string> { player3, player4 };

        var winners = new HashSet<string> { winner1, winner2 };

        if (winners.IsSubsetOf(team1))
        {
            UpdateTeamRatings(team1, team2);
        }
        else if (winners.IsSubsetOf(team2))
        {
            UpdateTeamRatings(team2, team1);
        }
        else
        {
            return false;
        }

        return true;
    }

    private void UpdateTeamRatings(HashSet<string> winningTeam, HashSet<string> losingTeam)
    {
        int winningRating = winningTeam.Sum(player => _rating2x2[player]);
        int losingRating = losingTeam.Sum(player => _rating2x2[player]);

        double expectedWinning = 1.0 / (1.0 + Math.Pow(10, (losingRating - winningRating) / 800.0));
        double expectedLosing = 1.0 / (1.0 + Math.Pow(10, (winningRating - losingRating) / 800.0));

        foreach (var player in winningTeam)
        {
            _rating2x2[player] += (int)(KFactor * (1 - expectedWinning));
        }

        foreach (var player in losingTeam)
        {
            _rating2x2[player] += (int)(KFactor * (0 - expectedLosing));
        }
    }

    public string GetFormattedStats1x1()
    {
        return string.Join("\n", _rating1x1.OrderByDescending(r => r.Value)
            .Select(r => $"{r.Key}: {r.Value}"));
    }

    public string GetFormattedStats2x2()
    {
        return string.Join("\n", _rating2x2.OrderByDescending(r => r.Value)
            .Select(r => $"{r.Key}: {r.Value}"));
    }

    public bool IsEmpty()
    {
        return !_rating1x1.Any() && !_rating2x2.Any();
    }
}
