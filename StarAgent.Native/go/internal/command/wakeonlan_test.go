package command

import (
	"context"
	"net"
	"strings"
	"testing"
	"time"

	"github.com/NewLifeX/Stardust/StarAgent.Native/go/internal/protocol"
)

func TestBuildMagicPacket(t *testing.T) {
	mac := net.HardwareAddr{0xAA, 0xBB, 0xCC, 0xDD, 0xEE, 0xFF}

	packet := BuildMagicPacket(mac)

	if len(packet) != 102 {
		t.Fatalf("packet length = %d, want 102", len(packet))
	}
	for i := 0; i < 6; i++ {
		if packet[i] != 0xFF {
			t.Fatalf("packet[%d] = %#x, want 0xFF", i, packet[i])
		}
	}
	for i := 0; i < 16; i++ {
		for j := 0; j < 6; j++ {
			if packet[6+i*6+j] != mac[j] {
				t.Fatalf("packet[%d] = %#x, want %#x", 6+i*6+j, packet[6+i*6+j], mac[j])
			}
		}
	}
}

func TestParseMacs(t *testing.T) {
	list := ParseMacs("AA-BB-CC-DD-EE-FF, aa:bb:cc:dd:ee:ff; aabb.ccdd.eeff 11-22-33-44-55-66")

	if len(list) != 2 {
		t.Fatalf("parse result = %v, want 2 entries", list)
	}
	if list[0].String() != "aa:bb:cc:dd:ee:ff" {
		t.Fatalf("first mac = %s", list[0])
	}
	if list[1].String() != "11:22:33:44:55:66" {
		t.Fatalf("second mac = %s", list[1])
	}
}

func TestParseMacsInvalid(t *testing.T) {
	for _, text := range []string{"", "ZZ-BB-CC-DD-EE-FF", "AA-BB-CC-DD-EE", "AA-BB-CC-DD-EE-FF-00", "hello world"} {
		if list := ParseMacs(text); len(list) != 0 {
			t.Fatalf("ParseMacs(%q) = %v, want empty", text, list)
		}
	}
}

func TestBroadcastAddr(t *testing.T) {
	ip := net.IPv4(192, 168, 1, 20).To4()
	mask := net.CIDRMask(24, 32)

	broadcast := broadcastAddr(ip, mask)

	if broadcast.String() != "192.168.1.255" {
		t.Fatalf("broadcast = %s", broadcast)
	}
	if broadcastAddr(net.IPv6loopback, mask) != nil {
		t.Fatal("IPv6 address must not produce an IPv4 broadcast")
	}
}

func TestWakeOnLanCommandJSON(t *testing.T) {
	var got []net.HardwareAddr
	executor := New(Options{
		WakeOnLan: func(macs []net.HardwareAddr) (int, error) {
			got = macs
			return 4, nil
		},
	})

	replies := collectReply(t, func(reply func(protocol.CommandReplyModel)) {
		executor.Handle(context.Background(), protocol.CommandModel{
			Id:       1,
			Command:  "node/wakeonlan",
			Argument: `{"macs":"AA-BB-CC-DD-EE-FF","code":"T1","name":"target"}`,
		}, reply)
	})

	if len(replies) != 1 || replies[0].Status != protocol.StatusCompleted || !strings.Contains(replies[0].Data, "WOL sent: 1 MAC(s), 4 packet(s)") {
		t.Fatalf("unexpected replies: %+v", replies)
	}
	if len(got) != 1 || got[0].String() != "aa:bb:cc:dd:ee:ff" {
		t.Fatalf("macs = %v", got)
	}
}

func TestWakeOnLanCommandPlainMac(t *testing.T) {
	var got []net.HardwareAddr
	executor := New(Options{
		WakeOnLan: func(macs []net.HardwareAddr) (int, error) {
			got = macs
			return 2, nil
		},
	})

	replies := collectReply(t, func(reply func(protocol.CommandReplyModel)) {
		executor.Handle(context.Background(), protocol.CommandModel{Id: 1, Command: "node/wakeonlan", Argument: "11-22-33-44-55-66"}, reply)
	})

	if len(replies) != 1 || replies[0].Status != protocol.StatusCompleted || len(got) != 1 {
		t.Fatalf("replies=%+v macs=%v", replies, got)
	}
}

func TestWakeOnLanCommandInvalidMac(t *testing.T) {
	executor := New(Options{})

	replies := collectReply(t, func(reply func(protocol.CommandReplyModel)) {
		executor.Handle(context.Background(), protocol.CommandModel{Id: 1, Command: "node/wakeonlan", Argument: "hello"}, reply)
	})

	if len(replies) != 1 || replies[0].Status != protocol.StatusError || !strings.Contains(replies[0].Data, "No valid MAC address") {
		t.Fatalf("unexpected replies: %+v", replies)
	}
}

func TestWakeOnLanRateLimit(t *testing.T) {
	now := time.Date(2026, 11, 25, 1, 0, 0, 0, time.UTC)
	sends := 0
	executor := New(Options{
		Now: func() time.Time { return now },
		WakeOnLan: func(macs []net.HardwareAddr) (int, error) {
			sends++
			return 1, nil
		},
	})

	send := func(id int64, macs string) protocol.CommandReplyModel {
		replies := collectReply(t, func(reply func(protocol.CommandReplyModel)) {
			executor.Handle(context.Background(), protocol.CommandModel{Id: id, Command: "node/wakeonlan", Argument: macs}, reply)
		})
		if len(replies) != 1 {
			t.Fatalf("expected one reply, got %+v", replies)
		}
		return replies[0]
	}

	if r := send(1, "AA-BB-CC-DD-EE-FF"); r.Status != protocol.StatusCompleted {
		t.Fatalf("first send: %+v", r)
	}
	if r := send(2, "AA-BB-CC-DD-EE-FF"); r.Status != protocol.StatusCanceled || !strings.Contains(r.Data, "Too frequent") {
		t.Fatalf("same mac within 3s should be ignored: %+v", r)
	}
	if r := send(3, "11-22-33-44-55-66"); r.Status != protocol.StatusCompleted {
		t.Fatalf("different mac must not be throttled: %+v", r)
	}

	now = now.Add(3 * time.Second)
	if r := send(4, "AA-BB-CC-DD-EE-FF"); r.Status != protocol.StatusCompleted {
		t.Fatalf("after 3s the same mac should be allowed: %+v", r)
	}
	if sends != 3 {
		t.Fatalf("send count = %d, want 3", sends)
	}
}
