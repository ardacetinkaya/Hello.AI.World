public sealed class Settings
{
    public required string APIKey { get; set; }
    public required string ModelId { get; set; }
    public required string URI { get; set; }
    public required List<Movie> Movies { get; set; } = null!;

    public required string MongoDBConnectionString { get; set; }
}

public sealed class Movie
{
    public required string Title { get; set; } = null!;
    public required string Plot { get; set; } = null!;
    public required int Year { get; set; }
    public required string[] Writers { get; set; } = [];
    public required string[] Directors { get; set; } = [];
    public required string[] Genres { get; set; } = [];
    public required string[] Cast { get; set; } = [];
}