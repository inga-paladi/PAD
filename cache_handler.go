package main

import (
	"context"
	"errors"
	blogpb "meoworld-gateway/gen/blog"
	"time"

	"github.com/redis/go-redis/v9"
	"google.golang.org/protobuf/proto"
)

type CacheHandler struct {
	ctx         context.Context
	redisClient *redis.Client
}

func (handler *CacheHandler) Init() {
	handler.ctx = context.Background()
	handler.redisClient = redis.NewClient(&redis.Options{Addr: redisAddress})
}

func (handler *CacheHandler) addBlogPost(post *blogpb.BlogPost, expiration time.Duration) {
	if len(post.Guid) == 0 {
		return
	}
	postData, err := proto.Marshal(post)
	if err == nil {
		handler.redisClient.Set(handler.ctx, post.Guid, postData, expiration)
	}
}

func (handler *CacheHandler) getBlogPost(guid string) (*blogpb.BlogPost, error) {
	postData, err := handler.redisClient.Get(handler.ctx, guid).Bytes()
	if err == redis.Nil {
		return nil, errors.New("NOT_FOUND")
	}

	blogPost := blogpb.BlogPost{}
	proto.Unmarshal(postData, &blogPost)
	return &blogPost, nil
}
