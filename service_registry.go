package main

import (
	"context"
	"encoding/base64"
	"fmt"
	"log"
	mqpb "meoworld-gateway/gen/mq"
	"os"
	"sync"
	"time"

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
	listenChannel := mqpb.EventType_EVENT_TYPE_SERVICE_DISCOVERY_BEACON.String()

	// I just need to make a successful connection, then, redis will
	// take care of reconnecting if the server is down.
	go func() {
		for {
			redisSubscriber := redisClient.Subscribe(listenContext, listenChannel)
			defer redisSubscriber.Close()
			_, err := redisSubscriber.Receive(listenContext)
			if err == nil {
				log.Printf("Subscribed to Redis channel: %s\n", listenChannel)
				registry.ProcessMessages(redisSubscriber.Channel())
			} else {
				log.Printf("Failed to subscribe to channel: %v", err)
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

func (registry *ServiceRegistry) ProcessMessages(redisChannel <-chan *redis.Message) {
	for msg := range redisChannel {
		newServiceDetails, err := base64.StdEncoding.DecodeString(msg.Payload)
		if err != nil {
			log.Println("Error dispatching message payload")
			continue
		}

		var serviceStartedProto mqpb.ServiceDiscoveryBeacon
		err = proto.Unmarshal(newServiceDetails, &serviceStartedProto)
		if err != nil {
			log.Println("Error dispatching message payload")
			continue
		}

		registry.NewServiceStarted(serviceStartedProto.ServiceName, serviceStartedProto.ServerAddress, uint16(serviceStartedProto.ServerPort))
	}
}

func (registry *ServiceRegistry) NewServiceStarted(serviceName string, serverAddress string, serverPort uint16) {
	newEndpoint := resolver.Endpoint{
		Addresses: []resolver.Address{
			{
				Addr: fmt.Sprintf("%s:%d", serverAddress, serverPort),
			},
		},
	}

	registry.mutex.Lock()
	defer registry.mutex.Unlock()

	if registry.endpoints[serviceName] == nil {
		registry.endpoints[serviceName] = make([]resolver.Endpoint, 0)
	}

	// Check for duplicates
	for _, endpoint := range registry.endpoints[serviceName] {
		if endpoint.Addresses[0].Addr == newEndpoint.Addresses[0].Addr {
			return
		}
	}

	log.Printf("Add new endpoint. ServiceName: %s, ServerAddress: %s, ServerPort: %d\n", serviceName, serverAddress, serverPort)
	registry.endpoints[serviceName] = append(registry.endpoints[serviceName], newEndpoint)
}

func (registry *ServiceRegistry) GetEndpoints(serviceName string) []resolver.Endpoint {
	registry.mutex.RLock()
	defer registry.mutex.RUnlock()
	if registry.endpoints[serviceName] == nil {
		return make([]resolver.Endpoint, 0)
	}
	return registry.endpoints[serviceName]
}
