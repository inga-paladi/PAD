using Grpc.Core;
using Meoworld.V1.Blog;
using Meoworld.V1.Shared;

namespace posts.Services
{
    public class PostsService : Blog.BlogBase
    {
        public override Task<PublishPostResponse> PublishPost(PublishPostRequest request, ServerCallContext context)
        {
            var post = new Post
            {
                Id = Guid.NewGuid(),
                Title = request.Title,
                Content = request.Content,
                CreationTime = DateTime.UtcNow,
                LastEditedTime = DateTime.UtcNow
            };

            var dbContext = new PostsDbContext();
            dbContext.Posts.Add(post);
            dbContext.SaveChanges();

            return Task.FromResult(new PublishPostResponse { Guid = post.Id.ToString() });
        }

        public override Task<EditPostResponse> EditPost(EditPostRequest request, ServerCallContext context)
        {
            var dbContext = new PostsDbContext();
            var post = dbContext.Posts.Find(Guid.Parse(request.Guid));

            if (post == null)
                throw new RpcException(new Status(StatusCode.NotFound, "Post not found"));

            post.Title = request.Title;
            post.Content = request.Content;
            post.LastEditedTime = DateTime.UtcNow;

            dbContext.Posts.Update(post);
            dbContext.SaveChanges();

            return Task.FromResult(new EditPostResponse());
        }

        public override Task<DeletePostResponse> DeletePost(DeletePostRequest request, ServerCallContext context)
        {
            var transactionId = context.RequestHeaders.GetValue("Saga-Transaction-Id");
            return string.IsNullOrEmpty(transactionId)
                ? DeletePostAsSimpleRequest(request)
                : DeletePostAsPartOfSaga(request, transactionId);
        }

        private Task<DeletePostResponse> DeletePostAsPartOfSaga(DeletePostRequest request, string transactionId)
        {
            var hasRunningTransaction = shared.TransactionManager<PostsDbContext>.Instance.HasTransaction(transactionId);
            if (!hasRunningTransaction && !shared.TransactionManager<PostsDbContext>.Instance.StartTransaction(transactionId))
                throw new RpcException(new Status(StatusCode.Internal, "Can't initiate a transaction"));

            var dbContext = shared.TransactionManager<PostsDbContext>.Instance.GetDbContext(transactionId);

            var post = dbContext.Posts.Find(Guid.Parse(request.Guid));
            if (post == null)
            {
                shared.TransactionManager<PostsDbContext>.Instance.DisposeTransaction(transactionId);
                throw new RpcException(new Status(StatusCode.NotFound, "Post not found"));
            }

            dbContext.Posts.Remove(post);
            dbContext.SaveChanges();

            return Task.FromResult(new DeletePostResponse
            {
                TransactionContext = new TransactionContext
                {
                    TransactionId = transactionId,
                    Status = hasRunningTransaction ? TransactionStatus.InProgress : TransactionStatus.Initiated,
                }
            });
        }

        private Task<DeletePostResponse> DeletePostAsSimpleRequest(DeletePostRequest request)
        {
            var dbContext = new PostsDbContext();
            var post = dbContext.Posts.Find(Guid.Parse(request.Guid));
            if (post == null)
                throw new RpcException(new Status(StatusCode.NotFound, "Post not found"));

            dbContext.Posts.Remove(post);
            dbContext.SaveChanges();

            return Task.FromResult(new DeletePostResponse());
        }

        public override Task<ListPostsResponse> ListPosts(ListPostsRequest request, ServerCallContext context)
        {
            var posts = (new PostsDbContext()).Posts
                .OrderByDescending(p => p.CreationTime)
                .Take((int)(request.Limit == 0 ? 10 : request.Limit))
                .ToList(); 

            var response = new ListPostsResponse();
            response.Posts.AddRange(posts.Select(p => new BlogPost
            {
                Guid = p.Id.ToString(),
                Title = p.Title,
                Content = p.Content,
                CreationTime = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(p.CreationTime.ToUniversalTime()),
                LastEditedTime = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(p.LastEditedTime?.ToUniversalTime() ?? new DateTime(0))
            }));

            return Task.FromResult(response); 
        }


        public override Task<GetPostResponse> GetPost(GetPostRequest request, ServerCallContext context)
        {
            var post = (new PostsDbContext()).Posts.Find(Guid.Parse(request.Guid));
            if (post == null)
                throw new RpcException(new Status(StatusCode.NotFound, "Post not found"));

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

        public override Task<CommitResponse> Commit(CommitRequest request, ServerCallContext context)
        {
            if (string.IsNullOrEmpty(request.TransactionId))
                throw new RpcException(new Status(StatusCode.InvalidArgument, "No transaction id provided"));

            try
            {
                shared.TransactionManager<PostsDbContext>.Instance.CommitTransaction(request.TransactionId);
                return Task.FromResult(new CommitResponse());
            }
            catch (ArgumentNullException)
            {
                throw new RpcException(new Status(StatusCode.NotFound, "The transaction could not be found"));
            }
        }

        public override Task<CancelResponse> Cancel(CancelRequest request, ServerCallContext context)
        {
            if (string.IsNullOrEmpty(request.TransactionId))
                throw new RpcException(new Status(StatusCode.InvalidArgument, "No transaction id provided"));

            try
            {
                shared.TransactionManager<PostsDbContext>.Instance.RollbackTransaction(request.TransactionId);
                return Task.FromResult(new CancelResponse());
            }
            catch (ArgumentNullException)
            {
                throw new RpcException(new Status(StatusCode.NotFound, "The transaction could not be found"));
            }
        }
    }
}


