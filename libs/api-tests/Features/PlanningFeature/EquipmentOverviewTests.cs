using FluentAssertions;
using MicroMES.ApiCore.Persistence.Entities;
using MicroMES.ApiFeatures.EquipmentFeature.Models;
using MicroMES.ApiFeatures.PlanningFeature.Models;
using MicroMES.ApiFeatures.PlanningFeature.Services;
using MicroMES.ApiFeatures.ProductionOrderFeature.Services;
using MicroMES.ApiTests.Shared.Fixtures;
using static MicroMES.ApiTests.Features.CalendarFeature.CalendarTestKit;

namespace MicroMES.ApiTests.Features.PlanningFeature;

public class EquipmentOverviewTests
{
    private static readonly DateTime Monday = Local(2026, 1, 5);

    private static (EquipmentNowState State, string? Reason) State(DateTime now, bool active = true, bool running = false)
        => EquipmentOverviewCalculator.StateOf(active, running, Source(), Calc(), now);

    [Fact]
    public void State_InsideShift_IsFree()
        => State(Local(2026, 1, 5, 9)).Should().Be((EquipmentNowState.Free, null));

    [Fact]
    public void State_Running_IsWorking_EvenWhenClosed()
        => State(Local(2026, 1, 5, 22), running: true).State.Should().Be(EquipmentNowState.Working);

    [Fact]
    public void State_Inactive_WinsOverEverything()
        => State(Local(2026, 1, 5, 9), active: false, running: true).Should().Be((EquipmentNowState.Inactive, null));

    [Fact]
    public void State_BetweenShifts_IsBreak()
        => State(Local(2026, 1, 5, 12, 30)).Should().Be((EquipmentNowState.ClosedNow, "pausa"));

    [Theory]
    [InlineData(2026, 1, 5, 6)]
    [InlineData(2026, 1, 5, 18)]
    [InlineData(2026, 1, 10, 10)] // Saturday
    public void State_OutsideShifts_IsOutOfShift(int y, int m, int d, int h)
        => State(Local(y, m, d, h)).Should().Be((EquipmentNowState.ClosedNow, "fuori turno"));

    [Fact]
    public void State_ClosureException_UsesItsReason()
    {
        var source = Source(null, Closed(D(2026, 1, 5), D(2026, 1, 5), reason: "Ferie"));
        EquipmentOverviewCalculator.StateOf(true, false, source, Calc(), Local(2026, 1, 5, 9)).Should().Be((EquipmentNowState.ClosedNow, "Ferie"));
    }

    private static TimelineLoadModel L(decimal capacity, decimal busy) => new()
        { EquipmentId = 1, PeriodStart = Monday, PeriodEnd = Monday.AddDays(1), CapacityMinutes = capacity, BusyMinutes = busy, LoadPercent = 0 };

    [Fact]
    public void Aggregate_SumsBusyOverCapacity()
        => EquipmentOverviewCalculator.Aggregate([L(480, 240), L(480, 480)]).Should().Be(75m);

    [Fact]
    public void Aggregate_NoCapacity_IsNull()
    {
        EquipmentOverviewCalculator.Aggregate([L(0, 0)]).Should().BeNull();
        EquipmentOverviewCalculator.Aggregate([]).Should().BeNull();
    }

    [Fact]
    public void MountedState_NoHistory_IsUnknown()
    {
        var data = new ProductionData(new FakeTimeProvider(new DateTimeOffset(Monday)));
        var s = EquipmentOverviewCalculator.MountedStateOf(data, 1);
        s.Known.Should().BeFalse();
        s.Tooling.Should().BeEmpty();
        s.FromWorkOrderCode.Should().BeNull();
    }

    [Fact]
    public void MountedState_FromLastExecuted_AndFlagsToolingAsMounted()
    {
        var data = new ProductionData(new FakeTimeProvider(new DateTimeOffset(Monday)));
        data.Orders[1] = new ProductionOrder { Id = 1, Code = "ORD-1" };
        data.WorkOrders[7] = new WorkOrder { Id = 7, ProductionOrderId = 1, OperationNumber = 10, Status = WorkOrderStatus.Running };
        data.Sessions.Add(new WorkOrderResponse { Id = 1, WorkOrderId = 7, StartedAt = Monday, EquipmentId = 1 });
        data.ToolingLines.Add(new WorkOrderTooling { Id = 1, WorkOrderId = 7, ToolingId = 5, ToolingCode = "ST-5", ToolingName = "Stampo" });
        data.SetupValueLines.Add(new WorkOrderSetupValue { Id = 1, WorkOrderId = 7, SetupAttributeId = 1, AttributeCode = "COLORE", AttributeName = "Colore", Value = "blu" });

        var s = EquipmentOverviewCalculator.MountedStateOf(data, 1);
        s.Known.Should().BeTrue();
        s.FromWorkOrderCode.Should().Be("ORD-1/10");
        s.Tooling.Should().ContainSingle(t => t.Id == 5 && t.Code == "ST-5");
        s.SetupValues.Should().ContainSingle(v => v.AttributeCode == "COLORE" && v.Value == "blu");

        var model = new EquipmentModel { Id = 1, Code = "M1", Name = "M1", IsActive = true, Capacity = 1, EfficiencyPercent = 100, ProcessSegments = [], CreatedAt = Monday, UpdatedAt = Monday };
        var tools = new[]
        {
            new Tooling { Id = 5, Code = "ST-5", Name = "Stampo", Quantity = 2, SetupMinutes = 10 },
            new Tooling { Id = 6, Code = "ST-6", Name = "Altro", IsActive = false },
            new Tooling { Id = 8, Code = "ST-8", Name = "Solo M2" }
        };
        var links = new[] { new ToolingEquipment { Id = 1, ToolingId = 5, EquipmentId = 1 }, new ToolingEquipment { Id = 2, ToolingId = 8, EquipmentId = 2 } };
        EquipmentToolingEnricher.Apply(model, tools, links, s.Tooling.Select(t => t.Id).ToHashSet());

        model.CompatibleTooling.Should().ContainSingle().Which.Should().Match<EquipmentToolingModel>(t => t.Id == 5 && t.IsMountedNow && t.Quantity == 2 && t.SetupMinutes == 10);
        model.UniversalTooling.Should().ContainSingle().Which.Should().Match<EquipmentToolingModel>(t => t.Id == 6 && !t.IsActive && !t.IsMountedNow);
    }
}
