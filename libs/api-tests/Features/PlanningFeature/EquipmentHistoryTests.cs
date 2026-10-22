using FluentAssertions;
using MicroMES.ApiFeatures.PlanningFeature.Services;
using static MicroMES.ApiTests.Features.CalendarFeature.CalendarTestKit;

namespace MicroMES.ApiTests.Features.PlanningFeature;

public class EquipmentHistoryTests
{
    private static readonly DateTime PeriodStart = Local(2026, 1, 1);
    private static readonly DateTime Now = Local(2026, 1, 31, 12);

    [Fact]
    public void Clipped_ClosedSessionInside_IsItsDuration()
        => EquipmentHistoryCalculator.ClippedMinutes(Local(2026, 1, 10, 8), Local(2026, 1, 10, 10), PeriodStart, Now).Should().Be(120);

    [Fact]
    public void Clipped_StartsBeforePeriod_IsClippedAtPeriodStart()
        => EquipmentHistoryCalculator.ClippedMinutes(Local(2025, 12, 31, 23), Local(2026, 1, 1, 1), PeriodStart, Now).Should().Be(60);

    [Fact]
    public void Clipped_Running_CountsUpToNow()
        => EquipmentHistoryCalculator.ClippedMinutes(Local(2026, 1, 31, 10), null, PeriodStart, Now).Should().Be(120);

    [Fact]
    public void Clipped_EntirelyBeforePeriod_IsZero()
        => EquipmentHistoryCalculator.ClippedMinutes(Local(2025, 12, 1), Local(2025, 12, 2), PeriodStart, Now).Should().Be(0);

    [Fact]
    public void Utilization_NoAvailableTime_IsNull()
        => EquipmentHistoryCalculator.Utilization(30, 0).Should().BeNull();

    [Fact]
    public void Utilization_CanExceed100()
        => EquipmentHistoryCalculator.Utilization(150, 100).Should().Be(150m);

    [Fact]
    public void ScrapPercent_Rules()
    {
        EquipmentHistoryCalculator.ScrapPercent(0, 0).Should().BeNull();
        EquipmentHistoryCalculator.ScrapPercent(90, 10).Should().Be(10m);
    }

    [Fact]
    public void ByReason_GroupsAndPutsUndetailedInNoReasonBucket()
    {
        var r = EquipmentHistoryCalculator.ByReason(
            [(1, "GR", "Graffio", 2m), (1, "GR", "Graffio", 1m), (null, null, null, 1m)], undetailed: 2m);
        r.Should().HaveCount(2);
        r[0].Code.Should().Be("GR");
        r[0].Quantity.Should().Be(3);
        r[1].Name.Should().Be("Senza motivo");
        r[1].Quantity.Should().Be(3);
    }
}
