package main

import (
	"context"
	"net"
	"net/http"
	"reflect"
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
	interceptorServerListenAddress string = ":8081"
	gatewayAddress                 string = ":8080"
)

var interceptorServer *grpc.Server
var requestForwarder RequestForwarder
var cacheBalancer CacheBalancer
var sagaOrchestrator SagaOrchestrator = NewSagaOrchestrator(&requestForwarder)

func init() {
	init_logger()
	cacheBalancer.Start()
	requestForwarder.Start()
}

func clean() {
	interceptorServer.GracefulStop()
	requestForwarder.Close()
}

func main() {
	defer clean()

	RunInterceptorServer()
	RunMultiplexer()
}

func RunInterceptorServer() {
	interceptorServer = grpc.NewServer(
		grpc.UnaryInterceptor(serverInterceptor),
		grpc.Creds(insecure.NewCredentials()),
	)

	blogpb.RegisterBlogServer(interceptorServer, blogpb.UnimplementedBlogServer{})
	commentspb.RegisterCommentsServer(interceptorServer, commentspb.UnimplementedCommentsServer{})

	grpcServerListener, err := net.Listen("tcp", interceptorServerListenAddress)
	if err != nil {
		// panic
		return
	}

	go interceptorServer.Serve(grpcServerListener)
}

func RunMultiplexer() {
	gatewayMux := runtime.NewServeMux(runtime.WithMiddlewares())

	blogOpts := []grpc.DialOption{
		grpc.WithTransportCredentials(insecure.NewCredentials()),
		grpc.WithUnaryInterceptor(blogCacheUnaryInterceptor),
	}
	blogpb.RegisterBlogHandlerFromEndpoint(context.Background(), gatewayMux, interceptorServerListenAddress, blogOpts)

	commentOpts := []grpc.DialOption{
		grpc.WithTransportCredentials(insecure.NewCredentials()),
	}
	commentspb.RegisterCommentsHandlerFromEndpoint(context.Background(), gatewayMux, interceptorServerListenAddress, commentOpts)

	zap.L().Sugar().Infof("Serving gateway on %s", gatewayAddress)
	err := http.ListenAndServe(gatewayAddress, wsproxy.WebsocketProxy(gatewayMux))
	if err != nil {
		zap.L().Sugar().Infof("%s", err.Error())
	}
}

func serverInterceptor(ctx context.Context, req interface{}, info *grpc.UnaryServerInfo, handler grpc.UnaryHandler) (response interface{}, err error) {
	responseType, _ := handler(ctx, req)
	response = reflect.New(reflect.TypeOf(responseType).Elem()).Interface()

	zap.L().Sugar().Infof("Received client request: %s", info.FullMethod)

	err = sagaOrchestrator.Handle(ctx, req, info.FullMethod, response)
	return
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
