using illyW.Framework.Core.RepositoryPattern;
using illyW.Framework.Core.ResultPattern;
using MicroMES.ApiCore.Persistence.Entities;
using MicroMES.ApiFeatures.BomFeature.Repositories;
using MicroMES.ApiFeatures.EquipmentFeature.Repositories;
using MicroMES.ApiFeatures.MaterialFeature.Repositories;
using MicroMES.ApiFeatures.ProcessSegmentFeature.Repositories;
using MicroMES.ApiFeatures.RoutingFeature.Repositories;
using MicroMES.ApiFeatures.ScrapFeature.Repositories;

namespace MicroMES.ApiFeatures.SampleDataFeature;

/// <summary>
/// Sample master data for local development: a small workshop producing steel and oak furniture.
/// It only goes through the repositories of the unit of work, in one transaction. Runs only when Materials is empty.
/// </summary>
public class MasterDataDevelopmentSeeder(IUnitOfWork unitOfWork)
{
    private const double ColumnSpacing = 260;
    private const double RowSpacing = 120;

    public async Task SeedAsync()
    {
        var materialRepo = unitOfWork.GetRepository<IMaterialRepository>();
        await foreach (var _ in materialRepo.FetchAsync(x => true)) return;

        unitOfWork.BeginTransaction();
        try
        {
            await SeedCoreAsync();
            unitOfWork.Commit();
        }
        catch
        {
            try { unitOfWork.Rollback(); } catch { /* transaction already gone */ }
            throw;
        }
    }

    /// <summary>Saves the entity (the same instance comes back with its generated id).</summary>
    private static async Task<T> Save<T>(Task<IResult<T>> add) where T : class
    {
        var result = await add;
        if (!result.IsSuccessful || result.Data is null)
            throw new InvalidOperationException($"Sample data not saved: {string.Join("; ", result.Errors)}");
        return result.Data;
    }

