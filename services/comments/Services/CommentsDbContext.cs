using Microsoft.EntityFrameworkCore;

namespace comments.Services
{
    public class CommentsDbContext : DbContext
    {
        public DbSet<Comment> Comments { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder options)
        {
            var dbAddress = Environment.GetEnvironmentVariable("DATABASE_ADDRESS") ?? "localhost";
            var dbPort = Environment.GetEnvironmentVariable("DATABASE_PORT") ?? "3306";
            NLog.LogManager.GetCurrentClassLogger().Info($"Connection string for comments database: Server={dbAddress};Port={dbPort};Database=comments;Uid=root;Pwd=changeme;");
            options.UseMySQL($"Server={dbAddress};Port={dbPort};Database=comments;Uid=root;Pwd=changeme;");
        }
    }
    public class Comment
    {
        public Guid Id { get; set; }
        public Guid PostGuid { get; set; }
        public Guid? ReplyGuid { get; set; }
        public ulong OwnerId { get; set; }
        public string Content { get; set; } = "";
        public DateTime CreationTime { get; set; } = DateTime.UtcNow;
        public DateTime? LastEditedTime { get; set; } = DateTime.UtcNow;
    }

}
