using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore;
using CallbreakApp.Models;

namespace CallbreakApp.Data;

public class ApplicationDbContext : IdentityDbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

    public DbSet<GameSession> GameSessions { get; set; } // game sessions tables    
    public DbSet<PlayerSession> PlayerSessions { get; set; } // player sessions table
    public DbSet<Round> Rounds { get; set; } //Round tables
    public DbSet<RoundScore> RoundScores { get; set; }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

      
        builder.Entity<IdentityUser>(b =>
        {
            b.Property(u => u.Id).HasColumnType("varchar(36)").HasMaxLength(36); // user GUID
            b.Property(u => u.NormalizedUserName).HasMaxLength(256);
            b.Property(u => u.NormalizedEmail).HasMaxLength(256);
        });

        builder.Entity<IdentityRole>(b =>
        {
            b.Property(r => r.Id).HasColumnType("varchar(36)").HasMaxLength(36);
            b.Property(r => r.NormalizedName).HasMaxLength(256);
        });

        builder.Entity<IdentityUserLogin<string>>(b =>
        {
            b.Property(l => l.LoginProvider).HasColumnType("varchar(128)").HasMaxLength(128); //external login
            b.Property(l => l.ProviderKey).HasColumnType("varchar(128)").HasMaxLength(128);
        });

        builder.Entity<IdentityUserToken<string>>(b =>
        {
            b.Property(t => t.LoginProvider).HasColumnType("varchar(128)").HasMaxLength(128); //token storage
            b.Property(t => t.Name).HasColumnType("varchar(128)").HasMaxLength(128);
        });

        builder.Entity<IdentityUserRole<string>>(b =>
        {
            b.Property(ur => ur.UserId).HasColumnType("varchar(36)").HasMaxLength(36);
            b.Property(ur => ur.RoleId).HasColumnType("varchar(36)").HasMaxLength(36);
        });
        //  below represents relationships between gamesession, playersession, roundscore and round tables
        builder.Entity<GameSession>()
            .HasMany(g => g.Players)
            .WithOne(p => p.GameSession)
            .HasForeignKey(p => p.GameSessionId);

        builder.Entity<GameSession>()
            .HasMany(g => g.Rounds)
            .WithOne(r => r.GameSession)
            .HasForeignKey(r => r.GameSessionId);

       builder.Entity<RoundScore>(b =>
    {
        b.HasOne(rs => rs.Round)  // RoundScore belongs to one Round
         .WithMany()  // Round has many RoundScores (no navigation prop)
         .HasForeignKey(rs => rs.RoundId)
         .OnDelete(DeleteBehavior.Cascade);  // Delete scores if round deleted

        b.HasOne(rs => rs.PlayerSession)  // RoundScore belongs to one PlayerSession
         .WithMany()  // Player has many RoundScores
         .HasForeignKey(rs => rs.PlayerSessionId)
         .OnDelete(DeleteBehavior.Cascade);  // Delete if player deleted
    });
    }
}