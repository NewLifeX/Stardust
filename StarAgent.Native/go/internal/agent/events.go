package agent

import (
	"encoding/json"
	"errors"
	"os"
	"sync"

	"github.com/NewLifeX/Stardust/StarAgent.Native/go/internal/config"
	"github.com/NewLifeX/Stardust/StarAgent.Native/go/internal/protocol"
)

// EventQueue 是带落盘能力的有界事件队列。
type EventQueue struct {
	mu     sync.Mutex
	path   string
	max    int
	events []protocol.EventModel
}

// NewEventQueue 创建并恢复事件队列。
func NewEventQueue(path string, max int) (*EventQueue, error) {
	if max <= 0 {
		max = 1000
	}
	queue := &EventQueue{path: path, max: max}
	if path == "" {
		return queue, nil
	}

	data, err := os.ReadFile(path)
	if err != nil {
		if errors.Is(err, os.ErrNotExist) {
			return queue, nil
		}
		return queue, err
	}
	if len(data) > 0 {
		if err := json.Unmarshal(data, &queue.events); err != nil {
			return queue, err
		}
	}
	queue.trimLocked()

	return queue, nil
}

// Add 入队并落盘。队列满时丢弃最旧事件。
func (q *EventQueue) Add(event protocol.EventModel) error {
	q.mu.Lock()
	defer q.mu.Unlock()

	q.events = append(q.events, event)
	q.trimLocked()
	return q.saveLocked()
}

// Take 取出最多 n 条事件并落盘。
func (q *EventQueue) Take(n int) []protocol.EventModel {
	q.mu.Lock()
	defer q.mu.Unlock()

	if len(q.events) == 0 || n <= 0 {
		return nil
	}
	if n > len(q.events) {
		n = len(q.events)
	}
	batch := append([]protocol.EventModel(nil), q.events[:n]...)
	q.events = append([]protocol.EventModel(nil), q.events[n:]...)
	_ = q.saveLocked()

	return batch
}

// PutFront 把未成功上报的事件放回队首。
func (q *EventQueue) PutFront(batch []protocol.EventModel) {
	if len(batch) == 0 {
		return
	}

	q.mu.Lock()
	defer q.mu.Unlock()

	merged := make([]protocol.EventModel, 0, len(batch)+len(q.events))
	merged = append(merged, batch...)
	merged = append(merged, q.events...)
	q.events = merged
	q.trimLocked()
	_ = q.saveLocked()
}

// Len 返回队列长度。
func (q *EventQueue) Len() int {
	q.mu.Lock()
	defer q.mu.Unlock()

	return len(q.events)
}

func (q *EventQueue) trimLocked() {
	if q.max > 0 && len(q.events) > q.max {
		q.events = q.events[len(q.events)-q.max:]
	}
}

func (q *EventQueue) saveLocked() error {
	if q.path == "" {
		return nil
	}

	data, err := json.Marshal(q.events)
	if err != nil {
		return err
	}

	return config.WriteFileAtomic(q.path, data, 0o600)
}
