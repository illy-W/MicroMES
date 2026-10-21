using System.Net;
using FluentAssertions;
using MicroMES.ApiFeatures.EquipmentFeature.Models;
using MicroMES.ApiFeatures.ProcessSegmentFeature.Models;
using MicroMES.ApiTests.Integration.Shared;

namespace MicroMES.ApiTests.Integration.Features;

public class EquipmentApiTests(ApiFixture fixture) : IntegrationTest(fixture)
{
    [Fact]
    public async Task Equipment_CreateGetUpdateDeactivate_Success_FullCycle()
    {
        var sal = await Api.CreateSegmentAsync("SAL");
        var created = await Api.PostAsync<EquipmentModel>("/api/equipment",
            new CreateEquipmentRequest { Code = " sal-1 ", Name = "Saldatrice", ProcessSegmentIds = [sal.Id] });

        created.Status.Should().Be(HttpStatusCode.Created);
        created.Ok.Code.Should().Be("SAL-1");
        created.Ok.ProcessSegments.Should().ContainSingle().Which.Code.Should().Be("SAL");

        var id = created.Ok.Id;
        (await Api.GetAsync<EquipmentModel>($"/api/equipment/{id}")).Ok.Name.Should().Be("Saldatrice");

        var updated = await Api.PutAsync<EquipmentModel>($"/api/equipment/{id}",
            new UpdateEquipmentRequest { Code = "SAL-1", Name = "Saldatrice 2", IsActive = true, ProcessSegmentIds = [] });
        updated.Ok.Name.Should().Be("Saldatrice 2");
        updated.Ok.ProcessSegments.Should().BeEmpty();

        (await Api.DeleteAsync($"/api/equipment/{id}")).Status.Should().Be(HttpStatusCode.NoContent);
        (await Api.GetAsync<List<EquipmentModel>>("/api/equipment")).Ok.Should().BeEmpty();
        (await Api.GetAsync<List<EquipmentModel>>("/api/equipment?includeInactive=true")).Ok.Should().ContainSingle(e => !e.IsActive);
    }

    [Fact]
    public async Task Equipment_DuplicateCode_Failure_Conflict409WithSpecificMessage()
    {
        await Api.CreateEquipmentAsync("M1");

        var duplicate = await Api.CreateEquipmentRawAsync("m1");

        duplicate.Status.Should().Be(HttpStatusCode.Conflict);
        duplicate.Raw.Should().Contain("macchina o postazione").And.NotContainEquivalentOf("riprova");
    }

    [Fact]
    public async Task Equipment_InvalidBodyOrUnknownSegment_Failure_Unprocessable422()
    {
        (await Api.CreateEquipmentRawAsync("M1", 404)).Status.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await Api.PostAsync<EquipmentModel>("/api/equipment", new { code = "", name = "" })).Status
            .Should().Be(HttpStatusCode.UnprocessableEntity);
        (await Api.GetAsync<EquipmentModel>("/api/equipment/404")).Status.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Equipment_LinkedFromBothSides_Success_SameData()
    {
        var sal = await Api.CreateSegmentAsync("SAL");
        var ver = await Api.CreateSegmentAsync("VER");
        var m1 = await Api.CreateEquipmentAsync("M1", sal.Id);
        var m2 = await Api.CreateEquipmentAsync("M2");

        // From the segment side.
        var put = await Api.PutAsync<ProcessSegmentModel>($"/api/process-segments/{sal.Id}",
            new UpdateProcessSegmentRequest { Code = "SAL", Name = "Saldatura", IsActive = true, EquipmentIds = [m1.Id, m2.Id] });
        put.Ok.Equipment.Select(e => e.Code).Should().Equal("M1", "M2");
        (await Api.GetAsync<EquipmentModel>($"/api/equipment/{m2.Id}")).Ok.ProcessSegments.Select(s => s.Code).Should().Equal("SAL");

        // From the machine side (one machine on two segments).
        await Api.PutAsync<EquipmentModel>($"/api/equipment/{m2.Id}",
            new UpdateEquipmentRequest { Code = "M2", Name = "x", IsActive = true, ProcessSegmentIds = [sal.Id, ver.Id] });
        (await Api.GetAsync<ProcessSegmentModel>($"/api/process-segments/{ver.Id}")).Ok.Equipment.Select(e => e.Code).Should().Equal("M2");

        // Filter by segment and by search.
        (await Api.GetAsync<List<EquipmentModel>>($"/api/equipment?processSegmentId={ver.Id}")).Ok.Select(e => e.Code).Should().Equal("M2");
        (await Api.GetAsync<List<EquipmentModel>>("/api/equipment?search=m1")).Ok.Select(e => e.Code).Should().Equal("M1");

        // The segment list carries the machines too.
        (await Api.GetAsync<List<ProcessSegmentModel>>("/api/process-segments")).Ok
            .Single(s => s.Code == "SAL").Equipment.Should().HaveCount(2);
    }

    [Fact]
    public async Task ProcessSegment_UpdateWithoutEquipmentIds_Success_LinksUnchanged()
    {
        var sal = await Api.CreateSegmentAsync("SAL");
        await Api.CreateEquipmentAsync("M1", sal.Id);

        // Old clients send no equipmentIds at all.
        var put = await Api.PutAsync<ProcessSegmentModel>($"/api/process-segments/{sal.Id}",
            new { code = "SAL", name = "Saldatura 2", setupMinutes = 0, runMinutesPerUnit = 0, isActive = true });

        put.Ok.Name.Should().Be("Saldatura 2");
        put.Ok.Equipment.Should().ContainSingle().Which.Code.Should().Be("M1");
    }

    [Fact]
    public async Task ProcessSegment_UnknownEquipment_Failure_Unprocessable422()
    {
        var sal = await Api.CreateSegmentAsync("SAL");

        var put = await Api.PutAsync<ProcessSegmentModel>($"/api/process-segments/{sal.Id}",
            new UpdateProcessSegmentRequest { Code = "SAL", Name = "x", IsActive = true, EquipmentIds = [404] });

        put.Status.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Equipment_Deactivate_Success_LinksKeptAndNotOfferedForNewLinks()
    {
        var sal = await Api.CreateSegmentAsync("SAL");
        var m = await Api.CreateEquipmentAsync("M1", sal.Id);
        await Api.DeleteAsync($"/api/equipment/{m.Id}");

        var segment = (await Api.GetAsync<ProcessSegmentModel>($"/api/process-segments/{sal.Id}")).Ok;
        segment.Equipment.Should().ContainSingle(e => !e.IsActive, "the link survives the deactivation");

        var other = await Api.CreateSegmentAsync("VER");
        var link = await Api.PutAsync<ProcessSegmentModel>($"/api/process-segments/{other.Id}",
            new UpdateProcessSegmentRequest { Code = "VER", Name = "x", IsActive = true, EquipmentIds = [m.Id] });
        link.Status.Should().Be(HttpStatusCode.UnprocessableEntity);
    }
}
