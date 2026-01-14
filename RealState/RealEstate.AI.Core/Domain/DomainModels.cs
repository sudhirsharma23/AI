using System;
using System.Collections.Generic;

namespace RealEstate.AI.Core.Domain
{
    // Basic domain models
    public class Property
    {
        public Guid Id { get; set; }
        public string Address { get; set; }
        public int YearBuilt { get; set; }
        public decimal AreaSqFt { get; set; }
        public int Bedrooms { get; set; }
        public int Bathrooms { get; set; }
        public decimal? ListedPrice { get; set; }
        public string Description { get; set; }
        public IList<byte[]> Photos { get; set; } = new List<byte[]>();
    }

    public class TaxRecord
    {
        public Guid PropertyId { get; set; }
        public decimal AssessedValue { get; set; }
        public string ParcelNumber { get; set; }
    }

    public class MlsListing
    {
        public Guid PropertyId { get; set; }
        public decimal ListPrice { get; set; }
        public DateTime ListedDate { get; set; }
        public string ListingAgent { get; set; }
    }

    public class EconomicIndicator
    {
        public string Name { get; set; }
        public decimal Value { get; set; }
        public DateTime Date { get; set; }
    }

    // Feature vector DTO for ML models
    public class PropertyFeatureVector
    {
        public Guid PropertyId { get; set; }
        public decimal AreaSqFt { get; set; }
        public int YearBuilt { get; set; }
        public int Bedrooms { get; set; }
        public int Bathrooms { get; set; }
        public decimal ListedPrice { get; set; }
        public decimal AssessedValue { get; set; }
        public decimal AvgEconomicIndex { get; set; }
        public decimal DescriptionSentiment { get; set; }
        public IList<string> KeyPhrases { get; set; } = new List<string>();
        public decimal ImageConditionScore { get; set; }
    }

    public class ValuationResult
    {
        public decimal EstimatedValue { get; set; }
        public string Methodology { get; set; }
        public string Notes { get; set; }
    }

    public class ValuationSummary
    {
        public Guid PropertyId { get; set; }
        public decimal AiEstimate { get; set; }
        public ValuationResult SalesComparison { get; set; }
        public ValuationResult IncomeApproach { get; set; }
        public ValuationResult CostApproach { get; set; }
        public IList<string> Notes { get; set; } = new List<string>();
    }

    public class RiskAssessmentResult
    {
        public Guid PropertyId { get; set; }
        public double RiskScore { get; set; }
        public IList<string> RiskFlags { get; set; } = new List<string>();
        public IList<string> SuggestedActions { get; set; } = new List<string>();
    }

    public class RankedPropertyResult
    {
        public Guid PropertyId { get; set; }
        public decimal Score { get; set; }
        public decimal EstimatedValue { get; set; }
    }

    // Ingestion result to report successes and row-level errors
    public class IngestionResult
    {
        public IList<Property> Saved { get; set; } = new List<Property>();
        public IList<string> Errors { get; set; } = new List<string>();
    }
}
