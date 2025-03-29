var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.AddChatClient("llm");
builder.AddRedisClient("cache");
builder.AddNpgsqlDbContext<AppDbContext>("conversations");

builder.Services.AddSignalR();
builder.Services.AddSingleton<ChatStreamingCoordinator>();
builder.Services.AddHostedService<EnsureDatabaseCreatedHostedService>();

builder.Services.AddSingleton<IConversationState, RedisConversationState>();
builder.Services.AddSingleton<ICancellationManager, RedisCancellationManager>();

var app = builder.Build();

app.MapDefaultEndpoints();

app.MapChatApi();

app.Run();
