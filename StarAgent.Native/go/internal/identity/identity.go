package identity

import (
	"context"
	"fmt"
	"net"
	"os"
	"os/exec"
	"path/filepath"
	"runtime"
	"sort"
	"strings"
	"time"
)

// Info 是稳定的节点硬件标识。取不到的字段保持空字符串，不生成随机值。
type Info struct {
	UUID         string
	MachineGuid  string
	SerialNumber string
	Board        string
	DiskID       string
	Macs         string
}

// Gather 采集稳定硬件标识。所有采集失败都被容忍。
func Gather(ctx context.Context) Info {
	var info Info

	switch runtime.GOOS {
	case "linux":
		info.UUID = cleanID(readTrim("/sys/class/dmi/id/product_uuid"))
		info.MachineGuid = cleanID(readTrim("/etc/machine-id"))
		if info.MachineGuid == "" {
			info.MachineGuid = cleanID(readTrim("/var/lib/dbus/machine-id"))
		}
		info.SerialNumber = cleanID(readTrim("/sys/class/dmi/id/product_serial"))
		info.Board = cleanID(readTrim("/sys/class/dmi/id/board_serial"))
		if info.Board == "" {
			info.Board = cleanID(readTrim("/sys/class/dmi/id/board_name"))
		}
		info.DiskID = cleanID(linuxDiskSerial())
	case "windows":
		for key, value := range windowsHardware(ctx) {
			switch key {
			case "UUID":
				info.UUID = cleanID(value)
			case "SERIAL":
				info.SerialNumber = cleanID(value)
			case "BOARD":
				info.Board = cleanID(value)
			case "DISK":
				info.DiskID = cleanID(value)
			}
		}
		info.MachineGuid = cleanID(windowsMachineGuid(ctx))
	case "darwin":
		values := darwinHardware(ctx)
		info.UUID = cleanID(values["UUID"])
		info.SerialNumber = cleanID(values["SERIAL"])
		info.Board = cleanID(values["BOARD"])
	}

	info.Macs = formatMacs(collectMACs())

	return info
}

func readTrim(path string) string {
	data, err := os.ReadFile(path)
	if err != nil {
		return ""
	}

	return strings.TrimSpace(string(data))
}

var invalidIDs = map[string]bool{
	"":                                     true,
	"none":                                 true,
	"unknown":                              true,
	"not specified":                        true,
	"not settable":                         true,
	"to be filled by o.e.m.":               true,
	"to be filled by o.e.m":                true,
	"default string":                       true,
	"system serial number":                 true,
	"0":                                    true,
	"00000000-0000-0000-0000-000000000000": true,
	"ffffffff-ffff-ffff-ffff-ffffffffffff": true,
}

func cleanID(value string) string {
	value = strings.TrimSpace(strings.Trim(value, `"'`))
	if invalidIDs[strings.ToLower(value)] {
		return ""
	}

	// 服务端按大小写敏感比较 UUID/MachineGuid/Serial 等字段，这里必须保留原始大小写。
	// 只有 MAC 由 formatMacs 统一为大写、短横线分隔。
	return value
}

func collectMACs() []string {
	interfaces, err := net.Interfaces()
	if err != nil {
		return nil
	}

	seen := make(map[string]bool)
	var macs []string
	for _, iface := range interfaces {
		if iface.Flags&net.FlagLoopback != 0 || len(iface.HardwareAddr) == 0 {
			continue
		}
		if isVirtualInterface(iface.Name) {
			continue
		}
		text := iface.HardwareAddr.String()
		if text == "" || text == "00:00:00:00:00:00" || seen[text] {
			continue
		}
		seen[text] = true
		macs = append(macs, text)
	}
	sort.Strings(macs)

	return macs
}

func isVirtualInterface(name string) bool {
	name = strings.ToLower(name)
	prefixes := []string{"lo", "docker", "veth", "br-", "virbr", "vmnet", "tun", "tap", "utun", "awdl", "llw", "bridge", "vboxnet", "zt", "tailscale"}
	for _, prefix := range prefixes {
		if strings.HasPrefix(name, prefix) {
			return true
		}
	}

	return false
}

func formatMacs(macs []string) string {
	result := make([]string, 0, len(macs))
	for _, mac := range macs {
		text := strings.ToUpper(strings.ReplaceAll(mac, ":", "-"))
		if text != "" {
			result = append(result, text)
		}
	}
	sort.Strings(result)
	seen := make(map[string]bool, len(result))
	unique := result[:0]
	for _, mac := range result {
		if !seen[mac] {
			seen[mac] = true
			unique = append(unique, mac)
		}
	}

	return strings.Join(unique, ",")
}

