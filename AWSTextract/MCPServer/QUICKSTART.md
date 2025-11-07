# MCP Server Quick Start Guide

## What You'll Learn

1. What MCP (Model Context Protocol) is
2. How MCP servers work
3. How to create and use MCP tools
4. How AI applications interact with MCP servers

## 5-Minute Quick Start

### Step 1: Build and Run

```bash
cd E:\Sudhir\GitRepo\AWSTextract\MCPServer
dotnet build
dotnet run
```

### Step 2: List Available Tools

```
mcp> list
```

You'll see 4 tools:
- `calculator` - Basic math operations
- `textract_status` - Check document processing status
- `s3_list_files` - List files in S3
- `process_document` - Trigger document processing

### Step 3: Try the Calculator

```
mcp> call calculator {"operation":"add","a":10,"b":5}
```

**Output:**
```json
{
  "operation": "add",
  "a": 10,
  "b": 5,
  "result": 15,
  "formula": "10 + 5 = 15"
}
```

### Step 4: Check Textract Status

```
mcp> call textract_status {}
```

### Step 5: Run the Demo

```
mcp> demo
```

This runs 3 demo scenarios showing how tools work.

## Understanding MCP

### What Problem Does MCP Solve?

**Before MCP:**
```
AI: "I need to check the status of document processing"
Developer: *Writes custom API endpoint*
Developer: *Documents the API*
Developer: *Teaches AI how to use it*
```

**With MCP:**
```
AI: "I need to check the status"
MCP Server: *Exposes tool with standard interface*
AI: *Automatically understands and uses it*
```

### MCP Architecture

```
???????????????????????????????????????????????
?  AI Application (Claude, ChatGPT, etc.)    ?
?  - Understands MCP protocol     ?
?  - Discovers available tools             ?
?  - Calls tools when needed        ?
???????????????????????????????????????????????
      ?
      ? MCP Protocol (JSON-RPC)
        ?
???????????????????????????????????????????????
?  MCP Server (This Application)       ?
?  - Registers tools  ?
?  - Validates requests     ?
?  - Executes tools      ?
?  - Returns results             ?
???????????????????????????????????????????????
               ?
      ?????????????????????????????????????
 ?        ?           ?
?????????????  ???????????????  ???????????????
? Calculator ?  ?  Textract   ?  ?  S3 Files   ?
?    Tool    ?  ?    Status   ?  ?    List     ?
??????????????  ???????????????  ???????????????
```

## Creating Your First Tool

### Example: Weather Tool

```csharp
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MCPServer.Tools
{
    public class WeatherTool : IMcpTool
    {
        public string Name => "get_weather";

        public string Description => "Gets weather for a city";

  public ToolInputSchema InputSchema => new()
        {
      Properties = new Dictionary<string, PropertySchema>
          {
   ["city"] = new()
{
 Type = "string",
        Description = "City name"
        },
           ["units"] = new()
         {
 Type = "string",
      Description = "Temperature units",
  Enum = new List<string> { "celsius", "fahrenheit" }
                }
    },
   Required = new List<string> { "city" }
        };

        public Task<object> ExecuteAsync(Dictionary<string, object> parameters)
        {
            var city = parameters["city"].ToString();
      var units = parameters.ContainsKey("units") 
           ? parameters["units"].ToString() 
      : "celsius";

 // In real implementation, call weather API
          var temp = units == "celsius" ? 22 : 72;

        return Task.FromResult<object>(new
      {
city,
    temperature = temp,
      units,
      condition = "Sunny",
                humidity = 65
            });
        }
    }
}
```

### Register the Tool

In `Program.cs`:
```csharp
server.RegisterTool(new WeatherTool());
```

### Use the Tool

```
mcp> call get_weather {"city":"Los Angeles","units":"fahrenheit"}
```

## Key Concepts

### 1. Tools Are Functions

A tool is just a function that:
- Has a name
- Has a description
- Defines its input parameters (schema)
- Returns a result

### 2. Schema Defines Parameters

```csharp
public ToolInputSchema InputSchema => new()
{
    Properties = new Dictionary<string, PropertySchema>
    {
 ["param_name"] = new()
        {
       Type = "string",              // Data type
      Description = "What it does",  // Description
            Enum = new List<string> { "option1", "option2" }  // Optional: allowed values
 }
    },
    Required = new List<string> { "param_name" }  // Required parameters
};
```

### 3. Execution Returns Results

```csharp
public Task<object> ExecuteAsync(Dictionary<string, object> parameters)
{
    // Get parameters
    var value = parameters["param_name"].ToString();

    // Do work
    var result = ProcessData(value);
    
    // Return result
    return Task.FromResult<object>(new
    {
     success = true,
        data = result
    });
}
```

## Common Patterns

### Pattern 1: Information Retrieval

