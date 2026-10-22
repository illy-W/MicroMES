using System.Net;
using FluentAssertions;
using MicroMES.ApiFeatures.ToolingFeature.Models;
using MicroMES.ApiTests.Integration.Shared;

namespace MicroMES.ApiTests.Integration.Features;

public class ToolingApiTests(ApiFixture fixture) : IntegrationTest(fixture)
{
    [Fact]
    public async Task Tooling_CreateGetUpdateDeactivateReactivate_Success_FullCycle()
    {
        var m1 = await Api.CreateEquipmentAsync("M1");
        var created = await Api.PostAsync<ToolingDetailModel>("/api/tooling",
            new CreateToolingRequest { Code = " st-1 ", Name = "Stampo", Quantity = 2, SetupMinutes = 15, EquipmentIds = [m1.Id] });

        created.Status.Should().Be(HttpStatusCode.Created);
        created.Ok.Code.Should().Be("ST-1");
        created.Ok.Equipment.Should().ContainSingle().Which.Code.Should().Be("M1");
        created.Ok.RoutingSteps.Should().BeEmpty();
        created.Ok.WorkOrders.Should().BeEmpty();

        var id = created.Ok.Id;
        (await Api.GetAsync<ToolingDetailModel>($"/api/tooling/{id}")).Ok.Quantity.Should().Be(2);

        var updated = await Api.PutAsync<ToolingDetailModel>($"/api/tooling/{id}",
            new UpdateToolingRequest { Code = "ST-1", Name = "Stampo 2", IsActive = true, EquipmentIds = [] });
        updated.Ok.Name.Should().Be("Stampo 2");
        updated.Ok.Equipment.Should().BeEmpty();

        (await Api.DeleteAsync($"/api/tooling/{id}")).Status.Should().Be(HttpStatusCode.NoContent);
        (await Api.GetAsync<List<ToolingModel>>("/api/tooling?isActive=true")).Ok.Should().BeEmpty();
        (await Api.GetAsync<List<ToolingModel>>("/api/tooling?isActive=false&q=stampo")).Ok.Should().ContainSingle();
        (await Api.PostAsync<ToolingDetailModel>($"/api/tooling/{id}/reactivate")).Ok.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task Tooling_DuplicateCode_Failure_Conflict409WithSpecificMessage()
    {
        await Api.PostAsync<ToolingDetailModel>("/api/tooling", new CreateToolingRequest { Code = "ST-1", Name = "A" });

        var duplicate = await Api.PostAsync<ToolingDetailModel>("/api/tooling", new CreateToolingRequest { Code = "st-1", Name = "B" });

        duplicate.Status.Should().Be(HttpStatusCode.Conflict);
        duplicate.Raw.Should().Contain("attrezzatura").And.Contain("ST-1").And.NotContainEquivalentOf("riprova");
    }

    [Fact]
    public async Task Tooling_InvalidInput_Failure_Unprocessable422AndNotFound404()
    {
        (await Api.PostAsync<ToolingDetailModel>("/api/tooling", new CreateToolingRequest { Code = "A", Name = "A", Quantity = 0 }))
            .Status.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await Api.PostAsync<ToolingDetailModel>("/api/tooling", new CreateToolingRequest { Code = "A", Name = "A", SetupMinutes = -1 }))
            .Status.Should().Be(HttpStatusCode.UnprocessableEntity);
        var badMachine = await Api.PostAsync<ToolingDetailModel>("/api/tooling",
            new CreateToolingRequest { Code = "A", Name = "A", EquipmentIds = [404] });
        badMachine.Status.Should().Be(HttpStatusCode.UnprocessableEntity);
        badMachine.Raw.Should().Contain("404");
        (await Api.PostAsync<ToolingDetailModel>("/api/tooling", new { code = "", name = "" }))
            .Status.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await Api.GetAsync<ToolingDetailModel>("/api/tooling/404")).Status.Should().Be(HttpStatusCode.NotFound);
        (await Api.DeleteAsync("/api/tooling/404")).Status.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task SetupAttributes_Cycle_Success_WithConflictAndValidation()
    {
        var created = await Api.PostAsync<SetupAttributeModel>("/api/setup-attributes",
            new CreateSetupAttributeRequest { Code = " col ", Name = "Colore", ChangeMinutes = 20 });
        created.Status.Should().Be(HttpStatusCode.Created);
        created.Ok.Code.Should().Be("COL");
        var id = created.Ok.Id;

        var duplicate = await Api.PostAsync<SetupAttributeModel>("/api/setup-attributes",
            new CreateSetupAttributeRequest { Code = "COL", Name = "x" });
        duplicate.Status.Should().Be(HttpStatusCode.Conflict);
        duplicate.Raw.Should().Contain("attributo di setup").And.NotContainEquivalentOf("riprova");

        (await Api.PostAsync<SetupAttributeModel>("/api/setup-attributes",
            new CreateSetupAttributeRequest { Code = "N", Name = "x", ChangeMinutes = -5 })).Status
            .Should().Be(HttpStatusCode.UnprocessableEntity);

        (await Api.PutAsync<SetupAttributeModel>($"/api/setup-attributes/{id}",
            new UpdateSetupAttributeRequest { Code = "COL", Name = "Colore 2", ChangeMinutes = 30, IsActive = true })).Ok.ChangeMinutes.Should().Be(30);

        (await Api.DeleteAsync($"/api/setup-attributes/{id}")).Status.Should().Be(HttpStatusCode.NoContent);
        (await Api.GetAsync<List<SetupAttributeModel>>("/api/setup-attributes?isActive=true")).Ok.Should().BeEmpty();
        (await Api.GetAsync<List<SetupAttributeModel>>("/api/setup-attributes?q=col")).Ok.Should().ContainSingle();
        (await Api.PostAsync<SetupAttributeModel>($"/api/setup-attributes/{id}/reactivate")).Ok.IsActive.Should().BeTrue();
        (await Api.GetAsync<SetupAttributeModel>("/api/setup-attributes/404")).Status.Should().Be(HttpStatusCode.NotFound);
    }
}
