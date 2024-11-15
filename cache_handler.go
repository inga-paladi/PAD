package main

import (
	"context"
	"time"

	"github.com/redis/go-redis/v9"
)

type CacheHandler struct {
	ctx         context.Context
	redisClient *redis.Client
	address     string
}

func NewCacheHandler(address string) *CacheHandler {
	return &CacheHandler{
		ctx:         context.Background(),
		redisClient: redis.NewClient(&redis.Options{Addr: address}),
		address:     address,
	}
}

func (handler *CacheHandler) GetAddress() string {
	return handler.address
}

func (handler *CacheHandler) AddValue(key string, data string, expiration time.Duration) error {
	_, err := handler.redisClient.Set(handler.ctx, key, data, expiration).Result()
	return err
}

func (handler *CacheHandler) GetValue(key string) (string, error) {
	return handler.redisClient.Get(handler.ctx, key).Result()
}
