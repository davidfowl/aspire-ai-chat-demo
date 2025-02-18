using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Microsoft.Extensions.AI;

public class ChatStreamingCoordinator(
    IChatClient chatClient,
    IServiceScopeFactory scopeFactory,
    ILogger<ChatStreamingCoordinator> logger) : IDisposable
{
    private readonly ConcurrentDictionary<Guid, ConversationState> _stateCache = new();
    private readonly Lock _eventLock = new();
    private Action<Guid, ClientMessageFragment>? OnMessage;

    public async Task AddStreamingMessage(Guid conversationId, Guid assistantReplyId, List<ChatMessage> messages, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Adding streaming message for conversation {ConversationId}", conversationId);

        var state = _stateCache.GetOrAdd(conversationId, _ => new ConversationState());
        await state.Semaphore.WaitAsync(cancellationToken);

        try
        {
            await StreamMessages();
        }
        finally
        {
            state.Semaphore.Release();
        }

        async Task StreamMessages()
        {
            var allChunks = new List<ChatResponseUpdate>();
            var backlog = state.Backlog;


            // Before streaming, we need to set up a synthetic loading message
            var fragment = new ClientMessageFragment(assistantReplyId, "Generating reply...", Guid.CreateVersion7());

            lock (backlog)
            {
                backlog.Clear();
                backlog.Add(fragment);
            }

            NotifySubscribers(conversationId, fragment);

            try
            {
                await foreach (var update in chatClient.GetStreamingResponseAsync(messages).WithCancellation(cancellationToken))
                {
                    if (update.Text is not null)
                    {
                        allChunks.Add(update);
                        fragment = new ClientMessageFragment(assistantReplyId, update.Text, Guid.CreateVersion7());

                        lock (backlog)
                        {
                            backlog.Add(fragment);
                        }

                        NotifySubscribers(conversationId, fragment);
                    }
                }

                logger.LogInformation("Full message received for conversation {ConversationId}", conversationId);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                fragment = new ClientMessageFragment(assistantReplyId, "Error streaming message", Guid.CreateVersion7());
                lock (backlog)
                {
                    backlog.Add(fragment);
                }

                NotifySubscribers(conversationId, fragment);
                logger.LogError(ex, "Error streaming message for conversation {ConversationId}", conversationId);
            }
            finally
            {
                if (allChunks.Count > 0)
                {
                    var fullMessage = allChunks.ToChatResponse().Message;
                    await SaveMessageToDatabase(conversationId, assistantReplyId, fullMessage);
                }
            }
        }
    }

    private async Task SaveMessageToDatabase(Guid conversationId, Guid messageId, ChatMessage message)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var conversation = await db.Conversations.FindAsync(conversationId);
        if (conversation is not null)
        {
            conversation.Messages.Add(new ConversationChatMessage
            {
                Id = messageId,
                Role = message.Role.Value,
                Text = message.Text!
            });

            await db.SaveChangesAsync();
        }
    }

    private void NotifySubscribers(Guid conversationId, ClientMessageFragment fragment)
    {
        Action<Guid, ClientMessageFragment>? handlers;
        lock (_eventLock)
        {
            handlers = OnMessage;
        }
        handlers?.Invoke(conversationId, fragment);
    }

    public async IAsyncEnumerable<ClientMessageFragment> GetMessageStream(
        Guid conversationId,
        Guid? lastMessageId,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Getting message stream for conversation {ConversationId}, {LastMessageId}", conversationId, lastMessageId);

        var channel = Channel.CreateUnbounded<ClientMessageFragment>();
        var lastDeliveredFragment = Guid.Empty;

        // Resolve conversation state.
        _stateCache.TryGetValue(conversationId, out var convState);

        void WriteToChannel(Guid id, ClientMessageFragment message)
        {
            if (id != conversationId)
            {
                return;
            }

            if (message.Id == lastMessageId)
            {
                return;
            }

            channel.Writer.TryWrite(message);
        }

        lock (_eventLock)
        {
            OnMessage += WriteToChannel;
        }

        try
        {
            if (convState is not null)
            {
                lock (convState.Backlog)
                {
                    foreach (var m in convState.Backlog)
                    {
                        WriteToChannel(conversationId, m);
                    }
                }
            }

            using var reg = cancellationToken.Register(() => channel.Writer.TryComplete());

            await foreach (var message in channel.Reader.ReadAllAsync())
            {
                // Use lastMessageId to filter out fragments from an already delivered message,
                // while using lastDeliveredFragment (a sortable GUID) for ordering and de-duping.
                if (message.FragmentId > lastDeliveredFragment)
                {
                    lastDeliveredFragment = message.FragmentId;
                }

                yield return message;
            }
        }
        finally
        {
            lock (_eventLock)
            {
                OnMessage -= WriteToChannel;
            }
        }
    }

    public void Dispose()
    {
        foreach (var (_, state) in _stateCache)
        {
            state.Semaphore.Dispose();
        }
        _stateCache.Clear();
    }

    private sealed class ConversationState
    {
        public List<ClientMessageFragment> Backlog { get; } = [];
        public SemaphoreSlim Semaphore { get; } = new(1, 1);
    }
}
