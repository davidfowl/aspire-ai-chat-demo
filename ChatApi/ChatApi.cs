using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;

public static class ChatExtensions
{
    public static void MapChatApi(this WebApplication app)
    {
        var group = app.MapGroup("/api/chat");

        group.MapGet("/", (AppDbContext db) =>
        {
            return db.Conversations.ToListAsync();
        });

        group.MapGet("/{id}", async (Guid id, AppDbContext db) =>
        {
            var conversation = await db.Conversations.FindAsync(id);

            if (conversation is null)
            {
                return Results.NotFound();
            }

            return Results.Ok(from m in conversation.Messages
                              select new
                              {
                                  id = m.Id,
                                  sender = m.Role,
                                  text = m.Text
                              });
        });

        group.MapPost("/", async (NewConversation newConversation, AppDbContext db) =>
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

        group.MapPost("/{id}", async (Guid id, AppDbContext db, IChatClient chatClient, Prompt prompt) =>
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
    }
}

public record Prompt(string Text);

public record NewConversation(string Name);
