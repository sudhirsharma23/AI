# ?? Getting Started - 5 Minutes

## What You Need
- ? .NET 9.0 SDK installed
- ? VS Code with GitHub Copilot (optional, for AI integration)
- ? AWS credentials configured (for AWS tools)

---

## Quick Start (Choose One)

### Option A: Interactive Mode (Testing & Learning)

```powershell
cd E:\Sudhir\GitRepo\AWSTextract\MCPServer
dotnet run
```

Try these commands:
```
list    # Show all tools
call calculator {"operation":"add","a":10,"b":5"}
call textract_status {}
demo            # Run demos
help  # Show help
exit         # Quit
```

**That's it!** You're now using the MCP server interactively.

---

### Option B: VS Code Integration (AI Assistant)

#### Step 1: Run Setup
```powershell
cd E:\Sudhir\GitRepo\AWSTextract\MCPServer
.\setup-mcp.ps1
```

#### Step 2: Configure VS Code

1. Open VS Code
2. Press `Ctrl+Shift+P`
3. Type: "Open User Settings (JSON)"
4. Add this (or copy from `vscode-settings-snippet.json`):

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
      ]
    }
  }
}
```

5. Save and **restart VS Code**

#### Step 3: Use in Copilot

Open Copilot Chat (`Ctrl+Shift+I`) and try:

```
@aws-textract-processor Check status
@aws-textract-processor Calculate 15 + 27
@aws-textract-processor List files in my S3 bucket
```

**Done!** Copilot can now use your tools.

---

## What Just Happened?

You created an **MCP Server** that:
- ? Exposes 4 tools to AI assistants
- ? Works in interactive mode (testing)
- ? Works with GitHub Copilot (production)
- ? Integrates with AWS services

---

## Available Tools

| Tool | What It Does | Example |
|------|--------------|---------|
| `calculator` | Math operations | `{"operation":"add","a":10,"b":5}` |
| `textract_status` | Check AWS Textract | `{}` (no parameters) |
| `s3_list_files` | List S3 files | `{"bucket":"my-bucket","prefix":"uploads/"}` |
| `document_process` | Process documents | `{"bucket":"my-bucket","key":"file.pdf"}` |

---

## Troubleshooting

### "Build failed"
```powershell
# Check .NET version
dotnet --version

# Should be 9.0 or higher
```

### "Command not found: dotnet"
Install .NET 9.0 SDK from: https://dotnet.microsoft.com/download

### "Tools don't show in Copilot"
1. Make sure you **restarted VS Code**
2. Check settings were saved correctly
3. Update GitHub Copilot extension

### "AWS errors"
```powershell
# Configure AWS credentials
aws configure

# Or check existing
aws sts get-caller-identity
```

---

## What's Next?

### Learn More:
- ?? **Complete Guide**: [MCP_INTEGRATION_GUIDE.md](./MCP_INTEGRATION_GUIDE.md)
- ?? **Quick Reference**: [QUICK_REFERENCE.md](./QUICK_REFERENCE.md)
- ??? **Architecture**: [ARCHITECTURE.md](./ARCHITECTURE.md)
- ?? **All Docs**: [INDEX.md](./INDEX.md)

### Try Interactive Mode:
```powershell
dotnet run
```

Then:
```
demo    # See it in action
list    # View all tools
help# Get more info
```

### Create Your Own Tool:

See [QUICKSTART.md](./QUICKSTART.md) for step-by-step guide to creating custom tools.

---

## Common Questions

**Q: Do I need VS Code?**
A: No! Interactive mode works without VS Code.

**Q: Do I need AWS?**
A: Only for AWS-related tools. Calculator works without AWS.

**Q: Can I add my own tools?**
A: Yes! See QUICKSTART.md for examples.

**Q: Is this secure?**
A: For learning/testing, yes. For production, add authentication.

---

## Success!

You now have a working MCP server! ??

**Choose your next step:**
- ? **Learning?** ? Try interactive mode: `dotnet run`
- ? **Using Copilot?** ? Follow Option B above
- ? **Building tools?** ? Read QUICKSTART.md
- ? **Understanding?** ? Check ARCHITECTURE.md

---

**Need help?** See [INDEX.md](./INDEX.md) for all documentation.
