// Package protocol contains the JSON models shared by the fake StarServer,
// the built-in mock agent and the conformance runner.
//
// The models mirror StarAgent.Native/spec/schemas/node-agent.schema.json.
// Field names are PascalCase because the server uses System.Text.Json
// defaults, while unknown fields are tolerated by both sides.
package protocol

import (
	"encoding/json"
	"fmt"
	"net/http"
	"strings"
	"time"
)

// DateTimeLayout is the wire format used by CommandModel.StartTime/Expire and NodeInfo fields.
const DateTimeLayout = "2006-01-02 15:04:05"

// ZeroDateTime is the protocol value for an unset timestamp.
const ZeroDateTime = "0001-01-01 00:00:00"

// MaxOutputBytes is the recommended command output limit.
const MaxOutputBytes = 64 * 1024

// Command reply status values.
const (
	StatusReady      = 0
	StatusProcessing = 1
	StatusCompleted  = 2
	StatusCancelled  = 3
	StatusError      = 4
)

// NodeInfo describes the machine that hosts the agent.
type NodeInfo struct {
	Version            string   `json:"Version,omitempty"`
	Compile            string   `json:"Compile,omitempty"`
	OSName             string   `json:"OSName,omitempty"`
	OSVersion          string   `json:"OSVersion,omitempty"`
	Architecture       string   `json:"Architecture,omitempty"`
	MachineName        string   `json:"MachineName,omitempty"`
	UserName           string   `json:"UserName,omitempty"`
	IP                 string   `json:"IP,omitempty"`
	Gateway            string   `json:"Gateway,omitempty"`
	Dns                string   `json:"Dns,omitempty"`
	ProcessorCount     int      `json:"ProcessorCount,omitempty"`
	Memory             int64    `json:"Memory,omitempty"`
	AvailableMemory    int64    `json:"AvailableMemory,omitempty"`
	FreeMemory         int64    `json:"FreeMemory,omitempty"`
	TotalSize          int64    `json:"TotalSize,omitempty"`
	AvailableFreeSpace int64    `json:"AvailableFreeSpace,omitempty"`
	DriveSize          int64    `json:"DriveSize,omitempty"`
	DriveInfo          string   `json:"DriveInfo,omitempty"`
	MaxOpenFiles       int      `json:"MaxOpenFiles,omitempty"`
	CLibVersion        string   `json:"CLibVersion,omitempty"`
	Dpi                string   `json:"Dpi,omitempty"`
	Resolution         string   `json:"Resolution,omitempty"`
	Product            string   `json:"Product,omitempty"`
	Vendor             string   `json:"Vendor,omitempty"`
	Processor          string   `json:"Processor,omitempty"`
	GPU                string   `json:"GPU,omitempty"`
	UUID               string   `json:"UUID,omitempty"`
	MachineGuid        string   `json:"MachineGuid,omitempty"`
	SerialNumber       string   `json:"SerialNumber,omitempty"`
	Board              string   `json:"Board,omitempty"`
	DiskID             string   `json:"DiskID,omitempty"`
	Macs               string   `json:"Macs,omitempty"`
	InstallPath        string   `json:"InstallPath,omitempty"`
	Runtime            string   `json:"Runtime,omitempty"`
	Framework          string   `json:"Framework,omitempty"`
	TimeZone           string   `json:"TimeZone,omitempty"`
	Time               string   `json:"Time,omitempty"`
	Plugins            []string `json:"Plugins,omitempty"`
}

// LoginRequest is the body of POST /node/login.
type LoginRequest struct {
	Code        string    `json:"Code,omitempty"`
	Secret      string    `json:"Secret,omitempty"`
	ProductCode string    `json:"ProductCode"`
	ClientId    string    `json:"ClientId,omitempty"`
	Project     string    `json:"Project,omitempty"`
	Node        *NodeInfo `json:"Node"`
}

// LoginResponse is returned by POST /node/login and POST /node/logout.
type LoginResponse struct {
	Code       string `json:"Code,omitempty"`
	Secret     string `json:"Secret,omitempty"`
	Name       string `json:"Name,omitempty"`
	Token      string `json:"Token,omitempty"`
	Expire     int    `json:"Expire"`
	Time       int64  `json:"Time"`
	ServerTime int64  `json:"ServerTime"`
}

// PingInfo is the heartbeat body sent to POST /node/ping.
type PingInfo struct {
	Time               int64   `json:"Time"`
	IP                 string  `json:"IP,omitempty"`
	Gateway            string  `json:"Gateway,omitempty"`
	Dns                string  `json:"Dns,omitempty"`
	Macs               string  `json:"Macs,omitempty"`
	DriveInfo          string  `json:"DriveInfo,omitempty"`
	Framework          string  `json:"Framework,omitempty"`
	Processes          string  `json:"Processes,omitempty"`
	ProcessCount       int     `json:"ProcessCount,omitempty"`
	Memory             int64   `json:"Memory,omitempty"`
	AvailableMemory    int64   `json:"AvailableMemory,omitempty"`
	FreeMemory         int64   `json:"FreeMemory,omitempty"`
	TotalSize          int64   `json:"TotalSize,omitempty"`
	AvailableFreeSpace int64   `json:"AvailableFreeSpace,omitempty"`
	CpuRate            float64 `json:"CpuRate,omitempty"`
	Temperature        float64 `json:"Temperature,omitempty"`
	Battery            float64 `json:"Battery,omitempty"`
	Signal             int     `json:"Signal,omitempty"`
	UplinkSpeed        int64   `json:"UplinkSpeed,omitempty"`
	DownlinkSpeed      int64   `json:"DownlinkSpeed,omitempty"`
	Uptime             int64   `json:"Uptime,omitempty"`
	Delay              int     `json:"Delay,omitempty"`
	TcpConnections     int     `json:"TcpConnections,omitempty"`
	TcpTimeWait        int     `json:"TcpTimeWait,omitempty"`
	TcpCloseWait       int     `json:"TcpCloseWait,omitempty"`
	IntranetScore      float64 `json:"IntranetScore,omitempty"`
	InternetScore      float64 `json:"InternetScore,omitempty"`
	SystemLoad         float64 `json:"SystemLoad,omitempty"`
	DiskIOPS           int     `json:"DiskIOPS,omitempty"`
	DiskActiveTime     float64 `json:"DiskActiveTime,omitempty"`
	GatewayLatency     int     `json:"GatewayLatency,omitempty"`
	GatewayLossRate    float64 `json:"GatewayLossRate,omitempty"`
	DnsLatency         int     `json:"DnsLatency,omitempty"`
	DnsLossRate        float64 `json:"DnsLossRate,omitempty"`
	ServerLatency      int     `json:"ServerLatency,omitempty"`
	ServerLossRate     float64 `json:"ServerLossRate,omitempty"`
}

