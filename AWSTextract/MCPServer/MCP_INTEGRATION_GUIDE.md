# ?? MCP Server Integration with VS Code

## What is MCP (Model Context Protocol)?

MCP is a standardized protocol that allows AI assistants (like GitHub Copilot) to interact with external tools and data sources. Your MCP server exposes tools that AI can call to process documents, list files, check status, etc.

## ?? What This Integration Does

Your MCP server provides these tools to VS Code / GitHub Copilot:

1. **textract_status** - Check AWS Textract service status
2. **s3_list_files** - List files in S3 buckets
3. **document_process** - Process documents with Textract and Bedrock
4. **calculator** - Perform calculations (demo tool)

## ?? Prerequisites

1. **.NET 9.0 SDK** installed
2. **VS Code** with GitHub Copilot extension
3. **AWS credentials** configured (for AWS tools)
4. **MCP support** in VS Code (available in recent Copilot updates)

---

## ?? Setup Instructions

### Step 1: Build the MCP Server

```powershell
cd E:\Sudhir\GitRepo\AWSTextract\MCPServer
dotnet build
```

### Step 2: Test the Server Locally

**Interactive Mode** (for testing):
```powershell
dotnet run
```

This starts an interactive shell where you can test tools:
```
mcp> list                 # List all tools
mcp> call calculator {"operation":"add","a":5,"b":3}
mcp> call textract_status {}
mcp> demo          # Run demo scenarios
mcp> exit
```

**MCP Mode** (for VS Code):
```powershell
dotnet run -- --mcp
```

This starts the JSON-RPC server that communicates over stdin/stdout.

---

### Step 3: Configure VS Code

#### Option A: User Settings (Recommended)

1. Open VS Code
2. Press `Ctrl+Shift+P` (Windows) or `Cmd+Shift+P` (Mac)
3. Type "Preferences: Open User Settings (JSON)"
4. Add this configuration:

```json
{
  "github.copilot.chat.mcpServers": {
    "aws-textract-processor": {
      "command": "dotnet",
      "args": [
      "run",
        "--project",
        "E:\\Sudhir\\GitRepo\\AWSTextract\\MCPServer\\MCPServer.csproj",
        "--",
        "--mcp"
    ],
      "description": "AWS Textract and Bedrock document processing"
    }
  }
}
```

#### Option B: Workspace Settings

1. Copy `.vscode/mcp-settings.json` content
2. Open `.vscode/settings.json` in your workspace
3. Add the MCP configuration there

---

### Step 4: Verify Integration

1. **Restart VS Code** to apply MCP configuration

2. **Open GitHub Copilot Chat** (`Ctrl+Shift+I` or click Copilot icon)

3. **Check MCP Status**:
   - Look for "MCP" indicator in Copilot Chat
   - Your tools should appear when you type `@`

4. **Test with Prompts**:

```
@aws-textract-processor What's the status of the Textract service?
```

```
@aws-textract-processor List files in bucket testbucket-sudhir-bsi1 with prefix uploads/
```

```
@aws-textract-processor Calculate 15 + 27
```

---

## ?? Using Your MCP Server

### Example Prompts for Copilot Chat:

1. **Check Service Status**:
```
@aws-textract-processor Check if Textract service is available
```

2. **List S3 Files**:
```
@aws-textract-processor Show me all files in bucket testbucket-sudhir-bsi1 under uploads/ folder
```

3. **Process a Document**:
```
@aws-textract-processor Process document at s3://testbucket-sudhir-bsi1/uploads/2025-01-20/invoice.pdf
```

4. **Complex Workflow**:
```
@aws-textract-processor First check Textract status, then list files in testbucket-sudhir-bsi1, and tell me which one is most recent
```

---

## ?? Troubleshooting

### Issue 1: MCP Server Not Appearing in Copilot

**Check**:
1. VS Code restarted after config?
2. GitHub Copilot extension up to date?
3. MCP feature enabled? (May need Copilot Labs or Insider build)

**Solution**:
```powershell
# Test server manually
cd E:\Sudhir\GitRepo\AWSTextract\MCPServer
dotnet run -- --mcp

# Type this and press Enter:
{"jsonrpc":"2.0","id":1,"method":"initialize","params":{}}

# Should get JSON response
```

### Issue 2: Server Starts But Tools Don't Work

**Check logs**:
The server writes debug logs to `stderr`:
```powershell
dotnet run -- --mcp 2> mcp-debug.log
```

