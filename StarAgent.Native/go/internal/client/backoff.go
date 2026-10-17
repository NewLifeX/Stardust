package client

import (
	"context"
	"math/rand"
	"sync"
	"time"
)

// Backoff 实现指数退避 + 抖动，默认 1s、2s、4s、8s、16s、30s 封顶。
type Backoff struct {
	Min    time.Duration
	Max    time.Duration
	Jitter float64

	mu      sync.Mutex
	attempt int
	random  *rand.Rand
}

// NewBackoff 创建默认退避器。
func NewBackoff() *Backoff {
	return &Backoff{
		Min:    time.Second,
		Max:    30 * time.Second,
		Jitter: 0.2,
		random: rand.New(rand.NewSource(time.Now().UnixNano())),
	}
}

// Next 返回下一次等待时间。
func (b *Backoff) Next() time.Duration {
	b.mu.Lock()
	defer b.mu.Unlock()

	min := b.Min
	if min <= 0 {
		min = time.Second
	}
	max := b.Max
	if max <= 0 {
		max = 30 * time.Second
	}

	delay := min
	for i := 0; i < b.attempt && delay < max; i++ {
		delay *= 2
	}
	if delay > max {
		delay = max
	}
	b.attempt++

	if b.Jitter > 0 {
		if b.random == nil {
			b.random = rand.New(rand.NewSource(time.Now().UnixNano()))
		}
		factor := 1 + (b.random.Float64()*2-1)*b.Jitter
		delay = time.Duration(float64(delay) * factor)
		if delay < min {
			delay = min
		}
		if delay > max {
			delay = max
		}
	}

	return delay
}

// Reset 在一次成功连接后重置退避。
func (b *Backoff) Reset() {
	b.mu.Lock()
	b.attempt = 0
	b.mu.Unlock()
}

// Wait 等待下一次退避时间，返回 context 取消错误。
func (b *Backoff) Wait(ctx context.Context) error {
	delay := b.Next()
	timer := time.NewTimer(delay)
	defer timer.Stop()

	select {
	case <-ctx.Done():
		return ctx.Err()
	case <-timer.C:
		return nil
	}
}
