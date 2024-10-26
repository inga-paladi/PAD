using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;
namespace posts.Services
{

    public class PostsDbContext : DbContext
    {
        public PostsDbContext() { }
        public PostsDbContext(DbContextOptions<PostsDbContext> options) : base(options) { }
        
        public DbSet<Post> Posts { get; set; }
        
        protected override void OnConfiguring(DbContextOptionsBuilder options)
        {
            // Only configure if options are not provided
            if (!options.IsConfigured)
            {
            var dbDir = Environment.GetEnvironmentVariable("DB_PATH");
            var dbPath = dbDir == null
                ? System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "posts.db")
                : System.IO.Path.Combine(dbDir, "posts.db");
            options.UseSqlite($"Data Source={dbPath}");
        }
    }
    }

    public class Post
    {
        public Guid Id { get; set ; }
        public ulong OwnerId { get; set; }
        public string Title { get; set; } = "";
        public string Content { get; set; } = "";
        public DateTime CreationTime { get; set; } = DateTime.UtcNow;
        public DateTime? LastEditedTime { get; set; } = DateTime.UtcNow;
    }

}