// CommandModel is a remote command sent over WebSocket or in a ping response.
type CommandModel struct {
	Id        int64  `json:"Id"`
	Command   string `json:"Command"`
	Argument  string `json:"Argument,omitempty"`
	StartTime string `json:"StartTime,omitempty"`
	Expire    string `json:"Expire,omitempty"`
	TraceId   string `json:"TraceId,omitempty"`
	Status    int    `json:"Status"`
}

// PingResponse is returned by POST /node/ping.
type PingResponse struct {
	Time       int64          `json:"Time"`
	ServerTime int64          `json:"ServerTime"`
	Period     int            `json:"Period"`
	Token      string         `json:"Token,omitempty"`
	NewServer  string         `json:"NewServer,omitempty"`
	SyncTime   int64          `json:"SyncTime,omitempty"`
	Commands   []CommandModel `json:"Commands,omitempty"`
}

// CommandReplyModel is the body of POST /node/commandreply.
type CommandReplyModel struct {
	Id     int64  `json:"Id"`
	Status int    `json:"Status"`
	Data   string `json:"Data,omitempty"`
}

// EventModel is one element of the POST /node/postevents array.
type EventModel struct {
	Time   int64  `json:"Time"`
	Type   string `json:"Type"`
	Name   string `json:"Name"`
	Remark string `json:"Remark,omitempty"`
}

// UpgradeInfo is returned by POST /node/upgrade when an update is available.
type UpgradeInfo struct {
	Version     string `json:"Version,omitempty"`
	Source      string `json:"Source,omitempty"`
	FileHash    string `json:"FileHash,omitempty"`
	FileSize    int64  `json:"FileSize,omitempty"`
	Preinstall  string `json:"Preinstall,omitempty"`
	Executor    string `json:"Executor,omitempty"`
	Force       bool   `json:"Force,omitempty"`
	Description string `json:"Description,omitempty"`
}

// GetMachineName returns the machine name or an empty string for a nil node.
func (n *NodeInfo) GetMachineName() string {
	if n == nil {
		return ""
	}

	return n.MachineName
}

// NowMillis returns the current UTC Unix time in milliseconds.
func NowMillis() int64 { return time.Now().UTC().UnixMilli() }

// FormatDateTime converts a time to the protocol date-time string (UTC).
func FormatDateTime(t time.Time) string { return t.UTC().Format(DateTimeLayout) }

// ParseDateTime parses a protocol date-time string. Empty strings and the
// protocol zero date (0001-01-01 00:00:00) return the zero time.
func ParseDateTime(s string) time.Time {
	s = strings.TrimSpace(s)
	if s == "" || s == ZeroDateTime {
		return time.Time{}
	}
	t, err := time.ParseInLocation(DateTimeLayout, s, time.UTC)
	if err != nil {
		return time.Time{}
	}
	if t.Year() <= 1 {
		return time.Time{}
	}

	return t
}

// IsDateTime reports whether s is a valid protocol date-time string.
func IsDateTime(s string) bool {
	if len(s) != len(DateTimeLayout) {
		return false
	}
	_, err := time.ParseInLocation(DateTimeLayout, s, time.UTC)

	return err == nil
}

// BearerToken extracts the token from an Authorization header.
func BearerToken(r *http.Request) string {
	h := r.Header.Get("Authorization")
	if len(h) > 7 && strings.EqualFold(h[:7], "bearer ") {
		return strings.TrimSpace(h[7:])
	}

	return ""
}

// ParseEventCount parses the POST /node/postevents response, which may be a
// bare integer or an ApiFilter wrapper such as {"code":0,"data":1}.
func ParseEventCount(data []byte) (int, error) {
	var n int
	if err := json.Unmarshal(data, &n); err == nil {
		return n, nil
	}

	var wrapper struct {
		Code *int            `json:"code"`
		Data json.RawMessage `json:"data"`
	}
	if err := json.Unmarshal(data, &wrapper); err == nil && wrapper.Data != nil {
		if err := json.Unmarshal(wrapper.Data, &n); err == nil {
			return n, nil
		}
	}

	return 0, fmt.Errorf("unexpected postevents response: %s", truncate(string(data), 200))
}

// Truncate shortens s to at most n bytes.
func Truncate(s string, n int) string {
	if len(s) <= n {
		return s
	}
	if n <= 3 {
		return s[:n]
	}

	return s[:n-3] + "..."
}

func truncate(s string, n int) string { return Truncate(s, n) }
