using System.Text.Json;
using FluentAssertions;
using MicroMES.ApiFeatures.CalendarFeature.Models;

namespace MicroMES.ApiTests.Features.CalendarFeature;

public class TimeFormatFixture
{
    [Theory]
    [InlineData("\"08:00:00\"", 8, 0)]
    [InlineData("\"08:00\"", 8, 0)]
    [InlineData("\"13:30\"", 13, 30)]
    public void Deserialize_TimeOfDay_Success_AcceptsWithAndWithoutSeconds(string json, int hour, int minute)
    {
        var slot = JsonSerializer.Deserialize<TimeSlotRequest>($"{{\"startTime\":{json},\"endTime\":\"23:00\"}}", new JsonSerializerOptions(JsonSerializerDefaults.Web));
        slot!.StartTime.Should().Be(new TimeOnly(hour, minute));
    }
}
