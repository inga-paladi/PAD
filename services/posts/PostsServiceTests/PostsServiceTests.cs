using Grpc.Core;
using Meoworld;
using Microsoft.EntityFrameworkCore;
using Moq;
using posts.Services;
using Xunit;

namespace PostsServiceTests
{
    public class PostsServiceMock : posts.Services.PostsService
    {
        public PostsServiceMock(PostsDbContext context) => base._dbContext = context;
    }

    public class PostsServiceTests : IDisposable
    {
        private readonly PostsDbContext _context;
        private readonly PostsService _service;

        public PostsServiceTests()
        {
            var options = new DbContextOptionsBuilder<PostsDbContext>()
                .UseInMemoryDatabase(databaseName: "PostsTestDb")
                .Options;

            _context = new PostsDbContext(options);
            _service = new PostsServiceMock(_context);
        }

        [Fact]
        public async Task PublishPost_Should_AddPostToDatabase()
        {
            // Arrange
            var request = new PublishPostRequest
            {
                Title = "New Post",
                Content = "Post Content"
            };
            var mockServerCallContext = new Mock<ServerCallContext>();

            // Act
            var response = await _service.PublishPost(request, mockServerCallContext.Object);

            // Assert
            Assert.NotNull(response.Guid);
            var post = await _context.Posts.FindAsync(Guid.Parse(response.Guid));
            Assert.NotNull(post);
            Assert.Equal("New Post", post.Title);
            Assert.Equal("Post Content", post.Content);
        }

        [Fact]
        public async Task EditPost_Should_UpdatePostInDatabase()
        {
            // Arrange
            var post = new Post
            {
                Id = Guid.NewGuid(),
                Title = "Original Title",
                Content = "Original Content",
                CreationTime = DateTime.UtcNow
            };
            _context.Posts.Add(post);
            await _context.SaveChangesAsync();

            var request = new EditPostRequest
            {
                Guid = post.Id.ToString(),
                Title = "Updated Title",
                Content = "Updated Content"
            };
            var mockServerCallContext = new Mock<ServerCallContext>();

            // Act
            var response = await _service.EditPost(request, mockServerCallContext.Object);

            // Assert
            var updatedPost = await _context.Posts.FindAsync(post.Id);
            Assert.NotNull(updatedPost);
            Assert.Equal("Updated Title", updatedPost.Title);
            Assert.Equal("Updated Content", updatedPost.Content);
        }

        [Fact]
        public async Task GetPost_Should_ReturnPost_WhenExists()
        {
            // Arrange
            var post = new Post
            {
                Id = Guid.NewGuid(),
                Title = "Test Post",
                Content = "Test Content",
                CreationTime = DateTime.UtcNow
            };
            _context.Posts.Add(post);
            await _context.SaveChangesAsync();

            var request = new GetPostRequest { Guid = post.Id.ToString() };
            var mockServerCallContext = new Mock<ServerCallContext>();

            // Act
            var response = await _service.GetPost(request, mockServerCallContext.Object);

            // Assert
            Assert.NotNull(response.Post);
            Assert.Equal("Test Post", response.Post.Title);
            Assert.Equal("Test Content", response.Post.Content);
        }

        [Fact]
        public async Task GetPost_Should_ThrowRpcException_WhenPostDoesNotExist()
        {
            // Arrange
            var request = new GetPostRequest { Guid = Guid.NewGuid().ToString() };
            var mockServerCallContext = new Mock<ServerCallContext>();

            // Act & Assert
            var exception = await Assert.ThrowsAsync<RpcException>(async () =>
                await _service.GetPost(request, mockServerCallContext.Object));

            Assert.Equal(StatusCode.NotFound, exception.StatusCode);
        }

        public void Dispose()
        {
            _context.Dispose();
        }
    }
}
