using Microsoft.EntityFrameworkCore;
using LiftingCastTracker.Services.Models;

namespace LiftingCastTracker.Repositories.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
    {
    }

    public DbSet<Competition> Competitions => Set<Competition>();

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
        });
    }
}
