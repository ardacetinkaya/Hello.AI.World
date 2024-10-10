using Azure;
using Azure.AI.Inference;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Spectre.Console;

//Some standard configuration
IConfigurationRoot config = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json")
    .Build();

Settings settings = config.Get<Settings>();

IChatClient client = new ChatCompletionsClient(
        endpoint: new Uri(settings.URI),
        credential: new AzureKeyCredential(settings.APIKey)
        )
        .AsChatClient(settings.ModelId);

var messages = new List<ChatMessage>(){
    new(Microsoft.Extensions.AI.ChatRole.System, "You are a helpful Swedish tourist guide who can speak English but not very well. While you are talking you use some Swedish words in your sentences and try to also explain them. You are PRO about Stockholm but no idea about other cities.")
};

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
        await LoopAsync($"I agree. Let's chat!", async (question) =>
            await ProcessChatAsync(question, client));
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

async Task ProcessChatAsync(string question, IChatClient client)
{
    messages.Add(new ChatMessage(){
         Role= Microsoft.Extensions.AI.ChatRole.Assistant,
         Text = question
    });

    var result = await client.CompleteAsync(messages);

    AnsiConsole.Markup("[underline yellow]Me:[/] ");
    AnsiConsole.WriteLine(result.Message.Text);

    messages.Add(result.Message);
}



record Feature(string DisplayName, int Value)
{
    public override string ToString()
    {
        return DisplayName;
    }
}