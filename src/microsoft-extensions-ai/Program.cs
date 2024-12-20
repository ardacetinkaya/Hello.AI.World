using System.ClientModel;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Azure;
using Azure.AI.Inference;
using Azure.AI.OpenAI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.SemanticKernel.Memory;
using Spectre.Console;

#pragma warning disable SKEXP0001
#pragma warning disable SKEXP0050

IConfigurationRoot config = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json") //For some standard application settings
    .AddJsonFile("data.json") //Some custom data
    .AddEnvironmentVariables()
    .Build();

var builder = Host.CreateApplicationBuilder(args);

Settings settings = config.Get<Settings>();
Data<Movie> data = config.Get<Data<Movie>>();

// Registers a chat client service for dependency injection, configuring it based on the selected provider.
builder.Services.AddChatClient(c =>
{
    IChatClient client = null;
    switch (settings.Provider)
    {
        case Provider.GitHubModels:
        case Provider.AzureAIModels:
            client = new ChatCompletionsClient(
                    endpoint: new Uri(settings.URI),
                    credential: new AzureKeyCredential(settings.APIKey))
                .AsChatClient(settings.ModelId);
            break;
        case Provider.OpenAI:
            client = new OpenAI.OpenAIClient(
                    credential: new ApiKeyCredential(settings.APIKey))
                .AsChatClient(modelId: settings.ModelId);
            break;
    }

    return new ChatClientBuilder().Use(client);
});

// Registers an embedding generator service for transforming strings into embeddings (numerical representations).
// Specifying the model ID "text-embedding-3-small"(though other embedding-supported models can also be used if desired).
builder.Services.AddEmbeddingGenerator<string, Embedding<float>>(e =>
{
    IEmbeddingGenerator<string, Embedding<float>> generator = new AzureOpenAIClient(
            endpoint: new Uri(settings.URI),
            credential: new ApiKeyCredential(settings.APIKey))
        .AsEmbeddingGenerator(modelId: "text-embedding-3-small"); //Some other embedding supported models also can be used

    return new EmbeddingGeneratorBuilder<string, Embedding<float>>().Use(generator);
});


// Registers a scoped service for IMemoryStore, using a VolatileMemoryStore implementation.
// VolatileMemoryStore is typically used for in-memory storage that does not persist beyond the lifetime of the application.
// There are some other API provided stores like MongoDBMemoryStore, Redis...
builder.Services.AddScoped<IMemoryStore>(m => new VolatileMemoryStore());

// Registers a scoped service for ISemanticTextMemory with a specific key ("TextMemory").
// This allows for dependency injection of a semantic text memory component in the application.
// It retrieves an IEmbeddingGenerator service for generating text embeddings and uses it to create
// a custom text embedding generator (CustomTextEmbeddingGenerator).
builder.Services.AddKeyedScoped<ISemanticTextMemory>("TextMemory", (memory, key) =>
{
    var textEmbeddingGenerator = memory.GetService<IEmbeddingGenerator<string, Embedding<float>>>();
    return new MemoryBuilder()
            .WithTextEmbeddingGeneration(new CustomTextEmbeddingGenerator(textEmbeddingGenerator))
            .WithMemoryStore<IMemoryStore>(s =>
            {
                return memory.GetService<IMemoryStore>();
            })
            .Build();
});

var host = builder.Build();

await GenerateEmbeddings();

await GenerateMemory();


var selectionPrompt = new SelectionPrompt<Feature>()
    .Title(@"What do you want me to do?s")
    .PageSize(10)
    .MoreChoicesText("[grey](Move up and down to reveal more options)[/]")
    .AddChoices<Feature>(new[] {
        new Feature("Chat with me as a tourist guide", 0),
        new Feature("Chat with me for movies", 1),
        new Feature("Quit", -1)
    });

Feature choice = null;
while (true)
{
    choice = AnsiConsole.Prompt(selectionPrompt);

    if (choice.Value == -1)
    {
        break;
    }
    else if (choice.Value == 0)
    {
        var messages = new List<ChatMessage>(){
            new(Microsoft.Extensions.AI.ChatRole.System, $$"""
            You are a helpful Swedish tourist guide who can speak English but not very well. 
            While you are talking you use some Swedish words in your sentences. 
            You are PRO about Stockholm but no idea about other cities.
            """)
        };

        await LoopAsync($"I agree. Let's chat!", async (question) =>
            await ProcessChatAsync(question, messages));
    }
    else if (choice.Value == 1)
    {
        var messages = new List<ChatMessage>(){
            new(Microsoft.Extensions.AI.ChatRole.System, $$"""
            You are a cinephile. You watched given movies and you really like to talk about them.
            You can just talk about watched movies.
            You have a good memory, you can map asked questions within your memory easily.

            You really like to share your knowledge according to given questions.
            """)
        };

        await LoopAsync($"I agree. Let's talk about some movies!", async (question) =>
            await ProcessChatAsync(question, messages, true));
    }


}