```csharp
public class StatusTool : IMcpTool
{
    public Task<object> ExecuteAsync(Dictionary<string, object> parameters)
    {
        // Query database, check files, call API, etc.
        var status = GetStatus();
        
     return Task.FromResult<object>(new { status });
    }
}
```

### Pattern 2: Action Execution

```csharp
public class ProcessTool : IMcpTool
{
    public Task<object> ExecuteAsync(Dictionary<string, object> parameters)
    {
        // Trigger action, start job, send email, etc.
        var jobId = StartProcessing();
        
        return Task.FromResult<object>(new 
 { 
          jobId, 
            status = "started" 
        });
    }
}
```

### Pattern 3: Data Transformation

```csharp
public class ConvertTool : IMcpTool
{
    public Task<object> ExecuteAsync(Dictionary<string, object> parameters)
    {
        // Transform data, convert formats, etc.
        var input = parameters["data"].ToString();
        var output = Transform(input);
        
        return Task.FromResult<object>(new { output });
    }
}
```

## Integration with Your Projects

### Access TextractProcessor

```csharp
public class TextractIntegrationTool : IMcpTool
{
    public async Task<object> ExecuteAsync(Dictionary<string, object> parameters)
 {
        // Create instance of your existing code
        var processor = new TextractProcessor.Function();
        
        // Call it
        var result = await processor.FunctionHandler(context);
      
        return result;
    }
}
```

### Access S3

```csharp
public class S3IntegrationTool : IMcpTool
{
    private readonly IAmazonS3 _s3Client = new AmazonS3Client();
    
    public async Task<object> ExecuteAsync(Dictionary<string, object> parameters)
    {
        var bucket = parameters["bucket"].ToString();
        
        var response = await _s3Client.ListObjectsV2Async(
   new ListObjectsV2Request
     {
        BucketName = bucket
       });
        
  return new
   {
        files = response.S3Objects.Select(o => new
{
          key = o.Key,
           size = o.Size,
         lastModified = o.LastModified
    })
     };
    }
}
```

## Testing Your Tools

### Manual Testing

```bash
# Start server
dotnet run

# In the prompt:
mcp> call your_tool {"param":"value"}
```

### Unit Testing

```csharp
[Test]
public async Task TestCalculator()
{
    var tool = new CalculatorTool();
    var parameters = new Dictionary<string, object>
    {
        ["operation"] = "add",
      ["a"] = 5,
        ["b"] = 3
 };
    
    var result = await tool.ExecuteAsync(parameters);
    var json = JsonSerializer.Serialize(result);
    
    Assert.Contains("\"result\":8", json);
}
```

## Real-World Scenarios

### Scenario 1: Document Processing Pipeline

**AI Request:** "Process all documents uploaded today"

**Tools Used:**
1. `s3_list_files` - Get today's documents
2. `process_document` - Process each one
3. `textract_status` - Check completion

### Scenario 2: Status Dashboard

**AI Request:** "Give me a status report"

**Tools Used:**
1. `textract_status` - Processing status
2. `s3_list_files` - Files in bucket
3. Custom tools for other metrics

### Scenario 3: Automated Workflow

**AI Request:** "If processing fails, retry"

**Tools Used:**
1. `textract_status` - Check status
2. `process_document` - Retry if failed

## Troubleshooting

### Tool Not Found
```
Error: Tool not found: my_tool
Solution: Check tool name matches exactly (case-sensitive)
```

### Invalid Parameters
```
Error: Missing required parameter: param_name
Solution: Check InputSchema.Required list
```

### Execution Error
```
Error: Division by zero
Solution: Add validation in ExecuteAsync
```

## Next Steps

### Beginner
1. ? Run the demo
2. ? Try each tool
3. ? Modify the calculator tool
4. ? Create a simple tool

### Intermediate
1. ? Create a tool that reads from file
2. ? Create a tool that writes to database
3. ? Integrate with TextractProcessor
4. ? Add error handling

### Advanced
1. ? Implement streaming responses
2. ? Add authentication
3. ? Create resource providers
4. ? Build prompt templates
5. ? Deploy as service

## Resources

- **Full Documentation**: See `README.md`
- **MCP Spec**: https://spec.modelcontextprotocol.io/
- **Examples**: Check the `Tools/` folder

## Questions?

Common questions:

**Q: Can I use MCP with any AI?**
A: Yes! Any AI that supports MCP protocol (Claude, some ChatGPT integrations)

**Q: Do I need to deploy this?**
A: For learning, no. For production with AI apps, yes.

**Q: Can tools call other tools?**
A: Yes! Tools can orchestrate other tools.

**Q: Is this secure?**
A: For learning, it's fine. For production, add authentication and validation.

---

**Start Learning Now!**

```bash
cd MCPServer
dotnet run
```

Then type `demo` to see it in action! ??
