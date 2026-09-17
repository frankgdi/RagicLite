using Microsoft.EntityFrameworkCore;

namespace TableMint.Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options)
    : DbContext(options)
{
    internal DbSet<TableRow> Tables => Set<TableRow>();

    internal DbSet<FieldRow> Fields => Set<FieldRow>();

    internal DbSet<RecordRow> Records => Set<RecordRow>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TableRow>(builder =>
        {
            builder.ToTable("tables");
            builder.HasKey(table => table.Id);
            builder.Property(table => table.Name).HasMaxLength(200);
            builder.Property(table => table.Version).IsConcurrencyToken();
            builder.HasIndex(table => new { table.TenantId, table.Id });
        });

        modelBuilder.Entity<RecordRow>(builder =>
        {
            builder.ToTable("records");
            builder.HasKey(record => record.Id);
            builder.Property(record => record.Data).HasColumnType("jsonb");
            builder.Property(record => record.Version).IsConcurrencyToken();
            builder.HasIndex(record => new
            {
                record.TenantId,
                record.TableId,
                record.Id,
            });
            builder.HasOne<TableRow>()
                .WithMany()
                .HasForeignKey(record => record.TableId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<FieldRow>(builder =>
        {
            builder.ToTable("fields");
            builder.HasKey(field => field.Id);
            builder.Property(field => field.FieldKey).HasMaxLength(100);
            builder.Property(field => field.Label).HasMaxLength(200);
            builder.Property(field => field.SelectOptions).HasColumnType("jsonb");
            builder.HasIndex(field => new { field.TableId, field.FieldKey })
                .IsUnique();
            builder.HasOne<TableRow>()
                .WithMany()
                .HasForeignKey(field => field.TableId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
