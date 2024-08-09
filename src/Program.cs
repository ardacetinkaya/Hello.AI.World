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

var builder = Kernel.CreateBuilder();
builder.AddOpenAIChatCompletion(
    modelId: "phi3",
    endpoint: new Uri("http://localhost:11434"),
    apiKey: "apikey");


builder.AddLocalTextEmbeddingGeneration();
Kernel kernel = builder.Build();

var chat = kernel.GetRequiredService<IChatCompletionService>();
var history = new ChatHistory();

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
        history.AddSystemMessage(@"You are a chatbot. You can answer questions and have conversations with me. If you don't know an answer, say 'I don't know!'. Reply with short answers. Don't write long paragraphs.");

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

            var result = await chat.GetChatMessageContentsAsync(history);
            
            AnsiConsole.Markup("[underline yellow]Me:[/] ");
            AnsiConsole.WriteLine("");
            AnsiConsole.WriteLine(result[^1].Content);
            
            history.Add(result[^1]);
        }
    }

}

