using illyW.Framework.Core.RepositoryPattern;
using illyW.Framework.Core.ResultPattern;
using MicroMES.ApiCore.Persistence.Entities;
using MicroMES.ApiFeatures.EquipmentFeature.Repositories;
using MicroMES.ApiFeatures.MaterialFeature.Repositories;
using MicroMES.ApiFeatures.RoutingFeature.Repositories;
using MicroMES.ApiFeatures.ToolingFeature.Repositories;

namespace MicroMES.ApiFeatures.ToolingFeature.Seeding;

/// <summary>
/// Sample setup attributes and toolings for local development, linked to the routing steps of the sample cycles
/// (steps and machines are looked up by code: what is missing is skipped). Runs when the Toolings table is empty, so it also works
/// on a database that is already seeded. Only repositories of the unit of work, one transaction.
/// <para>
/// No backfill: work orders already released do not get tooling/setup snapshots (they are taken at release). On a fresh database
/// this seeder runs before the order seeder, so the released orders go through the normal release path and have their snapshots.
/// </para>
/// </summary>
public class ToolingDevelopmentSeeder(IUnitOfWork unitOfWork)
{
    private static async Task<T> Save<T>(Task<IResult<T>> add) where T : class
    {
        var result = await add;
        if (!result.IsSuccessful || result.Data is null)
            throw new InvalidOperationException($"Sample data not saved: {string.Join("; ", result.Errors)}");
        return result.Data;
    }

    public async Task SeedAsync()
    {
        var toolings = unitOfWork.GetRepository<IToolingRepository>();
        await foreach (var _ in toolings.FetchAsync(x => true)) return;

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

    private async Task SeedCoreAsync()
    {
        var now = DateTime.UtcNow;
        var attributes = unitOfWork.GetRepository<ISetupAttributeRepository>();
        var toolings = unitOfWork.GetRepository<IToolingRepository>();
        var toolingEquipment = unitOfWork.GetRepository<IToolingEquipmentRepository>();
        var equipmentRepo = unitOfWork.GetRepository<IEquipmentRepository>();
        var materialRepo = unitOfWork.GetRepository<IMaterialRepository>();
        var stepRepo = unitOfWork.GetRepository<IRoutingStepRepository>();
        var stepTooling = unitOfWork.GetRepository<IRoutingStepToolingRepository>();
        var stepValues = unitOfWork.GetRepository<IRoutingStepSetupValueRepository>();

        var machines = new Dictionary<string, int>();
        await foreach (var e in equipmentRepo.FetchAsync(x => true)) machines[e.Code] = e.Id;
        var materials = new Dictionary<int, string>();
        await foreach (var m in materialRepo.FetchAsync(x => true)) materials[m.Id] = m.Code;
        var steps = new Dictionary<(string Material, int Op), RoutingStep>();
        await foreach (var s in stepRepo.FetchAsync(x => true))
            if (materials.TryGetValue(s.MaterialId, out var code)) steps[(code, s.OperationNumber)] = s;

        async Task<SetupAttribute> Attribute(string code, string name, int minutes)
            => await Save(attributes.AddAsync(new SetupAttribute { Code = code, Name = name, ChangeMinutes = minutes, IsActive = true, CreatedAt = now, UpdatedAt = now }));

        var colore = await Attribute("COLORE", "Colore", 5);
        var materiale = await Attribute("MATERIALE", "Materiale", 8);

        async Task<Tooling> Tool(string code, string name, int quantity, int setup, params string[] machineCodes)
        {
            var tool = await Save(toolings.AddAsync(new Tooling
            {
                Code = code, Name = name, Quantity = quantity, SetupMinutes = setup, IsActive = true, CreatedAt = now, UpdatedAt = now
            }));
            foreach (var machine in machineCodes.Where(machines.ContainsKey))
                await Save(toolingEquipment.AddAsync(new ToolingEquipment { ToolingId = tool.Id, EquipmentId = machines[machine] }));
            return tool;
        }

        var segaA = await Tool("SEGA-TUB-A", "Sega per tubolare A", 1, 6, "TAG-TUB-1", "TAG-TUB-2");
        var segaB = await Tool("SEGA-TUB-B", "Sega per tubolare B", 1, 6, "TAG-TUB-2");
        var dima = await Tool("DIMA-SALD-1", "Dima di saldatura telaio", 1, 10, "SAL-1", "SAL-2");
        var stampo = await Tool("STAMPO-FIN-1", "Stampo di finitura", 2, 4);

        async Task Link(string material, int op, Tooling tool)
        {
            if (!steps.TryGetValue((material, op), out var step)) return;
            await Save(stepTooling.AddAsync(new RoutingStepTooling { RoutingStepId = step.Id, ToolingId = tool.Id }));
        }

        async Task Value(string material, int op, SetupAttribute attribute, string value)
        {
            if (!steps.TryGetValue((material, op), out var step)) return;
            await Save(stepValues.AddAsync(new RoutingStepSetupValue { RoutingStepId = step.Id, SetupAttributeId = attribute.Id, Value = value }));
        }

        // Tube cutting
        await Link("SL-TAV-STR", 10, segaA);
        await Link("PF-SED-01", 10, segaA);
        await Link("SL-LIB-FIA", 10, segaB);
        // Sheet cutting
        await Link("PF-SED-01", 20, stampo);
        // Welding
        await Link("SL-TAV-STR", 20, dima);
        await Link("PF-SED-01", 30, dima);
        await Link("SL-LIB-FIA", 20, dima);
        // Powder coating: color changes cost time
        await Value("SL-TAV-STR", 40, colore, "rosso");
        await Value("PF-SED-01", 50, colore, "bianco");
        await Value("SL-LIB-FIA", 30, colore, "rosso");
        await Value("SL-TAV-STR", 40, materiale, "acciaio");
        await Value("SL-LIB-FIA", 30, materiale, "acciaio");
        // Wood finishing
        await Value("SL-TAV-PIA", 30, colore, "naturale");
        await Value("SL-TAV-PIA", 30, materiale, "rovere");
        await Value("PF-LIB-01", 20, colore, "naturale");
    }
}
