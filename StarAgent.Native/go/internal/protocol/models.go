package protocol

// 命令状态取值。
const (
	StatusReady      = 0
	StatusProcessing = 1
	StatusCompleted  = 2
	StatusCanceled   = 3
	StatusError      = 4
)

// NodeInfo 是节点硬件与系统信息。
type NodeInfo struct {
	Version            string   `json:"Version,omitempty"`
	Compile            Time     `json:"Compile"`
	OSName             string   `json:"OSName,omitempty"`
	OSVersion          string   `json:"OSVersion,omitempty"`
	Architecture       string   `json:"Architecture,omitempty"`
	MachineName        string   `json:"MachineName,omitempty"`
	UserName           string   `json:"UserName,omitempty"`
	IP                 string   `json:"IP,omitempty"`
	Gateway            string   `json:"Gateway,omitempty"`
	Dns                string   `json:"Dns,omitempty"`
	ProcessorCount     int      `json:"ProcessorCount"`
	Memory             uint64   `json:"Memory"`
	AvailableMemory    uint64   `json:"AvailableMemory"`
	FreeMemory         uint64   `json:"FreeMemory"`
	TotalSize          uint64   `json:"TotalSize"`
	AvailableFreeSpace uint64   `json:"AvailableFreeSpace"`
	DriveSize          uint64   `json:"DriveSize"`
	DriveInfo          string   `json:"DriveInfo,omitempty"`
	MaxOpenFiles       uint64   `json:"MaxOpenFiles,omitempty"`
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
	Time               Time     `json:"Time"`
	Plugins            []string `json:"Plugins,omitempty"`
}

// LoginInfo 是 POST /node/login 请求体。
type LoginInfo struct {
	Code        string   `json:"Code"`
	Secret      string   `json:"Secret"`
	ProductCode string   `json:"ProductCode"`
	ClientId    string   `json:"ClientId,omitempty"`
	Project     string   `json:"Project,omitempty"`
	Node        NodeInfo `json:"Node"`
}

// LoginResponse 是登录/注销响应。
type LoginResponse struct {
	Code       string `json:"Code,omitempty"`
	Secret     string `json:"Secret,omitempty"`
	Name       string `json:"Name,omitempty"`
	Token      string `json:"Token,omitempty"`
	Expire     int    `json:"Expire"`
	Time       int64  `json:"Time"`
	ServerTime int64  `json:"ServerTime"`
}

// PingInfo 是 POST /node/ping 请求体。
type PingInfo struct {
	Time               int64   `json:"Time"`
	IP                 string  `json:"IP,omitempty"`
	Gateway            string  `json:"Gateway,omitempty"`
	Dns                string  `json:"Dns,omitempty"`
	Macs               string  `json:"Macs,omitempty"`
	DriveInfo          string  `json:"DriveInfo,omitempty"`
	Framework          string  `json:"Framework,omitempty"`
	Processes          string  `json:"Processes,omitempty"`
	ProcessCount       uint64  `json:"ProcessCount"`
	Memory             uint64  `json:"Memory"`
	AvailableMemory    uint64  `json:"AvailableMemory"`
	FreeMemory         uint64  `json:"FreeMemory"`
	TotalSize          uint64  `json:"TotalSize"`
	AvailableFreeSpace uint64  `json:"AvailableFreeSpace"`
	CpuRate            float64 `json:"CpuRate"`
	Temperature        float64 `json:"Temperature"`
	Battery            float64 `json:"Battery"`
	Signal             int     `json:"Signal"`
	UplinkSpeed        uint64  `json:"UplinkSpeed"`
	DownlinkSpeed      uint64  `json:"DownlinkSpeed"`
	Uptime             uint64  `json:"Uptime"`
	Delay              int64   `json:"Delay"`
	TcpConnections     uint64  `json:"TcpConnections"`
	TcpTimeWait        uint64  `json:"TcpTimeWait"`
	TcpCloseWait       uint64  `json:"TcpCloseWait"`
	IntranetScore      float64 `json:"IntranetScore"`
	InternetScore      float64 `json:"InternetScore"`
	SystemLoad         float64 `json:"SystemLoad"`
	DiskIOPS           uint64  `json:"DiskIOPS"`
	DiskActiveTime     float64 `json:"DiskActiveTime"`
	GatewayLatency     uint64  `json:"GatewayLatency"`
	GatewayLossRate    float64 `json:"GatewayLossRate"`
	DnsLatency         uint64  `json:"DnsLatency"`
	DnsLossRate        float64 `json:"DnsLossRate"`
	ServerLatency      uint64  `json:"ServerLatency"`
	ServerLossRate     float64 `json:"ServerLossRate"`
}

// PingResponse 是 POST /node/ping 响应体。
type PingResponse struct {
	Time       int64          `json:"Time"`
	ServerTime int64          `json:"ServerTime"`
	Period     int            `json:"Period"`
	Token      string         `json:"Token,omitempty"`
	NewServer  string         `json:"NewServer,omitempty"`
	SyncTime   int            `json:"SyncTime"`
	Commands   []CommandModel `json:"Commands,omitempty"`
}

// CommandModel 是服务端下发的命令。
type CommandModel struct {
	Id        int64  `json:"Id"`
	Command   string `json:"Command"`
	Argument  string `json:"Argument,omitempty"`
	StartTime Time   `json:"StartTime"`
	Expire    Time   `json:"Expire"`
	TraceId   string `json:"TraceId,omitempty"`
	Status    int    `json:"Status"`
}

// CommandReplyModel 是 POST /node/commandreply 请求体。
type CommandReplyModel struct {
	Id     int64  `json:"Id"`
	Status int    `json:"Status"`
	Data   string `json:"Data,omitempty"`
}

// EventModel 是事件上报模型。
type EventModel struct {
	Time   int64  `json:"Time"`
	Type   string `json:"Type"`
	Name   string `json:"Name"`
	Remark string `json:"Remark,omitempty"`
}

// UpgradeInfo 是升级检查响应。
type UpgradeInfo struct {
	Version     string `json:"Version,omitempty"`
	Source      string `json:"Source,omitempty"`
	FileHash    string `json:"FileHash,omitempty"`
	FileSize    int64  `json:"FileSize"`
	Preinstall  string `json:"Preinstall,omitempty"`
	Executor    string `json:"Executor,omitempty"`
	Force       bool   `json:"Force"`
	Description string `json:"Description,omitempty"`
}
