using FluentAssertions;
using MicroMES.ApiFeatures.EquipmentFeature.Models;
using MicroMES.ApiFeatures.EquipmentFeature.Services;
using MicroMES.ApiFeatures.ToolingFeature.Models;
using MicroMES.ApiFeatures.ToolingFeature.Services;
using MicroMES.ApiTests.Shared.Fixtures;

namespace MicroMES.ApiTests.Features.ToolingFeature;

public class ToolingServiceFixture : BaseFeatureFixture
{
    private async Task<int> NewMachineAsync(string code, bool active = true)
    {
        var id = (await Service<EquipmentService>().CreateAsync(new CreateEquipmentRequest { Code = code, Name = code })).Data!.Id;
        if (!active) await Service<EquipmentService>().DeactivateAsync(id);
        return id;
    }

    private static CreateToolingRequest New(string code, params int[] equipment)
        => new() { Code = code, Name = " Stampo ", EquipmentIds = equipment.ToList() };

    private static UpdateToolingRequest Upd(string code, bool active, params int[] equipment)
        => new() { Code = code, Name = "Stampo", IsActive = active, EquipmentIds = equipment.ToList() };

    [Fact]
    public async Task CreateAsync_LowerCaseCode_Success_NormalizedWithDefaults()
    {
        var result = await Service<ToolingService>().CreateAsync(New(" st-1 "));

        result.IsSuccessful.Should().BeTrue(string.Join(";", result.Errors));
        result.Data!.Code.Should().Be("ST-1");
        result.Data.Name.Should().Be("Stampo");
        result.Data.Quantity.Should().Be(1);
        result.Data.SetupMinutes.Should().Be(0);
        result.Data.Equipment.Should().BeEmpty();
        result.Data.RoutingSteps.Should().BeEmpty();
        result.Data.WorkOrders.Should().BeEmpty();
    }

    [Theory]
    [InlineData("ST-1")]
    [InlineData("st-1")]
    public async Task CreateAsync_DuplicateCode_Failure_ConflictWithCode(string code)
    {
        await Service<ToolingService>().CreateAsync(New("ST-1"));

        var result = await Service<ToolingService>().CreateAsync(New(code));

        result.Failure.Should().Be(ToolingFailure.Conflict);
        result.Errors.Should().Contain("Esiste già un'attrezzatura con codice ST-1.");
        Query(db => db.Tooling.Count()).Should().Be(1);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(-3, 5)]
    [InlineData(1, -1)]
    public async Task CreateAsync_InvalidNumbers_Failure_Invalid(int quantity, int minutes)
    {
        var request = New("ST-1");
        request.Quantity = quantity;
        request.SetupMinutes = minutes;

        var result = await Service<ToolingService>().CreateAsync(request);

        result.Failure.Should().Be(ToolingFailure.Invalid);
        Query(db => db.Tooling.Count()).Should().Be(0);
    }

    [Fact]
    public async Task CreateAsync_UnknownMachine_Failure_InvalidNamingIdAndNothingSaved()
    {
        var result = await Service<ToolingService>().CreateAsync(New("ST-1", 404));

        result.Failure.Should().Be(ToolingFailure.Invalid);
        result.Errors.Should().Contain(e => e.Contains("404"));
        Query(db => db.Tooling.Count()).Should().Be(0);
    }

    [Fact]
    public async Task CreateAsync_InactiveMachine_Failure_InvalidNamingCode()
    {
        var old = await NewMachineAsync("OLD", active: false);

        var result = await Service<ToolingService>().CreateAsync(New("ST-1", old));

        result.Failure.Should().Be(ToolingFailure.Invalid);
        result.Errors.Should().Contain(e => e.Contains("OLD"));
    }

    [Fact]
    public async Task UpdateAsync_ReplacesMachinesAndCode_Success()
    {
        var m1 = await NewMachineAsync("M1");
        var m2 = await NewMachineAsync("M2");
        var id = (await Service<ToolingService>().CreateAsync(New("ST-1", m1))).Data!.Id;

        var result = await Service<ToolingService>().UpdateAsync(id, Upd("st-2", true, m2));

        result.IsSuccessful.Should().BeTrue(string.Join(";", result.Errors));
        result.Data!.Code.Should().Be("ST-2");
        result.Data.Equipment.Select(e => e.Code).Should().Equal("M2");
        Query(db => db.ToolingEquipment.Count()).Should().Be(1);
    }

    [Fact]
    public async Task UpdateAsync_KeepsAlreadyLinkedMachineDeactivatedSince_Success()
    {
        var m1 = await NewMachineAsync("M1");
        var id = (await Service<ToolingService>().CreateAsync(New("ST-1", m1))).Data!.Id;
        await Service<EquipmentService>().DeactivateAsync(m1);

        var result = await Service<ToolingService>().UpdateAsync(id, Upd("ST-1", true, m1));

        result.IsSuccessful.Should().BeTrue(string.Join(";", result.Errors));
        result.Data!.Equipment.Should().ContainSingle().Which.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task UpdateAsync_DuplicateCodeOrUnknownId_Failure_ConflictOrNotFound()
    {
        await Service<ToolingService>().CreateAsync(New("A"));
        var b = (await Service<ToolingService>().CreateAsync(New("B"))).Data!.Id;

        (await Service<ToolingService>().UpdateAsync(b, Upd("a", true))).Failure.Should().Be(ToolingFailure.Conflict);
        (await Service<ToolingService>().UpdateAsync(404, Upd("Z", true))).Failure.Should().Be(ToolingFailure.NotFound);
    }

    [Fact]
    public async Task DeactivateAndReactivate_Success_IdempotentAndKeepsLinks()
    {
        var m1 = await NewMachineAsync("M1");
        var id = (await Service<ToolingService>().CreateAsync(New("ST-1", m1))).Data!.Id;

        (await Service<ToolingService>().DeactivateAsync(id)).Data!.IsActive.Should().BeFalse();
        (await Service<ToolingService>().DeactivateAsync(id)).IsSuccessful.Should().BeTrue();
        Query(db => db.ToolingEquipment.Count()).Should().Be(1);

        (await Service<ToolingService>().ReactivateAsync(id)).Data!.IsActive.Should().BeTrue();
        (await Service<ToolingService>().DeactivateAsync(404)).Failure.Should().Be(ToolingFailure.NotFound);
    }

    [Fact]
    public async Task GetAllAsync_SearchAndActiveFilter_Success()
    {
        await Service<ToolingService>().CreateAsync(New("ALFA"));
        var beta = (await Service<ToolingService>().CreateAsync(New("BETA"))).Data!.Id;
        await Service<ToolingService>().DeactivateAsync(beta);

        (await Service<ToolingService>().GetAllAsync(null, null)).Data!.Should().HaveCount(2);
        (await Service<ToolingService>().GetAllAsync(null, true)).Data!.Select(t => t.Code).Should().Equal("ALFA");
        (await Service<ToolingService>().GetAllAsync("bet", false)).Data!.Select(t => t.Code).Should().Equal("BETA");
        (await Service<ToolingService>().GetAllAsync("zzz", null)).Data!.Should().BeEmpty();
    }
}
