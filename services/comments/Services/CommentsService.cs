using Grpc.Core;
using Grpc.Client;
using Meoworld;
using Microsoft.EntityFrameworkCore;

namespace comments.Services
{
    public class CommentsService : Comments.CommentsBase
    {
        private readonly CommentsDbContext _commentsDbContext;
        private readonly Posts.PostsClient _postClient;

        public CommentsService(CommentsDbContext commentsDbContext, PostsDbContext postsDbContext)
        {
            _commentsDbContext = commentsDbContext;
            _postsDbContext = postsDbContext; // Optional: If you need to check post existence
        }

        public override async Task<AddCommentResponse> AddComment(AddCommentRequest request, ServerCallContext context)
        {
            // Optionally check if the post exists
            var postExists = await _postsDbContext.Posts.AnyAsync(p => p.Guid == request.PostGuid);
            if (!postExists)
            {
                throw new RpcException(new Status(StatusCode.NotFound, "Post not found"));
            }

            var comment = new Comment
            {
                PostGuid = request.PostGuid,
                Content = request.Content,
                ReplyGuid = request.ReplyGuid,
                CreationTime = Timestamp.FromDateTime(DateTime.UtcNow),
                LastEditedTime = Timestamp.FromDateTime(DateTime.UtcNow)
            };

            _commentsDbContext.Comments.Add(comment);
            await _commentsDbContext.SaveChangesAsync();

            return new AddCommentResponse { Guid = comment.Guid };
        }

        public override async Task<ListCommentsResponse> ListComments(ListCommentsRequest request, ServerCallContext context)
        {
            var comments = await _commentsDbContext.Comments
                .Where(c => c.PostGuid == request.PostGuid)
                .ToListAsync();

            var response = new ListCommentsResponse();
            response.Comments.AddRange(comments.Select(c => new Comment
            {
                Guid = c.Guid,
                PostGuid = c.PostGuid,
                ReplyGuid = c.ReplyGuid,
                OwnerId = c.OwnerId,
                Content = c.Content,
                CreationTime = c.CreationTime,
                LastEditedTime = c.LastEditedTime
            }));

            return response;
        }

        public override async Task<EditCommentResponse> EditComment(EditCommentRequest request, ServerCallContext context)
        {
            var comment = await _commentsDbContext.Comments.FindAsync(request.Guid);
            if (comment == null)
            {
                throw new RpcException(new Status(StatusCode.NotFound, "Comment not found"));
            }

            comment.Content = request.Content;
            comment.LastEditedTime = Timestamp.FromDateTime(DateTime.UtcNow);

            _commentsDbContext.Comments.Update(comment);
            await _commentsDbContext.SaveChangesAsync();

            return new EditCommentResponse();
        }

        public override async Task<DeleteCommentResponse> DeleteComment(DeleteCommentRequest request, ServerCallContext context)
        {
            var comment = await _commentsDbContext.Comments.FindAsync(request.Guid);
            if (comment == null)
            {
                throw new RpcException(new Status(StatusCode.NotFound, "Comment not found"));
            }

            _commentsDbContext.Comments.Remove(comment);
            await _commentsDbContext.SaveChangesAsync();

            return new DeleteCommentResponse();
        }
    }
}
