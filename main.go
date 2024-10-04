package main

import (
	"context"
	"fmt"
	"log"
	"net/http"
	"strings"
	"time"

	blogpb "meoworld-gateway/gen/blog"
	commentspb "meoworld-gateway/gen/comments"

	"github.com/grpc-ecosystem/go-grpc-middleware/v2/interceptors/timeout"
	"github.com/grpc-ecosystem/grpc-gateway/v2/runtime"
	"github.com/tmc/grpc-websocket-proxy/wsproxy"
	"google.golang.org/grpc"
	"google.golang.org/grpc/credentials/insecure"
	"google.golang.org/grpc/resolver"
)

const (
	gatewayAddress string = ":8080"
)

var serviceRegistry ServiceRegistry
var cacheHandler CacheHandler

func init() {
	serviceRegistry.Start()
	resolver.Register(&GrpcResolveBuilder{})
	cacheHandler.Init()
}

func clean() {
	serviceRegistry.Close()
}

func main() {
	defer clean()

	RunServer()
}

func RunServer() {
	gatewayMux := runtime.NewServeMux(runtime.WithMiddlewares())

	blogOpts := []grpc.DialOption{
		grpc.WithTransportCredentials(insecure.NewCredentials()),
		grpc.WithChainUnaryInterceptor(timeout.UnaryClientInterceptor(500*time.Millisecond), blogCacheUnaryInterceptor),
		grpc.WithDefaultServiceConfig("{  load_balancing_config: { round_robin: {} }}"),
	}
	blogpb.RegisterBlogHandlerFromEndpoint(context.Background(), gatewayMux, fmt.Sprintf("%s:///%s", resolverScheme, blogServiceName), blogOpts)

	commentOpts := []grpc.DialOption{
		grpc.WithTransportCredentials(insecure.NewCredentials()),
		grpc.WithChainUnaryInterceptor(timeout.UnaryClientInterceptor(500 * time.Millisecond)),
	}
	commentspb.RegisterCommentsHandlerFromEndpoint(context.Background(), gatewayMux, ":5002", commentOpts)

	log.Printf("Serving http on %v", gatewayAddress)
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
		blogPost, err := cacheHandler.getBlogPost(getPostRequest.Guid)
		if err == nil {
			log.Printf("Data for post with guid %s was found in cache\n", getPostRequest.Guid)
			reply.(*blogpb.GetPostResponse).Post = blogPost
			return nil
		}
		log.Printf("Data for post with guid %s not found in cache\n", getPostRequest.Guid)
		invokeErr := invoker(ctx, method, req, reply, cc, opts...)
		if invokeErr == nil {
			getPostResponse, ok := reply.(*blogpb.GetPostResponse)
			if ok {
				cacheHandler.addBlogPost(getPostResponse.Post, 3000*time.Second)
			}
		}
		return invokeErr
	}

	return invoker(ctx, method, req, reply, cc, opts...)
}