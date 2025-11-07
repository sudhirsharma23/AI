using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using MCPServer.Tools;

namespace MCPServer.Core
{
    /// <summary>
    /// Core MCP Server implementation following the Model Context Protocol specification
    /// </summary>
    public class McpServer
    {
        private readonly Dictionary<string, IMcpTool> _tools = new();
        private bool _isRunning;

        public void RegisterTool(IMcpTool tool)
        {
            _tools[tool.Name] = tool;
            Console.WriteLine($"  [+] Registered tool: {tool.Name}");
            Console.WriteLine($"      Description: {tool.Description}");
        }

        public int GetToolCount() => _tools.Count;

        public async Task StartAsync()
        {
            _isRunning = true;
            Console.WriteLine("MCP Server is running...");
            Console.WriteLine();
            Console.WriteLine("Available Commands:");
            Console.WriteLine("  list    - List all available tools");
            Console.WriteLine("  call    - Call a tool");
            Console.WriteLine("  demo    - Run demo scenarios");
            Console.WriteLine("  help    - Show help");
            Console.WriteLine("  exit    - Exit server");
            Console.WriteLine();

            while (_isRunning)
            {
                Console.Write("mcp> ");
                var input = Console.ReadLine()?.Trim();

                if (string.IsNullOrEmpty(input))
                    continue;

                var parts = input.Split(' ', 2);
                var command = parts[0].ToLowerInvariant();
                var args = parts.Length > 1 ? parts[1] : string.Empty;

                try
                {
                    await HandleCommandAsync(command, args);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error: {ex.Message}");
                }

                Console.WriteLine();
            }
        }

        private async Task HandleCommandAsync(string command, string args)
        {
            switch (command)
            {
                case "list":
                    ListTools();
                    break;

                case "call":
                    await CallToolAsync(args);
                    break;

                case "demo":
                    await RunDemoAsync();
                    break;

                case "help":
                    ShowHelp();
                    break;

                case "exit":
                case "quit":
                    _isRunning = false;
                    Console.WriteLine("Shutting down MCP Server...");
                    break;

                default:
                    Console.WriteLine($"Unknown command: {command}");
                    Console.WriteLine("Type 'help' for available commands");
                    break;
            }
        }

        private void ListTools()
        {
            Console.WriteLine($"Available Tools ({_tools.Count}):");
            Console.WriteLine();

            foreach (var tool in _tools.Values)
            {
                Console.WriteLine($"Tool: {tool.Name}");
                Console.WriteLine($"  Description: {tool.Description}");
                Console.WriteLine($"  Parameters:");

                if (tool.InputSchema.Properties.Count == 0)
                {
                    Console.WriteLine("    (none)");
                }
                else
                {
                    foreach (var prop in tool.InputSchema.Properties)
                    {
                        var required = tool.InputSchema.Required.Contains(prop.Key) ? " (required)" : "";
                        Console.WriteLine($"    - {prop.Key}: {prop.Value.Type}{required}");
                        if (!string.IsNullOrEmpty(prop.Value.Description))
                            Console.WriteLine($"      {prop.Value.Description}");
                    }
                }
                Console.WriteLine();
            }
        }

        private async Task CallToolAsync(string args)
        {
            if (string.IsNullOrEmpty(args))
            {
                Console.WriteLine("Usage: call <tool_name> [json_params]");
                Console.WriteLine("Example: call calculator {\"operation\":\"add\",\"a\":5,\"b\":3}");
                return;
            }

            var parts = args.Split(' ', 2);
            var toolName = parts[0];
            var paramsJson = parts.Length > 1 ? parts[1] : "{}";

            if (!_tools.TryGetValue(toolName, out var tool))
            {
                Console.WriteLine($"Tool not found: {toolName}");
                Console.WriteLine("Use 'list' to see available tools");
                return;
            }

            Console.WriteLine($"Calling tool: {toolName}");
            Console.WriteLine($"Parameters: {paramsJson}");
            Console.WriteLine();

            try
            {
                var parameters = JsonSerializer.Deserialize<Dictionary<string, object>>(paramsJson);
                var result = await tool.ExecuteAsync(parameters);

                Console.WriteLine("Result:");
                Console.WriteLine("-------");
                var resultJson = JsonSerializer.Serialize(result, new JsonSerializerOptions
                {
                    WriteIndented = true
                });
                Console.WriteLine(resultJson);
            }
            catch (JsonException ex)
            {
                Console.WriteLine($"Invalid JSON parameters: {ex.Message}");
            }
        }

        private async Task RunDemoAsync()
        {
            Console.WriteLine("Running MCP Demo Scenarios...");
            Console.WriteLine();

            // Demo 1: Calculator
            Console.WriteLine("=== Demo 1: Calculator Tool ===");
            await CallToolAsync("calculator {\"operation\":\"add\",\"a\":5,\"b\":3}");
            Console.WriteLine();

            // Demo 2: List S3 Files
            Console.WriteLine("=== Demo 2: S3 File List ===");
            await CallToolAsync("s3_list_files {\"bucket\":\"testbucket-sudhir-bsi1\",\"prefix\":\"uploads/\"}");
            Console.WriteLine();

            // Demo 3: Textract Status
            Console.WriteLine("=== Demo 3: Textract Status ===");
            await CallToolAsync("textract_status {}");
            Console.WriteLine();

            Console.WriteLine("Demo completed!");
        }

        private void ShowHelp()
        {
            Console.WriteLine("MCP Server Help");
            Console.WriteLine("===============");
            Console.WriteLine();
            Console.WriteLine("Commands:");
            Console.WriteLine("  list");
            Console.WriteLine("Lists all available tools with their parameters");
            Console.WriteLine();
            Console.WriteLine("  call <tool_name> [json_params]");
            Console.WriteLine("    Calls a tool with optional JSON parameters");
            Console.WriteLine("    Example: call calculator {\"operation\":\"add\",\"a\":5,\"b\":3}");
            Console.WriteLine();
            Console.WriteLine("  demo");
            Console.WriteLine("    Runs demo scenarios showing how tools work");
            Console.WriteLine();
            Console.WriteLine("  help");
            Console.WriteLine("    Shows this help message");
            Console.WriteLine();
            Console.WriteLine("  exit / quit");
            Console.WriteLine("    Shuts down the MCP server");
            Console.WriteLine();
            Console.WriteLine("MCP Concepts:");
            Console.WriteLine("-------------");
            Console.WriteLine("Tool: A function that can be called by AI clients");
            Console.WriteLine("Schema: Defines the input parameters a tool accepts");
            Console.WriteLine("Resource: Data that can be read by AI clients");
            Console.WriteLine("Prompt: Pre-defined prompt templates");
        }
    }
}
