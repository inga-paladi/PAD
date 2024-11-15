using Microsoft.EntityFrameworkCore;

namespace comments.Services
{
    public class CommentsDbContext : DbContext
    {
        public DbSet<Comment> Comments { get; set; }
        
        protected override void OnConfiguring(DbContextOptionsBuilder options)
        {
            var dbAddress = Environment.GetEnvironmentVariable("DATABASE_ADDRESS");
            dbAddress ??= "localhost";
            options.UseMySQL($"Server={dbAddress};Database=comments;Uid=root;Pwd=changeme;");
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