func linuxDiskSerial() string {
	entries, err := os.ReadDir("/sys/block")
	if err != nil {
		return ""
	}
	for _, entry := range entries {
		name := entry.Name()
		if strings.HasPrefix(name, "loop") || strings.HasPrefix(name, "ram") || strings.HasPrefix(name, "dm-") || strings.HasPrefix(name, "md") {
			continue
		}
		if serial := readTrim(filepath.Join("/sys/block", name, "device", "serial")); serial != "" {
			return serial
		}
		if serial := readTrim(filepath.Join("/sys/block", name, "serial")); serial != "" {
			return serial
		}
	}

	return ""
}

func windowsMachineGuid(ctx context.Context) string {
	output := runCommand(ctx, "reg", "query", `HKLM\SOFTWARE\Microsoft\Cryptography`, "/v", "MachineGuid")
	for _, line := range strings.Split(output, "\n") {
		fields := strings.Fields(line)
		if len(fields) >= 3 && strings.EqualFold(fields[0], "MachineGuid") {
			return fields[len(fields)-1]
		}
	}

	return ""
}

func windowsHardware(ctx context.Context) map[string]string {
	script := `$ErrorActionPreference='SilentlyContinue'
"UUID=" + (Get-CimInstance -ClassName Win32_ComputerSystemProduct).UUID
"SERIAL=" + (Get-CimInstance -ClassName Win32_BIOS).SerialNumber
"BOARD=" + (Get-CimInstance -ClassName Win32_BaseBoard).SerialNumber
"DISK=" + (Get-CimInstance -ClassName Win32_DiskDrive | Select-Object -First 1).SerialNumber`
	output := runCommand(ctx, "powershell", "-NoProfile", "-NonInteractive", "-Command", script)

	return parseKeyValues(output)
}

func darwinHardware(ctx context.Context) map[string]string {
	values := make(map[string]string)

	ioreg := runCommand(ctx, "ioreg", "-rd1", "-c", "IOPlatformExpertDevice")
	for _, line := range strings.Split(ioreg, "\n") {
		if strings.Contains(line, "IOPlatformUUID") {
			if parts := strings.SplitN(line, "=", 2); len(parts) == 2 {
				values["UUID"] = strings.Trim(strings.TrimSpace(parts[1]), `"`)
			}
		}
	}

	profiler := runCommand(ctx, "system_profiler", "SPHardwareDataType")
	for _, line := range strings.Split(profiler, "\n") {
		parts := strings.SplitN(line, ":", 2)
		if len(parts) != 2 {
			continue
		}
		key := strings.TrimSpace(parts[0])
		value := strings.TrimSpace(parts[1])
		switch {
		case strings.EqualFold(key, "Serial Number (system)"):
			values["SERIAL"] = value
		case strings.EqualFold(key, "Hardware UUID"):
			if values["UUID"] == "" {
				values["UUID"] = value
			}
		case strings.EqualFold(key, "Model Identifier"):
			values["BOARD"] = value
		}
	}

	return values
}

func parseKeyValues(output string) map[string]string {
	values := make(map[string]string)
	for _, line := range strings.Split(output, "\n") {
		parts := strings.SplitN(strings.TrimSpace(line), "=", 2)
		if len(parts) == 2 {
			values[strings.ToUpper(strings.TrimSpace(parts[0]))] = strings.TrimSpace(parts[1])
		}
	}

	return values
}

func runCommand(ctx context.Context, name string, args ...string) string {
	ctx, cancel := context.WithTimeout(ctx, 8*time.Second)
	defer cancel()

	cmd := exec.CommandContext(ctx, name, args...)
	output, err := cmd.Output()
	if err != nil {
		return ""
	}

	return string(output)
}

// LocalIP 返回本机出口 IPv4 地址。
func LocalIP() string {
	if conn, err := net.DialTimeout("udp4", "8.8.8.8:80", 2*time.Second); err == nil {
		defer conn.Close()
		if addr, ok := conn.LocalAddr().(*net.UDPAddr); ok && addr.IP != nil {
			return addr.IP.String()
		}
	}

	interfaces, err := net.Interfaces()
	if err != nil {
		return ""
	}
	for _, iface := range interfaces {
		if iface.Flags&net.FlagLoopback != 0 || iface.Flags&net.FlagUp == 0 {
			continue
		}
		addrs, err := iface.Addrs()
		if err != nil {
			continue
		}
		for _, addr := range addrs {
			ipNet, ok := addr.(*net.IPNet)
			if ok && ipNet.IP.To4() != nil {
				return ipNet.IP.String()
			}
		}
	}

	return ""
}

