using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.AddChatClient("llm");

builder.AddCosmosDbContext<AppDbContext>("conversations", "db");

var app = builder.Build();

app.MapDefaultEndpoints();

app.MapGet("/api/chat", (AppDbContext db) =>
{
    return db.Conversations.ToListAsync();
});

app.MapGet("/api/chat/{id}", async (Guid id, AppDbContext db) =>
{
    var conversation = await db.Conversations.FindAsync(id);

    if (conversation is null)
    {
        return Results.NotFound();
    }

    return Results.Ok(conversation.Messages.Select((m, index) => new
    {
        id = m.Id,
        sender = m.Role,
        text = m.Text
    }));
});

app.MapPost("/api/chat", async (NewConversation newConversation, AppDbContext db) =>
{
    if (string.IsNullOrWhiteSpace(newConversation.Name))
    {
        return Results.BadRequest();
    }

    var conversation = new Conversation
    {
        Id = Guid.CreateVersion7(),
        Name = newConversation.Name,
        Messages = []
    };

    db.Conversations.Add(conversation);
    await db.SaveChangesAsync();

    return Results.Created($"/api/chats/{conversation.Id}", conversation);
});

app.MapPost("/api/chat/{id}", async (Guid id, AppDbContext db, IChatClient chatClient, Prompt prompt) =>
{
    var conversation = await db.Conversations.FindAsync(id);

    if (conversation is null)
    {
        return Results.NotFound();
    }

    conversation.Messages.Add(new()
    {
        Id = Guid.CreateVersion7(),
        Role = ChatRole.User.Value,
        Text = prompt.Text
    });

    var allChunks = new List<StreamingChatCompletionUpdate>();

    // This is inefficient
    var messages = conversation.Messages
        .Select(m => new ChatMessage(new(m.Role), m.Text))
        .ToList();

    async IAsyncEnumerable<string?> StreamMessages([EnumeratorCancellation] CancellationToken ct)
    {
        await foreach (var update in chatClient.CompleteStreamingAsync(messages, cancellationToken: ct))
        {
            allChunks.Add(update);
            yield return update.Text;
        }

        var assistantMessage = allChunks.ToChatCompletion().Message;

        conversation.Messages.Add(new()
        {
            Id = Guid.CreateVersion7(),
            Role = assistantMessage.Role.Value,
            Text = assistantMessage.Text!
        });

        await db.SaveChangesAsync();
    }

    return Results.Extensions.SseStream(StreamMessages);
});

app.Run();

public record Prompt(string Text);

public record NewConversation(string Name);

public class ConversationChatMessage
{
    public required Guid Id { get; set; }
    public required string Role { get; set; }
    public required string Text { get; set; }
}

public class Conversation
{
    public required Guid Id { get; set; }
    public required string Name { get; set; }
    public required List<ConversationChatMessage> Messages { get; set; } = [];
}

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public required DbSet<Conversation> Conversations { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
         modelBuilder.Entity<Conversation>()
            .HasPartitionKey(c => c.Id)
            .ToContainer("conversations");
    }
}