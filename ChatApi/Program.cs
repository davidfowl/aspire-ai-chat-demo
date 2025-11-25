using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

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

if (app.Environment.IsDevelopment())
{
    // Map OpenAPI and Scalar
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.MapDefaultEndpoints();

app.MapChatApi();

app.Run();
