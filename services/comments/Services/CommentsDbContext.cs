using Microsoft.Extensions.Hosting;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;
using Google.Protobuf.WellKnownTypes;

namespace comments.Services
{
    public class CommentsDbContext : DbContext
    {
        public DbSet<Comment> Comments { get; set; }
        public string DbPath { get; }

        public CommentsDbContext()
        {
            var folder = Environment.SpecialFolder.LocalApplicationData;
            var path = Environment.GetFolderPath(folder);
            DbPath = System.IO.Path.Join(path, "comments.db");
        }

        protected override void OnConfiguring(DbContextOptionsBuilder options)
            => options.UseSqlite($"Data Source={DbPath}");
    }
    public class Comment
    {
        public Guid Id { get; set; }
        public Guid PostGuid { get; set; }
        public Guid ReplyGuid { get; set; }
        public ulong OwnerId { get; set; }
        public string Content { get; set; }
        public DateTime CreationTime { get; set; } = DateTime.UtcNow;
        public DateTime LastEditedTime { get; set; } = DateTime.UtcNow;
    }

}