// Gateway 返回默认网关。
func Gateway(ctx context.Context) string {
	switch runtime.GOOS {
	case "linux":
		return linuxGateway()
	case "windows":
		return windowsGateway(ctx)
	case "darwin":
		output := runCommand(ctx, "route", "-n", "get", "default")
		for _, line := range strings.Split(output, "\n") {
			parts := strings.SplitN(strings.TrimSpace(line), ":", 2)
			if len(parts) == 2 && strings.EqualFold(strings.TrimSpace(parts[0]), "gateway") {
				return strings.TrimSpace(parts[1])
			}
		}
	}

	return ""
}

func linuxGateway() string {
	data := readTrim("/proc/net/route")
	for _, line := range strings.Split(data, "\n")[1:] {
		fields := strings.Fields(line)
		if len(fields) < 3 || fields[1] != "00000000" {
			continue
		}
		var b [4]byte
		value, err := parseHex32(fields[2])
		if err != nil {
			continue
		}
		b[0] = byte(value)
		b[1] = byte(value >> 8)
		b[2] = byte(value >> 16)
		b[3] = byte(value >> 24)
		return net.IPv4(b[0], b[1], b[2], b[3]).String()
	}

	return ""
}

func parseHex32(text string) (uint32, error) {
	var value uint32
	_, err := fmt.Sscanf(text, "%x", &value)
	if err != nil {
		return 0, err
	}

	return value, nil
}

func windowsGateway(ctx context.Context) string {
	output := runCommand(ctx, "route", "print", "0.0.0.0")
	for _, line := range strings.Split(output, "\n") {
		fields := strings.Fields(line)
		if len(fields) >= 3 && fields[0] == "0.0.0.0" && fields[1] == "0.0.0.0" && net.ParseIP(fields[2]) != nil {
			return fields[2]
		}
	}

	return ""
}

// DNS 返回 DNS 服务器列表。StarServer 的 Node.Dns 字段限制 50 字符，超长会导致节点校验失败。
func DNS(ctx context.Context) string {
	switch runtime.GOOS {
	case "windows":
		output := runCommand(ctx, "powershell", "-NoProfile", "-NonInteractive", "-Command",
			`(Get-DnsClientServerAddress -AddressFamily IPv4 | ForEach-Object { $_.ServerAddresses }) -join ','`)
		return truncateDNS(strings.TrimSpace(output))
	default:
		data := readTrim("/etc/resolv.conf")
		var servers []string
		for _, line := range strings.Split(data, "\n") {
			fields := strings.Fields(line)
			if len(fields) >= 2 && fields[0] == "nameserver" {
				servers = append(servers, fields[1])
			}
		}
		return truncateDNS(strings.Join(servers, ","))
	}
}

func truncateDNS(value string) string {
	const maxLength = 50
	if len(value) <= maxLength {
		return value
	}
	return value[:maxLength]
}

// Architecture 返回协议使用的处理器架构名。
func Architecture() string {
	switch runtime.GOARCH {
	case "amd64":
		return "X64"
	case "386":
		return "X86"
	case "arm64":
		return "Arm64"
	case "arm":
		return "Arm"
	case "mips64", "mips64le":
		return "Mips64"
	case "riscv64":
		return "RiscV64"
	case "loong64":
		return "LA64"
	default:
		return runtime.GOARCH
	}
}

// HostName 返回主机名。
func HostName() string {
	if runtime.GOOS == "linux" {
		if name := readTrim("/etc/hostname"); name != "" {
			return name
		}
	}
	name, _ := os.Hostname()

	return name
}

// UserName 返回当前用户。
func UserName() string {
	if user := os.Getenv("USER"); user != "" {
		return user
	}
	if user := os.Getenv("USERNAME"); user != "" {
		return user
	}

	return ""
}

// ExecutableDir 返回当前可执行文件所在目录。
func ExecutableDir() string {
	exe, err := os.Executable()
	if err != nil {
		return ""
	}

	return filepath.Dir(exe)
}
