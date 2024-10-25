using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Meoworld;

namespace comments.Services
{
    public class CommentsService : Comments.CommentsBase
    {
        private readonly CommentsDbContext _commentsDbContext = new();

        public override Task<AddCommentResponse> AddComment(AddCommentRequest request, ServerCallContext context)
        {
            var comment = new Comment
            {
                PostGuid = Guid.Parse(request.PostGuid),
                Content = request.Content,
                OwnerId = 1,
                CreationTime = DateTime.UtcNow,
                LastEditedTime = DateTime.UtcNow
            };
            if (request.ReplyGuid != "")
                comment.ReplyGuid = Guid.Parse(request.ReplyGuid);

            _commentsDbContext.Comments.Add(comment);
            _commentsDbContext.SaveChanges();

            return Task.FromResult(new AddCommentResponse { Guid = comment.Id.ToString() });
        }

        public override Task<ListCommentsResponse> ListComments(ListCommentsRequest request, ServerCallContext context)
        {
            var comments = _commentsDbContext.Comments
                .Where(comment => comment.PostGuid == Guid.Parse(request.PostGuid))
                .ToList();

            var response = new ListCommentsResponse();
            response.Comments.AddRange(comments.Select(comment => new Meoworld.Comment
            {
                Guid = comment.Id.ToString(),
                PostGuid = comment.PostGuid.ToString(),
                ReplyGuid = comment.ReplyGuid.ToString(),
                OwnerId = comment.OwnerId,
                Content = comment.Content,
                CreationTime = Timestamp.FromDateTime(comment.CreationTime.ToUniversalTime()),
                LastEditedTime = Timestamp.FromDateTime(comment.LastEditedTime?.ToUniversalTime() ?? new DateTime(0))
            }));

            return Task.FromResult(response);
        }

        public override Task<EditCommentResponse> EditComment(EditCommentRequest request, ServerCallContext context)
        {
            var comment = _commentsDbContext.Comments.Find(Guid.Parse(request.Guid));
            if (comment == null)
            {
                throw new RpcException(new Status(StatusCode.NotFound, "Comment not found"));
            }

            comment.Content = request.Content;

            _commentsDbContext.Comments.Update(comment);
            _commentsDbContext.SaveChanges();

            return Task.FromResult(new EditCommentResponse());
        }

        public override Task<DeleteCommentResponse> DeleteComment(DeleteCommentRequest request, ServerCallContext context)
        {
            var comment = _commentsDbContext.Comments.Find(Guid.Parse(request.Guid));
            if (comment == null)
            {
                throw new RpcException(new Status(StatusCode.NotFound, "Comment not found"));
            }

            _commentsDbContext.Comments.Remove(comment);
            _commentsDbContext.SaveChanges();

            return Task.FromResult(new DeleteCommentResponse());
        }
    }
}
