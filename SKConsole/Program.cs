// Import packages
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.Ollama;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using OllamaSharp;
using Microsoft.Extensions.AI;

namespace SKConsole
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var builder = Kernel.CreateBuilder();

            // https://learn.microsoft.com/en-us/dotnet/ai/quickstarts/quickstart-local-ai

            /* // Uncomment to use OpenAI instead
            // Populate values from your OpenAI deployment
            var modelId = "";
            var endpoint = "";
            var apiKey = "";

            // Create a kernel with Azure OpenAI chat completion
            builder.AddAzureOpenAIChatCompletion(modelId, endpoint, apiKey);
            */

            /* - Semantic Kernel Implementation Throws Error
            // Ollama Chat Completion Service
            // Currently in alpha so disabling warning. 
            #pragma warning disable SKEXP0070
            var modelId = "llama3.2:1b";
            var endpoint = new Uri("http://localhost:11434");
            builder.AddOllamaChatCompletion(modelId, endpoint);
            #pragma warning restore SKEXP0070

            // Add enterprise components
            builder.Services.AddLogging(services => services.AddConsole().SetMinimumLevel(LogLevel.Trace));

            // Build the kernel
            Kernel kernel = builder.Build();
            var chatCompletionService = kernel.GetRequiredService<IChatCompletionService>();

            // Create a history store the conversation
            var history = new ChatHistory();

            // Initiate a back-and-forth chat
            string? userInput;
            do {
                // Collect user input
                Console.Write("User > ");
                userInput = Console.ReadLine();

                // Add user input
                history.AddUserMessage(userInput);

                // Get the response from the AI
                var result = await chatCompletionService.GetChatMessageContentAsync(
                    history,
                    kernel: kernel);

                // Print the results
                Console.WriteLine("Assistant > " + result);

                // Add the message from the agent to the chat history
                history.AddMessage(result.Role, result.Content ?? string.Empty);
            } while (userInput is not null);
            */

            await RunUsingOllamaChatClient();
        }

        private static async Task RunUsingOllamaChatClient(){
            // Trying this....
            // https://learn.microsoft.com/en-us/dotnet/ai/quickstarts/quickstart-local-ai
            Microsoft.Extensions.AI.IChatClient chatClient = 
                new OllamaChatClient(new Uri("http://localhost:11434/"), "llama3.2:1b");

            // Start the conversation with context for the AI model
            List<ChatMessage> chatHistory = new();

            while (true)
            {
                // Get user prompt and add to chat history
                Console.WriteLine("Your prompt:");
                var userPrompt = Console.ReadLine();
                chatHistory.Add(new ChatMessage(ChatRole.User, userPrompt));

                // Stream the AI response and add to chat history
                Console.WriteLine("AI Response:");
                var response = "";
                await foreach (var item in
                    chatClient.GetStreamingResponseAsync(chatHistory))
                    //chatClient.CompleteStreamingAsync(chatHistory))
                {
                    Console.Write(item.Text);
                    response += item.Text;
                }
                chatHistory.Add(new ChatMessage(ChatRole.Assistant, response));
                Console.WriteLine();
            }
        }
    }
}

