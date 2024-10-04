using Grpc.Core;
using Meoworld;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using posts.Services;
using static Google.Rpc.Context.AttributeContext.Types;

namespace posts.Services
{
    public class PostsService : Blog.BlogBase
    {
        private readonly PostsDbContext _dbContext;

        public PostsService()
        {
            _dbContext = new PostsDbContext();
        }

        public override async Task<PublishPostResponse> PublishPost(PublishPostRequest request, ServerCallContext context)
        {
            var post = new Post
            {
                Id = Guid.NewGuid(),
                Title = request.Title,
                Content = request.Content,
                CreationTime = DateTime.UtcNow,
                LastEditedTime = DateTime.UtcNow
            };

            _dbContext.Posts.Add(post);
            await _dbContext.SaveChangesAsync();

            return new PublishPostResponse { Guid = post.Id.ToString() };
        }

        public override Task<EditPostResponse> EditPost(EditPostRequest request, ServerCallContext context)
        {
            var post = _dbContext.Posts.Find(Guid.Parse(request.Guid));

            if (post == null)
            {
                throw new RpcException(new Status(StatusCode.NotFound, "Post not found"));
            }

            post.Title = request.Title;
            post.Content = request.Content;
            post.LastEditedTime = DateTime.UtcNow;

            _dbContext.Posts.Update(post);

            _dbContext.SaveChanges();

            return Task.FromResult(new EditPostResponse());
        }
        
        public override Task<DeletePostResponse> DeletePost(DeletePostRequest request, ServerCallContext context)
        {
            var post = _dbContext.Posts.Find(Guid.Parse(request.Guid));
            if (post == null)
            {
                throw new RpcException(new Status(StatusCode.NotFound, "Post not found"));
            }

            _dbContext.Posts.Remove(post);
            _dbContext.SaveChangesAsync();

            return Task.FromResult(new DeletePostResponse());
        }

        public override Task<ListPostsResponse> ListPosts(ListPostsRequest request, ServerCallContext context)
        {
            var posts = _dbContext.Posts.ToList(); 

            var response = new ListPostsResponse();
            response.Posts.AddRange(posts.Select(p => new BlogPost
            {
                Guid = p.Id.ToString(),
                Title = p.Title,
                Content = p.Content,
                CreationTime = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(p.CreationTime.ToUniversalTime()),
                LastEditedTime = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(p.LastEditedTime.Value.ToUniversalTime())
            
            }));

            return Task.FromResult(response); 
        }


        public override Task<GetPostResponse> GetPost(GetPostRequest request, ServerCallContext context)
        {
            var post = _dbContext.Posts.Find(Guid.Parse(request.Guid));
            if (post == null)
            {
                throw new RpcException(new Status(StatusCode.NotFound, "Post not found"));
            }
            var response = new GetPostResponse
            {
                Post = new BlogPost
                {
                    Guid = post.Id.ToString(),
                    Title = post.Title,
                    Content = post.Content,
                    CreationTime = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(post.CreationTime.ToUniversalTime()),
                }
            };
            if (post.LastEditedTime.HasValue)
                response.Post.LastEditedTime = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(post.LastEditedTime.Value.ToUniversalTime());

            return Task.FromResult(response);
        }

        public override async Task Listen(ListenRequest request, IServerStreamWriter<ListenResponse> responseStream, ServerCallContext context)
        {
            // Implementation for streaming new posts
            await Task.CompletedTask;
        }
    }
}


