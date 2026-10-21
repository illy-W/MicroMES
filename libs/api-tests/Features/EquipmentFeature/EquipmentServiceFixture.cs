using FluentAssertions;
using MicroMES.ApiFeatures.EquipmentFeature.Models;
using MicroMES.ApiFeatures.EquipmentFeature.Services;
using MicroMES.ApiFeatures.ProcessSegmentFeature.Models;
using MicroMES.ApiFeatures.ProcessSegmentFeature.Services;
using MicroMES.ApiTests.Shared.Fixtures;

namespace MicroMES.ApiTests.Features.EquipmentFeature;

public class EquipmentServiceFixture : BaseFeatureFixture
{
    private static CreateEquipmentRequest New(string code, params int[] segments)
        => new() { Code = code, Name = " Troncatrice ", ProcessSegmentIds = segments.ToList() };

    private static UpdateEquipmentRequest Upd(string code, bool active, params int[] segments)
        => new() { Code = code, Name = "Troncatrice", IsActive = active, ProcessSegmentIds = segments.ToList() };

    private static UpdateProcessSegmentRequest SegUpd(string code, List<int>? equipment)
        => new() { Code = code, Name = "Fase", IsActive = true, EquipmentIds = equipment };

    [Fact]
    public async Task CreateAsync_LowerCaseCodeWithSegments_Success_NormalizedAndLinked()
    {
        var sal = AddSegment("SAL");

        var result = await Service<EquipmentService>().CreateAsync(New(" sal-1 ", sal));

        result.IsSuccessful.Should().BeTrue(string.Join(";", result.Errors));
        result.Data!.Code.Should().Be("SAL-1");
        result.Data.Name.Should().Be("Troncatrice");
        result.Data.IsActive.Should().BeTrue();
        result.Data.ProcessSegments.Should().ContainSingle().Which.Code.Should().Be("SAL");
    }

    [Fact]
    public async Task CreateAsync_NoSegments_Success_NoLinks()
    {
        var result = await Service<EquipmentService>().CreateAsync(new CreateEquipmentRequest { Code = "X", Name = "x" });

        result.IsSuccessful.Should().BeTrue();
        result.Data!.ProcessSegments.Should().BeEmpty();
    }

    [Theory]
    [InlineData("SAL-1")]
    [InlineData("sal-1")]
    public async Task CreateAsync_DuplicateCode_Failure_Conflict(string code)
    {
        await Service<EquipmentService>().CreateAsync(New("SAL-1"));

        var result = await Service<EquipmentService>().CreateAsync(New(code));

        result.Failure.Should().Be(EquipmentFailure.Conflict);
        Query(db => db.Equipment.Count()).Should().Be(1);
    }

    [Fact]
    public async Task CreateAsync_UnknownSegment_Failure_InvalidAndNothingSaved()
    {
        var result = await Service<EquipmentService>().CreateAsync(New("M1", 404));

        result.Failure.Should().Be(EquipmentFailure.Invalid);
        Query(db => db.Equipment.Count()).Should().Be(0);
    }

    [Fact]
    public async Task CreateAsync_InactiveSegment_Failure_Invalid()
    {
        var inactive = AddSegment("OLD", active: false);

        var result = await Service<EquipmentService>().CreateAsync(New("M1", inactive));

        result.Failure.Should().Be(EquipmentFailure.Invalid);
    }

    [Fact]
    public async Task UpdateAsync_ReplacesLinksAndCode_Success()
    {
        var sal = AddSegment("SAL");
        var ver = AddSegment("VER");
        var id = (await Service<EquipmentService>().CreateAsync(New("M1", sal))).Data!.Id;

        var result = await Service<EquipmentService>().UpdateAsync(id, Upd("m2", true, ver));

        result.IsSuccessful.Should().BeTrue(string.Join(";", result.Errors));
        result.Data!.Code.Should().Be("M2");
        result.Data.ProcessSegments.Select(s => s.Code).Should().Equal("VER");
        Query(db => db.EquipmentProcessSegments.Count()).Should().Be(1);
    }

