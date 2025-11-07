# MCP Server - Model Context Protocol Learning Project

## What is MCP?

**MCP (Model Context Protocol)** is an open standard protocol developed by Anthropic for enabling communication between AI applications (like Claude, ChatGPT) and external tools, data sources, and services.

### Key Concepts

```
???????????????         MCP Protocol        ????????????????
?   ??????????????????????????????      ?
?  AI Client  ?    (JSON-RPC over stdio)   ?  MCP Server  ?
? (Claude AI) ?           ?  (This App)  ?
?      ? ?      ?
???????????????     ????????????????
            ?
    ?
      ?????????????????
           ?  Your Tools   ?
          ?  & Resources  ?
  ?????????????????
```

## Project Structure

```
MCPServer/
??? MCPServer.csproj       # Project file
??? Program.cs  # Entry point
??? Core/
?   ??? McpServer.cs         # Core server implementation
??? Tools/     # MCP Tools
?   ??? IMcpTool.cs         # Tool interface
?   ??? CalculatorTool.cs   # Example: Calculator
?   ??? TextractStatusTool.cs   # Integration with Textract
?   ??? S3FileListTool.cs   # S3 file listing
?   ??? DocumentProcessTool.cs  # Document processing
??? README.md               # This file
```

## MCP Components

### 1. **Tools**
Functions that AI can call to perform actions.

**Example:**
```json
{
  "name": "calculator",
  "description": "Performs arithmetic operations",
  "inputSchema": {
    "type": "object",
    "properties": {
  "operation": { "type": "string", "enum": ["add", "subtract"] },
      "a": { "type": "number" },
      "b": { "type": "number" }
    },
    "required": ["operation", "a", "b"]
  }
}
```

### 2. **Resources**
Data that AI can read (files, databases, APIs).

**Example:**
```json
{
  "uri": "file:///documents/invoice.pdf",
  "name": "Invoice Document",
  "mimeType": "application/pdf"
}
```

### 3. **Prompts**
Pre-defined prompt templates AI can use.

**Example:**
```json
{
  "name": "analyze_document",
  "description": "Analyzes a document",
  "arguments": [
    {
      "name": "document_path",
   "description": "Path to document",
      "required": true
    }
  ]
}
```

## Getting Started

### Prerequisites
- .NET 9.0 SDK
- Basic understanding of JSON and APIs

### Running the Server

```bash
cd MCPServer
dotnet run
```

### Available Commands

Once the server is running:

```
mcp> list       # List all tools
mcp> call calculator {"operation":"add","a":5,"b":3}
mcp> call textract_status {}
mcp> demo        # Run demo scenarios
mcp> help        # Show help
mcp> exit               # Exit server
```

## Example Usage

### Example 1: Using the Calculator Tool

```bash
mcp> call calculator {"operation":"add","a":10,"b":5}
```

**Response:**
```json
{
  "operation": "add",
  "a": 10,
  "b": 5,
  "result": 15,
  "formula": "10 + 5 = 15"
}
```

### Example 2: Checking Textract Status

```bash
mcp> call textract_status {}
```

**Response:**
```json
{
  "status": "running",
  "output_directory": "E:\\..\\CachedFiles_OutputFiles",
  "output_directory_exists": true,
  "processed_files": 3,
  "last_check": "2025-01-20T10:30:00Z",
  "message": "Found 3 processed document(s)"
}
```

### Example 3: Listing S3 Files

```bash
mcp> call s3_list_files {"bucket":"testbucket-sudhir-bsi1","prefix":"uploads/","max_results":5}
```

**Response:**
```json
{
  "bucket": "testbucket-sudhir-bsi1",
  "prefix": "uploads/",
  "file_count": 4,
  "files": [
    {
      "key": "uploads/2025-01-20/invoice/invoice.pdf",
      "size": 245678,
      "last_modified": "2025-01-20T08:15:00Z",
"storage_class": "STANDARD"
    }
  ]
}
```

## How MCP Works

### Request-Response Flow

1. **Client sends request** (AI application):
```json
{
  "jsonrpc": "2.0",
  "id": 1,
  "method": "tools/call",
  "params": {
    "name": "calculator",
    "arguments": {
      "operation": "add",
   "a": 5,
      "b": 3
    }
  }
}
```

2. **Server processes** (this application):
   - Validates input against schema
   - Executes the tool
   - Returns result

3. **Server sends response**:
```json
{
  "jsonrpc": "2.0",
  "id": 1,
  "result": {
    "content": [
      {
        "type": "text",
        "text": "{\"result\": 8, \"formula\": \"5 + 3 = 8\"}"
      }
    ]
  }
}
```

## Creating Your Own Tool

### Step 1: Implement IMcpTool

```csharp
public class MyTool : IMcpTool
{
 public string Name => "my_tool";
    
    public string Description => "What my tool does";
  
    public ToolInputSchema InputSchema => new()
    {
        Properties = new Dictionary<string, PropertySchema>
      {
      ["param1"] = new()
    {
     Type = "string",
      Description = "Description of param1"
     }
    },
        Required = new List<string> { "param1" }
    };
    
    public Task<object> ExecuteAsync(Dictionary<string, object> parameters)
    {
    // Your logic here
        var param1 = parameters["param1"].ToString();
   
      return Task.FromResult<object>(new
   {
 result = $"Processed: {param1}"
        });
    }
}
```

