using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MCPServer.Tools
{
    /// <summary>
    /// Simple calculator tool to demonstrate MCP tool implementation
    /// </summary>
    public class CalculatorTool : IMcpTool
    {
        public string Name => "calculator";

        public string Description => "Performs basic arithmetic operations (add, subtract, multiply, divide)";

        public ToolInputSchema InputSchema => new()
        {
            Type = "object",
            Properties = new Dictionary<string, PropertySchema>
            {
                ["operation"] = new()
                {
                    Type = "string",
                    Description = "The operation to perform",
                    Enum = new List<string> { "add", "subtract", "multiply", "divide" }
                },
                ["a"] = new()
                {
                    Type = "number",
                    Description = "First number"
                },
                ["b"] = new()
                {
                    Type = "number",
                    Description = "Second number"
                }
            },
            Required = new List<string> { "operation", "a", "b" }
        };

        public Task<object> ExecuteAsync(Dictionary<string, object> parameters)
        {
            var operation = parameters["operation"].ToString();
            var a = Convert.ToDouble(parameters["a"].ToString());
            var b = Convert.ToDouble(parameters["b"].ToString());

            double result = operation.ToLowerInvariant() switch
            {
                "add" => a + b,
                "subtract" => a - b,
                "multiply" => a * b,
                "divide" => b != 0 ? a / b : throw new InvalidOperationException("Division by zero"),
                _ => throw new ArgumentException($"Unknown operation: {operation}")
            };

            return Task.FromResult<object>(new
            {
                operation,
                a,
                b,
                result,
                formula = $"{a} {GetSymbol(operation)} {b} = {result}"
            });
        }

        private string GetSymbol(string operation) => operation.ToLowerInvariant() switch
        {
            "add" => "+",
            "subtract" => "-",
            "multiply" => "×",
            "divide" => "÷",
            _ => "?"
        };
    }
}
