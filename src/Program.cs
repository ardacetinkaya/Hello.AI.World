#pragma warning disable SKEXP0001
#pragma warning disable SKEXP0003
#pragma warning disable SKEXP0010
#pragma warning disable SKEXP0011
#pragma warning disable SKEXP0050
#pragma warning disable SKEXP0052

using System.Numerics.Tensors;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.KernelMemory;
using Microsoft.KernelMemory.AI;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.AI.Embeddings;
using Microsoft.SemanticKernel.ChatCompletion;
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

builder.AddLocalTextEmbeddingGeneration();

Kernel kernel = builder.Build();
///////

var memoryName = "BRAIN";
var choice = "";

while (choice.ToLower() != "quit")
{
    choice = AnsiConsole.Prompt(
        new SelectionPrompt<string>()
            .Title(""""
    __  __       __ __          ___     ____
   / / / /___   / // /____     /   |   /  _/
  / /_/ // _ \ / // // __ \   / /| |   / /  
 / __  //  __// // // /_/ /  / ___ | _/ /   
/_/ /_/ \___//_//_/ \____/  /_/  |_|/___/   
                                            
What do you want me to do?
"""")
            .PageSize(10)
            .MoreChoicesText("[grey](Move up and down to reveal more options)[/]")
            .AddChoices(new[] {
                "Chat with me",
                "Suggest me a movie",
                "Suggest me a movie v2",
                "Quit"
            }));

    if (choice == "Chat with me")
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
            TopP = 1

        };
        //Define a system user prompt so that chat system can behave according to given prompt
        var chatHistory = new ChatHistory("""
            You are a friendly assistant. You will answer given questions. Answer them in short form, not long sentences.
            If you can not answer, feel free to say I don't know.
            """);

        await LoopAsync($"I agree. Let's chat!", async (question) =>
            await ProcessChatAsync(question, chat, chatHistory, executionSettings));
    }
    else if (choice == "Suggest me a movie")
    {
        var memory = await GenerateMemory(memoryName);

        await LoopAsync($"Tell me more about what are you looking for?", async (question) =>
            await ProcessMovieSuggestionAsync(question, memory, memoryName));

    }
    else if (choice == "Suggest me a movie v2")
    {
        var embeddingGenerator = kernel.Services.GetRequiredService<ITextEmbeddingGenerationService>();

        ISemanticTextMemory memory = new MemoryBuilder()
            .WithLoggerFactory(kernel.LoggerFactory)
            .WithMemoryStore(new VolatileMemoryStore())
            .WithTextEmbeddingGeneration(embeddingGenerator)
            // .WithOpenAITextEmbeddingGeneration("text-embedding-3-small", settings.APIKey)
            .Build();


        foreach (var movie in settings.Movies)
        {
            var plotTexts = TextChunker.SplitPlainTextLines(movie.Plot, 100);
            for (int i = 0; i < plotTexts.Count; i++)
            {
                await memory.SaveInformationAsync(memoryName, plotTexts[i], $"Title: {movie.Title}", $"Plot: {movie.Plot}");
            }
            var genres = TextChunker.SplitPlainTextLines(String.Join(",", movie.Genres), 5);
            for (int i = 0; i < genres.Count; i++)
            {
                await memory.SaveInformationAsync(memoryName, genres[i], $"Title: {movie.Title}", $"Plot: {movie.Plot}");
            }
        }

        await LoopAsync($"Tell me more about what are you looking for?", async (question) =>
            await ProcessMovieSuggestionAsync(question, memory, memoryName));
    }

}

async Task LoopAsync(string welcomeMessage, Func<string, Task> processInput)
{
    while (true)
    {
        AnsiConsole.Markup("[underline green]You:[/] ");
        AnsiConsole.WriteLine("");
        var input = Console.ReadLine();
        if (string.IsNullOrEmpty(input))
        {
            break;
        }

        await processInput(input);
    }
}

async Task ProcessChatAsync(string question, IChatCompletionService chat, ChatHistory chatHistory, OpenAIPromptExecutionSettings executionSettings)
{
    chatHistory.AddUserMessage(question);

    IReadOnlyList<ChatMessageContent> result = await chat.GetChatMessageContentsAsync(chatHistory, executionSettings: executionSettings);

    AnsiConsole.Markup("[underline yellow]Me:[/] ");
    AnsiConsole.WriteLine("");
    AnsiConsole.WriteLine(result[^1].Content);

    chatHistory.Add(result[^1]);
}

async Task ProcessMovieSuggestionAsync(string question, ISemanticTextMemory memoryWithCustomData, string memoryName)
{
    var memoryResults = memoryWithCustomData
                            .SearchAsync(memoryName, question, limit: 2, minRelevanceScore: 0.5);

    AnsiConsole.Markup("[underline yellow]Me:[/] ");
    AnsiConsole.WriteLine("");
    await foreach (MemoryQueryResult memoryResult in memoryResults)
    {
        AnsiConsole.WriteLine(memoryResult.Metadata.Id);
        AnsiConsole.WriteLine($"{memoryResult.Metadata.Description}");
        AnsiConsole.WriteLine();
        AnsiConsole.WriteLine("Relevance for your query is " + memoryResult.Relevance);
        AnsiConsole.WriteLine();
    }
}

async Task<ISemanticTextMemory> GenerateMemory(string memoryName)
{
    var textEmbeddingGenerationService = kernel.GetRequiredService<ITextEmbeddingGenerationService>();
    //Build memory for Volatile local memory
    var memoryWithCustomData = new MemoryBuilder()
                .WithTextEmbeddingGeneration(textEmbeddingGenerationService)
                .WithMemoryStore(new VolatileMemoryStore())
                .Build();

    //Store some mock data in memory
    await StoreInMemoryAsync(memoryWithCustomData, memoryName, settings);

    return memoryWithCustomData;
}

async Task StoreInMemoryAsync(ISemanticTextMemory memory, string memoryName, Settings settings)
{
    foreach (var movie in settings.Movies)
    {
        await memory.SaveReferenceAsync(
            collection: memoryName,
            externalSourceName: "LOCAL",
            externalId: $"Title: {movie.Title}",
            description: $"Plot: {movie.Plot}",
            text: movie.Plot);
    }
}

