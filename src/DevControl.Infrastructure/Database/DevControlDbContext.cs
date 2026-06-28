using DevControl.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DevControl.Infrastructure.Database;

public sealed class DevControlDbContext(DbContextOptions<DevControlDbContext> options) : DbContext(options)
{
    public DbSet<SchemaVersion> SchemaVersions => Set<SchemaVersion>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SchemaVersion>(entity =>
        {
            entity.ToTable("schema_versions");
            entity.HasKey(schemaVersion => schemaVersion.Id);

            entity.Property(schemaVersion => schemaVersion.Id)
                .HasColumnName("id");

            entity.Property(schemaVersion => schemaVersion.Name)
                .HasColumnName("name")
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(schemaVersion => schemaVersion.CreatedAt)
                .HasColumnName("created_at")
                .IsRequired();
        });
    }
}

