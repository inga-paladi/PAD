package main

import (
	"context"
	"encoding/base64"
	"fmt"
	"log"
	mqpb "meoworld-gateway/gen/mq"
	"os"
	"sync"

	"github.com/redis/go-redis/v9"
	"google.golang.org/grpc/resolver"
	"google.golang.org/protobuf/proto"
)

var redisAddress = fmt.Sprintf("%s:%s", os.Getenv("REDIS_ADDRESS"), "6379")

type ServiceRegistry struct {
	mutex                  sync.RWMutex
	endpoints              map[string][]resolver.Endpoint
	cancelServiceDiscovery context.CancelFunc
}

func (registry *ServiceRegistry) Start() {
	if registry.endpoints == nil {
		registry.endpoints = make(map[string][]resolver.Endpoint)
	}

	registry.ListenForNewServices()
}

func (registry *ServiceRegistry) Close() {
	registry.cancelServiceDiscovery()
}

func (registry *ServiceRegistry) ListenForNewServices() {
	listenContext, cancel := context.WithCancel(context.Background())
	registry.cancelServiceDiscovery = cancel
	redisClient := redis.NewClient(&redis.Options{Addr: redisAddress})
	listenChannel := mqpb.EventType_EVENT_TYPE_SERVICE_STARTED.String()
	redisSubscriber := redisClient.Subscribe(listenContext, listenChannel)

	// Trebuie asta?
	_, err := redisSubscriber.Receive(listenContext)
	if err != nil {
		log.Fatalf("Failed to subscribe to channel: %v", err)
	}

	redisReceiverChannel := redisSubscriber.Channel()
	log.Printf("Subscribed to Redis channel: %s\n", listenChannel)
	go func() {
		for msg := range redisReceiverChannel {
			if msg.Channel != listenChannel {
				continue
			}

			newServiceDetails, err := base64.StdEncoding.DecodeString(msg.Payload)
			if err != nil {
				log.Println("Error dispatching message payload")
				continue
			}

			var serviceStartedProto mqpb.ServiceStarted
			err = proto.Unmarshal(newServiceDetails, &serviceStartedProto)
			if err != nil {
				log.Println("Error dispatching message payload")
				continue
			}

			log.Printf("Received message from channel %s: { ServiceName: %s, ServerAddress: %s, ServerPort: %d }\n",
				msg.Channel, serviceStartedProto.ServiceName,
				serviceStartedProto.ServerAddress, serviceStartedProto.ServerPort)

			registry.NewServiceStarted(serviceStartedProto.ServiceName, serviceStartedProto.ServerAddress, uint16(serviceStartedProto.ServerPort))
		}
	}()
}

func (registry *ServiceRegistry) NewServiceStarted(serviceName string, serverAddress string, serverPort uint16) {
	registry.mutex.Lock()
	defer registry.mutex.Unlock()

	newEndpoint := resolver.Endpoint{
		Addresses: []resolver.Address{
			{
				Addr: fmt.Sprintf("%s:%d", serverAddress, serverPort),
			},
		},
	}

	// TODO: Check if this endpoint is not present already
	if registry.endpoints[serviceName] == nil {
		registry.endpoints[serviceName] = make([]resolver.Endpoint, 0)
	}
	registry.endpoints[serviceName] = append(registry.endpoints[serviceName], newEndpoint)
}

func (registry *ServiceRegistry) GetEndpoints(serviceName string) []resolver.Endpoint {
	registry.mutex.RLock()
	if registry.endpoints[serviceName] == nil {
		return make([]resolver.Endpoint, 0)
	}
	return registry.endpoints[serviceName]
}
