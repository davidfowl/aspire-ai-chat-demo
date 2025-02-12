using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.AddChatClient("llm");

var app = builder.Build();

app.MapDefaultEndpoints();

var conversations = new ConcurrentDictionary<Guid, Conversation>();

app.MapGet("/api/chat", () =>
{
    return conversations.Values;
});

app.MapGet("/api/chat/{id}", (Guid id) =>
{
    if (!conversations.TryGetValue(id, out var conversation))
    {
        return Results.NotFound();
    }

    return Results.Ok(conversation.Messages.Select((m, index) => new
    {
        id = index,
        sender = m.Role.Value,
        text = m.Text
    }));
});

app.MapPost("/api/chat", (NewConversation newConversation) =>
{
    if (string.IsNullOrWhiteSpace(newConversation.Name))
    {
        return Results.BadRequest();
    }

    var conversation = new Conversation(Guid.CreateVersion7(), newConversation.Name, []);

    conversations.TryAdd(conversation.Id, conversation);
    return Results.Created($"/api/chats/{conversation.Id}", conversation);
});

app.MapPost("/api/chat/{id}", (Guid id, IChatClient chatClient, Prompt prompt) =>
{
    if (!conversations.TryGetValue(id, out var conversation))
    {
        return Results.NotFound();
    }

    conversation.Messages.Add(new(ChatRole.User, prompt.Text));

    var allChunks = new List<StreamingChatCompletionUpdate>();

    async IAsyncEnumerable<string?> GetMessages([EnumeratorCancellation] CancellationToken ct)
    {
        await foreach (var update in chatClient.CompleteStreamingAsync(conversation.Messages, cancellationToken: ct))
        {
            allChunks.Add(update);
            yield return update.Text;
        }

        var assistantMessage = allChunks.ToChatCompletion().Message;

        conversation.Messages.Add(assistantMessage);
    }

    return Results.Extensions.SseStream(GetMessages);
});

app.Run();

public record Prompt(string Text);

public record NewConversation(string Name);

public record Conversation(Guid Id, string Name, List<ChatMessage> Messages);