using System.ComponentModel.DataAnnotations;
using System.Linq;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;

namespace CallbreakApp.Models;

public partial class GameSession 
{
    public int Id { get; set; }
    [Required]
    public DateTime Created { get; set; } = DateTime.Now;
    public List<PlayerSession> Players { get; set; } = new();
    public List<Round> Rounds { get; set; } = new();
    public bool IsCompleted { get; set; } = false;

    public GameSession()
    {
    }

    public virtual PlayerSession? GetWinner()
    {
        if (!IsCompleted || Players.Count == 0) return null;
        return Players.OrderByDescending(p => p.TotalScore).FirstOrDefault();
    }
}