using MicroMES.ApiCore.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace MicroMES.ApiCore.Persistence;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Material> Materials { get; set; } = null!;

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
    }
}
