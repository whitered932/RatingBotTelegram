namespace JustAnotherRating;

public class Ratings
{
    public Dictionary<string, int> Rating1X1 = new();
    public  Dictionary<string, int> Rating2X2 = new();
    private const double KFactor = 32.0;
    
    public void InitializePlayer(string player)
    {
        Rating1X1.TryAdd(player, 1500);
        Rating2X2.TryAdd(player, 1500);
    }

    public bool UpdateRating1X1(string player1, string player2, string winner)
    {
        if (winner != player1 && winner != player2)
        {
            return false;
        }

        var loser = winner == player1 ? player2 : player1;

        var ratingWinner = Rating1X1[winner];
        var ratingLoser = Rating1X1[loser];

        var expectedWinner = 1.0 / (1.0 + Math.Pow(10, (ratingLoser - ratingWinner) / 400.0));
        var expectedLoser = 1.0 / (1.0 + Math.Pow(10, (ratingWinner - ratingLoser) / 400.0));

        Rating1X1[winner] += (int)(KFactor * (1 - expectedWinner));
        Rating1X1[loser] += (int)(KFactor * (0 - expectedLoser));

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
        var winningRating = winningTeam.Sum(player => Rating2X2[player]);
        var losingRating = losingTeam.Sum(player => Rating2X2[player]);

        var expectedWinning = 1.0 / (1.0 + Math.Pow(10, (losingRating - winningRating) / 800.0));
        var expectedLosing = 1.0 / (1.0 + Math.Pow(10, (winningRating - losingRating) / 800.0));

        foreach (var player in winningTeam)
        {
            Rating2X2[player] += (int)(KFactor * (1 - expectedWinning));
        }

        foreach (var player in losingTeam)
        {
            Rating2X2[player] += (int)(KFactor * (0 - expectedLosing));
        }
    }

    public string GetFormattedStats1X1()
    {
        return string.Join("\n", Rating1X1.OrderByDescending(r => r.Value)
            .Select(r => $"{r.Key[1..]}: {r.Value}"));
    }

    public string GetFormattedStats2X2()
    {
        return string.Join("\n", Rating2X2.OrderByDescending(r => r.Value)
            .Select(r => $"{r.Key[1..]}: {r.Value}"));
    }

    public bool IsEmpty()
    {
        return Rating1X1.Count == 0 && Rating2X2.Count == 0;
    }
}