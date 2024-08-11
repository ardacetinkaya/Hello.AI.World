public sealed class Settings
{
    public required string APIKey { get; set; }
    public required string ModelId { get; set; }
    public required string URI { get; set; }
    public required List<Movie> Movies { get; set; } = null!;
}

public sealed class Movie
{
    public required string MovieName { get; set; } = null!;
    public required string Description { get; set; } = null!;
}