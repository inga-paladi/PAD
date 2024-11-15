package main

import (
	"context"
	"encoding/base64"
	"fmt"
	blogpb "meoworld-gateway/gen/blog"
	commentspb "meoworld-gateway/gen/comments"
	mqpb "meoworld-gateway/gen/mq"
	"net"
	"os"
	"reflect"
	"regexp"
	"sync"
	"time"

	"github.com/redis/go-redis/v9"
	"go.uber.org/zap"
	"google.golang.org/grpc"
	"google.golang.org/grpc/codes"
	"google.golang.org/grpc/credentials/insecure"
	"google.golang.org/grpc/status"
	"google.golang.org/protobuf/proto"
)

const (
	requestForwarderListenAddress string = ":8081"
	maxRedirects                  uint8  = 2
)

var messageBrokerAddress = fmt.Sprintf("%s:%s", os.Getenv("MESSAGE_BROKER_ADDRESS"), "6379")

type RequestForwarder struct {
	server                 *grpc.Server
	data                   map[string]*serviceInstances
	cancelServiceDiscovery context.CancelFunc
}

type serviceInstances struct {
	availableServices []*CircuitBreaker
	nextServiceOrder  uint8
	mutex             sync.Mutex
}

// Start ------------------------------------------
func (forwarder *RequestForwarder) Start() {
	forwarder.server = grpc.NewServer(
		grpc.UnaryInterceptor(serverInterceptor),
		grpc.Creds(insecure.NewCredentials()),
	)

	blogpb.RegisterBlogServer(forwarder.server, blogpb.UnimplementedBlogServer{})
	commentspb.RegisterCommentsServer(forwarder.server, commentspb.UnimplementedCommentsServer{})

	grpcServerListener, err := net.Listen("tcp", requestForwarderListenAddress)
	if err != nil {
		// panic
		return
	}

	go forwarder.server.Serve(grpcServerListener)

	forwarder.data = make(map[string]*serviceInstances)
	forwarder.listenForNewServices()
}

// Close --------------------------------------------
func (forwarder *RequestForwarder) Close() {
	forwarder.server.GracefulStop()
	forwarder.cancelServiceDiscovery()
}

// Forward --------------------------------------------
func (forwarder *RequestForwarder) Forward(ctx context.Context, req interface{}, fullMethod string, response interface{}) (err error) {
	serviceName := getServiceName(fullMethod)
	for attempt := 1; attempt <= int(maxRedirects); {
		serviceInstance := forwarder.getNextServiceInstance(serviceName)
		if serviceInstance == nil {
			zap.L().Sugar().Errorf("No %s instance available to handle the request", serviceName)
			return status.Error(codes.Unavailable, "No available instances")
		}

		zap.L().Sugar().Infof("Request for %s is forwarded to %s, attempt %d", fullMethod, serviceInstance.GetAddress(), attempt)
		err = serviceInstance.HandleRequest(ctx, fullMethod, req, response)
		if response == HandleRequest_Error_CircuitDead {
			// Remove it and try the next one
			continue
		}

		if response == HandleRequest_Error_CircuitOpen {
			// Try the next one
			continue
		}

		statusCode, _ := status.FromError(err)
		if statusCode.Code() == codes.OK {
			return
		}

		// Canceled or InvalidArgument are not server erros, so just return it to the caller.
		if !IsRelevantErrorCode(statusCode.Code()) {
			return status.Error(statusCode.Code(), "")
		}

		zap.L().Sugar().Errorf("Error forwarding the request %s to %s.", fullMethod, serviceInstance.GetAddress())
		attempt++
	}

	// Return the last response and error
	return
}

func serverInterceptor(ctx context.Context, req interface{}, info *grpc.UnaryServerInfo, handler grpc.UnaryHandler) (response interface{}, err error) {
	responseType, _ := handler(ctx, req)
	response = reflect.New(reflect.TypeOf(responseType).Elem()).Interface()

	zap.L().Sugar().Infof("Received client request: %s", info.FullMethod)

	err = requestForwarder.Forward(ctx, req, info.FullMethod, response)
	return
}

