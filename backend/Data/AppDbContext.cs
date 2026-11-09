using Microsoft.EntityFrameworkCore;
using Tennis.Api.Data.Entities;

namespace Tennis.Api.Data;

// AppDbContext is the EF Core session for players, tournaments, matches, and import history.
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Player> Players => Set<Player>();
    public DbSet<Tournament> Tournaments => Set<Tournament>();
    public DbSet<TournamentEdition> TournamentEditions => Set<TournamentEdition>();
    public DbSet<Match> Matches => Set<Match>();
    public DbSet<MatchPlayer> MatchPlayers => Set<MatchPlayer>();
    public DbSet<MatchPlayerStatistics> MatchPlayerStatistics => Set<MatchPlayerStatistics>();
    public DbSet<MatchSet> MatchSets => Set<MatchSet>();
    public DbSet<ImportBatch> ImportBatches => Set<ImportBatch>();
    public DbSet<ImportFileRecord> ImportFiles => Set<ImportFileRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // A match belongs to one tournament edition and two match-players. Restrict keeps history if a parent is still referenced.
        modelBuilder.Entity<Player>(entity =>
        {
            entity.HasKey(player => player.Id);
            entity.Property(player => player.Name).IsRequired();
            entity.HasIndex(player => player.SourceId).IsUnique();
        });

        modelBuilder.Entity<Tournament>(entity =>
        {
            entity.HasKey(tournament => tournament.Id);
            entity.Property(tournament => tournament.Name).IsRequired();
            entity.HasIndex(tournament => tournament.SourceId).IsUnique();
        });

        modelBuilder.Entity<TournamentEdition>(entity =>
        {
            entity.HasKey(edition => edition.Id);
            entity.HasOne(edition => edition.Tournament)
                .WithMany(tournament => tournament.Editions)
                .HasForeignKey(edition => edition.TournamentId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(edition => new { edition.TournamentId, edition.Year }).IsUnique();
            entity.HasIndex(edition => edition.Year);
            entity.HasIndex(edition => edition.Surface);
            entity.HasIndex(edition => edition.Level);
        });

        modelBuilder.Entity<Match>(entity =>
        {
            entity.HasKey(match => match.Id);
            entity.Property(match => match.SourceKey).IsRequired();
            entity.Property(match => match.RawRecord).HasColumnType("jsonb").IsRequired();
            entity.HasIndex(match => match.SourceKey).IsUnique();
            entity.HasIndex(match => match.SourceYear);
            entity.HasIndex(match => match.PlayedOn);
            entity.HasOne(match => match.TournamentEdition)
                .WithMany(edition => edition.Matches)
                .HasForeignKey(match => match.TournamentEditionId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(match => match.ImportFile)
                .WithMany(file => file.Matches)
                .HasForeignKey(match => match.ImportFileId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<MatchPlayer>(entity =>
        {
            entity.HasKey(row => row.Id);
            entity.Property(row => row.Side).IsRequired();
            entity.Property(row => row.Statistics).HasColumnType("jsonb").IsRequired();
            entity.Property(row => row.Odds).HasColumnType("jsonb").IsRequired();
            entity.HasIndex(row => new { row.MatchId, row.Side }).IsUnique();
            entity.HasOne(row => row.Match)
                .WithMany(match => match.Players)
                .HasForeignKey(row => row.MatchId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(row => row.Player)
                .WithMany(player => player.MatchPlayers)
                .HasForeignKey(row => row.PlayerId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<MatchPlayerStatistics>(entity =>
        {
            entity.HasKey(stats => stats.Id);
            entity.HasIndex(stats => stats.MatchPlayerId).IsUnique();
            entity.Property(stats => stats.ServicePointsWonPercent).HasPrecision(7, 3);
            entity.Property(stats => stats.ReturnPointsWonPercent).HasPrecision(7, 3);
            entity.Property(stats => stats.BreakPointsWonPercent).HasPrecision(7, 3);
            entity.Property(stats => stats.BreakPointsSavedPercent).HasPrecision(7, 3);
            entity.Property(stats => stats.FirstServePercent).HasPrecision(7, 3);
            entity.Property(stats => stats.FirstServePointsWonPercent).HasPrecision(7, 3);
            entity.Property(stats => stats.SecondServePointsWonPercent).HasPrecision(7, 3);
            entity.HasOne(stats => stats.MatchPlayer)
                .WithOne(row => row.PlayerStatistics)
                .HasForeignKey<MatchPlayerStatistics>(stats => stats.MatchPlayerId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<MatchSet>(entity =>
        {
            entity.HasKey(set => set.Id);
            entity.HasIndex(set => new { set.MatchId, set.SetNumber }).IsUnique();
            entity.ToTable(table => table.HasCheckConstraint("CK_MatchSets_SetNumber", "\"SetNumber\" BETWEEN 1 AND 5"));
            entity.HasOne(set => set.Match)
                .WithMany(match => match.Sets)
                .HasForeignKey(set => set.MatchId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ImportBatch>(entity =>
        {
            entity.HasKey(batch => batch.Id);
            entity.Property(batch => batch.Mode).IsRequired();
            entity.Property(batch => batch.Provenance).IsRequired();
            entity.HasIndex(batch => batch.StartedAt);
        });

        modelBuilder.Entity<ImportFileRecord>(entity =>
        {
            entity.HasKey(file => file.Id);
            entity.Property(file => file.Sha256).IsRequired();
            entity.Property(file => file.HeaderJson).HasColumnType("jsonb").IsRequired();
            entity.Property(file => file.UnmappedColumns).HasColumnType("jsonb").IsRequired();
            entity.Property(file => file.Status).IsRequired();
            entity.HasIndex(file => file.Sha256);
            entity.HasOne(file => file.ImportBatch)
                .WithMany(batch => batch.Files)
                .HasForeignKey(file => file.ImportBatchId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