    private async Task SeedCoreAsync()
    {
        var materialRepo = unitOfWork.GetRepository<IMaterialRepository>();
        var segmentRepo = unitOfWork.GetRepository<IProcessSegmentRepository>();
        var bomRepo = unitOfWork.GetRepository<IBomItemRepository>();
        var stepRepo = unitOfWork.GetRepository<IRoutingStepRepository>();
        var dependencyRepo = unitOfWork.GetRepository<IRoutingStepDependencyRepository>();

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
        var fiancataLib = M("SL-LIB-FIA", "Fiancata libreria saldata e verniciata", MaterialType.Intermediate, UnitOfMeasure.Piece, "Montante in tubolare con traversi, verniciato nero");
        var libreria = M("PF-LIB-01", "Libreria a giorno 4 ripiani", MaterialType.FinishedGood, UnitOfMeasure.Piece, "Due fiancate in acciaio nero e quattro ripiani in rovere");

        var materials = new[]
        {
            tubQ30, tubQ25, lamS235, tavRov, verPol, verAcq, vit, pie, imb, filSal, disTag, carAbr,
            strutturaTav, pianoTav, tavolino, sedia, fiancataLib, libreria
        };
        foreach (var material in materials) await Save(materialRepo.AddAsync(material));

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
        foreach (var segment in segments) await Save(segmentRepo.AddAsync(segment));

        // ---- Equipment ---------------------------------------------------------------------------------
        // A segment with no machine has no constraint (here: fresatura legno, levigatura, collaudo).
        var equipmentRepo = unitOfWork.GetRepository<IEquipmentRepository>();
        var equipmentLinkRepo = unitOfWork.GetRepository<IEquipmentProcessSegmentRepository>();

        // Default machine of a segment (one per segment): TAG-LAM has two machines and no default, to show the manual choice.
        var defaults = new HashSet<(string Machine, string Segment)> { ("TAG-TUB-1", "TAG-TUB"), ("SAL-1", "SAL") };

        async Task Machine(string code, string name, string? description, params ProcessSegment[] linked)
        {
            var machine = await Save(equipmentRepo.AddAsync(new Equipment
            {
                Code = code, Name = name, Description = description, IsActive = true, CreatedAt = now, UpdatedAt = now
            }));
            foreach (var segment in linked)
                await Save(equipmentLinkRepo.AddAsync(new EquipmentProcessSegment
                {
                    EquipmentId = machine.Id, ProcessSegmentId = segment.Id, IsDefault = defaults.Contains((code, segment.Code))
                }));
        }

        await Machine("TAG-TUB-1", "Troncatrice a disco 1", "Troncatrice principale per tubolare", tagTub);
        await Machine("TAG-TUB-2", "Troncatrice a disco 2", "Seconda troncatrice, usata nei picchi e per le piastre di lamiera", tagTub, tagLam);
        await Machine("TAG-LAM-1", "Cesoia e piegatrice lamiera", "Cesoia con piegatrice a controllo numerico", tagLam);
        await Machine("SAL-1", "Postazione di saldatura 1", "Saldatrice MIG con dima per telai", sal);
        await Machine("SAL-2", "Postazione di saldatura 2", "Saldatrice MIG; usata anche per la sbavatura dei cordoni", sal, sba);
        await Machine("VER-POL-1", "Cabina e forno a polvere", "Cabina di applicazione e forno di cottura", verPolSeg);
        await Machine("VER-ACQ-1", "Cabina vernice acquosa", "Verniciatura a spruzzo e asciugatura del legno", finLeg);
        await Machine("ASS-B1", "Banco di assemblaggio", null, ass);
        await Machine("IMB-T1", "Tavolo di imballaggio", null, imbSeg);

        // ---- Scrap reasons ----------------------------------------------------------------------------
        // No linked segment = valid for every segment.
        var reasonRepo = unitOfWork.GetRepository<IScrapReasonRepository>();
        var reasonLinkRepo = unitOfWork.GetRepository<IScrapReasonProcessSegmentRepository>();

        async Task Reason(string code, string name, string? description, bool active, params ProcessSegment[] linked)
        {
            var reason = await Save(reasonRepo.AddAsync(new ScrapReason
            {
                Code = code, Name = name, Description = description, IsActive = active, CreatedAt = now, UpdatedAt = now
            }));
            foreach (var segment in linked)
                await Save(reasonLinkRepo.AddAsync(new ScrapReasonProcessSegment { ScrapReasonId = reason.Id, ProcessSegmentId = segment.Id }));
        }

        await Reason("GRAFFIO", "Graffio", "Segni o graffi sulla superficie", true);
        await Reason("MAT-DIF", "Materiale difettoso", "Difetto del materiale ricevuto", true);
        await Reason("MISURA", "Misura errata", "Fuori tolleranza dimensionale", true, tagTub, tagLam, freLeg, ass);
        await Reason("FUORI-SQ", "Taglio fuori squadra", null, true, tagTub, tagLam);
        await Reason("SAL-INC", "Saldatura incompleta", "Cordone non completo o con porosità", true, sal);
        await Reason("VER-DIF", "Verniciatura difettosa", "Colature, bolle o copertura insufficiente", true, verPolSeg, finLeg);
        await Reason("ERR-MON", "Errore di montaggio", null, true, ass, col);
        await Reason("DANN-IMB", "Danneggiato nell'imballo", null, true, imbSeg);
        await Reason("ROT-UT", "Rottura utensile", "Non più usato: sostituito da «Misura errata»", false, freLeg);

        // ---- BOM -------------------------------------------------------------------------------------
        // The BOM rows are saved before the routings exist: the step links (operation numbers) are applied afterwards.
        var bomRows = new List<(BomItem row, Material parent, int? op)>();

        async Task Bom(Material parent, params (Material component, decimal quantity, int? op)[] items)
        {
            foreach (var (component, quantity, op) in items)
            {
                var row = new BomItem
                {
                    ParentMaterialId = parent.Id, ComponentMaterialId = component.Id, Quantity = quantity,
                    CreatedAt = now, UpdatedAt = now
                };
                await Save(bomRepo.AddAsync(row));
                bomRows.Add((row, parent, op));
            }
        }

        // The third value is the operation number of the parent routing where the component is consumed
        // (null = automatic: first start step).
        await Bom(tavolino, (strutturaTav, 1, null), (pianoTav, 1, null), (vit, 8, null), (pie, 4, null), (imb, 1, 30));
        await Bom(strutturaTav, (tubQ30, 3.2m, 10), (lamS235, 0.8m, 10), (filSal, 0.05m, 20), (disTag, 0.1m, 10), (verPol, 0.15m, 40));
        await Bom(pianoTav, (tavRov, 1, 10), (carAbr, 3, 20), (verAcq, 0.12m, 30));
        await Bom(sedia, (tubQ30, 4.5m, 10), (lamS235, 1.2m, 20), (tavRov, 0.5m, 60), (filSal, 0.08m, 30), (verPol, 0.2m, 50), (vit, 6, 60), (pie, 4, 60), (imb, 1, 80));
        await Bom(fiancataLib, (tubQ30, 3.6m, 10), (filSal, 0.06m, null), (disTag, 0.1m, 10), (verPol, 0.18m, 30));
        await Bom(libreria, (fiancataLib, 2, 30), (tavRov, 4, null), (verAcq, 0.3m, 20), (vit, 16, 30), (pie, 4, 30), (imb, 1, 50));

        // ---- Routings --------------------------------------------------------------------------------
        // Each step: (operation number, segment, column, row, setup override, run override, notes).
        // Column/row give the diagram position (left to right, parallel branches on separate rows).
        // The edges refer to operation numbers.
        var pendingEdges = new List<(RoutingStep, RoutingStep)>();
        var allSteps = new List<RoutingStep>();

        async Task Routing(Material material,
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
                allSteps.Add(step);
                await Save(stepRepo.AddAsync(step));
            }

            pendingEdges.AddRange(edges.Select(e => (byOp[e.from], byOp[e.to])));
        }

