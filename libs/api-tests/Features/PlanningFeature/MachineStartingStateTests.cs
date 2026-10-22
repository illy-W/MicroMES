using FluentAssertions;
using MicroMES.ApiCore.Persistence.Entities;
using MicroMES.ApiFeatures.PlanningFeature.Services;
using MicroMES.ApiFeatures.ProductionOrderFeature.Services;
using MicroMES.ApiTests.Shared.Fixtures;
using static MicroMES.ApiTests.Features.CalendarFeature.CalendarTestKit;

namespace MicroMES.ApiTests.Features.PlanningFeature;

public class MachineStartingStateTests
{
    private static readonly DateTime Now = Local(2026, 1, 5, 8);
    private readonly ProductionData _data = new(new FakeTimeProvider(new DateTimeOffset(Now)));
    private int _session, _line;

    private WorkOrder Wo(int id, WorkOrderStatus status, int? machine, DateTime? start, DateTime? end = null, int toolingId = 0, string? color = null,
        decimal setup = 20)
    {
        var wo = _data.WorkOrders[id] = new WorkOrder
        {
            Id = id, ProductionOrderId = 1, OperationNumber = id, ProcessSegmentId = 1, SetupMinutes = setup, PlannedQuantity = 1,
            Status = status, EndedAt = end
        };
        if (machine is { } m && start is { } s)
            _data.Sessions.Add(new WorkOrderResponse { Id = ++_session, WorkOrderId = id, StartedAt = s, EndedAt = end, EquipmentId = m });
        if (toolingId > 0)
            _data.ToolingLines.Add(new WorkOrderTooling { Id = ++_line, WorkOrderId = id, ToolingId = toolingId, ToolingCode = $"ST-{toolingId}", ToolingName = "d", ToolingSetupMinutes = 10 });
        if (color is not null)
            _data.SetupValueLines.Add(new WorkOrderSetupValue { Id = ++_line, WorkOrderId = id, SetupAttributeId = 1, AttributeCode = "COLORE", AttributeName = "Colore", AttributeChangeMinutes = 5, Value = color });
        return wo;
    }

    [Fact]
    public void NoHistory_IsUnknown()
    {
        Wo(1, WorkOrderStatus.Waiting, null, null);
        MachineStartingState.Of(_data, 1).Should().BeNull();
    }

    [Fact]
    public void LastEndedByRealEnd_WinsOverOlder()
    {
        Wo(1, WorkOrderStatus.Ended, 1, Now.AddDays(-3), Now.AddDays(-3).AddHours(2), toolingId: 1, color: "rosso");
        Wo(2, WorkOrderStatus.Ended, 1, Now.AddDays(-1), Now.AddDays(-1).AddHours(2), toolingId: 2, color: "blu");
        Wo(3, WorkOrderStatus.Ended, 2, Now.AddHours(-1), Now.AddHours(-1).AddMinutes(10), toolingId: 3);

        var s = MachineStartingState.Of(_data, 1)!;
        s.ToolingIds.Should().BeEquivalentTo([2]);
        s.Values.Should().Equal(new Dictionary<int, string> { [1] = "blu" });
    }

    [Fact]
    public void RunningWins_OverEnded_AndUsesTheSessionMachine()
    {
        Wo(1, WorkOrderStatus.Ended, 1, Now.AddDays(-1), Now.AddHours(-5), toolingId: 1);
        Wo(2, WorkOrderStatus.Running, 1, Now.AddHours(-1), toolingId: 2, color: "verde");
        MachineStartingState.Of(_data, 1)!.ToolingIds.Should().BeEquivalentTo([2]);
        MachineStartingState.Of(_data, 2).Should().BeNull();
    }

    [Fact]
    public void WorkOrderWithoutSnapshots_LeavesAKnownEmptyState()
    {
        Wo(1, WorkOrderStatus.Ended, 1, Now.AddDays(-1), Now.AddHours(-5));
        var s = MachineStartingState.Of(_data, 1)!;
        s.ToolingIds.Should().BeEmpty();
        s.Values.Should().BeEmpty();
    }

    [Fact]
    public void RequirementsOf_CarriesSnapshotsAndFullSetup()
    {
        var wo = Wo(1, WorkOrderStatus.Waiting, null, null, toolingId: 4, color: "blu", setup: 12);
        var r = MachineStartingState.RequirementsOf(_data, wo);
        r.FullSetupMinutes.Should().Be(12);
        r.Tooling.Should().ContainSingle().Which.Should().Match<MicroMES.ApiFeatures.PlanningFeature.Engine.JobTooling>(t => t.ToolingId == 4 && t.SetupMinutes == 10);
        r.SetupValues.Should().ContainSingle().Which.Value.Should().Be("blu");
    }
}
