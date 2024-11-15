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
                var dbAddress = Environment.GetEnvironmentVariable("DATABASE_ADDRESS");
                dbAddress ??= "localhost";
                options.UseMySQL($"Server={dbAddress};Database=posts;Uid=root;Pwd=changeme;");
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
