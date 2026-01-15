using System;
using System.Collections.Generic;
using RealEstate.AI.Core.Domain;

namespace RealEstate.AI.WebApi.DTOs
{
    /// <summary>Represents a property in API responses.</summary>
    public class PropertyDto
    {
        /// <summary>Unique identifier.</summary>
        public Guid Id { get; set; }
        /// <summary>Street address.</summary>
        public string Address { get; set; }
        /// <summary>Year built.</summary>
        public int YearBuilt { get; set; }
        /// <summary>Area in square feet.</summary>
        public decimal AreaSqFt { get; set; }
        /// <summary>Bedrooms count.</summary>
        public int Bedrooms { get; set; }
        /// <summary>Bathrooms count.</summary>
        public int Bathrooms { get; set; }
        /// <summary>Listed price if present.</summary>
        public decimal? ListedPrice { get; set; }
        /// <summary>Free-text description.</summary>
        public string Description { get; set; }

        public static PropertyDto From(Property p)
        {
            if (p == null) return null;
            return new PropertyDto
            {
                Id = p.Id,
                Address = p.Address,
                YearBuilt = p.YearBuilt,
                AreaSqFt = p.AreaSqFt,
                Bedrooms = p.Bedrooms,
                Bathrooms = p.Bathrooms,
                ListedPrice = p.ListedPrice,
                Description = p.Description
            };
        }
    }

    /// <summary>Valuation summary DTO used as response for valuation endpoint.</summary>
    public class ValuationSummaryDto
    {
        public Guid PropertyId { get; set; }
        public decimal AiEstimate { get; set; }
        public ValuationResultDto SalesComparison { get; set; }
        public ValuationResultDto IncomeApproach { get; set; }
        public ValuationResultDto CostApproach { get; set; }
        public IList<string> Notes { get; set; }

        public static ValuationSummaryDto From(RealEstate.AI.Core.Domain.ValuationSummary s)
        {
            if (s == null) return null;
            return new ValuationSummaryDto
            {
                PropertyId = s.PropertyId,
                AiEstimate = s.AiEstimate,
                SalesComparison = ValuationResultDto.From(s.SalesComparison),
                IncomeApproach = ValuationResultDto.From(s.IncomeApproach),
                CostApproach = ValuationResultDto.From(s.CostApproach),
                Notes = s.Notes
            };
        }
    }

    /// <summary>Simple valuation result DTO.</summary>
    public class ValuationResultDto
    {
        public decimal EstimatedValue { get; set; }
        public string Methodology { get; set; }
        public string Notes { get; set; }

        public static ValuationResultDto From(RealEstate.AI.Core.Domain.ValuationResult r)
        {
            if (r == null) return null;
            return new ValuationResultDto
            {
                EstimatedValue = r.EstimatedValue,
                Methodology = r.Methodology,
                Notes = r.Notes
            };
        }
    }

    /// <summary>Risk assessment DTO.</summary>
    public class RiskAssessmentDto
    {
        public Guid PropertyId { get; set; }
        public double RiskScore { get; set; }
        public IList<string> RiskFlags { get; set; }
        public IList<string> SuggestedActions { get; set; }

        public static RiskAssessmentDto From(RealEstate.AI.Core.Domain.RiskAssessmentResult r)
        {
            if (r == null) return null;
            return new RiskAssessmentDto
            {
                PropertyId = r.PropertyId,
                RiskScore = r.RiskScore,
                RiskFlags = r.RiskFlags,
                SuggestedActions = r.SuggestedActions
            };
        }
    }

    /// <summary>Ranked property DTO used by ranking endpoint.</summary>
    public class RankedPropertyDto
    {
        public Guid PropertyId { get; set; }
        public decimal Score { get; set; }
        public decimal EstimatedValue { get; set; }

        public static RankedPropertyDto From(RealEstate.AI.Core.Domain.RankedPropertyResult r)
        {
            if (r == null) return null;
            return new RankedPropertyDto
            {
                PropertyId = r.PropertyId,
                Score = r.Score,
                EstimatedValue = r.EstimatedValue
            };
        }
    }

    /// <summary>Response from ingestion endpoints including saved objects and parsing errors.</summary>
    public class IngestResponseDto
    {
        public IList<PropertyDto> Saved { get; set; } = new List<PropertyDto>();
        public IList<string> Errors { get; set; } = new List<string>();

        public static IngestResponseDto From(RealEstate.AI.Core.Domain.IngestionResult r)
        {
            if (r == null) return null;
            return new IngestResponseDto
            {
                Saved = r.Saved == null ? new List<PropertyDto>() : new List<PropertyDto>(System.Linq.Enumerable.Select(r.Saved, PropertyDto.From)),
                Errors = r.Errors
            };
        }
    }
}
