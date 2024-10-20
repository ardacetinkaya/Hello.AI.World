
public sealed class Movie
{
    public required string Title { get; set; } = null!;
    public required string Plot { get; set; } = null!;
    public required int Year { get; set; }
    public required string[] Writers { get; set; } = [];
    public required string[] Directors { get; set; } = [];
    public required string[] Genres { get; set; } = [];
    public required string[] Cast { get; set; } = [];

    public override string ToString()
    {
        return $"Title is {Title}\n" +
               $"Plot for the movie is {Plot}\n" +
               $"Movie is released on {Year}\n" +
               $"Writers are {string.Join(", ", Writers)}\n" +
               $"Directors of the movie are {string.Join(", ", Directors)}\n" +
               $"Genres for movie are {string.Join(", ", Genres)}\n" +
               $"Cast contains {string.Join(", ", Cast)}";
    }
}