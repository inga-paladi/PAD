package main

import (
	"errors"
	"fmt"
	"os"

	"go.uber.org/zap"
	"go.uber.org/zap/zapcore"
)

const logFilePath = "logs/gateway.log"

func init_logger() {
	logFileSyncer, err := NewFileWriteSyncer(logFilePath)
	if err != nil {
		panic(fmt.Sprintf("Failed to create file syncer: %v", err))
	}

	level := zap.NewAtomicLevelAt(zap.InfoLevel)
	encoderConfig := zap.NewProductionEncoderConfig()
	encoder := zapcore.NewJSONEncoder(encoderConfig)
	core := zapcore.NewCore(encoder, zapcore.AddSync(logFileSyncer), level)
	zap.ReplaceGlobals(zap.New(core).With(zap.String("service", "gateway")))
}

type FileWriteSyncer struct {
	file *os.File
	path string
}

func NewFileWriteSyncer(path string) (*FileWriteSyncer, error) {
	file, err := openLogFile(path)
	if err != nil {
		return nil, err
	}
	return &FileWriteSyncer{
		file: file,
		path: path,
	}, nil
}

func (fws *FileWriteSyncer) Write(p []byte) (n int, err error) {
	_, statsErr := os.Stat(fws.path)
	if errors.Is(statsErr, os.ErrNotExist) {
		fws.file, err = openLogFile(fws.path)
		if err != nil {
			return 0, err
		}
	}
	return fws.file.Write(p)
}

func (fws *FileWriteSyncer) Sync() error {
	if fws.file != nil {
		return fws.file.Sync()
	}
	return nil
}

func openLogFile(path string) (*os.File, error) {
	file, err := os.OpenFile(path, os.O_APPEND|os.O_CREATE|os.O_WRONLY, 0666)
	if err != nil {
		return nil, fmt.Errorf("failed to open or create log file: %v", err)
	}
	return file, nil
}
