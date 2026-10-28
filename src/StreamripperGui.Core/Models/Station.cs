namespace StreamripperGui.Core.Models;

public sealed record Station
{
    [System.Text.Json.Serialization.JsonRequired]
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    [System.Text.Json.Serialization.JsonRequired]
    public string Name { get; init; } = "";
    [System.Text.Json.Serialization.JsonRequired]
    public string Url { get; init; } = "";
    public string? RecordingProfileId { get; init; }
    public string? MetadataRuleSetId { get; init; }
    public string? ImportedName { get; init; }
    public System.Text.Json.Nodes.JsonObject? LastObservedStream { get; init; }

    [System.Text.Json.Serialization.JsonExtensionData]
    public Dictionary<string, System.Text.Json.JsonElement>? AdditionalData { get; init; }
}
