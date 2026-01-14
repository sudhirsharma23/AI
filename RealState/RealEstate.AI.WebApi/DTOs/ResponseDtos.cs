using System;
using System.Collections.Generic;
using RealEstate.AI.Core.Domain;

namespace RealEstate.AI.WebApi.DTOs
{
    public class PropertyDto
    {
        public Guid Id { get; set; }
        public string Address { get; set; }
        public int YearBuilt { get; set; }
        public decimal AreaSqFt { get; set; }
        public int Bedrooms { get; set; }
        public int Bathrooms { get; set; }
        public decimal? ListedPrice { get; set; }
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
