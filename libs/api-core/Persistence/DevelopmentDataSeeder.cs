using MicroMES.ApiCore.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace MicroMES.ApiCore.Persistence;

/// <summary>
/// Sample data for local development: a small workshop producing steel and oak furniture.
/// Infrastructure code, so it uses <see cref="AppDbContext"/> directly. Runs only when Materials is empty.
/// </summary>
public static class DevelopmentDataSeeder
{
    private const double ColumnSpacing = 260;
    private const double RowSpacing = 120;

    public static async Task SeedAsync(AppDbContext db)
    {
        if (await db.Materials.AnyAsync()) return;

        var now = DateTime.UtcNow;

        // ---- Materials -------------------------------------------------------------------------------
        Material M(string code, string name, MaterialType type, UnitOfMeasure uom, string? description = null,
            string? vendor = null, string? vendorId = null, bool active = true) => new()
        {
            Code = code, Name = name, Type = type, UnitOfMeasure = uom, Description = description,
            Vendor = vendor, VendorMaterialId = vendorId, IsActive = active, CreatedAt = now, UpdatedAt = now
        };

        var raw = MaterialType.RawMaterial;
        var cons = MaterialType.Consumable;

        var tubQ30 = M("TUB-Q30-2", "Tubolare acciaio quadro 30x30x2", raw, UnitOfMeasure.Meter, "Tubolare S235 saldato, barre da 6 m", "Ferriera Bresciana S.p.A.", "FB-TQ3030-2");
        var tubQ25 = M("TUB-Q25-2", "Tubolare acciaio quadro 25x25x2", raw, UnitOfMeasure.Meter, "Sostituito da TUB-Q30-2", "Ferriera Bresciana S.p.A.", "FB-TQ2525-2", active: false);
        var lamS235 = M("LAM-S235-2", "Lamiera S235 spessore 2 mm", raw, UnitOfMeasure.Kilogram, "Lamiera decapata per piegatura", "Acciai Lombardi S.r.l.", "AL-LAM-S235-20");
        var tavRov = M("TAV-ROV-25", "Tavola rovere massello 25 mm", raw, UnitOfMeasure.Piece, "Tavola 600x400x25 mm stagionata", "Legnami Dolomiti S.n.c.", "LD-ROV-600-25");
        var verPol = M("VER-POL-NER", "Vernice a polvere nera opaca", raw, UnitOfMeasure.Kilogram, "Poliestere RAL 9005 opaco", "Colorificio Veneto S.r.l.", "CV-POL-9005");
        var verAcq = M("VER-ACQ-TRA", "Vernice acquosa trasparente per legno", raw, UnitOfMeasure.Liter, "Finitura satinata a base acqua", "Colorificio Veneto S.r.l.", "CV-ACQ-TR-5");
        var vit = M("VIT-M6-30", "Vite TCEI M6x30 zincata", raw, UnitOfMeasure.Piece, null, "Viteria Emiliana S.r.l.", "VE-TCEI-M6-30");
        var pie = M("PIE-GOM-30", "Piedino in gomma regolabile 30 mm", raw, UnitOfMeasure.Piece, null, "Viteria Emiliana S.r.l.", "VE-PIE-30");
        var imb = M("IMB-SCA-60", "Scatola di imballo 60x60x50", raw, UnitOfMeasure.Piece, "Cartone doppia onda con angolari", "Cartotecnica Piemonte S.r.l.", "CP-SC-606050");
        var filSal = M("FIL-SAL-08", "Filo di saldatura MIG 0,8 mm", cons, UnitOfMeasure.Kilogram, "Bobina da 15 kg", "Saldature Nord S.r.l.", "SN-MIG-08");
        var disTag = M("DIS-TAG-125", "Disco da taglio 125 mm", cons, UnitOfMeasure.Piece, null, "Abrasivi Torino S.r.l.", "AT-DT-125");
        var carAbr = M("CAR-ABR-120", "Carta abrasiva grana 120", cons, UnitOfMeasure.Piece, "Foglio 230x280 mm", "Abrasivi Torino S.r.l.", "AT-CA-120");

        var strutturaTav = M("SL-TAV-STR", "Struttura tavolino saldata e verniciata", MaterialType.Intermediate, UnitOfMeasure.Piece, "Telaio in tubolare con piastra, verniciato nero");
        var pianoTav = M("SL-TAV-PIA", "Piano tavolino in rovere finito", MaterialType.Intermediate, UnitOfMeasure.Piece, "Piano levigato e verniciato trasparente");
        var tavolino = M("PF-TAV-60", "Tavolino da caffè 60 cm", MaterialType.FinishedGood, UnitOfMeasure.Piece, "Struttura in acciaio nero e piano in rovere");
        var sedia = M("PF-SED-01", "Sedia in acciaio e rovere", MaterialType.FinishedGood, UnitOfMeasure.Piece, "Telaio in tubolare, seduta in rovere");

        var materials = new[]
        {
            tubQ30, tubQ25, lamS235, tavRov, verPol, verAcq, vit, pie, imb, filSal, disTag, carAbr,
            strutturaTav, pianoTav, tavolino, sedia
        };
        db.Materials.AddRange(materials);

        // ---- Process segments ------------------------------------------------------------------------
        ProcessSegment S(string code, string name, decimal setup, decimal run, string? description = null) => new()
        {
            Code = code, Name = name, Description = description, SetupMinutes = setup, RunMinutesPerUnit = run,
            IsActive = true, CreatedAt = now, UpdatedAt = now
        };

        var tagTub = S("TAG-TUB", "Taglio tubolare", 10, 2.5m, "Troncatrice a disco");
        var tagLam = S("TAG-LAM", "Taglio e piegatura lamiera", 20, 4m, "Cesoia e piegatrice");
        var sal = S("SAL", "Saldatura", 15, 12m, "Saldatura MIG su dima");
        var sba = S("SBA", "Sbavatura", 5, 3m, "Smerigliatura dei cordoni");
        var verPolSeg = S("VER-POL", "Verniciatura a polvere", 30, 6m, "Applicazione e cottura in forno");
        var freLeg = S("FRE-LEG", "Fresatura legno", 25, 8m, "Fresa CNC a tre assi");
        var lev = S("LEV", "Levigatura", 10, 7m);
        var finLeg = S("FIN-LEG", "Finitura legno", 15, 5m, "Verniciatura a spruzzo e asciugatura");
        var ass = S("ASS", "Assemblaggio", 10, 9m);
        var col = S("COL", "Collaudo", 5, 2m, "Controllo dimensionale e stabilità");
        var imbSeg = S("IMB", "Imballaggio", 5, 3m);

        var segments = new[] { tagTub, tagLam, sal, sba, verPolSeg, freLeg, lev, finLeg, ass, col, imbSeg };
        db.ProcessSegments.AddRange(segments);

        await db.SaveChangesAsync(); // generates the ids needed by BOM and routings

        // ---- BOM -------------------------------------------------------------------------------------
        void Bom(Material parent, params (Material component, decimal quantity)[] items)
        {
            foreach (var (component, quantity) in items)
                db.BomItems.Add(new BomItem
                {
                    ParentMaterialId = parent.Id, ComponentMaterialId = component.Id, Quantity = quantity,
                    CreatedAt = now, UpdatedAt = now
                });
        }

        Bom(tavolino, (strutturaTav, 1), (pianoTav, 1), (vit, 8), (pie, 4), (imb, 1));
        Bom(strutturaTav, (tubQ30, 3.2m), (lamS235, 0.8m), (filSal, 0.05m), (disTag, 0.1m), (verPol, 0.15m));
        Bom(pianoTav, (tavRov, 1), (carAbr, 3), (verAcq, 0.12m));
        Bom(sedia, (tubQ30, 4.5m), (lamS235, 1.2m), (tavRov, 0.5m), (filSal, 0.08m), (verPol, 0.2m), (vit, 6), (pie, 4), (imb, 1));

        // ---- Routings --------------------------------------------------------------------------------
        // Each step: (operation number, segment, column, row, setup override, run override, notes).
        // Column/row give the diagram position (left to right, parallel branches on separate rows).
        // The edges refer to operation numbers.
        var pendingEdges = new List<(RoutingStep, RoutingStep)>();

        void Routing(Material material,
            (int op, ProcessSegment segment, int column, int row, decimal? setup, decimal? run, string? notes)[] defs,
            params (int from, int to)[] edges)
        {
            var byOp = new Dictionary<int, RoutingStep>();
            foreach (var d in defs)
            {
                var step = new RoutingStep
                {
                    MaterialId = material.Id, ProcessSegmentId = d.segment.Id, OperationNumber = d.op,
                    SetupMinutes = d.setup, RunMinutesPerUnit = d.run, Notes = d.notes,
                    PositionX = d.column * ColumnSpacing, PositionY = d.row * RowSpacing,
                    CreatedAt = now, UpdatedAt = now
                };
                byOp[d.op] = step;
                db.RoutingSteps.Add(step);
            }

            pendingEdges.AddRange(edges.Select(e => (byOp[e.from], byOp[e.to])));
        }

        Routing(strutturaTav,
        [
            (10, tagTub, 0, 0, null, 3m, "Quattro gambe e due traverse da barra da 6 m"),
            (20, sal, 1, 0, null, null, null),
            (30, sba, 2, 0, null, null, null),
            (40, verPolSeg, 3, 0, 40m, null, "Lotto minimo di 10 pezzi per cottura in forno")
        ], (10, 20), (20, 30), (30, 40));

        Routing(pianoTav,
        [
            (10, freLeg, 0, 0, null, null, "Profilo del bordo con fresa da 12 mm"),
            (20, lev, 1, 0, null, 8m, "Grana 120 poi 180"),
            (30, finLeg, 2, 0, null, null, "Due mani con asciugatura intermedia")
        ], (10, 20), (20, 30));

        Routing(tavolino,
        [
            (10, ass, 0, 0, null, 10m, "Fissaggio del piano con 8 viti M6"),
            (20, col, 1, 0, null, null, null),
            (30, imbSeg, 2, 0, null, null, null)
        ], (10, 20), (20, 30));

        Routing(sedia,
        [
            (10, tagTub, 0, 0, null, 3.5m, "Gambe, schienale e traverse"),
            (20, tagLam, 0, 1, 25m, null, "Piastra di appoggio della seduta"),
            (30, sal, 1, 0, 20m, 15m, "Telaio completo su dima; attendere le due lavorazioni di taglio"),
            (40, sba, 2, 0, null, null, null),
            (50, verPolSeg, 3, 0, null, null, null),
            (60, ass, 4, 0, null, 11m, "Montaggio della seduta in rovere e dei piedini"),
            (70, col, 5, 0, null, null, "Prova di carico a 120 kg"),
            (80, imbSeg, 6, 0, null, null, null)
        ], (10, 30), (20, 30), (30, 40), (40, 50), (50, 60), (60, 70), (70, 80));

        await db.SaveChangesAsync(); // generates the step ids

        foreach (var (pred, succ) in pendingEdges)
            db.RoutingStepDependencies.Add(new RoutingStepDependency { PredecessorStepId = pred.Id, SuccessorStepId = succ.Id });

        await db.SaveChangesAsync();
    }

}
