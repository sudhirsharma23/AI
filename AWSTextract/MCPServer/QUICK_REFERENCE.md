# ?? MCP Server Quick Reference Card

## ?? One-Time Setup

### 1. Build & Test
```powershell
cd E:\Sudhir\GitRepo\AWSTextract\MCPServer
.\setup-mcp.ps1
```

### 2. Configure VS Code
1. Press `Ctrl+Shift+P`
2. Type: "Preferences: Open User Settings (JSON)"
3. Copy content from `vscode-settings-snippet.json`
4. Paste into settings.json
5. Save and **restart VS Code**

---

## ?? Using in Copilot Chat

### Format:
```
@aws-textract-processor <your request>
```

### Quick Examples:
```
@aws-textract-processor status
@aws-textract-processor list files
@aws-textract-processor calculate 10 + 5
@aws-textract-processor help
```

---

## ??? Available Commands

| Command | What it does |
|---------|--------------|
| `@aws-textract-processor status` | Check Textract service |
| `@aws-textract-processor list files in BUCKET` | List S3 files |
| `@aws-textract-processor process s3://BUCKET/KEY` | Process document |
| `@aws-textract-processor calculate X + Y` | Math operations |

---

## ?? Testing Locally

### Interactive Mode:
```powershell
cd E:\Sudhir\GitRepo\AWSTextract\MCPServer
dotnet run
```

Commands in interactive mode:
```
list     # Show all tools
call tool_name {} # Execute a tool
demo          # Run demos
help   # Show help
exit        # Quit
```

### MCP Server Mode:
```powershell
dotnet run -- --mcp
```
(This is what VS Code calls automatically)

---

## ?? Tool Details

### textract_status
- **No parameters needed**
- Returns: Service availability status

### s3_list_files
- **bucket** (required): S3 bucket name
- **prefix** (optional): Folder path
- Returns: List of files with metadata

### document_process
- **bucket** (required): S3 bucket name
- **key** (required): Full path to file
- Returns: Processed document data

### calculator
- **operation** (required): add/subtract/multiply/divide
- **a** (required): First number
- **b** (required): Second number
- Returns: Calculation result

---

## ?? Troubleshooting

| Issue | Solution |
|-------|----------|
| Tools don't show in Copilot | Restart VS Code |
| "Command failed" error | Check path in settings.json |
| AWS errors | Run: `aws sts get-caller-identity` |
| Server won't start | Run: `dotnet build` |

---

## ?? Files Reference

| File | Purpose |
|------|---------|
| `INTEGRATION_SUMMARY.md` | Complete overview |
| `MCP_INTEGRATION_GUIDE.md` | Detailed setup guide |
| `vscode-settings-snippet.json` | VS Code config |
| `setup-mcp.ps1` | Automated setup |
| `README.md` | Project documentation |

---

## ?? Common Workflows

### Check & List:
```
@aws-textract-processor Check if Textract is working, then list files in testbucket-sudhir-bsi1
```

### Process Document:
```
@aws-textract-processor Process the PDF at s3://testbucket-sudhir-bsi1/uploads/invoice.pdf
```

### Batch Operation:
```
@aws-textract-processor List all files in bucket testbucket-sudhir-bsi1 with prefix uploads/, then process each PDF file
```

---

## ?? Pro Tips

1. **Be Specific**: Include full S3 paths
2. **Check Status First**: Verify services are up
3. **Use Natural Language**: Copilot understands context
4. **Combine Tools**: Ask for multi-step workflows

---

## ?? Quick Links

- **MCP Spec**: https://spec.modelcontextprotocol.io/
- **Copilot Docs**: https://docs.github.com/copilot
- **AWS Textract**: https://docs.aws.amazon.com/textract/

---

## ?? Need Help?

1. Check `MCP_INTEGRATION_GUIDE.md`
2. Run `dotnet run` for interactive testing
3. Check logs: `dotnet run -- --mcp 2> error.log`
4. Verify AWS: `aws sts get-caller-identity`

---

**Version**: 1.0  
**Last Updated**: 2025-01-20  
**Status**: ? Production Ready
