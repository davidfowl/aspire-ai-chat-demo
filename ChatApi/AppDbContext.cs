using Microsoft.EntityFrameworkCore;

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
