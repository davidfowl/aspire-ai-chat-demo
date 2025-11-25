using System.Text.Json;
using StackExchange.Redis;
using System.Threading.Channels;
using System.Runtime.CompilerServices;
using System.Collections.Concurrent;

public partial class RedisConversationState : IConversationState, IDisposable
{
    private readonly IDatabase _db;
    private readonly ISubscriber _subscriber;
    private readonly ILogger<RedisConversationState> _logger;

    // Global registry mapping conversationId to list of local callbacks.
    private static readonly ConcurrentDictionary<Guid, List<Action<ClientMessageFragment>>> GlobalSubscribers = new();

    // Store the pattern subscription so we can unsubscribe.
    private readonly RedisChannel _patternChannel;

    // Nagle algorithm: Buffer messages to Redis to reduce network round trips.
    private readonly ConcurrentDictionary<Guid, MessageBuffer> _messageBuffers = [];

    public RedisConversationState(IConnectionMultiplexer redis, ILogger<RedisConversationState> logger)
    {
        _db = redis.GetDatabase();
        _subscriber = redis.GetSubscriber();
        _logger = logger;
        _patternChannel = RedisChannel.Pattern("conversation:*:channel");
        _subscriber.Subscribe(_patternChannel, OnRedisMessage);
        _logger.LogInformation("Subscribed to pattern {Pattern}", _patternChannel);
    }

    private void OnRedisMessage(RedisChannel channel, RedisValue value)
    {
        // Parse conversationId from channel name, assuming format "conversation:{id}:channel".
        var channelStr = channel.ToString();
        var parts = channelStr.Split(':');
        if (parts.Length < 3 || !Guid.TryParse(parts[1], out var conversationId))
        {
            return;
        }

        ClientMessageFragment? fragment = null;

        try
        {
            fragment = JsonSerializer.Deserialize<ClientMessageFragment>((byte[])value!);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deserializing message from channel {Channel}", channel);
            return;
        }

        if (fragment is null)
        {
            return;
        }

        _logger.LogDebug("Redis message received for conversation {ConversationId} with fragment {FragmentId}", conversationId, fragment.Id);

        if (GlobalSubscribers.TryGetValue(conversationId, out var subscribers))
        {
            lock (subscribers)
            {
                foreach (var sub in subscribers)
                {
                    sub(fragment);
                }
            }
        }
    }

    public async Task PublishFragmentAsync(Guid conversationId, ClientMessageFragment fragment)
    {
        _logger.LogDebug("Publishing fragment {FragmentId} for conversation {ConversationId} to Redis", fragment.Id, conversationId);

        var buffer = _messageBuffers.GetOrAdd(fragment.Id, _ => new(_db, _subscriber, _logger, conversationId));
        await buffer.AddFragmentAsync(fragment);
    }

    private static void AddLocalSubscriber(Guid conversationId, Action<ClientMessageFragment> callback)
    {
        var list = GlobalSubscribers.GetOrAdd(conversationId, _ => []);
        lock (list)
        {
            list.Add(callback);
        }
    }

    private static void RemoveLocalSubscriber(Guid conversationId, Action<ClientMessageFragment> callback)
    {
        if (GlobalSubscribers.TryGetValue(conversationId, out var list))
        {
            lock (list)
            {
                list.Remove(callback);
                if (list.Count == 0)
                {
                    GlobalSubscribers.TryRemove(conversationId, out _);
                }
            }
        }
    }

    public async IAsyncEnumerable<ClientMessageFragment> Subscribe(Guid conversationId, Guid? lastMessageId, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("New Redis subscription for conversation {ConversationId} with lastMessageId {LastMessageId}", conversationId, lastMessageId);
        var channel = Channel.CreateUnbounded<ClientMessageFragment>();

        // Register a local callback BEFORE retrieving the backlog.
        void LocalCallback(ClientMessageFragment fragment)
        {
            if (lastMessageId != null && fragment.Id <= lastMessageId)
            {
                return;
            }

            _logger.LogDebug("Fanning out fragment {FragmentId} for conversation {ConversationId}", fragment.Id, conversationId);

            channel.Writer.TryWrite(fragment);
        }

        AddLocalSubscriber(conversationId, LocalCallback);

        // Inline backlog logic: iterate over the fetched RedisValues and yield fragments directly.

        var key = GetBacklogKey(conversationId);
        var values = await _db.ListRangeAsync(key);
        for (int i = 0; i < values.Length; i++)
        {
            var fragment = JsonSerializer.Deserialize<ClientMessageFragment>((ReadOnlySpan<byte>)values[i]!);
            if (fragment is not null && (lastMessageId is null || fragment.Id > lastMessageId))
            {
                yield return fragment;
            }
        }

        try
        {
            using var reg = cancellationToken.Register(() => channel.Writer.TryComplete());
            await foreach (var fragment in channel.Reader.ReadAllAsync())
            {
                yield return fragment;
            }
        }
        finally
        {
            RemoveLocalSubscriber(conversationId, LocalCallback);
            _logger.LogInformation("Redis subscription for conversation {ConversationId} ended", conversationId);
        }
    }

    public async Task CompleteAsync(Guid conversationId, Guid messageId)
    {
        if (_messageBuffers.TryRemove(messageId, out var buffer))
        {
            await buffer.DisposeAsync();
        }

        // Remove the backlog after 5 minutes of inactivity.
        await _db.KeyExpireAsync(GetBacklogKey(conversationId), TimeSpan.FromMinutes(5));
    }

    public async Task<IList<ClientMessage>> GetUnpublishedMessagesAsync(Guid conversationId)
    {
        var key = GetBacklogKey(conversationId);
        var values = await _db.ListRangeAsync(key);
        var fragments = new List<ClientMessageFragment>(values.Length);

        for (int i = 0; i < values.Length; i++)
        {
            var fragment = JsonSerializer.Deserialize<ClientMessageFragment>((ReadOnlySpan<byte>)values[i]!);
            if (fragment is not null)
            {
                fragments.Add(fragment);
            }
        }

        var messages = new List<ClientMessage>();

        foreach (var g in fragments.GroupBy(f => f.Id))
        {
            // The first fragment has the "Generating reply..." text.
            var coalescedFragment = CoalesceFragments([.. g.Skip(1)]);

            var message = new ClientMessage(coalescedFragment.Id, coalescedFragment.Sender, coalescedFragment.Text);

            messages.Add(message);
        }

        return messages;
    }

    public void Dispose()
    {
        try
        {
            _subscriber.Unsubscribe(_patternChannel);
            _logger.LogInformation("Unsubscribed from pattern {Pattern}", _patternChannel);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during unsubscription for pattern {Pattern}", _patternChannel);
        }
    }

    private static RedisKey GetBacklogKey(Guid conversationId) => $"conversation:{conversationId}:backlog";
    private static RedisChannel GetRedisChannelName(Guid conversationId) => RedisChannel.Literal($"conversation:{conversationId}:channel");
}
