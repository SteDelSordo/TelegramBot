using Microsoft.EntityFrameworkCore;
using TelegramBotClassifica.Models;

public class BotDbContext : DbContext
{
    public BotDbContext(DbContextOptions<BotDbContext> options) : base(options) { }

    // Questa proprietà rappresenta la tabella "UserPoints" nel nostro database SQLite
    public DbSet<UserPoints> UserPoints { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Qui possiamo definire regole aggiuntive. Per esempio, ci assicuriamo
        // che lo UserId sia unico per evitare duplicati.
        modelBuilder.Entity<UserPoints>()
            .HasIndex(u => u.UserId)
            .IsUnique();
    }
}