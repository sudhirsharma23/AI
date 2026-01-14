using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using RealEstate.AI.Core.Domain;

namespace RealEstate.AI.Core.Interfaces
{
    public enum RankingCriterion
    {
        ExpectedRoi,
        RiskAdjustedReturn,
        ValueForMoney
    }

    public interface IPropertyDataSource
    {
        Task<IEnumerable<Property>> GetPropertiesAsync();
        Task<Property> GetPropertyByIdAsync(Guid id);
    }

    public interface IDataIngestionService
    {
        Task<IEnumerable<Property>> IngestAllAsync();
        Task<Property> IngestPropertyAsync(Property p);
        // Ingest properties from a file (e.g., CSV/JSON upload). Implementations should parse the stream.
        Task<IEnumerable<Property>> IngestFromFileAsync(System.IO.Stream fileStream, string contentType);
        // Trigger an ingestion from an external connector configured by id or name.
        Task<IEnumerable<Property>> IngestFromExternalAsync(string connectorId);
        // Ingest from file and return row-level report of successes and errors
        Task<IngestionResult> IngestFromFileWithReportAsync(System.IO.Stream fileStream, string contentType);
    }

    public interface IPropertyRepository
    {
        Task SaveAsync(Property property);
        Task<Property> GetByIdAsync(Guid id);
        Task<IEnumerable<Property>> ListAsync();
    }

    public interface INlpService
    {
        Task<IEnumerable<string>> ExtractKeyPhrases(string text);
        Task<decimal> DetectSentiment(string text);
    }

    public interface IImageAnalysisService
    {
        Task<decimal> AnalyzeConditionScore(byte[] imageBytes);
        Task<bool> DetectInconsistencies(string description, object imageAnalysisResult);
    }

    public interface IFeatureEngineeringService
    {
        Task<PropertyFeatureVector> BuildFeatureVectorAsync(Property property);
    }

    public interface IRegressionModelService
    {
        Task<decimal> PredictMarketValueAsync(PropertyFeatureVector features);
    }

    public interface IRankingModelService
    {
        Task<IList<RankedPropertyResult>> RankPropertiesAsync(IEnumerable<PropertyFeatureVector> properties, RankingCriterion criterion);
    }

    public interface IRiskDetectionService
    {
        Task<RiskAssessmentResult> AssessAsync(Property property, PropertyFeatureVector features);
    }

    // Valuation methodology interfaces
    public interface ISalesComparisonValuationService
    {
        Task<ValuationResult> CalculateAsync(Property property, IEnumerable<Property> comps);
    }

    public interface IIncomeValuationService
    {
        Task<ValuationResult> CalculateAsync(Property property, decimal noi, decimal capRate);
    }

    public interface ICostValuationService
    {
        Task<ValuationResult> CalculateAsync(Property property, decimal replacementCost, decimal depreciationFactor);
    }
}
