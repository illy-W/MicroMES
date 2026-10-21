using FluentAssertions;
using MicroMES.ApiFeatures.PlanningFeature.Engine;
using Xunit;

namespace MicroMES.ApiTests.Features.PlanningFeature;

public class PlanningDateFormatTests
{
    private static readonly TimeZoneInfo Rome = TimeZoneInfo.FindSystemTimeZoneById("Europe/Rome");

    [Fact]
    public void Resolve_ShowsCompanyLocalTimeWithoutIsoOrUtcMarkers()
    {
        var until = new DateTime(2026, 10, 22, 6, 21, 0, DateTimeKind.Utc); // CEST: 08:21
        var message = PlanningDateFormat.Resolve($"Aspetta il lavoro OP-1/20, che finisce il {PlanningDateFormat.UntilToken}.", until, Rome);

        message.Should().Be("Aspetta il lavoro OP-1/20, che finisce il 22/10 alle 08:21.");
        message.Should().NotContain("Z").And.NotContain("2026-");
    }

    [Fact]
    public void Resolve_MachineBusyAndCalendarClosed_ReadNaturally()
    {
        var until = new DateTime(2026, 10, 22, 8, 14, 0, DateTimeKind.Utc);
        PlanningDateFormat.Resolve($"La macchina SAL-1 è occupata fino al {PlanningDateFormat.UntilToken}.", until, Rome)
            .Should().Be("La macchina SAL-1 è occupata fino al 22/10 alle 10:14.");
    }

    [Fact]
    public void Local_UsesFullAlertFormat()
        => PlanningDateFormat.Local(Rome, new DateTime(2026, 10, 22, 6, 21, 0, DateTimeKind.Utc)).Should().Be("22/10/2026 08:21");

    [Fact]
    public void Engine_EmitsTokenInsteadOfRawTimestamps()
    {
        // The pure engine never formats instants itself: reasons carry the token, the service resolves it.
        var source = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../../api-features/PlanningFeature/Engine/ForwardFiniteCapacityEngine.cs"));
        source.Should().NotContain(":u}");
    }
}
