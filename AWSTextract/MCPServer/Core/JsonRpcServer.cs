using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using MCPServer.Tools;

namespace MCPServer.Core
{
    /// <summary>
    /// JSON-RPC 2.0 server implementation for MCP protocol
    /// Communicates over stdin/stdout as required by VS Code
    /// </summary>
    public class JsonRpcServer
    {
        private readonly Dictionary<string, IMcpTool> _tools = new();
        private readonly TextWriter _stdout;
        private readonly TextReader _stdin;
        private readonly TextWriter _stderr;

        public JsonRpcServer()
        {
            _stdout = Console.Out;
            _stdin = Console.In;
            _stderr = Console.Error;
        }

        public void RegisterTool(IMcpTool tool)
        {
            _tools[tool.Name] = tool;
            LogDebug($"Registered tool: {tool.Name}");
        }

        public async Task StartAsync()
        {
            LogDebug("MCP Server starting...");
            LogDebug($"Registered {_tools.Count} tools");

            while (true)
            {
                try
                {
                    var line = await _stdin.ReadLineAsync();
                    if (line == null)
                    {
                        LogDebug("EOF received, shutting down");
                        break;
                    }

                    if (string.IsNullOrWhiteSpace(line))
                        continue;

                    LogDebug($"Received: {line}");
                    await HandleRequestAsync(line);
                }
                catch (Exception ex)
                {
                    LogError($"Error processing request: {ex.Message}");
                }
            }
        }

        private async Task HandleRequestAsync(string requestJson)
        {
            try
            {
                var request = JsonSerializer.Deserialize<JsonRpcRequest>(requestJson);
                if (request == null)
                {
                    SendError(null, -32700, "Parse error");
                    return;
                }

                object result = null;

                switch (request.Method)
                {
                    case "initialize":
                        result = HandleInitialize(request);
                        break;

                    case "tools/list":
                        result = HandleToolsList();
                        break;

                    case "tools/call":
                        result = await HandleToolCallAsync(request);
                        break;

                    case "resources/list":
                        result = HandleResourcesList();
                        break;

                    case "prompts/list":
                        result = HandlePromptsList();
                        break;

                    default:
                        SendError(request.Id, -32601, $"Method not found: {request.Method}");
                        return;
                }

                SendResponse(request.Id, result);
            }
            catch (Exception ex)
            {
                LogError($"Error handling request: {ex.Message}");
                SendError(null, -32603, $"Internal error: {ex.Message}");
            }
        }

        private object HandleInitialize(JsonRpcRequest request)
        {
            return new
            {
                protocolVersion = "2024-11-05",
                capabilities = new
                {
                    tools = new { },
                    resources = new { },
                    prompts = new { }
                },
                serverInfo = new
                {
                    name = "AWS Textract MCP Server",
                    version = "1.0.0"
                }
            };
        }

        private object HandleToolsList()
        {
            var tools = new List<object>();

            foreach (var tool in _tools.Values)
            {
                tools.Add(new
                {
                    name = tool.Name,
                    description = tool.Description,
                    inputSchema = new
                    {
                        type = "object",
                        properties = tool.InputSchema.Properties,
                        required = tool.InputSchema.Required
                    }
                });
            }

            return new { tools };
        }

        private async Task<object> HandleToolCallAsync(JsonRpcRequest request)
        {
            if (request.Params == null)
            {
                throw new Exception("Missing params for tool call");
            }

            var paramsElement = (JsonElement)request.Params;

            if (!paramsElement.TryGetProperty("name", out var nameElement))
            {
                throw new Exception("Missing 'name' parameter");
            }

            var toolName = nameElement.GetString();

            if (!_tools.TryGetValue(toolName, out var tool))
            {
                throw new Exception($"Tool not found: {toolName}");
            }

            Dictionary<string, object> arguments = new();

            if (paramsElement.TryGetProperty("arguments", out var argsElement))
            {
                arguments = JsonSerializer.Deserialize<Dictionary<string, object>>(argsElement.GetRawText());
            }

            var result = await tool.ExecuteAsync(arguments);

            return new
            {
                content = new[]
               {
      new
            {
       type = "text",
text = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true })
   }
       }
            };
        }

        private object HandleResourcesList()
        {
            return new
            {
                resources = new object[] { }
            };
        }

        private object HandlePromptsList()
        {
            return new
            {
                prompts = new[]
                     {
           new
         {
         name = "analyze_document",
   description = "Analyze a document using AWS Textract and Bedrock",
    arguments = new[]
            {
    new
         {
         name = "bucket",
        description = "S3 bucket name",
        required = true
},
        new
       {
     name = "key",
             description = "S3 object key",
 required = true
           }
           }
           }
            }
            };
        }

        private void SendResponse(object id, object result)
        {
            var response = new
            {
                jsonrpc = "2.0",
                id,
                result
            };

            var json = JsonSerializer.Serialize(response);
            _stdout.WriteLine(json);
            _stdout.Flush();
            LogDebug($"Sent response: {json}");
        }

        private void SendError(object id, int code, string message)
        {
            var response = new
            {
                jsonrpc = "2.0",
                id,
                error = new
                {
                    code,
                    message
                }
            };

            var json = JsonSerializer.Serialize(response);
            _stdout.WriteLine(json);
            _stdout.Flush();
            LogDebug($"Sent error: {json}");
        }

        private void LogDebug(string message)
        {
            _stderr.WriteLine($"[MCP] {DateTime.Now:HH:mm:ss.fff} {message}");
            _stderr.Flush();
        }

        private void LogError(string message)
        {
            _stderr.WriteLine($"[MCP ERROR] {DateTime.Now:HH:mm:ss.fff} {message}");
            _stderr.Flush();
        }
    }

    public class JsonRpcRequest
    {
        [JsonPropertyName("jsonrpc")]
        public string JsonRpc { get; set; }

        [JsonPropertyName("id")]
        public object Id { get; set; }

        [JsonPropertyName("method")]
        public string Method { get; set; }

        [JsonPropertyName("params")]
        public object Params { get; set; }
    }
}
