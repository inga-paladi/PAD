using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;
namespace posts.Services
{

    public class PostsDbContext : DbContext
    {
        public DbSet<Post> Posts { get; set; }
        public string DbPath { get; }

        public PostsDbContext()
        {
            var folder = Environment.SpecialFolder.LocalApplicationData;
            var path = Environment.GetFolderPath(folder);
            DbPath = System.IO.Path.Join(path, "posts.db");
        }

        protected override void OnConfiguring(DbContextOptionsBuilder options)
            => options.UseSqlite($"Data Source={DbPath}");
    }

    public class Post
    {
        public Guid Id { get; set ; }
        public ulong OwnerId { get; set; }
        public string Title { get; set; }
        public string Content { get; set; }
        public DateTime CreationTime { get; set; } = DateTime.UtcNow;
        public DateTime? LastEditedTime { get; set; } = DateTime.UtcNow;
    }

}
