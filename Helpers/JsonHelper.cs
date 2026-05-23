using System.Text.Json;
using System.Text.Json.Serialization;

namespace TMM
{
    public static class JsonHelper
    {
        public static JsonSerializerOptions PrettyOptions { get; } = new() { WriteIndented = true };

        public static JsonSerializerOptions TmmGameOptions { get; } = new()
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };
    }
}
