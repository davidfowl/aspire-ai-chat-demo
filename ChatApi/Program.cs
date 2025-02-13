var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.AddChatClient("llm");

builder.AddCosmosDbContext<AppDbContext>("conversations", "db");

var app = builder.Build();

app.MapDefaultEndpoints();

app.MapChatApi();

app.Run();
