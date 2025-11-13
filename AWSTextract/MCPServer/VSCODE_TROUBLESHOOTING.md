# MCP Server VS Code Troubleshooting Guide

## ?? Issue: @aws-textract-processor not working in VS Code

### **Quick Diagnosis**

Run this script to check your MCP server status:

```powershell
cd E:\Sudhir\GitRepo\AWSTextract\MCPServer
.\test-vscode-mcp.ps1
```

---

## ?? **Step-by-Step Troubleshooting**

### **Step 1: Verify MCP Server Works Standalone**

First, let's confirm the MCP server itself is functional:

```powershell
cd E:\Sudhir\GitRepo\AWSTextract\MCPServer

# Test build
dotnet build

# Test interactive mode
dotnet run
```

In interactive mode, try:
```
list
call textract_status {}
```

**Expected**: Should show 4 tools and execute successfully.

---

### **Step 2: Test MCP Server Mode**

Test the exact mode VS Code will use:

```powershell
# Start MCP server in JSON-RPC mode
dotnet run -- --mcp
```

Then send a test request (in another terminal):
```powershell
'{"jsonrpc":"2.0","id":1,"method":"initialize","params":{}}' | 
  dotnet run -- --mcp
```

**Expected**: Should return JSON with `protocolVersion` and `serverInfo`.

---

### **Step 3: Check VS Code Settings**

#### **Option A: Check User Settings**

1. Open VS Code
2. Press `Ctrl+Shift+P`
3. Type: "Preferences: Open User Settings (JSON)"
4. Verify you have this **exact** configuration:

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

**Important**: 
- ? Use double backslashes `\\` in the path
- ? The `--mcp` argument must be after `--`
- ? No trailing commas if this is the last item

#### **Option B: Check Workspace Settings**

Sometimes workspace settings override user settings. Check:
`.vscode/settings.json` in your workspace

---

### **Step 4: Verify GitHub Copilot**

1. Check Copilot is active:
   - Look for Copilot icon in VS Code status bar
   - Should show as signed in

2. Check Copilot Chat is available:
   - Press `Ctrl+Shift+I` (or click chat icon)
   - Chat panel should open

3. Check MCP support:
   - Some Copilot versions don't support MCP yet
   - Update Copilot extension to latest version

---

### **Step 5: Check for Common Issues**

#### **Issue A: Path Problems**

Your path might need adjustment. Try:

```json
"command": "dotnet",
"args": [
  "run",
  "--project",
  "E:/Sudhir/GitRepo/AWSTextract/MCPServer/MCPServer.csproj",  // Forward slashes
  "--",
  "--mcp"
]
```

Or use absolute path to dotnet:

```json
"command": "C:\\Program Files\\dotnet\\dotnet.exe",
"args": [
  "run",
  "--project",
  "E:\\Sudhir\\GitRepo\\AWSTextract\\MCPServer\\MCPServer.csproj",
  "--",
  "--mcp"
]
```

#### **Issue B: VS Code Not Restarted**

**Critical**: You MUST completely restart VS Code after adding MCP config.

1. Close ALL VS Code windows
2. Wait 5 seconds
3. Open VS Code fresh
4. Try again

#### **Issue C: Copilot Version Too Old**

MCP support is relatively new. Check:

1. Go to Extensions
2. Find "GitHub Copilot"
3. Check version (should be recent, like 1.150+)
4. Update if needed

#### **Issue D: MCP Not Enabled**

Some Copilot plans don't have MCP enabled yet. Check:

1. Go to: https://github.com/settings/copilot
2. Look for "MCP Servers" or "Extensions" settings
3. Ensure it's enabled for your account

---

### **Step 6: Enable Debug Logging**

