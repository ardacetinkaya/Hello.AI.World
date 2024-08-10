#pragma warning disable SKEXP0001
#pragma warning disable SKEXP0003
#pragma warning disable SKEXP0010
#pragma warning disable SKEXP0011
#pragma warning disable SKEXP0050
#pragma warning disable SKEXP0052

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.KernelMemory;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.SemanticKernel.Embeddings;
using Microsoft.SemanticKernel.Memory;
using Microsoft.SemanticKernel.Plugins.Memory;
using Spectre.Console;


IConfigurationRoot config = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json")
    .AddEnvironmentVariables()
    .Build();

Settings? settings = config.Get<Settings>();


var builder = Kernel.CreateBuilder();
builder.AddOpenAIChatCompletion(
    modelId: settings.ModelId,
    endpoint: new Uri(settings.URI),
    apiKey: settings.APIKey);


builder.AddLocalTextEmbeddingGeneration();
Kernel kernel = builder.Build();

var chat = kernel.GetRequiredService<IChatCompletionService>();
OpenAIPromptExecutionSettings executionSettings = new() 
{
    ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions,
    Temperature = 0.7,
    MaxTokens = 1024,
    TopP = 1

};
var history = new ChatHistory("""
    You are a friendly assistant who likes to follow the rules. You will complete required steps
    and request approval before taking any consequential actions. If the user doesn't provide
    enough information for you to complete a task, you will keep asking questions until you have
    enough information to complete the task.
    """);

var choice = "";

while(choice.ToLower() != "quit"){
    choice = AnsiConsole.Prompt(
        new SelectionPrompt<string>()
            .Title("What do you want to do?")
            .PageSize(10)
            .MoreChoicesText("[grey](Move up and down to reveal more options)[/]")
            .AddChoices(new[] {
                "Chat", "Quit"
            }));

    if(choice.ToLower() == "chat"){

        AnsiConsole.WriteLine($"I agree. Let's chat!");
        AnsiConsole.WriteLine($"How are you?");
        // history.AddSystemMessage(@"Answer questions in a short way. If you don't know an answer, say 'I don't know!'. Don't write long sentences.");

        while (true)
        {
            
            AnsiConsole.Markup("[underline green]You:[/] ");
            AnsiConsole.WriteLine("");
            var question = Console.ReadLine();
            if (string.IsNullOrEmpty(question))
            {
                break;
            }

            history.AddUserMessage(question);

            var result = await chat.GetChatMessageContentsAsync(history, executionSettings:executionSettings);
            
            AnsiConsole.Markup("[underline yellow]Me:[/] ");
            AnsiConsole.WriteLine("");
            AnsiConsole.WriteLine(result[^1].Content);
            
            history.Add(result[^1]);
        }
    }

}

