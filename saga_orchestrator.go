package main

import (
	"context"
	blogpb "meoworld-gateway/gen/blog"
	commentspb "meoworld-gateway/gen/comments"

	"github.com/google/uuid"
	"google.golang.org/grpc/metadata"
)

type SagaOrchestrator struct {
	forwarder *RequestForwarder
	txSteps   map[string]bool
}

func NewSagaOrchestrator(forwarder *RequestForwarder) (orchestrator SagaOrchestrator) {
	orchestrator.forwarder = forwarder
	orchestrator.txSteps = make(map[string]bool)
	orchestrator.txSteps[blogpb.Blog_DeletePost_FullMethodName] = true
	return
}

func (orchestrator *SagaOrchestrator) Handle(ctx context.Context, req interface{}, fullMethod string, response interface{}) (err error) {
	if _, ok := orchestrator.txSteps[fullMethod]; !ok {
		err = orchestrator.forwarder.Forward(ctx, req, fullMethod, response)
		return
	}

	transactionId := uuid.New().String()
	sagaCtx := metadata.NewOutgoingContext(ctx, metadata.Pairs(
		"Saga-Transaction-Id", transactionId,
	))

	preparePosts := func() bool {
		err = orchestrator.forwarder.Forward(sagaCtx, req, fullMethod, response)
		return err == nil
	}

	prepareComments := func() bool {
		listCommentsReq := commentspb.ListCommentsRequest{PostGuid: req.(*blogpb.DeletePostRequest).Guid}
		listCommentsResponse := commentspb.ListCommentsResponse{Comments: make([]*commentspb.Comment, 0)}
		err = orchestrator.forwarder.Forward(ctx, &listCommentsReq, commentspb.Comments_ListComments_FullMethodName, &listCommentsResponse)

		for _, commentItem := range listCommentsResponse.Comments {
			deleteCommentRequest := commentspb.DeleteCommentRequest{Guid: commentItem.Guid}
			var deleteCommentResponse commentspb.DeleteCommentResponse
			err = orchestrator.forwarder.Forward(sagaCtx, &deleteCommentRequest, commentspb.Comments_DeleteComment_FullMethodName, &deleteCommentResponse)

			if err != nil {
				return false
			}
		}

		return true
	}

	commitPosts := func() {
		commitRequest := blogpb.CommitRequest{TransactionId: transactionId}
		var commitResponse blogpb.CommitResponse
		err = orchestrator.forwarder.Forward(sagaCtx, &commitRequest, blogpb.Blog_Commit_FullMethodName, &commitResponse)
	}

	commitComments := func() {
		commitRequest := commentspb.CommitRequest{TransactionId: transactionId}
		var commitResponse commentspb.CommitResponse
		err = orchestrator.forwarder.Forward(sagaCtx, &commitRequest, commentspb.Comments_Commit_FullMethodName, &commitResponse)
	}

	cancelPosts := func() {
		cancelRequest := blogpb.CancelRequest{TransactionId: transactionId}
		var cancelResponse blogpb.CancelResponse
		err = orchestrator.forwarder.Forward(sagaCtx, &cancelRequest, blogpb.Blog_Cancel_FullMethodName, &cancelResponse)
	}

	cancelComments := func() {
		cancelRequest := commentspb.CancelRequest{TransactionId: transactionId}
		var cancelResponse commentspb.CancelResponse
		err = orchestrator.forwarder.Forward(sagaCtx, &cancelRequest, commentspb.Comments_Cancel_FullMethodName, &cancelResponse)
	}

	if preparePosts() {
		if prepareComments() {
			commitPosts()
			commitComments()
		} else {
			cancelComments()
			cancelPosts()
		}
	}

	return
}
