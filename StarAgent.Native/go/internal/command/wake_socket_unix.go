//go:build unix

package command

import (
	"net"

	"golang.org/x/sys/unix"
)

// enableBroadcast 打开 SO_BROADCAST，允许向子网广播地址发送 UDP 报文。
func enableBroadcast(conn *net.UDPConn) error {
	raw, err := conn.SyscallConn()
	if err != nil {
		return err
	}

	var serr error
	if err := raw.Control(func(fd uintptr) {
		serr = unix.SetsockoptInt(int(fd), unix.SOL_SOCKET, unix.SO_BROADCAST, 1)
	}); err != nil {
		return err
	}

	return serr
}
