using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Meoworld.V1.Comments;
using Meoworld.V1.Shared;

namespace comments.Services
{
    public class CommentsService : Comments.CommentsBase
    {
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

            var commentsDbContext = new CommentsDbContext();
            commentsDbContext.Comments.Add(comment);
            commentsDbContext.SaveChanges();

            return Task.FromResult(new AddCommentResponse { Guid = comment.Id.ToString() });
        }

        public override Task<ListCommentsResponse> ListComments(ListCommentsRequest request, ServerCallContext context)
        {
            var commentsDbContext = new CommentsDbContext();
            var comments = commentsDbContext.Comments
                .Where(comment => comment.PostGuid == Guid.Parse(request.PostGuid))
                .ToList();

            var response = new ListCommentsResponse();
            response.Comments.AddRange(comments.Select(comment => new Meoworld.V1.Comments.Comment
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
            var commentsDbContext = new CommentsDbContext();
            var comment = commentsDbContext.Comments.Find(Guid.Parse(request.Guid));
            if (comment == null)
                throw new RpcException(new Status(StatusCode.NotFound, "Comment not found"));

            comment.Content = request.Content;
            comment.LastEditedTime = DateTime.UtcNow;

            commentsDbContext.Comments.Update(comment);
            commentsDbContext.SaveChanges();

            return Task.FromResult(new EditCommentResponse());
        }

        public override Task<DeleteCommentResponse> DeleteComment(DeleteCommentRequest request, ServerCallContext context)
        {
            var transactionId = context.RequestHeaders.GetValue("Saga-Transaction-Id");
            return string.IsNullOrEmpty(transactionId) ?
                DeleteCommentAsSimpleRequest(request)
                : DeleteCommentAsPartOfSaga(request, transactionId);
        }

        private Task<DeleteCommentResponse> DeleteCommentAsPartOfSaga(DeleteCommentRequest request,
            string transactionId)
        {
            NLog.LogManager.GetCurrentClassLogger().Info($"Delete comment with guid {request.Guid} and transaction {transactionId}");
            var hasRunningTransaction = shared.TransactionManager<CommentsDbContext>.Instance.HasTransaction(transactionId);
            if (!hasRunningTransaction && !shared.TransactionManager<CommentsDbContext>.Instance.StartTransaction(transactionId))
                throw new RpcException(new Status(StatusCode.Internal, "Can't initiate a transaction"));

            var dbContext = shared.TransactionManager<CommentsDbContext>.Instance.GetDbContext(transactionId);

            var comment = dbContext.Comments.Find(Guid.Parse(request.Guid));
            if (comment == null)
            {
                shared.TransactionManager<CommentsDbContext>.Instance.DisposeTransaction(transactionId);
                throw new RpcException(new Status(StatusCode.NotFound, "Comment not found"));
            }

            dbContext.Comments.Remove(comment);
            dbContext.SaveChanges();

            return Task.FromResult(new DeleteCommentResponse
            {
                TransactionContext = new TransactionContext
                {
                    TransactionId = transactionId,
                    Status = hasRunningTransaction ? TransactionStatus.InProgress : TransactionStatus.Initiated,
                }
            });
        }

        private Task<DeleteCommentResponse> DeleteCommentAsSimpleRequest(DeleteCommentRequest request)
        {
            var dbContext = new CommentsDbContext();
            var comment = dbContext.Comments.Find(Guid.Parse(request.Guid));
            if (comment == null)
                throw new RpcException(new Status(StatusCode.NotFound, "Comment not found"));

            dbContext.Comments.Remove(comment);
            dbContext.SaveChanges();

            return Task.FromResult(new DeleteCommentResponse());
        }

        public override Task<CommitResponse> Commit(CommitRequest request, ServerCallContext context)
        {
            NLog.LogManager.GetCurrentClassLogger().Info($"Commit transaction {request.TransactionId}.");
            if (string.IsNullOrEmpty(request.TransactionId))
                throw new RpcException(new Status(StatusCode.InvalidArgument, "No transaction id provided"));

            try
            {
                shared.TransactionManager<CommentsDbContext>.Instance.CommitTransaction(request.TransactionId);
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
                shared.TransactionManager<CommentsDbContext>.Instance.RollbackTransaction(request.TransactionId);
                return Task.FromResult(new CancelResponse());
            }
            catch (ArgumentNullException)
            {
                throw new RpcException(new Status(StatusCode.NotFound, "The transaction could not be found"));
            }
        }
    }
}
