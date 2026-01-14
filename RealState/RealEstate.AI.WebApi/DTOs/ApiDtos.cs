using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using RealEstate.AI.Core.Domain;

namespace RealEstate.AI.WebApi.DTOs
{
    public class IngestFileRequestDto
    {
        [Required]
        public string ContentType { get; set; }
    }

    public class ValuationRequestDto
    {
        // Either provide an existing PropertyId or inline payload
        public Guid? PropertyId { get; set; }
        public PropertyPayloadDto Property { get; set; }
    }

    public class PropertyPayloadDto
    {
        public string Address { get; set; }
        public int YearBuilt { get; set; }
        public decimal AreaSqFt { get; set; }
        public int Bedrooms { get; set; }
        public int Bathrooms { get; set; }
        public decimal? ListedPrice { get; set; }
        public string Description { get; set; }
    }

    public class ValidationHelper
    {
        public static bool TryValidate(object obj, out List<ValidationResult> results)
        {
            var ctx = new ValidationContext(obj);
            results = new List<ValidationResult>();
            return Validator.TryValidateObject(obj, ctx, results, true);
        }
    }
}
