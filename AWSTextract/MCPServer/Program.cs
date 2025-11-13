using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using MCPServer.Core;
using MCPServer.Tools;

namespace MCPServer
{
    /// <summary>
    /// MCP (Model Context Protocol) Server Implementation
    /// Supports both interactive demo mode and JSON-RPC mode for VS Code integration
    /// </summary>
    class Program
    {
        static async Task Main(string[] args)
        {
            // Check if running in MCP mode (called by VS Code)
            bool isMcpMode = args.Length > 0 && args[0] == "--mcp";

            if (isMcpMode)
            {
                // Run as JSON-RPC server for VS Code / GitHub Copilot
                var server = new JsonRpcServer();

                // Register tools
                server.RegisterTool(new TextractStatusTool());
                server.RegisterTool(new S3FileListTool());
                server.RegisterTool(new DocumentProcessTool());
                server.RegisterTool(new CalculatorTool());

                await server.StartAsync();
            }
            else
            {
                // Run in interactive demo mode
                Console.WriteLine("========================================");
                Console.WriteLine("MCP Server - Model Context Protocol Demo");
                Console.WriteLine("========================================");
                Console.WriteLine();
                Console.WriteLine("What is MCP?");
                Console.WriteLine("------------");
                Console.WriteLine("MCP (Model Context Protocol) is a standardized protocol for");
                Console.WriteLine("communication between AI applications and external tools/data sources.");
                Console.WriteLine();
                Console.WriteLine("Key Concepts:");
                Console.WriteLine("1. Server: Exposes tools and resources (this application)");
                Console.WriteLine("2. Client: AI application that calls tools (like Claude, ChatGPT, Copilot)");
                Console.WriteLine("3. Tools: Functions the server provides");
                Console.WriteLine("4. Resources: Data/context the server can provide");
                Console.WriteLine();
                Console.WriteLine("Usage Modes:");
                Console.WriteLine("  Interactive: dotnet run");
                Console.WriteLine("  MCP Server:  dotnet run -- --mcp");
                Console.WriteLine();
                Console.WriteLine("Starting MCP Server in Interactive Mode...");
                Console.WriteLine();

                var server = new McpServer();

                // Register tools
                server.RegisterTool(new TextractStatusTool());
                server.RegisterTool(new S3FileListTool());
                server.RegisterTool(new DocumentProcessTool());
                server.RegisterTool(new CalculatorTool());

                Console.WriteLine($"Registered {server.GetToolCount()} tools");
                Console.WriteLine();

                // Start server
                await server.StartAsync();
            }
        }
    }
}