    [Fact]
    public async Task UpdateAsync_EmptySet_Success_RemovesAllLinks()
    {
        var sal = AddSegment("SAL");
        var id = (await Service<EquipmentService>().CreateAsync(New("M1", sal))).Data!.Id;

        var result = await Service<EquipmentService>().UpdateAsync(id, Upd("M1", true));

        result.Data!.ProcessSegments.Should().BeEmpty();
    }

    [Fact]
    public async Task UpdateAsync_CodeOfAnotherMachine_Failure_Conflict()
    {
        await Service<EquipmentService>().CreateAsync(New("M1"));
        var id = (await Service<EquipmentService>().CreateAsync(New("M2"))).Data!.Id;

        var result = await Service<EquipmentService>().UpdateAsync(id, Upd("m1", true));

        result.Failure.Should().Be(EquipmentFailure.Conflict);
    }

    [Fact]
    public async Task UpdateAsync_UnknownId_Failure_NotFound()
        => (await Service<EquipmentService>().UpdateAsync(404, Upd("M", true))).Failure.Should().Be(EquipmentFailure.NotFound);

    [Fact]
    public async Task UpdateAsync_KeepsLinkToSegmentDeactivatedMeanwhile_Success_NewInactiveRejected()
    {
        var sal = AddSegment("SAL");
        var id = (await Service<EquipmentService>().CreateAsync(New("M1", sal))).Data!.Id;
        await Service<ProcessSegmentService>().DeactivateAsync(sal);
        var other = AddSegment("OLD", active: false);

        var kept = await Service<EquipmentService>().UpdateAsync(id, Upd("M1", true, sal));
        var added = await Service<EquipmentService>().UpdateAsync(id, Upd("M1", true, sal, other));

        kept.IsSuccessful.Should().BeTrue();
        kept.Data!.ProcessSegments.Should().ContainSingle().Which.IsActive.Should().BeFalse();
        added.Failure.Should().Be(EquipmentFailure.Invalid);
    }

    [Fact]
    public async Task DeactivateAsync_ActiveMachine_Success_SoftDeletesAndKeepsLinks()
    {
        var sal = AddSegment("SAL");
        var id = (await Service<EquipmentService>().CreateAsync(New("M1", sal))).Data!.Id;

        var result = await Service<EquipmentService>().DeactivateAsync(id);

        result.IsSuccessful.Should().BeTrue();
        Query(db => db.Equipment.Single(e => e.Id == id).IsActive).Should().BeFalse();
        Query(db => db.EquipmentProcessSegments.Count()).Should().Be(1);
        result.Data!.ProcessSegments.Should().HaveCount(1);
        (await Service<EquipmentService>().GetAllAsync(false, null, null)).Data.Should().BeEmpty();
        (await Service<EquipmentService>().GetAllAsync(true, null, null)).Data.Should().HaveCount(1);
    }

    [Fact]
    public async Task DeactivateAsync_UnknownId_Failure_NotFound()
        => (await Service<EquipmentService>().DeactivateAsync(404)).Failure.Should().Be(EquipmentFailure.NotFound);

    [Fact]
    public async Task GetAllAsync_SearchAndSegmentFilter_Success()
    {
        var sal = AddSegment("SAL");
        var ver = AddSegment("VER");
        await Service<EquipmentService>().CreateAsync(New("SAL-1", sal));
        await Service<EquipmentService>().CreateAsync(new CreateEquipmentRequest { Code = "VER-1", Name = "Cabina", ProcessSegmentIds = [ver] });
        await Service<EquipmentService>().CreateAsync(New("FREE"));

        (await Service<EquipmentService>().GetAllAsync(false, null, sal)).Data!.Select(e => e.Code).Should().Equal("SAL-1");
        (await Service<EquipmentService>().GetAllAsync(false, "cab", null)).Data!.Select(e => e.Code).Should().Equal("VER-1");
        (await Service<EquipmentService>().GetAllAsync(false, "ver-", null)).Data!.Select(e => e.Code).Should().Equal("VER-1");
        (await Service<EquipmentService>().GetAllAsync(false, null, null)).Data!.Select(e => e.Code).Should().Equal("FREE", "SAL-1", "VER-1");
    }

    // ---- from the process segment side -------------------------------------------------------------------