Add this to your VS Code settings to see what's happening:

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
      "env": {
        "DEBUG": "true",
        "LOG_LEVEL": "DEBUG"
      }
    }
  },
  "github.copilot.advanced": {
    "debug.overrideEngine": "gpt-4",
    "debug.testOverrideProxyUrl": ""
  }
}
```

Then check VS Code Output panel:
1. View ? Output
2. Select "GitHub Copilot" from dropdown
3. Look for MCP-related messages

---

### **Step 7: Alternative: Use Pre-built Executable**

Instead of `dotnet run`, use a published executable:

```powershell
# Publish the MCP server
cd E:\Sudhir\GitRepo\AWSTextract\MCPServer
dotnet publish -c Release -r win-x64 --self-contained -o published
```

Then update VS Code settings:

```json
{
  "github.copilot.chat.mcpServers": {
    "aws-textract-processor": {
      "command": "E:\\Sudhir\\GitRepo\\AWSTextract\\MCPServer\\published\\MCPServer.exe",
      "args": ["--mcp"],
      "description": "AWS Textract processor"
    }
  }
}
```

Benefits:
- ? Faster startup
- ? No .NET SDK required
- ? Simpler configuration

---

## ?? **Quick Test Commands**

Once configured, try these in Copilot Chat (Ctrl+Shift+I):

### **Test 1: Simple Status Check**
```
@aws-textract-processor status
```

### **Test 2: List Tools**
```
@aws-textract-processor What tools do you have?
```

### **Test 3: Calculate (Demo)**
```
@aws-textract-processor Calculate 15 + 27
```

### **Test 4: Check S3**
```
@aws-textract-processor List files in testbucket-sudhir-bsi1
```

---

## ?? **Debugging Checklist**

Use this checklist to diagnose issues:

- [ ] MCP server builds: `dotnet build` succeeds
- [ ] Interactive mode works: `dotnet run` ? `list` shows tools
- [ ] MCP mode starts: `dotnet run -- --mcp` doesn't crash
- [ ] VS Code settings correct: Check User Settings (JSON)
- [ ] Path uses double backslashes: `E:\\Sudhir\\...`
- [ ] `--mcp` is after `--` in args
- [ ] VS Code completely restarted (all windows closed)
- [ ] Copilot extension is updated
- [ ] Copilot is signed in and active
- [ ] Copilot Chat opens (Ctrl+Shift+I)

---

## ?? **Common Error Messages**

### **Error: "Command not found"**

**Cause**: VS Code can't find `dotnet` command

**Solution**: Use full path to dotnet.exe:
```json
"command": "C:\\Program Files\\dotnet\\dotnet.exe"
```

### **Error: "Server failed to start"**

**Cause**: MCP server crashed on startup

**Solution**: 
1. Test manually: `dotnet run -- --mcp`
2. Check for build errors
3. Check stderr output

### **Error: "@aws-textract-processor not recognized"**

**Cause**: Copilot doesn't see your MCP server

**Solutions**:
1. Restart VS Code (completely)
2. Check settings.json syntax (no trailing commas)
3. Update Copilot extension
4. Check if MCP is supported in your Copilot plan

### **Error: "Tool execution failed"**

**Cause**: Server started but tool failed

**Solution**:
1. Check AWS credentials: `aws sts get-caller-identity`
2. Check tool implementation
3. Look at VS Code Output ? GitHub Copilot

---

## ?? **Verification Commands**

### **Check .NET is Available**
```powershell
dotnet --version
# Should show: 8.0.x or 9.0.x
```

### **Check Project Builds**
```powershell
cd E:\Sudhir\GitRepo\AWSTextract\MCPServer
dotnet build --nologo
# Should show: Build succeeded
```

### **Check VS Code Settings**
```powershell
code "$env:APPDATA\Code\User\settings.json"
# Opens your user settings
```

### **Check MCP Server Logs**
```powershell
# Run with error logging
dotnet run -- --mcp 2> mcp-errors.log
# Check mcp-errors.log for issues
```

---

## ?? **Pro Tips**

1. **Start Simple**: Test with calculator tool first
   ```
   @aws-textract-processor Calculate 5 + 3
   ```

2. **Check Copilot Status**: Look in VS Code status bar (bottom right)

3. **Try Different Syntax**:
   ```
   @aws-textract-processor status
   @aws-textract-processor Check the status
   @aws-textract-processor What is the current status?
   ```

4. **Use Interactive Mode**: Great for debugging
   ```powershell
   dotnet run
   # Then: list, call textract_status {}
   ```

---

## ?? **Still Not Working?**

### **Option 1: Use Interactive Mode Instead**

The MCP server works great in interactive mode:

```powershell
cd E:\Sudhir\GitRepo\AWSTextract\MCPServer
dotnet run
```

Then use commands like:
```
call textract_status {}
call s3_list_files {"bucket":"testbucket-sudhir-bsi1"}
```

### **Option 2: Use Scripts**

The status check scripts work independently:

```powershell
.\status-check.ps1
.\check-status.ps1
```

### **Option 3: Direct API**

Call the tools directly from PowerShell:

```powershell
# Start MCP server
$process = Start-Process dotnet -ArgumentList "run","--","--mcp" `
  -WorkingDirectory "E:\Sudhir\GitRepo\AWSTextract\MCPServer" `
  -RedirectStandardInput input.txt `
  -RedirectStandardOutput output.txt `
  -NoNewWindow -PassThru

# Send request
'{"jsonrpc":"2.0","id":1,"method":"tools/call","params":{"name":"textract_status","arguments":{}}}' | 
  Out-File input.txt

# Wait and read response
Start-Sleep 2
Get-Content output.txt
```

---

## ?? **Additional Resources**

- **MCP Specification**: https://spec.modelcontextprotocol.io/
- **GitHub Copilot Docs**: https://docs.github.com/copilot
- **MCP Integration Guide**: See `MCP_INTEGRATION_GUIDE.md`
- **Quick Reference**: See `QUICK_REFERENCE.md`

---

## ? **Success Indicators**

You'll know it's working when:

1. ? You type `@aws` in Copilot Chat and see `@aws-textract-processor` autocomplete
2. ? Copilot responds to `@aws-textract-processor status` with actual data
3. ? You see 4 tools available when you ask about capabilities
4. ? Status checks return real information about your system

---

**Last Updated**: 2025-01-20  
**Tested With**: VS Code 1.85+, GitHub Copilot 1.150+, .NET 8.0/9.0
