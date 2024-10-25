using Microsoft.EntityFrameworkCore;

namespace comments.Services
{
    public class CommentsDbContext : DbContext
    {
        public DbSet<Comment> Comments { get; set; }
        
        protected override void OnConfiguring(DbContextOptionsBuilder options)
        {
            var dbDir = Environment.GetEnvironmentVariable("DB_PATH");
            var dbPath = dbDir == null
                ? System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "comments.db")
                : System.IO.Path.Combine(dbDir, "comments.db");
            options.UseSqlite($"Data Source={dbPath}");
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