    [Fact]
    public async Task SegmentUpdateAsync_EquipmentIds_Success_ReplacesSet()
    {
        var seg = AddSegment("SAL");
        var m1 = (await Service<EquipmentService>().CreateAsync(New("M1", seg))).Data!.Id;
        var m2 = (await Service<EquipmentService>().CreateAsync(New("M2"))).Data!.Id;

        var result = await Service<ProcessSegmentService>().UpdateAsync(seg, SegUpd("SAL", [m2]));

        result.IsSuccessful.Should().BeTrue(string.Join(";", result.Errors));
        result.Data!.Equipment.Select(e => e.Code).Should().Equal("M2");
        (await Service<EquipmentService>().GetAsync(m1)).Data!.ProcessSegments.Should().BeEmpty();
        (await Service<EquipmentService>().GetAsync(m2)).Data!.ProcessSegments.Select(s => s.Code).Should().Equal("SAL");
    }

    [Fact]
    public async Task SegmentUpdateAsync_EquipmentIdsOmitted_Success_LinksUnchanged()
    {
        var seg = AddSegment("SAL");
        await Service<EquipmentService>().CreateAsync(New("M1", seg));

        var result = await Service<ProcessSegmentService>().UpdateAsync(seg, SegUpd("SAL", null));

        result.IsSuccessful.Should().BeTrue();
        result.Data!.Equipment.Select(e => e.Code).Should().Equal("M1");
    }

    [Fact]
    public async Task SegmentUpdateAsync_EmptyEquipmentIds_Success_RemovesLinks()
    {
        var seg = AddSegment("SAL");
        await Service<EquipmentService>().CreateAsync(New("M1", seg));

        var result = await Service<ProcessSegmentService>().UpdateAsync(seg, SegUpd("SAL", []));

        result.Data!.Equipment.Should().BeEmpty();
    }

    [Fact]
    public async Task SegmentCreateAsync_WithEquipment_Success_Linked()
    {
        var m = (await Service<EquipmentService>().CreateAsync(New("M1"))).Data!.Id;

        var result = await Service<ProcessSegmentService>().CreateAsync(
            new CreateProcessSegmentRequest { Code = "sal", Name = "Saldatura", EquipmentIds = [m] });

        result.IsSuccessful.Should().BeTrue(string.Join(";", result.Errors));
        result.Data!.Equipment.Should().ContainSingle().Which.Code.Should().Be("M1");
    }

    [Fact]
    public async Task SegmentCreateAsync_UnknownOrInactiveEquipment_Failure_InvalidAndNothingSaved()
    {
        var inactive = (await Service<EquipmentService>().CreateAsync(New("OLD"))).Data!.Id;
        await Service<EquipmentService>().DeactivateAsync(inactive);

        var unknown = await Service<ProcessSegmentService>().CreateAsync(
            new CreateProcessSegmentRequest { Code = "A", Name = "A", EquipmentIds = [404] });
        var off = await Service<ProcessSegmentService>().CreateAsync(
            new CreateProcessSegmentRequest { Code = "B", Name = "B", EquipmentIds = [inactive] });

        unknown.Failure.Should().Be(ProcessSegmentFailure.Invalid);
        off.Failure.Should().Be(ProcessSegmentFailure.Invalid);
        Query(db => db.ProcessSegments.Count()).Should().Be(0);
    }

    [Fact]
    public async Task SegmentUpdateAsync_KeepsAlreadyLinkedInactiveMachine_Success()
    {
        var seg = AddSegment("SAL");
        var m = (await Service<EquipmentService>().CreateAsync(New("M1", seg))).Data!.Id;
        await Service<EquipmentService>().DeactivateAsync(m);

        var result = await Service<ProcessSegmentService>().UpdateAsync(seg, SegUpd("SAL", [m]));

        result.IsSuccessful.Should().BeTrue();
        result.Data!.Equipment.Should().ContainSingle().Which.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task SegmentDeactivateAsync_Success_KeepsLinks()
    {
        var seg = AddSegment("SAL");
        await Service<EquipmentService>().CreateAsync(New("M1", seg));

        var result = await Service<ProcessSegmentService>().DeactivateAsync(seg);

        result.IsSuccessful.Should().BeTrue();
        result.Data!.Equipment.Should().HaveCount(1);
        Query(db => db.EquipmentProcessSegments.Count()).Should().Be(1);
    }
}
