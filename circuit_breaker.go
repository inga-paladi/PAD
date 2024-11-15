package main

import (
	"context"
	"errors"
	"log"
	"sync"
	"time"

	"go.uber.org/zap"
	"google.golang.org/grpc"
	"google.golang.org/grpc/codes"
	"google.golang.org/grpc/credentials/insecure"
	"google.golang.org/grpc/status"
)

type CircuitBreakerState uint8

const (
	CircuitBreaker_State_Unspecified CircuitBreakerState = iota
	CircuitBreaker_State_Dead
	CircuitBreaker_State_Open
	CircuitBreaker_State_Closed
	CircuitBreaker_State_HalfClosed
)

const (
	HandleRequest_Error_Unspecified = 0
	HandleRequest_Error_CircuitDead = 20
	HandleRequest_Error_CircuitOpen = 21
)

const (
	thresholdRedirectCount         uint8         = 2
	thresholdRedirectsTimeInterval time.Duration = time.Minute
	thresholdForDeadState          uint8         = 6 // the number of half-closed -> closed state change to mark it dead.
	openStateTimeout               time.Duration = time.Second * 30
	retriesPerRpc                  uint8         = 3
)

type CircuitBreaker struct {
	state                       CircuitBreakerState
	conn                        *grpc.ClientConn
	mutex                       sync.Mutex
	halfClosedToOpenTransitions uint8
	redirectsQueue              [thresholdRedirectCount]time.Time
	timer                       *time.Timer
	// address string  ? poate de luat din conn, valoarea aia
}

func NewCircuitBreaker(address string) *CircuitBreaker {
	clientConn, err := grpc.NewClient(address, grpc.WithTransportCredentials(insecure.NewCredentials()))
	if err != nil {
		return nil
	}

	return &CircuitBreaker{
		state:                       CircuitBreaker_State_Closed,
		conn:                        clientConn,
		halfClosedToOpenTransitions: 0,
	}
}

func (circuitBreaker *CircuitBreaker) Stop() {
	circuitBreaker.timer.Stop()
	circuitBreaker.conn.Close()
}

func (circuitBreaker *CircuitBreaker) HandleRequest(ctx context.Context, fullMethod string, req interface{}, response interface{}) (err error) {
	circuitBreaker.mutex.Lock()
	currentState := circuitBreaker.state
	circuitBreaker.mutex.Unlock()

	if currentState == CircuitBreaker_State_Dead {
		response = HandleRequest_Error_CircuitDead
		return errors.New("circuit is dead, remove it")
	}
	if currentState == CircuitBreaker_State_Open {
		response = HandleRequest_Error_CircuitOpen
		log.Printf("Circuit is open for %s", circuitBreaker.GetAddress())
		return errors.New("circuit is open, try again later")
	}

	for attempt := 1; attempt <= int(retriesPerRpc); attempt++ {
		zap.L().Sugar().Infof("Invoke %s for %s, attempt %d", fullMethod, circuitBreaker.GetAddress(), attempt)
		timeoutCtx, cancel := context.WithTimeout(ctx, time.Second)
		defer cancel()

		err = circuitBreaker.conn.Invoke(timeoutCtx, fullMethod, req, response)
		statusCode, _ := status.FromError(err)
		if statusCode.Code() == codes.OK {
			circuitBreaker.closeCircuit()
			return
		}

		if !IsRelevantErrorCode(statusCode.Code()) {
			return
		}
	}

	circuitBreaker.registerError()
	return
}

func (circuitBreaker *CircuitBreaker) GetAddress() string {
	return circuitBreaker.conn.Target()
}

func (circuitBreaker *CircuitBreaker) registerError() {
	circuitBreaker.mutex.Lock()
	defer circuitBreaker.mutex.Unlock()

	if circuitBreaker.state == CircuitBreaker_State_Dead || circuitBreaker.state == CircuitBreaker_State_Open {
		return
	}

	if circuitBreaker.state == CircuitBreaker_State_HalfClosed {
		circuitBreaker.openCircuitUnsafe()
		return
	}

	// is closed state
	redirectsQueueLen := len(circuitBreaker.redirectsQueue)

	for i := 1; i < redirectsQueueLen; i++ {
		circuitBreaker.redirectsQueue[i-1] = circuitBreaker.redirectsQueue[i]
	}
	circuitBreaker.redirectsQueue[redirectsQueueLen-1] = time.Now()

	if circuitBreaker.redirectsQueue[redirectsQueueLen-1].Sub(circuitBreaker.redirectsQueue[0]) <= thresholdRedirectsTimeInterval {
		circuitBreaker.openCircuitUnsafe()
	}
}

func (circuitBreaker *CircuitBreaker) openCircuitUnsafe() {
	if circuitBreaker.state == CircuitBreaker_State_Open || circuitBreaker.state == CircuitBreaker_State_Dead {
		return
	}

	if circuitBreaker.state == CircuitBreaker_State_HalfClosed {
		circuitBreaker.halfClosedToOpenTransitions++
		if circuitBreaker.halfClosedToOpenTransitions >= thresholdForDeadState {
			circuitBreaker.state = CircuitBreaker_State_Dead
			zap.L().Sugar().Errorf("Mark %s as dead as the threshold for dead state reached", circuitBreaker.GetAddress())
			return
		}
	}

	circuitBreaker.state = CircuitBreaker_State_Open
	log.Printf("Open circuit for %s", circuitBreaker.GetAddress())
	zap.L().Sugar().Errorf("Open circuit for %s", circuitBreaker.GetAddress())

	circuitBreaker.timer = time.AfterFunc(openStateTimeout, func() {
		log.Printf("Half close circuit for %s", circuitBreaker.GetAddress())
		zap.L().Sugar().Infof("Half close circuit for %s", circuitBreaker.GetAddress())

		circuitBreaker.mutex.Lock()
		defer circuitBreaker.mutex.Unlock()

		// health check the service
		circuitBreaker.state = CircuitBreaker_State_HalfClosed
	})
}

func (circuitBreaker *CircuitBreaker) closeCircuit() {
	circuitBreaker.mutex.Lock()
	defer circuitBreaker.mutex.Unlock()

	if circuitBreaker.state != CircuitBreaker_State_Closed {
		circuitBreaker.state = CircuitBreaker_State_Closed
		circuitBreaker.halfClosedToOpenTransitions = 0
		log.Printf("Close circuit for %s", circuitBreaker.GetAddress())
		zap.L().Sugar().Infof("Close circuit for %s", circuitBreaker.GetAddress())
	}
}

func IsRelevantErrorCode(code codes.Code) (isRelevant bool) {
	switch code {
	case codes.Unknown,
		codes.DeadlineExceeded,
		codes.ResourceExhausted,
		codes.Aborted,
		codes.Internal,
		codes.Unavailable,
		codes.DataLoss:

		isRelevant = true
	}
	return
}
