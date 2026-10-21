using FluentAssertions;
using MicroMES.ApiFeatures.EquipmentFeature.Models;
using MicroMES.ApiFeatures.EquipmentFeature.Services;
using MicroMES.ApiFeatures.ProcessSegmentFeature.Models;
using MicroMES.ApiFeatures.ProcessSegmentFeature.Services;
using MicroMES.ApiTests.Shared.Fixtures;

namespace MicroMES.ApiTests.Features.EquipmentAssignment;

/// <summary>Default machine of a process segment: request semantics, flag survival, validation.</summary>
public class DefaultEquipmentFixture : BaseFeatureFixture
{
    private async Task<int> Machine(string code, params int[] segments)
        => (await Service<EquipmentService>().CreateAsync(new CreateEquipmentRequest { Code = code, Name = code, ProcessSegmentIds = segments.ToList() })).Data!.Id;

    private static UpdateProcessSegmentRequest Upd(List<int>? equipment, int? def)
        => new() { Code = "SAL", Name = "Saldatura", IsActive = true, EquipmentIds = equipment, DefaultEquipmentId = def };

    private bool IsDefault(int segment, int equipment)
        => Query(db => db.EquipmentProcessSegments.Single(l => l.ProcessSegmentId == segment && l.EquipmentId == equipment).IsDefault);

    [Fact]
    public async Task UpdateAsync_SetDefault_Success_ExposedOnBothSides()
    {
        var seg = AddSegment("SAL");
        var m1 = await Machine("M1", seg);
        var m2 = await Machine("M2", seg);

        var result = await Service<ProcessSegmentService>().UpdateAsync(seg, Upd([m1, m2], m2));

        result.IsSuccessful.Should().BeTrue(string.Join(";", result.Errors));
        result.Data!.Equipment.Single(e => e.IsDefault).Id.Should().Be(m2);
        (await Service<EquipmentService>().GetAsync(m2)).Data!.ProcessSegments.Single().IsDefault.Should().BeTrue();
    }

    [Fact]
    public async Task UpdateAsync_ChangeDefault_Success_OnlyOneDefault()
    {
        var seg = AddSegment("SAL");
        var m1 = await Machine("M1", seg);
        var m2 = await Machine("M2", seg);
        await Service<ProcessSegmentService>().UpdateAsync(seg, Upd([m1, m2], m1));

        var result = await Service<ProcessSegmentService>().UpdateAsync(seg, Upd([m1, m2], m2));

        result.IsSuccessful.Should().BeTrue();
        (IsDefault(seg, m1), IsDefault(seg, m2)).Should().Be((false, true));
    }

    [Fact]
    public async Task UpdateAsync_NullDefaultWithEquipmentIds_Success_ClearsDefault()
    {
        var seg = AddSegment("SAL");
        var m1 = await Machine("M1", seg);
        await Service<ProcessSegmentService>().UpdateAsync(seg, Upd([m1], m1));

        await Service<ProcessSegmentService>().UpdateAsync(seg, Upd([m1], null));

        IsDefault(seg, m1).Should().BeFalse();
    }

    [Fact]
    public async Task UpdateAsync_EquipmentIdsOmitted_Success_DefaultUnchanged()
    {
        var seg = AddSegment("SAL");
        var m1 = await Machine("M1", seg);
        await Service<ProcessSegmentService>().UpdateAsync(seg, Upd([m1], m1));

        var result = await Service<ProcessSegmentService>().UpdateAsync(seg, Upd(null, null));

        result.IsSuccessful.Should().BeTrue();
        IsDefault(seg, m1).Should().BeTrue();
    }

    [Fact]
    public async Task UpdateAsync_DefaultOutsideSetOrInactive_Failure_Invalid()
    {
        var seg = AddSegment("SAL");
        var m1 = await Machine("M1", seg);
        var m2 = await Machine("M2");
        var inactive = await Machine("OLD");
        await Service<EquipmentService>().DeactivateAsync(inactive);

        (await Service<ProcessSegmentService>().UpdateAsync(seg, Upd([m1], m2))).Failure.Should().Be(ProcessSegmentFailure.Invalid);
        (await Service<ProcessSegmentService>().UpdateAsync(seg, Upd([m1, inactive], inactive))).Failure.Should().Be(ProcessSegmentFailure.Invalid);
    }

    [Fact]
    public async Task UpdateAsync_RemoveDefaultMachineFromSet_Success_FlagDropsWithLink()
    {
        var seg = AddSegment("SAL");
        var m1 = await Machine("M1", seg);
        var m2 = await Machine("M2", seg);
        await Service<ProcessSegmentService>().UpdateAsync(seg, Upd([m1, m2], m1));

        await Service<ProcessSegmentService>().UpdateAsync(seg, Upd([m2], null));

        Query(db => db.EquipmentProcessSegments.Count(l => l.IsDefault)).Should().Be(0);
    }

    [Fact]
    public async Task EquipmentUpdateAsync_ReplaceSegments_Success_SurvivingLinkKeepsFlag()
    {
        var sal = AddSegment("SAL");
        var ver = AddSegment("VER");
        var m1 = await Machine("M1", sal);
        await Service<ProcessSegmentService>().UpdateAsync(sal, Upd([m1], m1));

        var result = await Service<EquipmentService>().UpdateAsync(m1,
            new UpdateEquipmentRequest { Code = "M1", Name = "M1", IsActive = true, ProcessSegmentIds = [sal, ver] });

        result.IsSuccessful.Should().BeTrue();
        IsDefault(sal, m1).Should().BeTrue();
        IsDefault(ver, m1).Should().BeFalse();
    }

    [Fact]
    public async Task UpdateAsync_DeactivatedDefault_Success_FlagKeptButPickIgnoresIt()
    {
        var seg = AddSegment("SAL");
        var m1 = await Machine("M1", seg);
        var m2 = await Machine("M2", seg);
        await Service<ProcessSegmentService>().UpdateAsync(seg, Upd([m1, m2], m1));
        await Service<EquipmentService>().DeactivateAsync(m1);

        IsDefault(seg, m1).Should().BeTrue();
        var machines = await Service<EquipmentSelector>().MachinesOfSegmentAsync(seg);
        EquipmentSelector.Pick(machines)!.Id.Should().Be(m2);
    }
}
