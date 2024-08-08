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

var builder = Kernel.CreateBuilder();
builder.AddOpenAIChatCompletion(
    modelId: "phi3",
    endpoint: new Uri("http://localhost:11434"),
    apiKey: "apikey");


builder.AddLocalTextEmbeddingGeneration();
Kernel kernel = builder.Build();

var chat = kernel.GetRequiredService<IChatCompletionService>();
var history = new ChatHistory();
history.AddSystemMessage(@"You are a useful chatbot. 
If you don't know an answer, say 'I don't know!'. 
Always reply in a funny ways. Use emojis if possible.");

while (true)
{
    Console.Write("Question:");
    
    var question = Console.ReadLine();
    if (string.IsNullOrEmpty(question))
    {
        break;
    }

    history.AddUserMessage(question);

    var result = await chat.GetChatMessageContentsAsync(history);
    
    Console.WriteLine(result[^1].Content);
    
    history.Add(result[^1]);
}