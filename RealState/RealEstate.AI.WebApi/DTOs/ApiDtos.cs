using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using RealEstate.AI.Core.Domain;

namespace RealEstate.AI.WebApi.DTOs
{
    /// <summary>
    /// Request DTO for ingesting files or external connectors.
    /// </summary>
    public class IngestFileRequestDto
    {
        /// <summary>
        /// Content type or connector URL depending on endpoint usage.
        /// </summary>
        [Required]
        public string ContentType { get; set; }
    }

    /// <summary>
    /// Request DTO for valuation endpoint.
    /// </summary>
    public class ValuationRequestDto
    {
        /// <summary>
        /// Optional existing property id to value from repository.
        /// </summary>
        public Guid? PropertyId { get; set; }

        /// <summary>
        /// Inline property payload to value without storing.
        /// </summary>
        public PropertyPayloadDto Property { get; set; }
    }

    /// <summary>
    /// Inline property payload used by valuation requests.
    /// </summary>
    public class PropertyPayloadDto
    {
        /// <summary>Street address.</summary>
        public string Address { get; set; }
        /// <summary>Year the property was built.</summary>
        public int YearBuilt { get; set; }
        /// <summary>Area in square feet.</summary>
        public decimal AreaSqFt { get; set; }
        /// <summary>Number of bedrooms.</summary>
        public int Bedrooms { get; set; }
        /// <summary>Number of bathrooms.</summary>
        public int Bathrooms { get; set; }
        /// <summary>Optional listed price.</summary>
        public decimal? ListedPrice { get; set; }
        /// <summary>Free-text description.</summary>
        public string Description { get; set; }
    }

    /// <summary>
    /// Helper for runtime validation.
    /// </summary>
    public class ValidationHelper
    {
        /// <summary>
        /// Validates an object using DataAnnotations.
        /// </summary>
        public static bool TryValidate(object obj, out List<ValidationResult> results)
        {
            var ctx = new ValidationContext(obj);
            results = new List<ValidationResult>();
            return Validator.TryValidateObject(obj, ctx, results, true);
        }
    }
}
