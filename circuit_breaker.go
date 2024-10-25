package main

import (
	"errors"
	"fmt"
	"math"
	"strings"
	"sync"
	"time"

	"google.golang.org/grpc"
	"google.golang.org/grpc/codes"
)

const errorIntervalMultiplier float32 = 3.5

type ConnectionData struct {
	errorIntervalMs int64 // task timeout for a specific service * errorIntervalMultiplier
	mutex           sync.Mutex
	errorHistoryMs  [2]int64 // holds first and second error. On the third one -> reset
}

type CircuitBreaker struct {
	mutex   sync.Mutex
	history map[*grpc.ClientConn]*ConnectionData
}

func NewCircuitBreaker() CircuitBreaker {
	return CircuitBreaker{
		history: make(map[*grpc.ClientConn]*ConnectionData),
	}
}

func (circuitBreaker *CircuitBreaker) RegisterError(cc *grpc.ClientConn) {
	circuitBreaker.mutex.Lock()
	if _, exists := circuitBreaker.history[cc]; !exists {
		timeout, err := getTaksTimeout(getServiceName(cc.Target()))
		if err != nil {
			return
		}
		circuitBreaker.history[cc] = &ConnectionData{errorIntervalMs: int64(math.Round(float64(timeout.Milliseconds()) * float64(errorIntervalMultiplier)))}
	}
	connectionData := circuitBreaker.history[cc]
	circuitBreaker.mutex.Unlock()

	connectionData.mutex.Lock()
	currentTime := time.Now().UnixMilli()
	difference := currentTime - connectionData.errorHistoryMs[0]
	connectionData.errorHistoryMs[0] = connectionData.errorHistoryMs[1]
	connectionData.errorHistoryMs[1] = currentTime
	connectionData.mutex.Unlock()

	if difference <= connectionData.errorIntervalMs {
		fmt.Printf("Three errors in %d ms for service %s", difference, getServiceName(cc.Target()))
	}
}

func IsRelevantErrorCode(code codes.Code) bool {
	switch code {
	case codes.DeadlineExceeded:
		return true
	case codes.ResourceExhausted:
		return true
	case codes.Aborted:
		return true
	case codes.Internal:
		return true
	case codes.Unavailable:
		return true
	case codes.DataLoss:
		return true
	default:
		return false
	}
}

func getTaksTimeout(serviceName string) (time.Duration, error) {
	switch serviceName {
	case "blog":
		return blogServiceTimeout, nil
	case "comments":
		return blogServiceTimeout, nil
	default:
		return 0, errors.New("unknown service name")
	}
}

func getServiceName(target string) string {
	return target[strings.LastIndex(target, "/")+1:]
}
