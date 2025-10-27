using System.ComponentModel.DataAnnotations;
using System.Collections.Generic; 
namespace CallbreakApp.Models;

public class PlayerSession
{
    public int Id { get; set; }
    public int GameSessionId { get; set; }
    [Required, StringLength(50)]
    public string Name { get; set; } = string.Empty;
    public double TotalScore { get; set; } = 0.0;

    public GameSession GameSession { get; set; } = null!;

    private Dictionary<int, double> _roundScores = new();
    public double this[int roundNum]
    {
        get => _roundScores.GetValueOrDefault(roundNum, 0);
        set => _roundScores[roundNum] = value;
    }
}