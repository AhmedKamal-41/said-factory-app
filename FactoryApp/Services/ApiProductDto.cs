using System.Text.Json.Serialization;

namespace FactoryApp.Services;

internal sealed class ApiProductDto
{
    [JsonPropertyName("ddid")]
    public string DDID { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("photoPath")]
    public string? PhotoPath { get; set; }
}
