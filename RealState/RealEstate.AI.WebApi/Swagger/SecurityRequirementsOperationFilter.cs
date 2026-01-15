using System.Collections.Generic;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace RealEstate.AI.WebApi.Swagger
{
    // Adds Security Requirements to selected operations based on route path patterns.
    public class CustomSecurityRequirementsOperationFilter : IOperationFilter
    {
        public void Apply(OpenApiOperation operation, OperationFilterContext context)
        {
            if (operation == null || context == null || context.ApiDescription == null) return;

            var path = context.ApiDescription.RelativePath?.Split('?')[0]?.TrimEnd('/');
            if (string.IsNullOrEmpty(path)) return;

            path = path.StartsWith("/") ? path.Substring(1) : path;

            // Determine security requirements for endpoints
            // Require JWT Bearer for valuation and risk endpoints
            if (path.StartsWith("api/valuation") || path.StartsWith("api/risk"))
            {
                operation.Security ??= new List<OpenApiSecurityRequirement>();

                var bearerScheme = new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
                };

                operation.Security.Add(new OpenApiSecurityRequirement
                {
                    [ bearerScheme ] = new List<string>()
                });
            }

            // Require ApiKey for external ingestion endpoints
            if (path.StartsWith("api/properties/ingest/external") || path.StartsWith("api/properties/ingest/upload"))
            {
                operation.Security ??= new List<OpenApiSecurityRequirement>();

                var apiKeyScheme = new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "ApiKey" }
                };

                operation.Security.Add(new OpenApiSecurityRequirement
                {
                    [ apiKeyScheme ] = new List<string>()
                });
            }
        }
    }
}
