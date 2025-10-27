using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;

namespace CallbreakApp.Models;

public class Round
{
    public int Id { get; set; }
    public int GameSessionId { get; set; }
    [Range(1, 5)]
    public int RoundNumber { get; set; }
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public Dictionary<int, int> Bids { get; set; } = new(); 

    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public Dictionary<int, int> Tricks { get; set; } = new(); 
    public bool IsConfirmed { get; set; } = false;

    public GameSession GameSession { get; set; } = null!;

    public void CalculateScores()
    {
        foreach (var player in GameSession.Players)
        {
            if (!Bids.TryGetValue(player.Id, out int bid) || !Tricks.TryGetValue(player.Id, out int tricks))
                throw new InvalidOperationException("Missing bid or tricks for player.");

            if (bid < 0 || bid > 13 || tricks < 0 || tricks > 13)
                throw new ArgumentOutOfRangeException("Bid/Tricks must be 0-13.");

            double score = tricks >= bid ? bid + 0.1 * (tricks - bid) : -(bid - tricks);
            player[RoundNumber] = score;
            player.TotalScore += score;
        }
    }
}