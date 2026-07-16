using System.ComponentModel;
using System.Data;
using System.Text.RegularExpressions;
using ModelContextProtocol.Server;

namespace CalculatorMcpServer;

[McpServerToolType]
internal static class CalculatorTools
{
    [McpServerTool]
    [Description("Calculate a simple math expression. Only numbers and + - * / ( ) are allowed.")]
    public static string Calculate(
        [Description("Simple math expression, for example: 15 * (4 + 2)")]
        string expression)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(expression))
            {
                return "Expression is empty.";
            }

            if (!Regex.IsMatch(expression, @"^[0-9+\-*/().\s]+$"))
            {
                return "Invalid expression. Only numbers and + - * / ( ) are allowed.";
            }

            var table = new DataTable();
            var result = table.Compute(expression, "");

            return result?.ToString() ?? "No result.";
        }
        catch (Exception ex)
        {
            return $"Calculation error: {ex.Message}";
        }
    }
}