async Task LoopAsync(string welcomeMessage, Func<string, Task> process)
{
    AnsiConsole.Markup($"[underline yellow]Me:[/] {welcomeMessage}");
    AnsiConsole.WriteLine("");
    while (true)
    {
        AnsiConsole.Markup("[underline green]You:[/] ");
        var input = Console.ReadLine();
        if (string.IsNullOrEmpty(input))
        {
            break;
        }

        await process(input);
    }
}

async Task ProcessChatAsync(string question, List<ChatMessage> messages, bool searchMemory = false)
{
    messages.Add(new ChatMessage()
    {
        Role = Microsoft.Extensions.AI.ChatRole.User,
        Text = question
    });

    if (searchMemory)
    {
        var results = await SearchInMemory(question);
        if (results.Any())
        {
            messages.AddRange(results);
        }
        else
        {
            messages.Add(new ChatMessage
            {
                Role = Microsoft.Extensions.AI.ChatRole.System,
                Text = $"""
                As a source for the question, you don't have watched film, answer politly and ask for some recomendation.
                """
            });

        }

    }

    var client = host.Services.GetService<IChatClient>();
    var result = await client.CompleteAsync(messages);

    AnsiConsole.Markup("[underline yellow]Me:[/] ");
    AnsiConsole.WriteLine(result.Message.Text);

    messages.Add(result.Message);
}

async Task GenerateEmbeddings([NotNull] string fileName = "embeddeddata.json")
{
    if (!File.Exists(fileName))
    {
        var embededDataList = new List<RecordEmbedding<Movie>>();
        var serializedData = new List<string>();

        foreach (var item in data.Records)
        {
            serializedData.Add(item.ToString());
        }

        var generator = host.Services.GetService<IEmbeddingGenerator<string, Embedding<float>>>();
        var embededData = await generator.GenerateAsync(serializedData);

        for (int i = 0; i < data.Records.Count; i++)
        {
            embededDataList.Add(new RecordEmbedding<Movie>
            {
                Title = data.Records[i].Title,
                Record = data.Records[i],
                Embeddings = embededData[i].Vector.ToArray()
            });
        }

        await File.WriteAllTextAsync(fileName, JsonSerializer.Serialize(embededDataList));
    }

}

async Task GenerateMemory([NotNull] string fromFile = "embeddeddata.json", [NotNull] string memoryName = "BRAIN")
{
    if (File.Exists(fromFile))
    {
        var movies = JsonSerializer.Deserialize<RecordEmbedding<Movie>[]>(File.ReadAllText(fromFile))!;
        var semanticMemory = host.Services.GetRequiredService<IMemoryStore>();
        await semanticMemory.CreateCollectionAsync(memoryName);
        var mappedRecords = movies.Select(movie =>
        {
            var id = movie.Title;
            var text = movie.Title;
            var description = movie.Record.Plot;

            var metadata = new MemoryRecordMetadata(false, id, text, description, string.Empty, movie.Record.ToString());
            return new MemoryRecord(metadata, movie.Embeddings, null);
        });

        await foreach (var _ in semanticMemory.UpsertBatchAsync(memoryName, mappedRecords)) { }

    }

}

async Task<List<ChatMessage>> SearchInMemory(string question, [NotNull] string memoryName = "BRAIN")
{
    var messages = new List<ChatMessage>();
    if (!string.IsNullOrEmpty(question))
    {
        var memory = host.Services.GetKeyedService<ISemanticTextMemory>("TextMemory");

        var searchResult = memory.SearchAsync(memoryName, question, 4, 0.4, true);
        await foreach (MemoryQueryResult memoryResult in searchResult)
        {
            messages.Add(new ChatMessage
            {
                Role = Microsoft.Extensions.AI.ChatRole.System,
                Text = $"""
                As a source for the question, you get this info inside your mind

                <watched_movie>
                    {memoryResult.Metadata.AdditionalMetadata}
                <watched_movie>
                """
            });
        }
    }

    return messages;
}
