#pragma warning disable SKEXP0001
#pragma warning disable SKEXP0010
#pragma warning disable SKEXP0050
#pragma warning disable SKEXP0020


using System.Numerics.Tensors;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.KernelMemory;
using Microsoft.KernelMemory.AI;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.AI.Embeddings;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.MongoDB;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.SemanticKernel.Embeddings;
using Microsoft.SemanticKernel.Memory;
using Microsoft.SemanticKernel.Plugins.Memory;
using Microsoft.SemanticKernel.Text;
using Spectre.Console;


//Some standard configuration
IConfigurationRoot config = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json")
    .AddEnvironmentVariables()
    .Build();

Settings settings = config.Get<Settings>();
////////

//Build the Kernel
var builder = Kernel.CreateBuilder();

builder.AddOpenAIChatCompletion(
    modelId: settings.ModelId,
    endpoint: new Uri(settings.URI),
    apiKey: settings.APIKey);

builder.Services.AddLogging(c => c.AddConsole()
    .SetMinimumLevel(LogLevel.Trace));

builder.AddLocalTextEmbeddingGeneration();
builder.Services.AddKeyedScoped<ISemanticTextMemory>("VolatileMemoryStore", (memory, key) =>
{
    var embeddingGenerator = memory.GetRequiredService<ITextEmbeddingGenerationService>();
    return new MemoryBuilder()
               .WithTextEmbeddingGeneration(embeddingGenerator)
               .WithMemoryStore(new VolatileMemoryStore())
               .Build();
});

builder.Services.AddKeyedScoped<ISemanticTextMemory>("MongoDBMemoryStore", (memory, key) =>
{
    var embeddingGenerator = memory.GetRequiredService<ITextEmbeddingGenerationService>();
    return new MemoryBuilder()
            .WithTextEmbeddingGeneration(embeddingGenerator)
            .WithMemoryStore(new MongoDBMemoryStore(settings.MongoDBConnectionString, "Movies", "embedding"))
            .Build();
});


Kernel kernel = builder.Build();
///////
 
