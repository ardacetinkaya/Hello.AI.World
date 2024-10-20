public sealed class Settings
{
    public required string APIKey { get; set; }
    public required string ModelId { get; set; }
    public required string URI { get; set; }
    public required Provider Provider { get; set; }
}

public enum Provider
{
    Ollama,
    GitHubModels,
    OpenAI,
    AzureAIModels
}