func (forwarder *RequestForwarder) getNextServiceInstance(serviceName string) (serviceInstance *CircuitBreaker) {
	instances, ok := forwarder.data[serviceName]
	if !ok {
		return
	}

	instances.mutex.Lock()
	defer instances.mutex.Unlock()

	serviceInstance = instances.availableServices[instances.nextServiceOrder]
	instances.nextServiceOrder = (instances.nextServiceOrder + 1) % uint8(len(instances.availableServices))
	return
}

// Registry listener ------------------------------------------------------
func (forwarder *RequestForwarder) listenForNewServices() {
	listenContext, cancel := context.WithCancel(context.Background())
	forwarder.cancelServiceDiscovery = cancel
	redisClient := redis.NewClient(&redis.Options{Addr: messageBrokerAddress})
	listenChannel := mqpb.EventType_EVENT_TYPE_SERVICE_DISCOVERY_BEACON.String()

	// I just need to make a successful connection, then, redis will
	// take care of reconnecting if the server is down.
	go func() {
		for {
			redisSubscriber := redisClient.Subscribe(listenContext, listenChannel)
			defer redisSubscriber.Close()
			_, err := redisSubscriber.Receive(listenContext)
			if err == nil {
				zap.L().Sugar().Infof("Subscribed to Redis channel: %s\n", listenChannel)
				forwarder.processMessages(redisSubscriber.Channel())
			} else {
				zap.L().Sugar().Errorf("Failed to subscribe to channel: %v", err)
			}

			select {
			case <-listenContext.Done():
				// cancel was triggered, exiting the loop
				return
			case <-time.After(5 * time.Second):
				// wait for 5 seconds for the next redis connection attempt.
				// if cancel is triggered meanwhile, it will return.
			}
		}
	}()
}

func (forwarder *RequestForwarder) processMessages(redisChannel <-chan *redis.Message) {
	for msg := range redisChannel {
		newServiceDetails, err := base64.StdEncoding.DecodeString(msg.Payload)
		if err != nil {
			zap.L().Error("Error dispatching message payload")
			continue
		}

		var serviceStartedProto mqpb.ServiceDiscoveryBeacon
		err = proto.Unmarshal(newServiceDetails, &serviceStartedProto)
		if err != nil {
			zap.L().Error("Error dispatching message payload")
			continue
		}

		forwarder.newServiceStarted(serviceStartedProto.ServiceName, serviceStartedProto.ServerAddress, uint16(serviceStartedProto.ServerPort))
	}
}

func (forwarder *RequestForwarder) newServiceStarted(serviceName string, serverAddress string, serverPort uint16) {
	newServiceAddress := fmt.Sprintf("%s:%d", serverAddress, serverPort)

	if _, exists := forwarder.data[serviceName]; !exists {
		forwarder.data[serviceName] = &serviceInstances{}
	}

	// Check for duplicates
	for _, service := range forwarder.data[serviceName].availableServices {
		if service.GetAddress() == newServiceAddress {
			return
		}
	}

	newCircuitBreakerObj := NewCircuitBreaker(newServiceAddress)
	if newCircuitBreakerObj == nil {
		zap.L().Sugar().Errorf("Error adding new endpoint. ServiceName: %s, ServerAddress: %s, ServerPort: %d\n", serviceName, serverAddress, serverPort)
		return
	}

	zap.L().Sugar().Infof("Add new endpoint. ServiceName: %s, ServerAddress: %s, ServerPort: %d\n", serviceName, serverAddress, serverPort)
	forwarder.data[serviceName].availableServices = append(forwarder.data[serviceName].availableServices, newCircuitBreakerObj)
}

// helpers -----------------------------------
func getServiceName(fullName string) string {
	// full name example "/meoworld.Blog/PublishPost"
	re := regexp.MustCompile(`^/([^/]+)/`)
	match := re.FindStringSubmatch(fullName)

	if len(match) > 1 {
		return match[1]
	} else {
		return ""
	}
}