var selectionPrompt = new SelectionPrompt<Feature>()
            .Title(@"
    __  __       __ __          ___     ____
   / / / /___   / // /____     /   |   /  _/
  / /_/ // _ \ / // // __ \   / /| |   / /  
 / __  //  __// // // /_/ /  / ___ | _/ /   
/_/ /_/ \___//_//_/ \____/  /_/  |_|/___/   
                                            
            What do you want me to do?
            ")
            .PageSize(10)
            .MoreChoicesText("[grey](Move up and down to reveal more options)[/]")
            .AddChoices<Feature>(new[] {
                new Feature("Chat with me", 0),
                new Feature("Suggest me a movie (w/VolatileMemoryStore)", 1),
                new Feature("Suggest me a movie (w/MongoDBMemoryStore)", 2),
                new Feature("Quit", -1)
            });

var memoryName = "BRAIN";
Feature choice = null;
ISemanticTextMemory memory = null;

while (true)
{
    choice = AnsiConsole.Prompt(selectionPrompt);

    if(choice.Value==-1){
        break;
    }
    else if (choice.Value == 0)
    {
        var chat = kernel.GetRequiredService<IChatCompletionService>();

        //Configure prompts execution settings
        OpenAIPromptExecutionSettings executionSettings = new()
        {
            ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions,
            // Controls randomness in the response, use lower to be more deterministic.
            Temperature = 0.7,
            //Limit the maximum output tokens for the model response.
            MaxTokens = 1024,
            // Controls text diversity by selecting the most probable words until a set probability is reached.
            TopP = 1,
            ModelId = settings.ModelId

        };
        //Define a system user prompt so that chat system can behave according to given prompt
        var chatHistory = new ChatHistory("You are a friendly assistant. You will answer given questions. Answer them in short form, not long sentences.If you can not answer, feel free to say I don't know.");

        await LoopAsync($"I agree. Let's chat!", async (question) =>
            await ProcessChatAsync(question, chat, chatHistory, executionSettings));
    }
    else if (choice.Value == 1)
    {
        //Generates a volatile memory and fill it with some data
        memory = await GenerateVolatileMemory(memoryName);

        await LoopAsync($"Tell me more about what are you looking for?", async (question) =>
            await ProcessMovieSuggestionWithSemanticSearchAsync(question, memory, memoryName));

    }
    else if (choice.Value == 2)
    {
        //Generates a persistent memory as MongoDB data store and fill it with some data
        memory = await GenerateMongoDBMemory(memoryName);

        await LoopAsync($"Tell me more about what are you looking for?", async (question) =>
        {
            await ProcessMovieSuggestionWithEmbeddedSearchAsync(question, memory, memoryName);
        });
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

async Task ProcessChatAsync(string question, IChatCompletionService chat, ChatHistory chatHistory, OpenAIPromptExecutionSettings executionSettings)
{
    chatHistory.AddUserMessage(question);

    IReadOnlyList<ChatMessageContent> result = await chat.GetChatMessageContentsAsync(chatHistory, executionSettings: executionSettings);

    AnsiConsole.Markup("[underline yellow]Me:[/] ");
    AnsiConsole.WriteLine(result[^1].Content);

    chatHistory.Add(result[^1]);
}

async Task ProcessMovieSuggestionWithSemanticSearchAsync(string question, ISemanticTextMemory memoryWithCustomData, string memoryName)
{
    var memoryResults = memoryWithCustomData
                            .SearchAsync(memoryName, question, limit: 2, minRelevanceScore: 0.5);

    AnsiConsole.Markup("[underline yellow]Me:[/] ");
    if (!await memoryResults.AnyAsync())
    {
        AnsiConsole.Write("I don't know any...");
        AnsiConsole.WriteLine("");
    }

    await foreach (MemoryQueryResult memoryResult in memoryResults)
    {
        AnsiConsole.WriteLine("");
        AnsiConsole.WriteLine(memoryResult.Metadata.Id);
        AnsiConsole.WriteLine($"{memoryResult.Metadata.Description}");
        AnsiConsole.WriteLine();
        AnsiConsole.WriteLine("Relevance for your query is " + memoryResult.Relevance);
        AnsiConsole.WriteLine();
    }
}

async Task ProcessMovieSuggestionWithEmbeddedSearchAsync(string question, ISemanticTextMemory memoryWithCustomData, string memoryName)
{
    var memoryResults = memoryWithCustomData
                            .SearchAsync(memoryName, question
                                , limit: 2
                                , minRelevanceScore: 0.6
                                , withEmbeddings: true
                                , kernel);


    AnsiConsole.Markup("[underline yellow]Me:[/] ");
    if (!await memoryResults.AnyAsync())
    {
        AnsiConsole.Write("I don't know any...");
        AnsiConsole.WriteLine("");
    }

    await foreach (MemoryQueryResult memoryResult in memoryResults.OrderByDescending(o => o.Relevance))
    {
        AnsiConsole.WriteLine("");
        AnsiConsole.WriteLine(memoryResult.Metadata.Id);
        AnsiConsole.WriteLine($"{memoryResult.Metadata.Description}");
        AnsiConsole.WriteLine();
        AnsiConsole.WriteLine("Relevance for your query is " + memoryResult.Relevance);
        AnsiConsole.WriteLine();
    }
}

async Task<ISemanticTextMemory> GenerateVolatileMemory(string memoryName)
{
    var memory = kernel.Services.GetRequiredKeyedService<ISemanticTextMemory>("VolatileMemoryStore");

    foreach (var movie in settings.Movies)
    {
        var movieString = JsonSerializer.Serialize(movie);

        await memory.SaveReferenceAsync(
            collection: memoryName,
            externalSourceName: "LOCAL",
            externalId: $"{movie.Title}",
            description: $"{movie.Plot}",
            additionalMetadata: movieString,
            text: movie.Plot);
    }

    return memory;
}

async Task<ISemanticTextMemory> GenerateMongoDBMemory(string memoryName)
{
    var memory = kernel.Services.GetRequiredKeyedService<ISemanticTextMemory>("MongoDBMemoryStore");

    //Save all data into the memory
    foreach (var movie in settings.Movies)
    {
        await memory.SaveReferenceAsync(
            collection: memoryName,
            text: $@"Plot: {movie.Plot}
Genres: {string.Join(",", movie.Genres)}
Directors: {string.Join(",", movie.Directors)}
Cast: {string.Join(",", movie.Cast)}
Year: {movie.Year}",
            externalId: $"{movie.Title}",
            externalSourceName: $"EXTERNAL_DATA",
            description: $"{movie.Plot}",
            additionalMetadata: string.Empty,
            kernel: kernel);
    }

    return memory;
}

record Feature(string DisplayName, int Value){
    public override string ToString()
    {
        return DisplayName;
    }
}