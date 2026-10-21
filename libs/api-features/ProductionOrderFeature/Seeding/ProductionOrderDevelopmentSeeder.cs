using Microsoft.Extensions.DependencyInjection;
using MicroMES.ApiFeatures.EquipmentFeature.Repositories;
using MicroMES.ApiFeatures.MaterialFeature.Repositories;
using MicroMES.ApiFeatures.ScrapFeature.Repositories;
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

    private Dictionary<string, int> _machines = [];

    private int EquipmentOf(string code)
        => _machines.TryGetValue(code, out var id) ? id : throw new InvalidOperationException($"Macchina {code} non trovata: eseguire prima il seeder dei dati anagrafici.");

    public async Task SeedAsync()
    {
        var existing = Unwrap(await Orders(o => o.GetAllAsync(null, null, null, true)));
        if (existing.Count > 0) return;

        using (var scope = scopes.CreateScope())
        {
            var equipment = scope.ServiceProvider.GetRequiredService<IEquipmentRepository>();
            _machines = [];
            await foreach (var e in equipment.FetchAsync(_ => true)) _machines[e.Code] = e.Id;
        }

        var today = DateTime.UtcNow.Date;
        var now = DateTime.UtcNow;
        var tavolino = await MaterialIdAsync("PF-TAV-60");
        var sedia = await MaterialIdAsync("PF-SED-01");
        var libreria = await MaterialIdAsync("PF-LIB-01");

        try
        {
            // 1. Chairs x4, completed (12 days ago): every work order ended, one scrapped piece.
            var d = today.AddDays(-12);
            clock.Set(d.AddHours(7));
            var done = await CreateAsync(sedia, 4, 5, d.AddDays(1), d.AddDays(5), "Campionatura per showroom");
            clock.Set(d.AddHours(7).AddMinutes(30));
            await ReleaseAsync(done.Id);
            var t = d.AddHours(8);
            t = await RunAsync(done.Id, "PF-SED-01", 10, t, [new S(24, 4, 2, null, [new E("MISURA", 1), new E("MAT-DIF", 1)])]);
            t = await RunAsync(done.Id, "PF-SED-01", 20, t, [new S(46, 4, 1, "Una piastra tagliata fuori squadra, rifatta", [new E("FUORI-SQ", 1)])], assign: "TAG-LAM-1");
            // Welding: the first session ran on SAL-2 (SAL-1 was busy), after lunch the work moved back to SAL-1.
            t = await RunAsync(done.Id, "PF-SED-01", 30, t.AddMinutes(10),
                [new S(44, 0, 0, "Pausa pranzo", Machine: "SAL-2"), new S(38, 4, 1, null, [new E("SAL-INC", 1)], Machine: "SAL-1")]);
            t = await RunAsync(done.Id, "PF-SED-01", 40, t, [new S(18, 4, 0)]);
            t = await RunAsync(done.Id, "PF-SED-01", 50, t.AddDays(1).Date.AddHours(8), [new S(58, 4, 1, null, [new E("VER-DIF", 1)])]);
            t = await RunAsync(done.Id, "PF-SED-01", 60, t, [new S(52, 4, 1)]);
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
            var s = await RunAsync(tav.Id, "SL-TAV-STR", 10, day, [new S(35, 9, 0), new S(38, 11, 1, "Un taglio fuori misura", [new E("MISURA", 1)])]);
            s = await RunAsync(tav.Id, "SL-TAV-STR", 20, s.AddMinutes(15), [new S(150, 12, 0, "Pausa pranzo"), new S(112, 8, 1, null, [new E("SAL-INC", 1)])]);
            s = await RunAsync(tav.Id, "SL-TAV-STR", 30, today.AddDays(-4).AddHours(7).AddMinutes(30), [new S(68, 20, 2, null, [new E("GRAFFIO", 2)])]);
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
            await RunAsync(sed.Id, "PF-SED-01", 10, d.AddHours(9), [new S(55, 12, 1, null, [new E("MAT-DIF", 1)])]);
            clock.Set(now.AddMinutes(-40));
            var chairCut = await FindAsync(sed.Id, "PF-SED-01", 20);
            // The plate cut runs on the second saw (TAG-LAM has no default machine).
            Unwrap(await WorkOrders(w => w.StartAsync(chairCut, new OpenSessionRequest { EquipmentId = EquipmentOf("TAG-TUB-2") })));

            // 5. Coffee tables x10, planned (not released).
            clock.Set(today.AddDays(-1).AddHours(16));
            var next = today.AddDays(30);
            await CreateAsync(tavolino, 10, 3, next, next.AddDays(14), "Da rilasciare dopo la consegna della commessa bar");

            // 6. Coffee tables x8, released: the oak top milling was paused (work order Interrupted).
            d = today.AddDays(-2);
            clock.Set(d.AddHours(8));
            // Requested end already passed (today 00:00): it is the late order of the planning demo.
            var paused = await CreateAsync(tavolino, 8, 4, d, today, "Fresatura piani sospesa: manca la fresa da 12 mm");
            clock.Set(d.AddHours(8).AddMinutes(10));
            await ReleaseAsync(paused.Id);
            var milling = await FindAsync(paused.Id, "SL-TAV-PIA", 10);
            clock.Set(d.AddHours(9));
            Unwrap(await WorkOrders(w => w.StartAsync(milling)));
            clock.Set(d.AddHours(9).AddMinutes(50));
            Unwrap(await WorkOrders(w => w.PauseAsync(milling, new CloseSessionRequest { GoodQuantity = 3, ScrapQuantity = 0, Notes = "Fresa da sostituire" })));

            // The frame cutting of the x8 order is Ready: the manager planned it on the second saw, which the x12
            // chair plate cut is using right now (the "in use" warning).
            clock.Set(d.AddHours(9).AddMinutes(55));
            var readyCut = await FindAsync(paused.Id, "SL-TAV-STR", 10);
            Unwrap(await WorkOrders(w => w.AssignEquipmentAsync(readyCut, new AssignEquipmentRequest { EquipmentId = EquipmentOf("TAG-TUB-2") })));

            // 7. Chairs x2, aborted after the first cut (order and work orders Aborted).
            d = today.AddDays(-1);
            clock.Set(d.AddHours(8));
            var aborted = await CreateAsync(sedia, 2, 1, d, today.AddDays(5), "Abortito: tubolare non conforme");
            clock.Set(d.AddHours(8).AddMinutes(10));
            await ReleaseAsync(aborted.Id);
            await RunAsync(aborted.Id, "PF-SED-01", 10, d.AddHours(9), [new S(10, 1, 1, "Lotto di tubolare fuori tolleranza", [new E("MAT-DIF", 1)])]);
            clock.Set(d.AddHours(11));
            Unwrap(await Orders(o => o.AbortAsync(aborted.Id, "Materiale non conforme")));

            // 8. Bookshelves x6, released: the side panels (child order) feed only the assembly (op 30);
            // shelf milling is done, shelf finishing and the panel cutting are in progress.
            d = today.AddDays(-1);
            clock.Set(d.AddHours(13));
            var shelves = await CreateAsync(libreria, 6, 2, d, today.AddDays(8), "Librerie per ufficio: le fiancate servono solo al montaggio");
            clock.Set(d.AddHours(13).AddMinutes(10));
            await ReleaseAsync(shelves.Id);
            await RunAsync(shelves.Id, "PF-LIB-01", 10, d.AddHours(14), [new S(44, 6, 1, null, [new E("MISURA", 1)])]);
            await RunAsync(shelves.Id, "SL-LIB-FIA", 10, d.AddHours(15), [new S(30, 6, 1, null, [new E("GRAFFIO", 1)])]);
            clock.Set(now.AddMinutes(-25));
            var shelfFinish = await FindAsync(shelves.Id, "PF-LIB-01", 20);
            Unwrap(await WorkOrders(w => w.StartAsync(shelfFinish)));
            var panelWeld = await FindAsync(shelves.Id, "SL-LIB-FIA", 20);
            Unwrap(await WorkOrders(w => w.StartAsync(panelWeld)));

            // Example correction on an open order: the shelf milling count was redone at the end of the shift.
            clock.Set(now.AddMinutes(-10));
            var shelfMilling = await FindAsync(shelves.Id, "PF-LIB-01", 10);
            var millingSession = Unwrap(await WorkOrders(w => w.GetAsync(shelfMilling))).Sessions.Single();
            var misura = await ReasonIdAsync("MISURA");
            var graffio = await ReasonIdAsync("GRAFFIO");
            Unwrap(await WorkOrders(w => w.CorrectSessionAsync(shelfMilling, millingSession.Id, new CorrectSessionRequest
            {
                GoodQuantity = 5, ScrapQuantity = 2, Notes = "Riconteggio a fine turno: un ripiano graffiato non era stato registrato",
                ScrapEntries = [new ScrapEntryRequest { ScrapReasonId = misura, Quantity = 1 },
                                new ScrapEntryRequest { ScrapReasonId = graffio, Quantity = 1 }]
            })));
        }
        finally
        {
            clock.Reset();
        }
    }

    /// <summary>Session to close: minutes, good, scrap, notes and scrap split by reason code (none = "no reason").</summary>
    /// <remarks>Machine: code of the machine used when it differs from the assigned one (null = the assigned / previous one).</remarks>
    private sealed record S(int Min, decimal Good, decimal Scrap, string? Notes = null, E[]? Entries = null, string? Machine = null);

    private sealed record E(string ReasonCode, decimal Quantity);

    // ---- helpers ------------------------------------------------------------------------------------

    /// <summary>
    /// Works a work order on the clock: start, then for each session (minutes, good, scrap) pause/resume;
    /// the last session stops it. When setupExtra is given, the first session is extended by it.
    /// </summary>
    private async Task<DateTime> RunAsync(int orderId, string materialCode, int op, DateTime start,
        S[] sessions, int setupExtra = 0, string? assign = null)
    {
        var id = await FindAsync(orderId, materialCode, op);
        if (assign is not null)
        {
            clock.Set(start.AddMinutes(-5));
            Unwrap(await WorkOrders(w => w.AssignEquipmentAsync(id, new AssignEquipmentRequest { EquipmentId = EquipmentOf(assign) })));
        }
        var t = start;
        for (var i = 0; i < sessions.Length; i++)
        {
            clock.Set(t);
            var body = sessions[i].Machine is { } machine ? new OpenSessionRequest { EquipmentId = EquipmentOf(machine) } : null;
            Unwrap(i == 0 ? await WorkOrders(w => w.StartAsync(id, body)) : await WorkOrders(w => w.ResumeAsync(id, body)));
            t = t.AddMinutes(sessions[i].Min + (i == 0 ? setupExtra : 0));
            clock.Set(t);
            var close = new CloseSessionRequest
            {
                GoodQuantity = sessions[i].Good, ScrapQuantity = sessions[i].Scrap, Notes = sessions[i].Notes
            };
            if (sessions[i].Entries is { } entries)
            {
                close.ScrapEntries = [];
                foreach (var e in entries)
                    close.ScrapEntries.Add(new ScrapEntryRequest { ScrapReasonId = await ReasonIdAsync(e.ReasonCode), Quantity = e.Quantity });
            }
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

    private async Task<int> ReasonIdAsync(string code)
    {
        using var scope = scopes.CreateScope();
        var reasons = scope.ServiceProvider.GetRequiredService<IScrapReasonRepository>();
        await foreach (var r in reasons.FetchAsync(x => x.Code == code)) return r.Id;
        throw new InvalidOperationException($"Motivo di scarto {code} non trovato: eseguire prima il seeder dei dati anagrafici.");
    }

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
