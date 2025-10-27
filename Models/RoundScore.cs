using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CallbreakApp.Models;

public class RoundScore
{
    [Key]
    public int Id { get; set; }
    public int RoundId { get; set; }
    public int PlayerSessionId { get; set; }
    public int Bid { get; set; }
    public int Tricks { get; set; }

    public Round Round { get; set; } = null!;
    public PlayerSession PlayerSession { get; set; } = null!;
}
