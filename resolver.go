package main

import (
	"fmt"

	"google.golang.org/grpc/resolver"
)

const (
	resolverScheme  = "meoworld-resolver"
	blogServiceName = "blog"
)

type GrpcResolver struct {
	target     resolver.Target
	clientConn resolver.ClientConn
}

func (res *GrpcResolver) Start() {
	endpoints := serviceRegistry.GetEndpoints(res.target.Endpoint())
	res.clientConn.UpdateState(resolver.State{Endpoints: endpoints})
}

func (res *GrpcResolver) Close() {}

func (res *GrpcResolver) ResolveNow(options resolver.ResolveNowOptions) {
	endpoints := serviceRegistry.GetEndpoints(res.target.Endpoint())
	res.clientConn.UpdateState(resolver.State{Endpoints: endpoints})
}

type GrpcResolveBuilder struct{}

func (*GrpcResolveBuilder) Build(target resolver.Target, clientConnection resolver.ClientConn, _ resolver.BuildOptions) (resolver.Resolver, error) {
	fmt.Printf("Building resolver: Target: %s\n", target.String())
	resolver := &GrpcResolver{
		target:     target,
		clientConn: clientConnection,
	}
	resolver.Start()
	return resolver, nil
}

func (*GrpcResolveBuilder) Scheme() string { return resolverScheme }