**Common issues**:
- AWS credentials not configured
- S3 bucket doesn't exist
- Network/permissions issues

### Issue 3: "Command Failed" Error

**Verify paths**:
```powershell
# Check project exists
Test-Path "E:\Sudhir\GitRepo\AWSTextract\MCPServer\MCPServer.csproj"

# Check dotnet works
dotnet --version
```

---

## ?? Project Structure

```
MCPServer/
??? Core/
?   ??? McpServer.cs        # Interactive demo server
?   ??? JsonRpcServer.cs      # JSON-RPC MCP server (NEW)
??? Tools/
?   ??? IMcpTool.cs         # Tool interface
?   ??? TextractStatusTool.cs  # AWS Textract status
?   ??? S3FileListTool.cs      # S3 file listing
?   ??? DocumentProcessTool.cs # Document processing
?   ??? CalculatorTool.cs      # Demo calculator
??? Program.cs     # Original demo entry
??? McpProgram.cs    # MCP entry point (NEW)
??? MCPServer.csproj
```

---

## ?? How It Works

### Communication Flow:

```
VS Code / Copilot
       ?
  (JSON-RPC over stdin/stdout)
       ?
  JsonRpcServer
       ?
  IMcpTool implementations
       ?
  AWS Services (S3, Textract, Bedrock)
```

### MCP Protocol Messages:

1. **Initialize**:
```json
{
  "jsonrpc": "2.0",
  "id": 1,
  "method": "initialize",
  "params": {}
}
```

2. **List Tools**:
```json
{
  "jsonrpc": "2.0",
  "id": 2,
  "method": "tools/list",
  "params": {}
}
```

3. **Call Tool**:
```json
{
  "jsonrpc": "2.0",
  "id": 3,
"method": "tools/call",
  "params": {
    "name": "calculator",
    "arguments": {"operation": "add", "a": 5, "b": 3}
  }
}
```

---

## ?? Security Notes

1. **AWS Credentials**: 
   - Server uses default AWS credential chain
   - Ensure `~/.aws/credentials` is configured
   - Or set `AWS_ACCESS_KEY_ID` and `AWS_SECRET_ACCESS_KEY` environment variables

2. **S3 Access**:
   - Server needs read access to S3 buckets
   - For document processing, needs write access too

3. **Bedrock Access**:
 - Requires AWS Bedrock permissions
   - May need to enable specific models in AWS Console

---

## ?? Advanced: Publishing as Standalone Executable

To make it easier to use, publish as a self-contained executable:

```powershell
cd E:\Sudhir\GitRepo\AWSTextract\MCPServer

# Publish for Windows
dotnet publish -c Release -r win-x64 --self-contained

# Publish for Linux
dotnet publish -c Release -r linux-x64 --self-contained

# Publish for macOS
dotnet publish -c Release -r osx-x64 --self-contained
```

Then update VS Code config to use the executable:
```json
{
  "github.copilot.chat.mcpServers": {
    "aws-textract-processor": {
      "command": "E:\\Sudhir\\GitRepo\\AWSTextract\\MCPServer\\bin\\Release\\net9.0\\win-x64\\publish\\MCPServer.exe",
   "args": ["--mcp"]
    }
  }
}
```

---

## ?? Additional Resources

- [MCP Specification](https://spec.modelcontextprotocol.io/)
- [GitHub Copilot MCP Documentation](https://docs.github.com/copilot)
- [AWS Textract Documentation](https://docs.aws.amazon.com/textract/)
- [AWS Bedrock Documentation](https://docs.aws.amazon.com/bedrock/)

---

## ?? Quick Start Checklist

- [ ] Build the project: `dotnet build`
- [ ] Test interactive mode: `dotnet run`
- [ ] Test MCP mode: `dotnet run -- --mcp`
- [ ] Configure VS Code settings (see Step 3)
- [ ] Restart VS Code
- [ ] Open Copilot Chat
- [ ] Try: `@aws-textract-processor Check Textract status`
- [ ] Success! ??

---

## ?? Tips

1. **Start Simple**: Test with the calculator tool first
2. **Check Logs**: Use `stderr` logs to debug issues
3. **AWS Setup**: Ensure AWS credentials are valid before testing AWS tools
4. **Copilot Updates**: Keep GitHub Copilot extension updated for best MCP support

---

**Need Help?** 
- Check the troubleshooting section
- Run in interactive mode to test tools directly
- Review stderr logs for detailed error messages
