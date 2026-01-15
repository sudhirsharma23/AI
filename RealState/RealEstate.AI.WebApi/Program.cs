using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RealEstate.AI.Core.Domain;
using RealEstate.AI.Core.Interfaces;
using RealEstate.AI.Infrastructure.Data;
using RealEstate.AI.Infrastructure.Repositories;
using RealEstate.AI.Infrastructure.Services;
using RealEstate.AI.ML.Services;
using RealEstate.AI.ML.Valuation;
using RealEstate.AI.WebApi.DTOs;
using RealEstate.AI.WebApi.Swagger;

var builder = WebApplication.CreateBuilder(args);

// Register services
builder.Services.AddHttpClient();
// provide named/typed clients for services that accept HttpClient in ctor
builder.Services.AddHttpClient<ExternalConnectorService>();
builder.Services.AddHttpClient<AzureNlpService>();

builder.Services.AddSingleton<IPropertyDataSource, SimplePropertyDataSource>();
builder.Services.AddSingleton<IPropertyRepository, InMemoryPropertyRepository>();
builder.Services.AddScoped<ExternalConnectorService>();
builder.Services.AddScoped<IDataIngestionService, DataIngestionService>();
// prefer Azure NLP if env vars are set, otherwise fallback to stub
builder.Services.AddScoped<INlpService, AzureNlpService>();
builder.Services.AddScoped<IImageAnalysisService, ImageAnalysisServiceStub>();
builder.Services.AddScoped<IFeatureEngineeringService, FeatureEngineeringService>();
builder.Services.AddScoped<IRegressionModelService, RegressionModelService>();
builder.Services.AddScoped<IRankingModelService, RankingModelService>();
builder.Services.AddScoped<IRiskDetectionService, RiskDetectionService>();
builder.Services.AddScoped<ISalesComparisonValuationService, SalesComparisonValuationService>();
builder.Services.AddScoped<IIncomeValuationService, IncomeValuationService>();
builder.Services.AddScoped<ICostValuationService, CostValuationService>();
builder.Services.AddScoped<ValuationOrchestrator>();

// Swagger with operation filter for examples
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.OperationFilter<ResponseExamplesOperationFilter>();
});

var app = builder.Build();

// Expose Swagger UI for local testing
app.UseSwagger();
app.UseSwaggerUI();

app.MapGet("/", () => "RealEstate.AI Web API");

app.MapPost("/api/properties/ingest", async (IDataIngestionService ingestion) =>
{
    var items = await ingestion.IngestAllAsync();
    return Results.Ok(items.Select(RealEstate.AI.WebApi.DTOs.PropertyDto.From));
});

app.MapPost("/api/properties/ingest/upload", async (HttpRequest request, IDataIngestionService ingestion) =>
{
    if (!request.HasFormContentType) return Results.BadRequest(new { message = "Expected multipart/form-data" });

    var form = await request.ReadFormAsync();
    var file = form.Files.FirstOrDefault();
    if (file == null) return Results.BadRequest(new { message = "No file uploaded" });

    using var stream = file.OpenReadStream();
    var report = await ingestion.IngestFromFileWithReportAsync(stream, file.ContentType);
    var dto = IngestResponseDto.From(report);
    return Results.Ok(dto);
});

app.MapPost("/api/properties/ingest/external", async (string connectorUrl, IDataIngestionService ingestion) =>
{
    if (string.IsNullOrWhiteSpace(connectorUrl)) return Results.BadRequest(new { message = "connectorUrl is required" });
    var items = await ingestion.IngestFromExternalAsync(connectorUrl);
    return Results.Ok(items.Select(RealEstate.AI.WebApi.DTOs.PropertyDto.From));
});

app.MapPost("/api/valuation/estimate", async (ValuationRequestDto req, IFeatureEngineeringService fe, IRegressionModelService reg, ValuationOrchestrator orchestrator, IPropertyRepository repo) =>
{
    if (req == null) return Results.BadRequest();

    Property target = null;
    if (req.PropertyId.HasValue)
    {
        target = await repo.GetByIdAsync(req.PropertyId.Value);
        if (target == null) return Results.NotFound(new { message = "Property not found" });
    }
    else if (req.Property != null)
    {
        target = new Property
        {
            Id = Guid.NewGuid(),
            Address = req.Property.Address,
            AreaSqFt = req.Property.AreaSqFt,
            YearBuilt = req.Property.YearBuilt,
            Bedrooms = req.Property.Bedrooms,
            Bathrooms = req.Property.Bathrooms,
            ListedPrice = req.Property.ListedPrice,
            Description = req.Property.Description
        };
    }
    else
    {
        return Results.BadRequest(new { message = "No property provided" });
    }

    var features = await fe.BuildFeatureVectorAsync(target);
    var ai = await reg.PredictMarketValueAsync(features);
    var properties = await repo.ListAsync();
    var comps = properties.Where(p => p.Id != target.Id).ToList();
    var summary = await orchestrator.OrchestrateAsync(target, features, comps);
    return Results.Ok(ValuationSummaryDto.From(summary));
});

app.MapPost("/api/valuation/rank", async (IRankingModelService ranker, IFeatureEngineeringService fe, IPropertyRepository repo) =>
{
    var properties = await repo.ListAsync();
    var vectors = new System.Collections.Generic.List<PropertyFeatureVector>();
    foreach (var p in properties)
    {
        vectors.Add(await fe.BuildFeatureVectorAsync(p));
    }

    var ranked = await ranker.RankPropertiesAsync(vectors, RankingCriterion.ValueForMoney);
    return Results.Ok(ranked.Select(RealEstate.AI.WebApi.DTOs.RankedPropertyDto.From));
});

app.MapPost("/api/risk/assess", async (IRiskDetectionService riskService, IFeatureEngineeringService fe, IPropertyRepository repo) =>
{
    var properties = await repo.ListAsync();
    var first = System.Linq.Enumerable.FirstOrDefault(properties);
    if (first == null) return Results.NotFound(new { message = "No properties available" });

    var features = await fe.BuildFeatureVectorAsync(first);
    var risk = await riskService.AssessAsync(first, features);
    return Results.Ok(RiskAssessmentDto.From(risk));
});

app.Run();

public partial class Program { }
