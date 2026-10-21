using illyW.Framework.Core.RepositoryPattern;
using MicroMES.ApiCore.Persistence.Entities;
using MicroMES.ApiFeatures.PlanningFeature.Models;
using MicroMES.ApiFeatures.PlanningFeature.Services;
using MicroMES.ApiFeatures.ProductionOrderFeature.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace MicroMES.ApiFeatures.PlanningFeature.Seeding;

/// <summary>
/// Sample plan for the open work orders. Runs after the calendar and the order seeders and works also on a database that is already
/// seeded: it plans (scope All, through <see cref="PlanningService"/> and the system clock) only when no open work order has a plan yet,
/// so a second run adds nothing and a plan made by the user is never overwritten. Nothing has to be emptied.
/// <para>
/// What the demo shows: queues on shared machines (welding, cutting), the work orders without a machine assigned going to the machine that
/// finishes first, phases without machines (levigatura, collaudo) on the default calendar, and the late order of the order seeder
/// (coffee tables x8, requested end already passed). One stale plan is simulated: the powder coating that is running now (the oldest
/// running work order) gets the plan it would have had before starting - 60 minutes from its real start - so it is past its planned end.
/// </para>
/// </summary>
public class PlanningDevelopmentSeeder(IServiceScopeFactory scopes)
{
    private static bool IsOpen(WorkOrder w) => w.Status is not (WorkOrderStatus.Ended or WorkOrderStatus.Aborted);

    public async Task SeedAsync()
    {
        using (var scope = scopes.CreateScope())
        {
            var workOrders = scope.ServiceProvider.GetRequiredService<IWorkOrderRepository>();
            var open = new List<WorkOrder>();
            await foreach (var w in workOrders.FetchAsync(x => x.Status != WorkOrderStatus.Ended && x.Status != WorkOrderStatus.Aborted)) open.Add(w);
            if (open.Count == 0 || open.Any(w => w.PlannedStartAt is not null)) return;
        }

        using (var scope = scopes.CreateScope())
        {
            var applied = await scope.ServiceProvider.GetRequiredService<PlanningService>().ApplyAsync(new PlanningRequest { Scope = PlanningScope.All });
            if (!applied.IsSuccessful)
                throw new InvalidOperationException($"Seed pianificazione fallito: {string.Join("; ", applied.Errors)}");
        }

        // Simulated stale plan: the oldest running work order, planned for 60 minutes from its real start.
        using (var scope = scopes.CreateScope())
        {
            var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var repository = uow.GetRepository<IWorkOrderRepository>();
            var running = new List<WorkOrder>();
            await foreach (var w in repository.FetchAsync(x => x.Status == WorkOrderStatus.Running && x.StartedAt != null)) running.Add(w);
            var oldest = running.OrderBy(w => w.StartedAt).ThenBy(w => w.Id).FirstOrDefault();
            if (oldest is null) return;

            try
            {
                uow.BeginTransaction();
                oldest.PlannedStartAt = oldest.StartedAt;
                oldest.PlannedEndAt = oldest.StartedAt!.Value.AddMinutes(60);
                oldest.PlannedAt = oldest.StartedAt!.Value.AddHours(-20);
                var result = await repository.UpdateAsync(oldest);
                if (!result.IsSuccessful) throw new InvalidOperationException(string.Join("; ", result.Errors));
                uow.Commit();
            }
            catch
            {
                try { uow.Rollback(); } catch { /* transaction already gone */ }
                throw;
            }
        }
    }
}
