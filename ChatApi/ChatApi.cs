using Microsoft.EntityFrameworkCore;

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

            var clientMessages = from m in conversation.Messages
                                 select new ClientMessage(m.Id, m.Role, m.Text);

            return Results.Ok(clientMessages);
        });

        group.MapPost("/stream/{id}", (Guid id, StreamContext streamContext, ChatStreamingCoordinator streaming, CancellationToken token) =>
        {
            async IAsyncEnumerable<ClientMessageFragment> StreamMessages()
            {
                // This simulates a flaky connection
                // #if CHAOS
                //                 using var ts = CancellationTokenSource.CreateLinkedTokenSource(token);
                //                 ts.CancelAfter(5000);
                //                 token = ts.Token;
                // #endif

                await foreach (var message in streaming.GetMessageStream(id, streamContext.LastMessageId, streamContext.LastFragmentId).WithCancellation(token))
                {
                    yield return message;
                    await Task.Yield();
                }
            }

            return StreamMessages();
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

        group.MapPost("/{id}", async (Guid id, AppDbContext db, Prompt prompt, CancellationToken token, ChatStreamingCoordinator streaming) =>
        {
            // Fire and forget
            await streaming.AddStreamingMessage(id, prompt.Text);

            return Results.Ok();
        });

        group.MapPost("/{id}/cancel", async (Guid id, ICancellationManager cancellationManager) =>
        {
            await cancellationManager.CancelAsync(id);

            return Results.Ok();
        });

        group.MapDelete("/{id}", async (Guid id, AppDbContext db) =>
        {
            var conversation = await db.Conversations.FindAsync(id);

            if (conversation is null)
            {
                return Results.NotFound();
            }

            db.Conversations.Remove(conversation);
            await db.SaveChangesAsync();

            return Results.Ok();
        });
    }
}

public record Prompt(string Text);

public record NewConversation(string Name);

public record ClientMessage(Guid Id, string Sender, string Text);

public record ClientMessageFragment(Guid Id, string Sender, string Text, Guid FragmentId, bool IsFinal = false);

public record StreamContext(Guid? LastMessageId, Guid? LastFragmentId);