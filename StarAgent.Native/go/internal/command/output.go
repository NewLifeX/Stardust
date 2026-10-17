package command

import (
	"sync"
	"unicode/utf8"
)

// MaxOutputBytes 是命令输出与命令回复的最大字节数。
const MaxOutputBytes = 64 * 1024

const truncationMarker = "\n...<output truncated>...\n"

// TruncateOutput 将文本截断到 MaxOutputBytes，保留头尾。
func TruncateOutput(text string) string {
	if len(text) <= MaxOutputBytes {
		return text
	}

	keep := (MaxOutputBytes - len(truncationMarker)) / 2
	head := text[:keep]
	for len(head) > 0 && !utf8.RuneStart(text[len(head)]) {
		head = head[:len(head)-1]
	}
	tailStart := len(text) - keep
	for tailStart < len(text) && !utf8.RuneStart(text[tailStart]) {
		tailStart++
	}

	return head + truncationMarker + text[tailStart:]
}

type headTailBuffer struct {
	mu        sync.Mutex
	headLimit int
	tailLimit int
	head      []byte
	tail      []byte
	truncated bool
}

func newHeadTailBuffer(max int) *headTailBuffer {
	return &headTailBuffer{headLimit: max / 2, tailLimit: max - max/2}
}

func (b *headTailBuffer) Write(p []byte) (int, error) {
	b.mu.Lock()
	defer b.mu.Unlock()

	total := len(p)
	if len(b.head) < b.headLimit {
		n := b.headLimit - len(b.head)
		if n > len(p) {
			n = len(p)
		}
		b.head = append(b.head, p[:n]...)
		p = p[n:]
	}
	if len(p) > 0 {
		b.tail = append(b.tail, p...)
		if len(b.tail) > b.tailLimit {
			b.tail = b.tail[len(b.tail)-b.tailLimit:]
			b.truncated = true
		}
	}

	return total, nil
}

func (b *headTailBuffer) String() string {
	b.mu.Lock()
	defer b.mu.Unlock()

	if !b.truncated {
		return string(b.head) + string(b.tail)
	}

	return string(b.head) + truncationMarker + string(b.tail)
}

func (b *headTailBuffer) Truncated() bool {
	b.mu.Lock()
	defer b.mu.Unlock()

	return b.truncated
}
