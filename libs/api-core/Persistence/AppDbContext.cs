using MicroMES.ApiCore.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace MicroMES.ApiCore.Persistence;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Material> Materials { get; set; } = null!;
    public DbSet<BomItem> BomItems { get; set; } = null!;
    public DbSet<ProcessSegment> ProcessSegments { get; set; } = null!;
    public DbSet<RoutingStep> RoutingSteps { get; set; } = null!;
    public DbSet<RoutingStepDependency> RoutingStepDependencies { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Material>(entity =>
        {
            entity.ToTable("Materials");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Code).IsRequired().HasMaxLength(50);
            entity.Property(x => x.Name).IsRequired().HasMaxLength(200);
            entity.Property(x => x.Description).HasMaxLength(1000);
            entity.Property(x => x.Type).HasConversion<string>().HasMaxLength(50);
            entity.Property(x => x.UnitOfMeasure).HasConversion<string>().HasMaxLength(50);
            entity.Property(x => x.Vendor).HasMaxLength(200);
            entity.Property(x => x.VendorMaterialId).HasMaxLength(100);
            entity.Property(x => x.IsActive).HasDefaultValue(true);
            entity.HasIndex(x => x.Code).IsUnique();
        });

        modelBuilder.Entity<BomItem>(entity =>
        {
            entity.ToTable("BomItems");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Quantity).HasPrecision(18, 4);
            entity.HasIndex(x => new { x.ParentMaterialId, x.ComponentMaterialId }).IsUnique();
            entity.HasOne<Material>().WithMany().HasForeignKey(x => x.ParentMaterialId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Material>().WithMany().HasForeignKey(x => x.ComponentMaterialId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ProcessSegment>(entity =>
        {
            entity.ToTable("ProcessSegments");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Code).IsRequired().HasMaxLength(50);
            entity.Property(x => x.Name).IsRequired().HasMaxLength(200);
            entity.Property(x => x.Description).HasMaxLength(1000);
            entity.Property(x => x.SetupMinutes).HasPrecision(10, 2).HasDefaultValue(0m);
            entity.Property(x => x.RunMinutesPerUnit).HasPrecision(10, 4).HasDefaultValue(0m);
            entity.Property(x => x.IsActive).HasDefaultValue(true);
            entity.HasIndex(x => x.Code).IsUnique();
        });

        modelBuilder.Entity<RoutingStep>(entity =>
        {
            entity.ToTable("RoutingSteps");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.SetupMinutes).HasPrecision(10, 2);
            entity.Property(x => x.RunMinutesPerUnit).HasPrecision(10, 4);
            entity.Property(x => x.Notes).HasMaxLength(1000);
            entity.HasIndex(x => new { x.MaterialId, x.OperationNumber }).IsUnique();
            entity.HasOne<Material>().WithMany().HasForeignKey(x => x.MaterialId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ProcessSegment>().WithMany().HasForeignKey(x => x.ProcessSegmentId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<RoutingStepDependency>(entity =>
        {
            entity.ToTable("RoutingStepDependencies");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.PredecessorStepId, x.SuccessorStepId }).IsUnique();
            entity.HasOne<RoutingStep>().WithMany().HasForeignKey(x => x.PredecessorStepId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<RoutingStep>().WithMany().HasForeignKey(x => x.SuccessorStepId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
