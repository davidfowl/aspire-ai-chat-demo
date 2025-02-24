var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.AddChatClient("llm");
builder.AddRedisClient("cache");

builder.AddCosmosDbContext<AppDbContext>("conversations", "db");

builder.Services.AddSingleton<ChatStreamingCoordinator>();
builder.Services.AddSingleton<IConversationState, RedisConversationState>();
builder.Services.AddSingleton<ICancellationManager, RedisCancellationManager>();

var app = builder.Build();

app.MapDefaultEndpoints();

app.MapChatApi();

app.Run();
