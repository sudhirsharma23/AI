using System.Collections.Generic;
using System.Threading.Tasks;

namespace MCPServer.Tools
{
    /// <summary>
    /// Interface for MCP Tools
    /// Tools are functions that can be called by AI clients
    /// </summary>
    public interface IMcpTool
    {
        /// <summary>
        /// Unique name for the tool (lowercase with underscores)
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Human-readable description of what the tool does
        /// </summary>
        string Description { get; }

        /// <summary>
        /// JSON Schema defining the input parameters
        /// </summary>
        ToolInputSchema InputSchema { get; }

        /// <summary>
        /// Execute the tool with given parameters
        /// </summary>
        Task<object> ExecuteAsync(Dictionary<string, object> parameters);
    }

    /// <summary>
    /// JSON Schema for tool input parameters
    /// </summary>
    public class ToolInputSchema
    {
        public string Type { get; set; } = "object";
        public Dictionary<string, PropertySchema> Properties { get; set; } = new();
        public List<string> Required { get; set; } = new();
    }

    /// <summary>
    /// Schema for individual property
    /// </summary>
    public class PropertySchema
    {
        public string Type { get; set; }
        public string Description { get; set; }
        public List<string>? Enum { get; set; }
    }
}