### Step 2: Register the Tool

```csharp
// In Program.cs
server.RegisterTool(new MyTool());
```

## Integration with Existing Projects

### Textract Integration

```csharp
public class TextractTool : IMcpTool
{
    public async Task<object> ExecuteAsync(Dictionary<string, object> parameters)
    {
    // Call your TextractProcessor
        var processor = new TextractProcessor.Function();
     var result = await processor.FunctionHandler(context);
        
      return new
        {
        status = "completed",
            result = result
  };
    }
}
```

### S3 Integration

```csharp
public class S3Tool : IMcpTool
{
 private readonly IAmazonS3 _s3Client;
    
    public async Task<object> ExecuteAsync(Dictionary<string, object> parameters)
    {
   var bucket = parameters["bucket"].ToString();
        var response = await _s3Client.ListObjectsV2Async(new ListObjectsV2Request
        {
         BucketName = bucket
  });
        
        return new
        {
            files = response.S3Objects.Select(o => o.Key)
        };
    }
}
```

## MCP Protocol Specification

The full MCP specification includes:

1. **Transport Layer**: JSON-RPC 2.0 over stdio
2. **Methods**:
   - `initialize` - Start MCP session
   - `tools/list` - List available tools
   - `tools/call` - Execute a tool
   - `resources/list` - List resources
   - `resources/read` - Read a resource
   - `prompts/list` - List prompts
   - `prompts/get` - Get a prompt

3. **Capabilities**:
   - Tools
   - Resources
   - Prompts
   - Logging
   - Sampling (AI can request completions)

## Advanced Features

### Streaming Responses

```csharp
public async IAsyncEnumerable<object> ExecuteStreamAsync(Dictionary<string, object> parameters)
{
    for (int i = 0; i < 10; i++)
    {
        yield return new { progress = i * 10, message = $"Processing {i}/10" };
        await Task.Delay(100);
    }
}
```

### Error Handling

```csharp
public Task<object> ExecuteAsync(Dictionary<string, object> parameters)
{
    if (!parameters.ContainsKey("required_param"))
    {
        throw new ArgumentException("Missing required parameter: required_param");
    }
    
    // Your logic...
}
```

### Resource Management

```csharp
public class FileResource : IMcpResource
{
    public string Uri => "file:///data/document.pdf";
    public string Name => "Document";
    public string MimeType => "application/pdf";
    
    public Task<byte[]> ReadAsync()
    {
        return File.ReadAllBytesAsync("/data/document.pdf");
    }
}
```

## Use Cases

### 1. AI Assistant for Your Application
- AI can check Textract status
- AI can trigger document processing
- AI can query S3 files

### 2. Workflow Automation
- Chain multiple tools together
- Process documents automatically
- Generate reports

### 3. Data Access
- Provide AI with access to your databases
- Allow AI to read files and documents
- Enable AI to query APIs

## Real-World Example

**User**: "Process all invoices from today"

**AI thinks**: 
1. Call `s3_list_files` with prefix "uploads/2025-01-20/"
2. For each file, call `process_document`
3. Call `textract_status` to check progress
4. Return summary to user

**Result**: AI orchestrates your application without hardcoded logic!

## Security Considerations

1. **Input Validation**: Always validate tool parameters
2. **Authentication**: Implement auth if exposing over network
3. **Rate Limiting**: Prevent abuse
4. **Sandboxing**: Limit what tools can access
5. **Logging**: Track all tool executions

## Best Practices

1. **Clear Descriptions**: Write descriptive tool descriptions
2. **Proper Schemas**: Define complete input schemas
3. **Error Handling**: Return meaningful error messages
4. **Documentation**: Document what each tool does
5. **Testing**: Test tools independently

## Debugging

Enable verbose logging:
```csharp
// Add to Program.cs
Console.WriteLine($"[DEBUG] Tool called: {toolName}");
Console.WriteLine($"[DEBUG] Parameters: {JsonSerializer.Serialize(parameters)}");
```

## Resources

- **MCP Specification**: https://spec.modelcontextprotocol.io/
- **MCP GitHub**: https://github.com/modelcontextprotocol
- **Examples**: https://github.com/modelcontextprotocol/servers

## Next Steps

1. ? Run the demo: `dotnet run` then type `demo`
2. ? List tools: `list`
3. ? Call a tool: `call calculator {"operation":"add","a":5,"b":3}`
4. ? Create your own tool
5. ? Integrate with AI client (Claude, GPT)

## Extending This Project

### Add More Tools
- Database query tool
- Email sender tool
- Report generator tool
- File converter tool

### Add Resources
- Configuration files
- Database tables
- API endpoints

### Add Prompts
- Document analysis prompts
- Data extraction prompts
- Report generation prompts

## Contributing

This is a learning project. Feel free to:
- Add new tools
- Improve documentation
- Add examples
- Create tutorials

## License

MIT License - Free to use and modify

---

**Happy Learning! ??**

This MCP server is a great starting point for understanding how AI applications can interact with your code and data through a standardized protocol.
