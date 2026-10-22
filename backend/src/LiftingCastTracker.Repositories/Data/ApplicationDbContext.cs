using Microsoft.EntityFrameworkCore;
using LiftingCastTracker.Services.Models;

namespace LiftingCastTracker.Repositories.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
    {
    }

    public DbSet<Competition> Competitions => Set<Competition>();
    public DbSet<CompetitionDocument> CompetitionDocuments => Set<CompetitionDocument>();
    public DbSet<CompetitionSyncState> CompetitionSyncStates => Set<CompetitionSyncState>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Competition>(entity =>
        {
            entity.ToTable("competitions");

            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id)
                .HasColumnName("id")
                .IsRequired();

            entity.Property(e => e.Name)
                .HasColumnName("name")
                .IsRequired();

            entity.Property(e => e.Date)
                .HasColumnName("date");

            entity.Property(e => e.DateFormat)
                .HasColumnName("date_format");

            entity.Property(e => e.CreateDate)
                .HasColumnName("create_date");

            entity.Property(e => e.ShowOnHomePage)
                .HasColumnName("show_on_home_page")
                .HasDefaultValue(false);

            entity.Property(e => e.AutomaticSyncEnabled)
                .HasColumnName("automatic_sync_enabled")
                .HasDefaultValue(false);

            entity.Ignore(e => e.ParsedDate);
        });

        modelBuilder.Entity<CompetitionDocument>(entity =>
        {
            entity.ToTable("competition_documents");

            entity.HasKey(document => new { document.CompetitionId, document.DocumentId });

            entity.Property(document => document.CompetitionId)
                .HasColumnName("competition_id")
                .IsRequired();

            entity.Property(document => document.DocumentId)
                .HasColumnName("document_id")
                .IsRequired();

            entity.Property(document => document.Revision)
                .HasColumnName("revision")
                .IsRequired();

            entity.Property(document => document.DocumentDataJson)
                .HasColumnName("document_data")
                .HasColumnType("jsonb")
                .IsRequired();

            entity.Property(document => document.LastSyncedAt)
                .HasColumnName("last_synced_at")
                .IsRequired();

            entity.HasOne<Competition>()
                .WithMany()
                .HasForeignKey(document => document.CompetitionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<CompetitionSyncState>(entity =>
        {
            entity.ToTable("competition_sync_state");

            entity.HasKey(state => state.CompetitionId);

            entity.Property(state => state.CompetitionId)
                .HasColumnName("competition_id")
                .IsRequired();

            entity.Property(state => state.LastSequence)
                .HasColumnName("last_sequence")
                .IsRequired();

            entity.Property(state => state.LastSyncedAt)
                .HasColumnName("last_synced_at")
                .IsRequired();

            entity.HasOne<Competition>()
                .WithMany()
                .HasForeignKey(state => state.CompetitionId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
