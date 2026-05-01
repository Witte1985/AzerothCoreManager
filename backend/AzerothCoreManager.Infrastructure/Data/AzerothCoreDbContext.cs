using Microsoft.EntityFrameworkCore;
using AzerothCoreManager.Core.Contracts;
using AzerothCoreManager.Infrastructure.Data.Entities;

namespace AzerothCoreManager.Infrastructure.Data;

/// <summary>
/// Database context for AzerothCore Manager
/// </summary>
public class AzerothCoreDbContext : DbContext
{
    public AzerothCoreDbContext(DbContextOptions<AzerothCoreDbContext> options)
        : base(options)
    {
    }

    public DbSet<ManagedStackEntity> ManagedStacks => Set<ManagedStackEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<ManagedStackEntity>(entity =>
        {
            entity.ToTable("ManagedStacks");
            entity.HasKey(stack => stack.Id);
            entity.HasIndex(stack => stack.NormalizedStackName).IsUnique();

            entity.Property(stack => stack.Id).HasMaxLength(64);
            entity.Property(stack => stack.StackName).HasMaxLength(50).IsRequired();
            entity.Property(stack => stack.NormalizedStackName).HasMaxLength(50).IsRequired();
            entity.Property(stack => stack.ServerType).HasConversion<string>().IsRequired();
            entity.Property(stack => stack.Status).HasConversion<string>().IsRequired();
            entity.Property(stack => stack.ModuleIdsJson).IsRequired();
            entity.Property(stack => stack.DatabaseRootPassword).HasMaxLength(256).IsRequired();
            entity.Property(stack => stack.RealmName).HasMaxLength(50).IsRequired();
            entity.Property(stack => stack.CustomEnvVarsJson).IsRequired();
        });
    }
}
