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
    public DbSet<ProductionOrder> ProductionOrders { get; set; } = null!;
    public DbSet<WorkOrder> WorkOrders { get; set; } = null!;
    public DbSet<WorkOrderDependency> WorkOrderDependencies { get; set; } = null!;
    public DbSet<WorkOrderMaterial> WorkOrderMaterials { get; set; } = null!;
    public DbSet<WorkOrderResponse> WorkOrderResponses { get; set; } = null!;
    public DbSet<ScrapReason> ScrapReasons { get; set; } = null!;
    public DbSet<ScrapReasonProcessSegment> ScrapReasonProcessSegments { get; set; } = null!;
    public DbSet<Equipment> Equipment { get; set; } = null!;
    public DbSet<Tooling> Tooling { get; set; } = null!;
    public DbSet<ToolingEquipment> ToolingEquipment { get; set; } = null!;
    public DbSet<SetupAttribute> SetupAttributes { get; set; } = null!;
    public DbSet<RoutingStepTooling> RoutingStepTooling { get; set; } = null!;
    public DbSet<RoutingStepSetupValue> RoutingStepSetupValues { get; set; } = null!;
    public DbSet<WorkOrderTooling> WorkOrderTooling { get; set; } = null!;
    public DbSet<WorkOrderSetupValue> WorkOrderSetupValues { get; set; } = null!;
    public DbSet<EquipmentProcessSegment> EquipmentProcessSegments { get; set; } = null!;
    public DbSet<WorkOrderResponseScrap> WorkOrderResponseScraps { get; set; } = null!;
    public DbSet<WorkOrderResponseCorrection> WorkOrderResponseCorrections { get; set; } = null!;
    public DbSet<WorkCalendar> WorkCalendars { get; set; } = null!;
    public DbSet<WorkCalendarSlot> WorkCalendarSlots { get; set; } = null!;
    public DbSet<CalendarException> CalendarExceptions { get; set; } = null!;
    public DbSet<CalendarExceptionSlot> CalendarExceptionSlots { get; set; } = null!;
    public DbSet<EquipmentException> EquipmentExceptions { get; set; } = null!;
    public DbSet<EquipmentExceptionSlot> EquipmentExceptionSlots { get; set; } = null!;
    public DbSet<SystemSetting> SystemSettings { get; set; } = null!;

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
            entity.HasOne<RoutingStep>().WithMany().HasForeignKey(x => x.ConsumedInStepId).OnDelete(DeleteBehavior.SetNull);
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

        modelBuilder.Entity<ProductionOrder>(entity =>
        {
            entity.ToTable("ProductionOrders");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Code).IsRequired().HasMaxLength(50);
            entity.Property(x => x.Quantity).HasPrecision(18, 4);
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(50);
            entity.Property(x => x.Priority).HasDefaultValue(0);
            entity.Property(x => x.Notes).HasMaxLength(1000);
            entity.HasIndex(x => x.Code).IsUnique();
            entity.HasOne<Material>().WithMany().HasForeignKey(x => x.MaterialId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ProductionOrder>().WithMany().HasForeignKey(x => x.ParentOrderId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<WorkOrder>(entity =>
        {
            entity.ToTable("WorkOrders");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.ProcessSegmentCode).IsRequired().HasMaxLength(50);
            entity.Property(x => x.ProcessSegmentName).IsRequired().HasMaxLength(200);
            entity.Property(x => x.SetupMinutes).HasPrecision(10, 2);
            entity.Property(x => x.RunMinutesPerUnit).HasPrecision(10, 4);
            entity.Property(x => x.PlannedQuantity).HasPrecision(18, 4);
            entity.Property(x => x.Notes).HasMaxLength(1000);
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(50);
            entity.Property(x => x.IsPlanLocked).HasDefaultValue(false);
            entity.HasIndex(x => new { x.ProductionOrderId, x.OperationNumber }).IsUnique();
            entity.HasOne<ProductionOrder>().WithMany().HasForeignKey(x => x.ProductionOrderId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ProcessSegment>().WithMany().HasForeignKey(x => x.ProcessSegmentId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Equipment>().WithMany().HasForeignKey(x => x.AssignedEquipmentId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<WorkOrderDependency>(entity =>
        {
            entity.ToTable("WorkOrderDependencies");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.PredecessorWorkOrderId, x.SuccessorWorkOrderId }).IsUnique();
            entity.HasOne<WorkOrder>().WithMany().HasForeignKey(x => x.PredecessorWorkOrderId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<WorkOrder>().WithMany().HasForeignKey(x => x.SuccessorWorkOrderId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<WorkOrderMaterial>(entity =>
        {
            entity.ToTable("WorkOrderMaterials");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.RequiredQuantity).HasPrecision(18, 4);
            entity.HasOne<WorkOrder>().WithMany().HasForeignKey(x => x.WorkOrderId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Material>().WithMany().HasForeignKey(x => x.MaterialId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ProductionOrder>().WithMany().HasForeignKey(x => x.ChildProductionOrderId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<WorkOrderResponse>(entity =>
        {
            entity.ToTable("WorkOrderResponses");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.GoodQuantity).HasPrecision(18, 4);
            entity.Property(x => x.ScrapQuantity).HasPrecision(18, 4);
            entity.Property(x => x.Notes).HasMaxLength(1000);
            entity.Property(x => x.EquipmentCode).HasMaxLength(50);
            entity.Property(x => x.EquipmentName).HasMaxLength(200);
            entity.HasOne<WorkOrder>().WithMany().HasForeignKey(x => x.WorkOrderId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Equipment>().WithMany().HasForeignKey(x => x.EquipmentId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<ScrapReason>(entity =>
        {
            entity.ToTable("ScrapReasons");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Code).IsRequired().HasMaxLength(50);
            entity.Property(x => x.Name).IsRequired().HasMaxLength(200);
            entity.Property(x => x.Description).HasMaxLength(1000);
            entity.Property(x => x.IsActive).HasDefaultValue(true);
            entity.HasIndex(x => x.Code).IsUnique();
        });

        modelBuilder.Entity<ScrapReasonProcessSegment>(entity =>
        {
            entity.ToTable("ScrapReasonProcessSegments");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.ScrapReasonId, x.ProcessSegmentId }).IsUnique();
            entity.HasOne<ScrapReason>().WithMany().HasForeignKey(x => x.ScrapReasonId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<ProcessSegment>().WithMany().HasForeignKey(x => x.ProcessSegmentId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Equipment>(entity =>
        {
            entity.ToTable("Equipment");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Code).IsRequired().HasMaxLength(50);
            entity.Property(x => x.Name).IsRequired().HasMaxLength(200);
            entity.Property(x => x.Description).HasMaxLength(1000);
            entity.Property(x => x.IsActive).HasDefaultValue(true);
            entity.Property(x => x.Capacity).HasDefaultValue(1);
            entity.Property(x => x.EfficiencyPercent).HasPrecision(7, 2).HasDefaultValue(100m);
            entity.HasIndex(x => x.Code).IsUnique();
            entity.HasOne<WorkCalendar>().WithMany().HasForeignKey(x => x.WorkCalendarId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Tooling>(entity =>
        {
            entity.ToTable("Tooling", t =>
            {
                t.HasCheckConstraint("CK_Tooling_Quantity", "\"Quantity\" >= 1");
                t.HasCheckConstraint("CK_Tooling_SetupMinutes", "\"SetupMinutes\" >= 0");
            });
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Code).IsRequired().HasMaxLength(50);
            entity.Property(x => x.Name).IsRequired().HasMaxLength(200);
            entity.Property(x => x.Description).HasMaxLength(1000);
            entity.Property(x => x.IsActive).HasDefaultValue(true);
            entity.Property(x => x.Quantity).HasDefaultValue(1);
            entity.Property(x => x.SetupMinutes).HasDefaultValue(0);
            entity.HasIndex(x => x.Code).IsUnique();
        });

        modelBuilder.Entity<ToolingEquipment>(entity =>
        {
            entity.ToTable("ToolingEquipment");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.ToolingId, x.EquipmentId }).IsUnique();
            entity.HasOne<Tooling>().WithMany().HasForeignKey(x => x.ToolingId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<Equipment>().WithMany().HasForeignKey(x => x.EquipmentId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<SetupAttribute>(entity =>
        {
            entity.ToTable("SetupAttributes", t => t.HasCheckConstraint("CK_SetupAttributes_ChangeMinutes", "\"ChangeMinutes\" >= 0"));
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Code).IsRequired().HasMaxLength(50);
            entity.Property(x => x.Name).IsRequired().HasMaxLength(200);
            entity.Property(x => x.IsActive).HasDefaultValue(true);
            entity.Property(x => x.ChangeMinutes).HasDefaultValue(0);
            entity.HasIndex(x => x.Code).IsUnique();
        });

        modelBuilder.Entity<RoutingStepTooling>(entity =>
        {
            entity.ToTable("RoutingStepTooling");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.RoutingStepId, x.ToolingId }).IsUnique();
            entity.HasIndex(x => x.ToolingId);
            entity.HasOne<RoutingStep>().WithMany().HasForeignKey(x => x.RoutingStepId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<Tooling>().WithMany().HasForeignKey(x => x.ToolingId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<RoutingStepSetupValue>(entity =>
        {
            entity.ToTable("RoutingStepSetupValues");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Value).IsRequired().HasMaxLength(200);
            entity.HasIndex(x => new { x.RoutingStepId, x.SetupAttributeId }).IsUnique();
            entity.HasIndex(x => x.SetupAttributeId);
            entity.HasOne<RoutingStep>().WithMany().HasForeignKey(x => x.RoutingStepId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<SetupAttribute>().WithMany().HasForeignKey(x => x.SetupAttributeId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<WorkOrderTooling>(entity =>
        {
            entity.ToTable("WorkOrderTooling");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.ToolingCode).IsRequired().HasMaxLength(50);
            entity.Property(x => x.ToolingName).IsRequired().HasMaxLength(200);
            entity.HasIndex(x => new { x.WorkOrderId, x.ToolingId }).IsUnique();
            entity.HasIndex(x => x.ToolingId);
            entity.HasOne<WorkOrder>().WithMany().HasForeignKey(x => x.WorkOrderId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Tooling>().WithMany().HasForeignKey(x => x.ToolingId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<WorkOrderSetupValue>(entity =>
        {
            entity.ToTable("WorkOrderSetupValues");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.AttributeCode).IsRequired().HasMaxLength(50);
            entity.Property(x => x.AttributeName).IsRequired().HasMaxLength(200);
            entity.Property(x => x.Value).IsRequired().HasMaxLength(200);
            entity.HasIndex(x => new { x.WorkOrderId, x.SetupAttributeId }).IsUnique();
            entity.HasOne<WorkOrder>().WithMany().HasForeignKey(x => x.WorkOrderId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<SetupAttribute>().WithMany().HasForeignKey(x => x.SetupAttributeId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<EquipmentProcessSegment>(entity =>
        {
            entity.ToTable("EquipmentProcessSegments");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.IsDefault).HasDefaultValue(false);
            entity.HasIndex(x => new { x.EquipmentId, x.ProcessSegmentId }).IsUnique();
            // At most one default machine per segment.
            entity.HasIndex(x => x.ProcessSegmentId).IsUnique()
                .HasDatabaseName("IX_EquipmentProcessSegments_ProcessSegmentId_Default")
                .HasFilter("\"IsDefault\" = TRUE");
            entity.HasOne<Equipment>().WithMany().HasForeignKey(x => x.EquipmentId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<ProcessSegment>().WithMany().HasForeignKey(x => x.ProcessSegmentId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<WorkOrderResponseScrap>(entity =>
        {
            entity.ToTable("WorkOrderResponseScraps");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.ReasonCode).HasMaxLength(50);
            entity.Property(x => x.ReasonName).HasMaxLength(200);
            entity.Property(x => x.Quantity).HasPrecision(18, 4);
            entity.HasIndex(x => x.WorkOrderResponseId);
            entity.HasOne<WorkOrderResponse>().WithMany().HasForeignKey(x => x.WorkOrderResponseId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ScrapReason>().WithMany().HasForeignKey(x => x.ScrapReasonId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<WorkOrderResponseCorrection>(entity =>
        {
            entity.ToTable("WorkOrderResponseCorrections");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Notes).IsRequired().HasMaxLength(1000);
            entity.Property(x => x.PreviousGoodQuantity).HasPrecision(18, 4);
            entity.Property(x => x.PreviousScrapQuantity).HasPrecision(18, 4);
            entity.Property(x => x.PreviousScrapDetail).HasMaxLength(1000);
            entity.Property(x => x.NewGoodQuantity).HasPrecision(18, 4);
            entity.Property(x => x.NewScrapQuantity).HasPrecision(18, 4);
            entity.Property(x => x.NewScrapDetail).HasMaxLength(1000);
            entity.Property(x => x.PreviousEquipmentCode).HasMaxLength(50);
            entity.Property(x => x.PreviousEquipmentName).HasMaxLength(200);
            entity.Property(x => x.NewEquipmentCode).HasMaxLength(50);
            entity.Property(x => x.NewEquipmentName).HasMaxLength(200);
            entity.HasIndex(x => x.WorkOrderResponseId);
            entity.HasOne<WorkOrderResponse>().WithMany().HasForeignKey(x => x.WorkOrderResponseId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<WorkCalendar>(entity =>
        {
            entity.ToTable("WorkCalendars");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Code).IsRequired().HasMaxLength(50);
            entity.Property(x => x.Name).IsRequired().HasMaxLength(200);
            entity.Property(x => x.Description).HasMaxLength(1000);
            entity.Property(x => x.IsActive).HasDefaultValue(true);
            entity.Property(x => x.IsDefault).HasDefaultValue(false);
            entity.HasIndex(x => x.Code).IsUnique();
            // Exactly one default calendar (the application keeps it always present and active).
            entity.HasIndex(x => x.IsDefault).IsUnique()
                .HasDatabaseName("IX_WorkCalendars_IsDefault")
                .HasFilter("\"IsDefault\" = TRUE");
        });

        modelBuilder.Entity<WorkCalendarSlot>(entity =>
        {
            entity.ToTable("WorkCalendarSlots");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.DayOfWeek).HasConversion<string>().HasMaxLength(20);
            entity.HasIndex(x => x.WorkCalendarId);
            entity.HasOne<WorkCalendar>().WithMany().HasForeignKey(x => x.WorkCalendarId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<CalendarException>(entity =>
        {
            entity.ToTable("CalendarExceptions");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Kind).HasConversion<string>().HasMaxLength(20);
            entity.Property(x => x.Reason).IsRequired().HasMaxLength(200);
            entity.HasIndex(x => x.WorkCalendarId);
            entity.HasOne<WorkCalendar>().WithMany().HasForeignKey(x => x.WorkCalendarId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<CalendarExceptionSlot>(entity =>
        {
            entity.ToTable("CalendarExceptionSlots");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.CalendarExceptionId);
            entity.HasOne<CalendarException>().WithMany().HasForeignKey(x => x.CalendarExceptionId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<EquipmentException>(entity =>
        {
            entity.ToTable("EquipmentExceptions");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Kind).HasConversion<string>().HasMaxLength(20);
            entity.Property(x => x.Reason).IsRequired().HasMaxLength(200);
            entity.HasIndex(x => x.EquipmentId);
            entity.HasOne<Equipment>().WithMany().HasForeignKey(x => x.EquipmentId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<EquipmentExceptionSlot>(entity =>
        {
            entity.ToTable("EquipmentExceptionSlots");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.EquipmentExceptionId);
            entity.HasOne<EquipmentException>().WithMany().HasForeignKey(x => x.EquipmentExceptionId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SystemSetting>(entity =>
        {
            entity.ToTable("SystemSettings");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Key).IsRequired().HasMaxLength(100);
            entity.Property(x => x.Value).IsRequired().HasMaxLength(500);
            entity.HasIndex(x => x.Key).IsUnique();
        });
    }
}
