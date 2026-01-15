using System;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace RealEstate.AI.WebApi.Swagger
{
    public class ResponseExamplesOperationFilter : IOperationFilter
    {
        public void Apply(OpenApiOperation operation, OperationFilterContext context)
        {
            if (operation == null || context == null || context.ApiDescription == null) return;

            var path = context.ApiDescription.RelativePath?.Split('?')[0]?.TrimEnd('/');
            if (string.IsNullOrEmpty(path)) return;

            // Normalize path
            path = path.StartsWith("/") ? path.Substring(1) : path;

            switch (path.ToLowerInvariant())
            {
                case "api/properties/ingest":
                    SetJsonExample(operation, new OpenApiArray
                    {
                        new OpenApiObject
                        {
                            ["id"] = new OpenApiString(Guid.Empty.ToString()),
                            ["address"] = new OpenApiString("123 Main St"),
                            ["areaSqFt"] = new OpenApiDouble(1800),
                            ["yearBuilt"] = new OpenApiInteger(1995),
                            ["bedrooms"] = new OpenApiInteger(3),
                            ["bathrooms"] = new OpenApiInteger(2),
                            ["listedPrice"] = new OpenApiDouble(350000)
                        }
                    });
                    break;

                case "api/properties/ingest/upload":
                    SetJsonExample(operation, new OpenApiObject
                    {
                        ["saved"] = new OpenApiArray
                        {
                            new OpenApiObject
                            {
                                ["id"] = new OpenApiString(Guid.NewGuid().ToString()),
                                ["address"] = new OpenApiString("10 Test St"),
                                ["areaSqFt"] = new OpenApiDouble(1200)
                            }
                        },
                        ["errors"] = new OpenApiArray()
                    });
                    break;

                case "api/properties/ingest/external":
                    SetJsonExample(operation, new OpenApiArray
                    {
                        new OpenApiObject
                        {
                            ["id"] = new OpenApiString(Guid.NewGuid().ToString()),
                            ["address"] = new OpenApiString("External St"),
                            ["areaSqFt"] = new OpenApiDouble(1000)
                        }
                    });
                    break;

                case "api/valuation/estimate":
                    SetJsonExample(operation, new OpenApiObject
                    {
                        ["propertyId"] = new OpenApiString(Guid.NewGuid().ToString()),
                        ["aiEstimate"] = new OpenApiDouble(320000),
                        ["salesComparison"] = new OpenApiObject
                        {
                            ["estimatedValue"] = new OpenApiDouble(310000),
                            ["methodology"] = new OpenApiString("SalesComparison"),
                            ["notes"] = new OpenApiString("Simple comp adjustment")
                        },
                        ["incomeApproach"] = new OpenApiObject
                        {
                            ["estimatedValue"] = new OpenApiDouble(400000),
                            ["methodology"] = new OpenApiString("Income"),
                            ["notes"] = new OpenApiString("NOI / cap rate")
                        },
                        ["costApproach"] = new OpenApiObject
                        {
                            ["estimatedValue"] = new OpenApiDouble(280000),
                            ["methodology"] = new OpenApiString("Cost"),
                            ["notes"] = new OpenApiString("Replacement cost minus depreciation")
                        },
                        ["notes"] = new OpenApiArray { new OpenApiString("AI model is a placeholder linear estimator.") }
                    });
                    break;

                case "api/valuation/rank":
                    SetJsonExample(operation, new OpenApiArray
                    {
                        new OpenApiObject
                        {
                            ["propertyId"] = new OpenApiString(Guid.NewGuid().ToString()),
                            ["score"] = new OpenApiDouble(1.23),
                            ["estimatedValue"] = new OpenApiDouble(250000)
                        }
                    });
                    break;

                case "api/risk/assess":
                    SetJsonExample(operation, new OpenApiObject
                    {
                        ["propertyId"] = new OpenApiString(Guid.NewGuid().ToString()),
                        ["riskScore"] = new OpenApiDouble(0.45),
                        ["riskFlags"] = new OpenApiArray { new OpenApiString("EXTREME_OUTLIER_PRICE_PER_SQFT") },
                        ["suggestedActions"] = new OpenApiArray { new OpenApiString("Manual review of listing price required.") }
                    });
                    break;
            }
        }

        private static void SetJsonExample(OpenApiOperation operation, IOpenApiAny example)
        {
            if (!operation.Responses.ContainsKey("200"))
            {
                operation.Responses["200"] = new OpenApiResponse { Description = "OK" };
            }

            if (!operation.Responses["200"].Content.ContainsKey("application/json"))
            {
                operation.Responses["200"].Content["application/json"] = new OpenApiMediaType();
            }

            operation.Responses["200"].Content["application/json"].Example = example;
        }
    }
}
