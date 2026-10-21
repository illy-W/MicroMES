using FluentAssertions;
using MicroMES.ApiCore.Persistence.Entities;
using MicroMES.ApiTests.Integration.Shared;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace MicroMES.ApiTests.Integration.Features;

/// <summary>What only a real PostgreSQL enforces: FK Restrict and unique indexes (SQLite/InMemory would not match).</summary>
public class PersistenceConstraintsTests(ApiFixture fixture) : IntegrationTest(fixture)
{
    private const string ForeignKeyViolation = "23503";
    private const string UniqueViolation = "23505";

    private static async Task ShouldFailWithAsync(Func<Task> action, string sqlState)
    {
        var failure = await Record.ExceptionAsync(action);
        failure.Should().NotBeNull();
        failure!.GetBaseException().Should().BeOfType<PostgresException>().Which.SqlState.Should().Be(sqlState);
    }

    [Fact]
    public async Task Material_UsedInBomOrRouting_Failure_PhysicalDeleteIsRestrictedByForeignKeys()
    {
        var segment = await Api.CreateSegmentAsync("SEG");
        var parent = await Api.CreateMaterialAsync("PF", MaterialType.FinishedGood);
        var raw = await Api.CreateMaterialAsync("RAW", MaterialType.RawMaterial);
        await Api.AddBomItemAsync(parent.Id, raw.Id, 1);
        await Api.PutLinearRoutingAsync(parent.Id, segment.Id);

        await ShouldFailWithAsync(() => Fixture.WithDbAsync(db => db.Materials.Where(m => m.Id == raw.Id).ExecuteDeleteAsync()), ForeignKeyViolation);
        await ShouldFailWithAsync(() => Fixture.WithDbAsync(db => db.Materials.Where(m => m.Id == parent.Id).ExecuteDeleteAsync()), ForeignKeyViolation);
        await ShouldFailWithAsync(() => Fixture.WithDbAsync(db => db.ProcessSegments.Where(s => s.Id == segment.Id).ExecuteDeleteAsync()), ForeignKeyViolation);
    }

    [Fact]
    public async Task ProductionOrder_WithWorkOrdersAndSessions_Failure_PhysicalDeleteIsRestrictedByForeignKeys()
    {
        var segment = await Api.CreateSegmentAsync("SEG");
        var product = await Api.CreateMaterialAsync("PF", MaterialType.FinishedGood);
        await Api.PutLinearRoutingAsync(product.Id, segment.Id);
        var order = await Api.CreateOrderAsync(product.Id, 1);
        await Api.ReleaseAsync(order.Id);
        var workOrder = (await Api.GetWorkOrdersAsync(order.Id)).Ok.Single();
        await Api.StartAsync(workOrder.Id); // opens a session (WorkOrderResponse)

        await ShouldFailWithAsync(() => Fixture.WithDbAsync(db => db.ProductionOrders.Where(o => o.Id == order.Id).ExecuteDeleteAsync()), ForeignKeyViolation);
        await ShouldFailWithAsync(() => Fixture.WithDbAsync(db => db.WorkOrders.ExecuteDeleteAsync()), ForeignKeyViolation);
    }

    [Fact]
    public async Task UniqueIndexes_DuplicateRows_Failure_RejectedByTheDatabase()
    {
        var segment = await Api.CreateSegmentAsync("SEG");
        var material = await Api.CreateMaterialAsync("PF", MaterialType.FinishedGood);
        var other = await Api.CreateMaterialAsync("RAW", MaterialType.RawMaterial);
        await Api.AddBomItemAsync(material.Id, other.Id, 1);
        await Api.PutLinearRoutingAsync(material.Id, segment.Id);
        var now = DateTime.UtcNow;

        await ShouldFailWithAsync(() => Fixture.WithDbAsync(async db =>
        {
            db.BomItems.Add(new BomItem { ParentMaterialId = material.Id, ComponentMaterialId = other.Id, Quantity = 2, CreatedAt = now, UpdatedAt = now });
            return await db.SaveChangesAsync();
        }), UniqueViolation);

        await ShouldFailWithAsync(() => Fixture.WithDbAsync(async db =>
        {
            db.RoutingSteps.Add(new RoutingStep { MaterialId = material.Id, ProcessSegmentId = segment.Id, OperationNumber = 10, CreatedAt = now, UpdatedAt = now });
            return await db.SaveChangesAsync();
        }), UniqueViolation);

        await ShouldFailWithAsync(() => Fixture.WithDbAsync(async db =>
        {
            db.ProcessSegments.Add(new ProcessSegment { Code = "SEG", Name = "x", CreatedAt = now, UpdatedAt = now });
            return await db.SaveChangesAsync();
        }), UniqueViolation);
    }
}
