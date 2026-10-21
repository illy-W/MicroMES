using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using MicroMES.ApiCore.Persistence.Entities;
using MicroMES.ApiFeatures.BomFeature.Models;
using MicroMES.ApiFeatures.EquipmentFeature.Models;
using MicroMES.ApiFeatures.MaterialFeature.Models;
using MicroMES.ApiFeatures.PlanningFeature.Models;
using MicroMES.ApiFeatures.ProcessSegmentFeature.Models;
using MicroMES.ApiFeatures.ProductionOrderFeature.Models;
using MicroMES.ApiFeatures.RoutingFeature.Models;
using MicroMES.ApiFeatures.ScrapFeature.Models;

namespace MicroMES.ApiTests.Integration.Shared;

/// <summary>Status, deserialized body (when the call succeeded) and raw body (problem details text on failure).</summary>
public sealed record ApiResult<T>(HttpStatusCode Status, T? Body, string Raw)
{
    public bool IsSuccess => (int)Status is >= 200 and < 300;

    /// <summary>The body of a successful call; fails the test with the server answer otherwise.</summary>
    public T Ok => IsSuccess && Body is not null
        ? Body
        : throw new InvalidOperationException($"Unexpected {(int)Status} {Status}: {Raw}");
}

/// <summary>Thin typed wrapper over the API HTTP surface, plus the builders of the small scenarios used by the tests.</summary>
public sealed class ApiClient(HttpClient http)
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    // ---- generic -------------------------------------------------------------------------------------

    public async Task<ApiResult<T>> SendAsync<T>(HttpMethod method, string url, object? body = null)
    {
        using var request = new HttpRequestMessage(method, url);
        if (body is not null) request.Content = JsonContent.Create(body, options: Json);
        else if (method == HttpMethod.Post) request.Content = JsonContent.Create(new { }, options: Json);

        using var response = await http.SendAsync(request);
        var raw = await response.Content.ReadAsStringAsync();
        T? parsed = default;
        if (response.IsSuccessStatusCode && !string.IsNullOrWhiteSpace(raw))
            parsed = JsonSerializer.Deserialize<T>(raw, Json);
        return new ApiResult<T>(response.StatusCode, parsed, raw);
    }

    public Task<ApiResult<T>> GetAsync<T>(string url) => SendAsync<T>(HttpMethod.Get, url);
    public Task<ApiResult<T>> PostAsync<T>(string url, object? body = null) => SendAsync<T>(HttpMethod.Post, url, body);
    public Task<ApiResult<T>> PutAsync<T>(string url, object body) => SendAsync<T>(HttpMethod.Put, url, body);
    public Task<ApiResult<object>> DeleteAsync(string url) => SendAsync<object>(HttpMethod.Delete, url);

    // ---- master data ---------------------------------------------------------------------------------

    public Task<ApiResult<MaterialModel>> CreateMaterialRawAsync(string code, MaterialType type, string? name = null)
        => PostAsync<MaterialModel>("/api/materials", new CreateMaterialRequest
        {
            Code = code, Name = name ?? $"Material {code}", Type = type, UnitOfMeasure = UnitOfMeasure.Piece
        });

    public async Task<MaterialModel> CreateMaterialAsync(string code, MaterialType type)
        => (await CreateMaterialRawAsync(code, type)).Ok;

    public async Task<ProcessSegmentModel> CreateSegmentAsync(string code, decimal setup = 0, decimal run = 0)
        => (await PostAsync<ProcessSegmentModel>("/api/process-segments", new CreateProcessSegmentRequest
        {
            Code = code, Name = $"Segment {code}", SetupMinutes = setup, RunMinutesPerUnit = run
        })).Ok;

    public Task<ApiResult<EquipmentModel>> CreateEquipmentRawAsync(string code, params int[] segmentIds)
        => PostAsync<EquipmentModel>("/api/equipment",
            new CreateEquipmentRequest { Code = code, Name = $"Macchina {code}", ProcessSegmentIds = segmentIds.ToList() });

    public async Task<EquipmentModel> CreateEquipmentAsync(string code, params int[] segmentIds)
        => (await CreateEquipmentRawAsync(code, segmentIds)).Ok;

    public Task<ApiResult<BomItemModel>> AddBomItemAsync(int parentId, int componentId, decimal quantity, int? consumedInStepId = null)
        => PostAsync<BomItemModel>($"/api/materials/{parentId}/bom/items",
            new CreateBomItemRequest { ComponentMaterialId = componentId, Quantity = quantity, ConsumedInStepId = consumedInStepId });

    public Task<ApiResult<RoutingModel>> PutRoutingAsync(int materialId, UpdateRoutingRequest request)
        => PutAsync<RoutingModel>($"/api/materials/{materialId}/routing", request);

    /// <summary>Linear routing: one step per segment with operation numbers 10, 20, 30...</summary>
    public async Task<RoutingModel> PutLinearRoutingAsync(int materialId, params int[] segmentIds)
    {
        var steps = segmentIds.Select((segment, i) => new UpdateRoutingStepRequest
        {
            Key = $"s{i}", ProcessSegmentId = segment, OperationNumber = (i + 1) * 10
        }).ToList();
        var dependencies = Enumerable.Range(0, segmentIds.Length - 1)
            .Select(i => new UpdateRoutingDependencyRequest { PredecessorKey = $"s{i}", SuccessorKey = $"s{i + 1}" })
            .ToList();
        return (await PutRoutingAsync(materialId, new UpdateRoutingRequest { Steps = steps, Dependencies = dependencies })).Ok;
    }

    // ---- planning -------------------------------------------------------------------------------------

    public Task<ApiResult<PlanningPreviewModel>> PreviewPlanAsync(PlanningScope scope, int? orderId = null)
        => PostAsync<PlanningPreviewModel>("/api/planning/preview", new PlanningRequest { Scope = scope, ProductionOrderId = orderId });

    public Task<ApiResult<PlanningApplyModel>> ApplyPlanAsync(PlanningScope scope, int? orderId = null, string? previewToken = null)
        => PostAsync<PlanningApplyModel>("/api/planning/apply",
            new PlanningRequest { Scope = scope, ProductionOrderId = orderId, PreviewToken = previewToken });

    public Task<ApiResult<RemovePlanModel>> RemovePlanAsync(int orderId)
        => SendAsync<RemovePlanModel>(HttpMethod.Delete, $"/api/planning/orders/{orderId}");

    public Task<ApiResult<PlanAlertsModel>> GetPlanAlertsAsync() => GetAsync<PlanAlertsModel>("/api/planning/alerts");

    // ---- production ----------------------------------------------------------------------------------

    public async Task<ProductionOrderDetailModel> CreateOrderAsync(int materialId, decimal quantity)
        => (await PostAsync<ProductionOrderDetailModel>("/api/production-orders",
            new CreateProductionOrderRequest { MaterialId = materialId, Quantity = quantity })).Ok;

    public Task<ApiResult<ProductionOrderDetailModel>> ReleaseAsync(int orderId)
        => PostAsync<ProductionOrderDetailModel>($"/api/production-orders/{orderId}/release");

    public Task<ApiResult<ProductionOrderDetailModel>> CancelAsync(int orderId)
        => PostAsync<ProductionOrderDetailModel>($"/api/production-orders/{orderId}/cancel");

    public Task<ApiResult<ProductionOrderDetailModel>> GetOrderAsync(int orderId)
        => GetAsync<ProductionOrderDetailModel>($"/api/production-orders/{orderId}");

    public Task<ApiResult<ProductionOrderOverviewModel>> GetOrderOverviewAsync(int orderId)
        => GetAsync<ProductionOrderOverviewModel>($"/api/production-orders/{orderId}/overview");

    public Task<ApiResult<List<ProductionOrderModel>>> GetOrdersAsync(bool includeChildren = true)
        => GetAsync<List<ProductionOrderModel>>($"/api/production-orders?includeChildren={includeChildren.ToString().ToLowerInvariant()}");

    public Task<ApiResult<List<WorkOrderModel>>> GetWorkOrdersAsync(int? productionOrderId = null)
        => GetAsync<List<WorkOrderModel>>("/api/work-orders" + (productionOrderId is null ? "" : $"?productionOrderId={productionOrderId}"));

    public Task<ApiResult<WorkOrderDetailModel>> GetWorkOrderAsync(int id)
        => GetAsync<WorkOrderDetailModel>($"/api/work-orders/{id}");

    /// <summary>Without a machine the request has no body (as the old client).</summary>
    public Task<ApiResult<WorkOrderDetailModel>> StartAsync(int workOrderId, int? equipmentId = null)
        => PostAsync<WorkOrderDetailModel>($"/api/work-orders/{workOrderId}/start",
            equipmentId is null ? null : new OpenSessionRequest { EquipmentId = equipmentId });

    public Task<ApiResult<WorkOrderDetailModel>> PauseAsync(int workOrderId, decimal good = 0, decimal scrap = 0, string? notes = null)
        => PostAsync<WorkOrderDetailModel>($"/api/work-orders/{workOrderId}/pause",
            new CloseSessionRequest { GoodQuantity = good, ScrapQuantity = scrap, Notes = notes });

    public Task<ApiResult<WorkOrderDetailModel>> ResumeAsync(int workOrderId, int? equipmentId = null)
        => PostAsync<WorkOrderDetailModel>($"/api/work-orders/{workOrderId}/resume",
            equipmentId is null ? null : new OpenSessionRequest { EquipmentId = equipmentId });

    public Task<ApiResult<WorkOrderDetailModel>> StopAsync(int workOrderId, decimal good = 0, decimal scrap = 0, string? notes = null)
        => PostAsync<WorkOrderDetailModel>($"/api/work-orders/{workOrderId}/stop",
            new CloseSessionRequest { GoodQuantity = good, ScrapQuantity = scrap, Notes = notes });

    // ---- scrap ---------------------------------------------------------------------------------------

    public Task<ApiResult<ScrapReasonModel>> CreateScrapReasonRawAsync(string code, params int[] segmentIds)
        => PostAsync<ScrapReasonModel>("/api/scrap-reasons",
            new CreateScrapReasonRequest { Code = code, Name = $"Motivo {code}", ProcessSegmentIds = segmentIds.ToList() });

    public async Task<ScrapReasonModel> CreateScrapReasonAsync(string code, params int[] segmentIds)
        => (await CreateScrapReasonRawAsync(code, segmentIds)).Ok;

    public Task<ApiResult<WorkOrderDetailModel>> StopWithScrapAsync(int workOrderId, decimal good, decimal scrap, params ScrapEntryRequest[] entries)
        => PostAsync<WorkOrderDetailModel>($"/api/work-orders/{workOrderId}/stop",
            new CloseSessionRequest { GoodQuantity = good, ScrapQuantity = scrap, ScrapEntries = entries.ToList() });

    public Task<ApiResult<WorkOrderDetailModel>> CorrectAsync(int workOrderId, int sessionId, decimal good, decimal scrap, string notes, params ScrapEntryRequest[] entries)
        => PostAsync<WorkOrderDetailModel>($"/api/work-orders/{workOrderId}/responses/{sessionId}/corrections",
            new CorrectSessionRequest { GoodQuantity = good, ScrapQuantity = scrap, Notes = notes, ScrapEntries = entries.ToList() });

    public Task<ApiResult<ScrapReportModel>> GetScrapReportAsync(string query = "")
        => GetAsync<ScrapReportModel>("/api/scrap/report" + query);
}
