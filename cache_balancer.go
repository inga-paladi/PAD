package main

import (
	"errors"
	"fmt"
	"hash/fnv"
	"log"
	"os"
	"sort"
	"strings"
	"sync"
	"time"

	"go.uber.org/zap"
)

const (
	ringSize                  = 255
	clockwise                 = true
	virtualServersPerInstance = 10
	cacheServerPort           = 6379
)

type CacheBalancer struct {
	ring  []*virtualServer
	mutex sync.Mutex
}

type virtualServer struct {
	name         string
	hashedName   uint8
	realInstance *CacheHandler
}

func (balancer *CacheBalancer) Start() {
	cacheServers := os.Getenv("CACHE_SERVERS")
	for _, cacheServer := range strings.Split(cacheServers, ";") {
		balancer.addServer(fmt.Sprintf("%s:%d", cacheServer, cacheServerPort))
	}
}

func (balancer *CacheBalancer) addServer(address string) {
	newCacheHandler := NewCacheHandler(address)
	if newCacheHandler == nil {
		return
	}

	zap.L().Sugar().Infof("Add cache server with address %s", address)
	var virtualServers []*virtualServer
	for virtServNr := 1; virtServNr <= virtualServersPerInstance; virtServNr++ {
		virtServerName := fmt.Sprintf("%s-%d", address, virtServNr)
		virtualServers = append(virtualServers, &virtualServer{
			realInstance: newCacheHandler,
			name:         virtServerName,
			hashedName:   getIntValue(virtServerName),
		})
	}

	balancer.mutex.Lock()
	defer balancer.mutex.Unlock()

	// Add the new virtual servers and reorder them in the ring
	balancer.ring = append(balancer.ring, virtualServers...)
	sort.Slice(balancer.ring, func(i, j int) bool {
		return balancer.ring[i].hashedName < balancer.ring[j].hashedName
	})

	// Leave just unique values
	rawHashRing := balancer.ring
	balancer.ring = make([]*virtualServer, 0)
	for i := 1; i < len(rawHashRing); i++ {
		if rawHashRing[i].hashedName != rawHashRing[i-1].hashedName {
			balancer.ring = append(balancer.ring, rawHashRing[i])
		}
	}
}

func (balancer *CacheBalancer) removeServer(address string) {
	balancer.mutex.Lock()
	defer balancer.mutex.Unlock()

	oldRing := balancer.ring
	balancer.ring = make([]*virtualServer, 0)
	for _, item := range oldRing {
		if item.realInstance.address != address {
			balancer.ring = append(balancer.ring, item)
		}
	}
}

func (balancer *CacheBalancer) AddValue(key string, data string, expiration time.Duration) error {
	cacheServer := balancer.getTheServerWhichHolds(key)
	if cacheServer == nil {
		zap.L().Sugar().Errorf("Can't add value with key \"%s\", as there are no available cache servers.", key)
		return errors.New("no instance available")
	}
	return cacheServer.AddValue(key, data, expiration)
}

func (balancer *CacheBalancer) GetValue(key string) (response string, err error) {
	cacheServer := balancer.getTheServerWhichHolds(key)
	if cacheServer == nil {
		log.Printf("Can't receive value with key \"%s\", as there are no available cache servers.", key)
		zap.L().Sugar().Errorf("Can't receive value with key \"%s\", as there are no available cache servers.", key)
		return "", errors.New("no instance available")
	}
	return cacheServer.GetValue(key)
}

func (balancer *CacheBalancer) getTheServerWhichHolds(key string) (cacheHandler *CacheHandler) {
	balancer.mutex.Lock()
	defer balancer.mutex.Unlock()
	if len(balancer.ring) == 0 {
		return nil
	}

	hashedKey := getIntValue(key)
	for _, virtualServer := range balancer.ring {
		if hashedKey <= virtualServer.hashedName {
			cacheHandler = virtualServer.realInstance
			break
		}
	}

	if cacheHandler == nil {
		// This means the key is between the last and the first one. Example: last one is 250, the first one is 10.
		// Key with 254 is not lower than 10, but it will be handled by it, because it's a RING.
		cacheHandler = balancer.ring[0].realInstance
	}

	log.Printf("%s is responsible for the key %s, with hash number of %d", cacheHandler.GetAddress(), key, hashedKey)
	zap.L().Sugar().Infof("%s is responsible for the key %s, with hash number of %d", cacheHandler.GetAddress(), key, hashedKey)
	return
}

func getIntValue(val string) uint8 {
	hasher := fnv.New32a()
	hasher.Write([]byte(val))
	return uint8(hasher.Sum32() % ringSize)
}
