package main

import (
	"context"
	"net/http"
	"strings"
	"time"

	blogpb "meoworld-gateway/gen/blog"
	commentspb "meoworld-gateway/gen/comments"

	"github.com/grpc-ecosystem/grpc-gateway/v2/runtime"
	"github.com/tmc/grpc-websocket-proxy/wsproxy"
	"go.uber.org/zap"
	"google.golang.org/grpc"
	"google.golang.org/grpc/credentials/insecure"
	"google.golang.org/protobuf/proto"
)

const (
	gatewayAddress string = ":8080"
)

var requestForwarder RequestForwarder
var cacheBalancer CacheBalancer

func init() {
	requestForwarder.Start()
	cacheBalancer.Start()
	init_logger()
}

func clean() {
	requestForwarder.Close()
}

func main() {
	defer clean()

	RunGateway()
}

func RunGateway() {
	gatewayMux := runtime.NewServeMux(runtime.WithMiddlewares())

	blogOpts := []grpc.DialOption{
		grpc.WithTransportCredentials(insecure.NewCredentials()),
		grpc.WithUnaryInterceptor(blogCacheUnaryInterceptor),
	}
	blogpb.RegisterBlogHandlerFromEndpoint(context.Background(), gatewayMux, requestForwarderListenAddress, blogOpts)

	commentOpts := []grpc.DialOption{
		grpc.WithTransportCredentials(insecure.NewCredentials()),
	}
	commentspb.RegisterCommentsHandlerFromEndpoint(context.Background(), gatewayMux, requestForwarderListenAddress, commentOpts)

	zap.L().Sugar().Infof("Serving gateway on %s", gatewayAddress)
	http.ListenAndServe(gatewayAddress, wsproxy.WebsocketProxy(gatewayMux))
}

func blogCacheUnaryInterceptor(ctx context.Context, method string, req, reply interface{}, cc *grpc.ClientConn, invoker grpc.UnaryInvoker, opts ...grpc.CallOption) error {
	rpc := strings.Split(method, "/")[2]
	switch rpc {
	case "GetPost":
		getPostRequest, ok := req.(*blogpb.GetPostRequest)
		if !ok {
			break
		}
		blogPostData, err := cacheBalancer.GetValue(getPostRequest.Guid)
		if err == nil {
			zap.L().Sugar().Infof("Data for post with guid %s was found in cache\n", getPostRequest.Guid)
			var blogPost blogpb.BlogPost
			proto.Unmarshal([]byte(blogPostData), &blogPost)
			reply.(*blogpb.GetPostResponse).Post = &blogPost
			return nil
		}
		zap.L().Sugar().Infof("Data for post with guid %s not found in cache\n", getPostRequest.Guid)
		invokeErr := invoker(ctx, method, req, reply, cc, opts...)
		if invokeErr == nil {
			if getPostResponse, ok := reply.(*blogpb.GetPostResponse); ok {
				if postData, err := proto.Marshal(getPostResponse.Post); err == nil {
					cacheBalancer.AddValue(getPostResponse.Post.Guid, string(postData), 10*time.Minute)
				}
			}
		}
		return invokeErr
	}

	return invoker(ctx, method, req, reply, cc, opts...)
}
