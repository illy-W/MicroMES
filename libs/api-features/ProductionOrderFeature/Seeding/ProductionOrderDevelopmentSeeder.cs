using Microsoft.Extensions.DependencyInjection;
using MicroMES.ApiFeatures.MaterialFeature.Repositories;
using MicroMES.ApiFeatures.ProductionOrderFeature.Models;
using MicroMES.ApiFeatures.ProductionOrderFeature.Services;
using MicroMES.ApiCore.Persistence.Entities;

namespace MicroMES.ApiFeatures.ProductionOrderFeature.Seeding;

/// <summary>
/// Sample production orders for a small steel + oak workshop. It only goes through the services
/// (release plan, state machines), with a moving clock so that sessions look like past work.
/// Runs only when there are no orders yet.
/// </summary>
public class ProductionOrderDevelopmentSeeder(
    IServiceScopeFactory scopes,
    OverridableTimeProvider clock)
{
    // The unit of work cannot reopen a transaction within the same scope: every command gets its own scope,
    // like a request would.
    private async Task<T> Orders<T>(Func<ProductionOrderService, Task<T>> call)
    {
        using var scope = scopes.CreateScope();
        return await call(scope.ServiceProvider.GetRequiredService<ProductionOrderService>());
    }

    private async Task<T> WorkOrders<T>(Func<WorkOrderService, Task<T>> call)
    {
        using var scope = scopes.CreateScope();
        return await call(scope.ServiceProvider.GetRequiredService<WorkOrderService>());
    }

    public async Task SeedAsync()
    {
        var existing = Unwrap(await Orders(o => o.GetAllAsync(null, null, null, true)));
        if (existing.Count > 0) return;

        var today = DateTime.UtcNow.Date;
        var now = DateTime.UtcNow;
        var tavolino = await MaterialIdAsync("PF-TAV-60");
        var sedia = await MaterialIdAsync("PF-SED-01");

        try
        {
            // 1. Chairs x4, completed (12 days ago): every work order ended, one scrapped piece.
            var d = today.AddDays(-12);
            clock.Set(d.AddHours(7));
            var done = await CreateAsync(sedia, 4, 5, d.AddDays(1), d.AddDays(5), "Campionatura per showroom");
            clock.Set(d.AddHours(7).AddMinutes(30));
            await ReleaseAsync(done.Id);
            var t = d.AddHours(8);
            t = await RunAsync(done.Id, "PF-SED-01", 10, t, [new S(24, 4, 0)]);
            t = await RunAsync(done.Id, "PF-SED-01", 20, t, [new S(46, 4, 1, "Una piastra tagliata fuori squadra, rifatta")]);
            t = await RunAsync(done.Id, "PF-SED-01", 30, t.AddMinutes(10), [new S(44, 0, 0, "Pausa pranzo"), new S(38, 4, 0)]);
            t = await RunAsync(done.Id, "PF-SED-01", 40, t, [new S(18, 4, 0)]);
            t = await RunAsync(done.Id, "PF-SED-01", 50, t.AddDays(1).Date.AddHours(8), [new S(58, 4, 0)]);
            t = await RunAsync(done.Id, "PF-SED-01", 60, t, [new S(52, 4, 0)]);
            t = await RunAsync(done.Id, "PF-SED-01", 70, t, [new S(14, 4, 0)]);
            await RunAsync(done.Id, "PF-SED-01", 80, t, [new S(17, 4, 0)]);

            // 2. Coffee tables x5: released then cancelled.
            d = today.AddDays(-8);
            clock.Set(d.AddHours(9));
            var cancelled = await CreateAsync(tavolino, 5, 0, d.AddDays(3), d.AddDays(10), "Annullato: il cliente ha ritirato l'ordine");
            clock.Set(d.AddHours(9).AddMinutes(20));
            await ReleaseAsync(cancelled.Id);
            clock.Set(d.AddDays(1).AddHours(10));
            Unwrap(await Orders(o => o.CancelAsync(cancelled.Id)));

            // 3. Coffee tables x20, high priority, released, frame in progress.
            d = today.AddDays(-6);
            clock.Set(d.AddHours(8));
            var tav = await CreateAsync(tavolino, 20, 10, today.AddDays(-4), today.AddDays(10), "Commessa arredo bar, consegna in due tranche");
            clock.Set(d.AddHours(8).AddMinutes(30));
            await ReleaseAsync(tav.Id);
            var day = today.AddDays(-5).AddHours(7).AddMinutes(30);
            // Frame (SL-TAV-STR): cutting 70 min with a pause, welding with lunch break, deburring.
            var s = await RunAsync(tav.Id, "SL-TAV-STR", 10, day, [new S(35, 9, 0), new S(38, 11, 1, "Un taglio fuori misura")]);
            s = await RunAsync(tav.Id, "SL-TAV-STR", 20, s.AddMinutes(15), [new S(150, 12, 0, "Pausa pranzo"), new S(112, 8, 0)]);
            s = await RunAsync(tav.Id, "SL-TAV-STR", 30, today.AddDays(-4).AddHours(7).AddMinutes(30), [new S(68, 20, 0)]);
            // Powder coating (op 40) is running now.
            clock.Set(now.AddMinutes(-95));
            var coating = await FindAsync(tav.Id, "SL-TAV-STR", 40);
            Unwrap(await WorkOrders(w => w.StartAsync(coating)));

            // 4. Chairs x12, released, both cutting branches started: one ended, one running.
            d = today.AddDays(-3);
            clock.Set(d.AddHours(8));
            var sed = await CreateAsync(sedia, 12, 5, d, today.AddDays(7), "Ordine ristorante, finitura nera");
            clock.Set(d.AddHours(8).AddMinutes(15));
            await ReleaseAsync(sed.Id);
            await RunAsync(sed.Id, "PF-SED-01", 10, d.AddHours(9), [new S(55, 12, 0)]);
            clock.Set(now.AddMinutes(-40));
            var chairCut = await FindAsync(sed.Id, "PF-SED-01", 20);
            Unwrap(await WorkOrders(w => w.StartAsync(chairCut)));

            // 5. Coffee tables x10, planned (not released).
            clock.Set(today.AddDays(-1).AddHours(16));
            var next = today.AddDays(30);
            await CreateAsync(tavolino, 10, 3, next, next.AddDays(14), "Da rilasciare dopo la consegna della commessa bar");
        }
        finally
        {
            clock.Reset();
        }
    }

    private sealed record S(int Min, decimal Good, decimal Scrap, string? Notes = null);

    // ---- helpers ------------------------------------------------------------------------------------

    /// <summary>
    /// Works a work order on the clock: start, then for each session (minutes, good, scrap) pause/resume;
    /// the last session stops it. When setupExtra is given, the first session is extended by it.
    /// </summary>
    private async Task<DateTime> RunAsync(int orderId, string materialCode, int op, DateTime start,
        S[] sessions, int setupExtra = 0)
    {
        var id = await FindAsync(orderId, materialCode, op);
        var t = start;
        for (var i = 0; i < sessions.Length; i++)
        {
            clock.Set(t);
            Unwrap(i == 0 ? await WorkOrders(w => w.StartAsync(id)) : await WorkOrders(w => w.ResumeAsync(id)));
            t = t.AddMinutes(sessions[i].Min + (i == 0 ? setupExtra : 0));
            clock.Set(t);
            var close = new CloseSessionRequest
            {
                GoodQuantity = sessions[i].Good, ScrapQuantity = sessions[i].Scrap, Notes = sessions[i].Notes
            };
            Unwrap(i == sessions.Length - 1
                ? await WorkOrders(w => w.StopAsync(id, close))
                : await WorkOrders(w => w.PauseAsync(id, close)));
            if (i < sessions.Length - 1) t = t.AddMinutes(20); // interruption
        }
        return t;
    }

    private async Task<int> FindAsync(int rootOrderId, string materialCode, int op)
    {
        var detail = Unwrap(await Orders(o => o.GetAsync(rootOrderId)));
        var own = detail.MaterialCode == materialCode
            ? detail.WorkOrders
            : Unwrap(await WorkOrders(w => w.GetAllAsync(null, FlattenId(detail, materialCode), null)));
        return own.Single(w => w.OperationNumber == op).Id;
    }

    private static int FlattenId(ProductionOrderDetailModel detail, string materialCode)
    {
        ProductionOrderTreeModel? Find(IEnumerable<ProductionOrderTreeModel> nodes)
        {
            foreach (var n in nodes)
            {
                if (n.MaterialCode == materialCode) return n;
                if (Find(n.Children) is { } f) return f;
            }
            return null;
        }
        return Find(detail.Children)?.Id ?? throw new InvalidOperationException($"Ordine figlio {materialCode} non trovato.");
    }

    private async Task<ProductionOrderDetailModel> CreateAsync(int materialId, decimal quantity, int priority,
        DateTime start, DateTime end, string notes)
        => Unwrap(await Orders(o => o.CreateAsync(new CreateProductionOrderRequest
        {
            MaterialId = materialId, Quantity = quantity, Priority = priority,
            RequestedStart = DateTime.SpecifyKind(start, DateTimeKind.Utc),
            RequestedEnd = DateTime.SpecifyKind(end, DateTimeKind.Utc), Notes = notes
        })));

    private async Task ReleaseAsync(int id) => Unwrap(await Orders(o => o.ReleaseAsync(id)));

    private async Task<int> MaterialIdAsync(string code)
    {
        using var scope = scopes.CreateScope();
        var materials = scope.ServiceProvider.GetRequiredService<IMaterialRepository>();
        await foreach (var m in materials.FetchAsync(x => x.Code == code)) return m.Id;
        throw new InvalidOperationException($"Materiale {code} non trovato: eseguire prima il seeder dei dati anagrafici.");
    }

    private static T Unwrap<T>(ProductionOrderResult<T> result)
        => result.IsSuccessful
            ? result.Data!
            : throw new InvalidOperationException($"Seed ordini fallito: {string.Join("; ", result.Errors)}");
}
