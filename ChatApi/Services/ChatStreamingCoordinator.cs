using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;

public class ChatStreamingCoordinator(
    IChatClient chatClient,
    IServiceScopeFactory scopeFactory,
    ILogger<ChatStreamingCoordinator> logger,
    IConversationState conversationState,
    ICancellationManager cancellationManager)
{
    // TODO: Read this from configuration
    private TimeSpan DefaultStreamItemTimeout = TimeSpan.FromMinutes(1);

    public async Task AddStreamingMessage(Guid conversationId, string text)
    {
        var messages = await SavePromptAndGetMessageHistoryAsync(conversationId, text);

        // Explicitly start the task to avoid blocking the caller.
        _ = Task.Run(StreamReplyAsync);

        async Task StreamReplyAsync()
        {
            Guid assistantReplyId = Guid.CreateVersion7();

            logger.LogInformation("Adding streaming message for conversation {ConversationId} {MessageId}", conversationId, assistantReplyId);

            var allChunks = new List<ChatResponseUpdate>();

            // Combine the provided cancellationToken with the distributed cancellation token.
            var token = cancellationManager.GetCancellationToken(assistantReplyId);

            var fragment = new ClientMessageFragment(assistantReplyId, ChatRole.Assistant.Value, "Generating reply...", Guid.CreateVersion7());
            await conversationState.PublishFragmentAsync(conversationId, fragment);

            try
            {
                using var tokenSource = CancellationTokenSource.CreateLinkedTokenSource(token);
                tokenSource.CancelAfter(DefaultStreamItemTimeout);

                await foreach (var update in chatClient.GetStreamingResponseAsync(messages).WithCancellation(tokenSource.Token))
                {
                    // Extend the cancellation token's timeout for each update.
                    tokenSource.CancelAfter(DefaultStreamItemTimeout);

                    if (update.Text is not null)
                    {
                        allChunks.Add(update);
                        fragment = new ClientMessageFragment(assistantReplyId, ChatRole.Assistant.Value, update.Text, Guid.CreateVersion7());
                        await conversationState.PublishFragmentAsync(conversationId, fragment);
                    }
                }

                logger.LogInformation("Full message received for conversation {ConversationId} {MessageId}", conversationId, assistantReplyId);

                if (allChunks.Count > 0)
                {
                    var fullMessage = allChunks.ToChatResponse().Text;
                    await SaveAssistantMessageToDatabase(conversationId, assistantReplyId, fullMessage);
                }
            }
            catch (OperationCanceledException)
            {
                logger.LogInformation("Streaming message cancelled for conversation {ConversationId} {MessageId}", conversationId, assistantReplyId);

                if (allChunks.Count > 0)
                {
                    var fullMessage = allChunks.ToChatResponse().Text;
                    await SaveAssistantMessageToDatabase(conversationId, assistantReplyId, fullMessage);
                }
            }
            catch (Exception ex)
            {
                fragment = new ClientMessageFragment(assistantReplyId, ChatRole.Assistant.Value, "Error streaming message", Guid.CreateVersion7());
                await conversationState.PublishFragmentAsync(conversationId, fragment);
                logger.LogError(ex, "Error streaming message for conversation {ConversationId} {MessageId}", conversationId, assistantReplyId);

                await SaveAssistantMessageToDatabase(conversationId, assistantReplyId, "Error streaming message");
            }
            finally
            {
                // Publish a final fragment to indicate the end of the message.
                fragment = new ClientMessageFragment(assistantReplyId, ChatRole.Assistant.Value, "", Guid.CreateVersion7(), IsFinal: true);
                await conversationState.PublishFragmentAsync(conversationId, fragment);

                // Clean up the cancellation token.
                await cancellationManager.CancelAsync(assistantReplyId);
            }
        }
    }

    private async Task<IList<ChatMessage>> SavePromptAndGetMessageHistoryAsync(Guid id, string text)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var conversation = await db.Conversations.FindAsync(id) ?? throw new InvalidOperationException($"Conversation {id} not found");

        var messageId = Guid.CreateVersion7();

        conversation.Messages.Add(new()
        {
            Id = messageId,
            Role = ChatRole.User.Value,
            Text = text
        });

        // Actually save conversation history
        await db.SaveChangesAsync();

        // This is inefficient
        var messages = conversation.Messages
            .Select(m => new ChatMessage(new(m.Role), m.Text))
            .ToList();

        // Publish the initial fragment with the prompt text.
        var fragment = new ClientMessageFragment(messageId, ChatRole.User.Value, text, Guid.CreateVersion7(), IsFinal: true);
        await conversationState.PublishFragmentAsync(id, fragment);

        return messages;
    }

    private async Task SaveAssistantMessageToDatabase(Guid conversationId, Guid messageId, string text)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var conversation = await db.Conversations.FindAsync(conversationId);
        if (conversation is not null)
        {
            conversation.Messages.Add(new ConversationChatMessage
            {
                Id = messageId,
                Role = ChatRole.Assistant.Value,
                Text = text
            });

            await db.SaveChangesAsync();
        }

        await conversationState.CompleteAsync(conversationId, messageId);
    }

    public async IAsyncEnumerable<ClientMessageFragment> GetMessageStream(
        Guid conversationId,
        Guid? lastMessageId,
        Guid? lastDeliveredFragment,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Getting message stream for conversation {ConversationId}, {LastMessageId}", conversationId, lastMessageId);
        var stream = conversationState.Subscribe(conversationId, lastMessageId, cancellationToken);

        await foreach (var fragment in stream.WithCancellation(cancellationToken))
        {
            // Use lastMessageId to filter out fragments from an already delivered message,
            // while using lastDeliveredFragment (a sortable GUID) for ordering and de-duping.
            if (lastDeliveredFragment is null || fragment.FragmentId > lastDeliveredFragment)
            {
                lastDeliveredFragment = fragment.FragmentId;
            }
            else
            {
                continue;
            }

            yield return fragment;
        }
    }
}
