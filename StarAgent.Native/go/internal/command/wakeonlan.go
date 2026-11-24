package command

import (
	"encoding/hex"
	"encoding/json"
	"fmt"
	"net"
	"strings"
)

// wakePorts 是 Wake-on-LAN 常用的 UDP 端口。
var wakePorts = []int{9, 7}

var macSeparators = strings.NewReplacer("-", "", ":", "", ".", "")

// ParseMacs 解析 MAC 列表。
// 支持 AA-BB-CC-DD-EE-FF、aa:bb:cc:dd:ee:ff、aabb.ccdd.eeff，多个用逗号、分号或空白分隔；非法项忽略，重复项去重。
func ParseMacs(text string) []net.HardwareAddr {
	var list []net.HardwareAddr
	seen := make(map[string]bool)
	for _, item := range strings.FieldsFunc(text, func(r rune) bool {
		return r == ',' || r == ';' || r == ' ' || r == '\t' || r == '\r' || r == '\n'
	}) {
		hexText := macSeparators.Replace(item)
		if len(hexText) != 12 {
			continue
		}
		raw, err := hex.DecodeString(hexText)
		if err != nil {
			continue
		}
		key := strings.ToLower(hexText)
		if seen[key] {
			continue
		}
		seen[key] = true
		list = append(list, net.HardwareAddr(raw))
	}

	return list
}

// BuildMagicPacket 构造魔术包：6 个 0xFF 加 16 次目标 MAC，共 102 字节。
func BuildMagicPacket(mac net.HardwareAddr) []byte {
	packet := make([]byte, 6+16*6)
	for i := 0; i < 6; i++ {
		packet[i] = 0xFF
	}
	for i := 0; i < 16; i++ {
		copy(packet[6+i*6:], mac)
	}

	return packet
}

// SendWakeOnLan 向本机所有局域网网卡广播魔术包，返回发送的报文数量。
// 单张网卡或单个目标发送失败只记录到返回值，不影响其它网卡。
func SendWakeOnLan(macs []net.HardwareAddr) (int, error) {
	if len(macs) == 0 {
		return 0, nil
	}

	ifaces, err := net.Interfaces()
	if err != nil {
		return 0, err
	}

	sent := 0
	var lastErr error
	for _, iface := range ifaces {
		if iface.Flags&net.FlagUp == 0 || iface.Flags&net.FlagLoopback != 0 {
			continue
		}
		addrs, err := iface.Addrs()
		if err != nil {
			continue
		}
		for _, addr := range addrs {
			ipnet, ok := addr.(*net.IPNet)
			if !ok {
				continue
			}
			ip4 := ipnet.IP.To4()
			// 自动专用地址（169.254.0.0/16）通常来自未连网网卡，广播出去没有意义
			if ip4 == nil || ip4.IsLoopback() || ip4.IsLinkLocalUnicast() {
				continue
			}

			conn, err := net.ListenUDP("udp4", &net.UDPAddr{IP: ip4})
			if err != nil {
				lastErr = err
				continue
			}
			if err := enableBroadcast(conn); err != nil {
				lastErr = err
				conn.Close()
				continue
			}

			// 绑定本网卡地址，确保报文从该网卡发出；多网卡机器需要逐张网卡发送
			broadcast := broadcastAddr(ip4, ipnet.Mask)
			targets := []net.IP{net.IPv4bcast}
			if broadcast != nil {
				targets = append([]net.IP{broadcast}, targets...)
			}
			for _, mac := range macs {
				packet := BuildMagicPacket(mac)
				for _, port := range wakePorts {
					for _, target := range targets {
						if _, err := conn.WriteToUDP(packet, &net.UDPAddr{IP: target, Port: port}); err != nil {
							lastErr = err
						} else {
							sent++
						}
					}
				}
			}
			conn.Close()
		}
	}

	// 部分网卡成功时忽略个别失败，全部失败才把错误交给调用方
	if sent > 0 {
		return sent, nil
	}

	return sent, lastErr
}

// broadcastAddr 计算子网定向广播地址。
func broadcastAddr(ip net.IP, mask net.IPMask) net.IP {
	if len(mask) == net.IPv6len {
		mask = mask[12:]
	}
	if len(ip) != net.IPv4len || len(mask) != net.IPv4len {
		return nil
	}

	broadcast := make(net.IP, net.IPv4len)
	for i := 0; i < net.IPv4len; i++ {
		broadcast[i] = ip[i] | ^mask[i]
	}

	return broadcast
}

// wakeKey 生成限频用的 MAC 集合键。
func wakeKey(macs []net.HardwareAddr) string {
	parts := make([]string, 0, len(macs))
	for _, mac := range macs {
		parts = append(parts, mac.String())
	}

	return strings.Join(parts, ",")
}

// extractMacs 从 JSON 参数取 macs 字段，兼容不同序列化策略产生的 Macs/macs；非 JSON 时按纯 MAC 文本处理。
func extractMacs(argument string) string {
	text := strings.TrimSpace(argument)
	if !strings.HasPrefix(text, "{") {
		return text
	}

	var body map[string]any
	if err := json.Unmarshal([]byte(text), &body); err != nil {
		return text
	}
	for key, value := range body {
		if strings.EqualFold(key, "macs") {
			return fmt.Sprint(value)
		}
	}

	return text
}
