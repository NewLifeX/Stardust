//go:build !windows && !unix

package command

import "net"

// enableBroadcast 在不支持的平台沿用系统默认行为。
func enableBroadcast(conn *net.UDPConn) error { return nil }
