using MicroMES.ApiFeatures.ProductionOrderFeature.Models;

namespace MicroMES.ApiFeatures.ProductionOrderFeature.Services;

/// <summary>Read-only overview of an order family: no transaction, repositories are reached through the loader.</summary>
public class ProductionOrderOverviewService(ProductionDataLoader loader)
{
    public async Task<ProductionOrderResult<ProductionOrderOverviewModel>> GetAsync(int id)
    {
        var data = await loader.LoadFamilyAsync(id);
        return data is null
            ? ProductionOrderResult<ProductionOrderOverviewModel>.NotFound("Ordine di produzione non trovato.")
            : ProductionOrderResult<ProductionOrderOverviewModel>.Ok(data.ToOverview(id));
    }
}