        await Routing(strutturaTav,
        [
            (10, tagTub, 0, 0, null, 3m, "Quattro gambe e due traverse da barra da 6 m"),
            (20, sal, 1, 0, null, null, null),
            (30, sba, 2, 0, null, null, null),
            (40, verPolSeg, 3, 0, 40m, null, "Lotto minimo di 10 pezzi per cottura in forno")
        ], (10, 20), (20, 30), (30, 40));

        await Routing(pianoTav,
        [
            (10, freLeg, 0, 0, null, null, "Profilo del bordo con fresa da 12 mm"),
            (20, lev, 1, 0, null, 8m, "Grana 120 poi 180"),
            (30, finLeg, 2, 0, null, null, "Due mani con asciugatura intermedia")
        ], (10, 20), (20, 30));

        await Routing(tavolino,
        [
            (10, ass, 0, 0, null, 10m, "Fissaggio del piano con 8 viti M6"),
            (20, col, 1, 0, null, null, null),
            (30, imbSeg, 2, 0, null, null, null)
        ], (10, 20), (20, 30));

        await Routing(sedia,
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

        // Intermediate with its own routing, consumed by the bookshelf at its third step.
        await Routing(fiancataLib,
        [
            (10, tagTub, 0, 0, null, 4m, "Due montanti e quattro traversi"),
            (20, sal, 1, 0, null, null, null),
            (30, verPolSeg, 2, 0, 40m, null, "Lotto minimo di 10 pezzi per cottura in forno")
        ], (10, 20), (20, 30));

        // Finished good with four sequential steps: the side panels (child order) are needed only at assembly.
        await Routing(libreria,
        [
            (10, freLeg, 0, 0, null, 6m, "Quattro ripiani da tavola in rovere"),
            (20, finLeg, 1, 0, null, null, "Una mano di vernice acquosa per ripiano"),
            (30, ass, 2, 0, null, 14m, "Montaggio delle due fiancate e dei ripiani"),
            (40, col, 3, 0, null, null, "Controllo della planarità dei ripiani"),
            (50, imbSeg, 4, 0, null, null, null)
        ], (10, 20), (20, 30), (30, 40), (40, 50));

        foreach (var (pred, succ) in pendingEdges)
            await Save(dependencyRepo.AddAsync(new RoutingStepDependency { PredecessorStepId = pred.Id, SuccessorStepId = succ.Id }));

        // ---- BOM links: where each component is consumed ------------------------------------------------
        foreach (var (row, parent, op) in bomRows.Where(r => r.op is not null))
        {
            var step = allSteps.Single(x => x.MaterialId == parent.Id && x.OperationNumber == op);
            row.ConsumedInStepId = step.Id;
            await Save(bomRepo.UpdateAsync(row));
        }
    }

}
