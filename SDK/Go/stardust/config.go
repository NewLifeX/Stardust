package stardust

import (
	"bytes"
	"encoding/json"
	"fmt"
	"io"
	"net/http"
	"net/url"
	"os"
	"sync"
	"time"
)

// ConfigInfo 配置信息
type ConfigInfo struct {
	Version     int               `json:"Version"`
	Scope       string            `json:"Scope"`
	SourceIP    string            `json:"SourceIP"`
	NextVersion int               `json:"NextVersion"`
	NextPublish string            `json:"NextPublish"`
	UpdateTime  int64             `json:"UpdateTime"`
	Configs     map[string]string `json:"Configs"`
}

// ConfigClient 配置中心客户端
type ConfigClient struct {
	Server   string
	AppID    string
	Secret   string
	ClientID string

	token       string
	tokenExpire int64
	version     int
	configs     map[string]string
	mu          sync.RWMutex
	running     bool
	stopCh      chan struct{}
	client      *http.Client
	onChange    func(map[string]string)
}

// NewConfigClient 创建配置客户端
func NewConfigClient(server, appID, secret string) *ConfigClient {
	return &ConfigClient{
		Server:   server,
		AppID:    appID,
		Secret:   secret,
		ClientID: fmt.Sprintf("%s@%d", getLocalIP(), os.Getpid()),
		configs:  make(map[string]string),
		stopCh:   make(chan struct{}),
		client:   &http.Client{Timeout: 10 * time.Second},
	}
}

// Start 启动配置客户端
func (c *ConfigClient) Start() {
	c.login()
	c.running = true
	go c.pollLoop()
}

// Stop 停止配置客户端
func (c *ConfigClient) Stop() {
	c.running = false
	close(c.stopCh)
}

// Get 获取配置项
func (c *ConfigClient) Get(key string) string {
	c.mu.RLock()
	defer c.mu.RUnlock()
	return c.configs[key]
}

// GetAll 获取所有配置
func (c *ConfigClient) GetAll() map[string]string {
	c.mu.RLock()
	defer c.mu.RUnlock()
	result := make(map[string]string, len(c.configs))
	for k, v := range c.configs {
		result[k] = v
	}
	return result
}

// OnChange 注册配置变更回调
func (c *ConfigClient) OnChange(callback func(map[string]string)) {
	c.onChange = callback
}

func (c *ConfigClient) login() {
	payload := map[string]interface{}{
		"AppId":    c.AppID,
		"Secret":   c.Secret,
		"ClientId": c.ClientID,
		"AppName":  c.AppID,
	}

	body, err := json.Marshal(payload)
	if err != nil {
		return
	}

	resp, err := c.client.Post(c.Server+"/App/Login", "application/json", bytes.NewReader(body))
	if err != nil {
		fmt.Fprintf(os.Stderr, "[Stardust] Config login failed: %v\n", err)
		return
	}
	defer resp.Body.Close()

	var apiResp APIResponse
	if err := json.NewDecoder(resp.Body).Decode(&apiResp); err != nil || apiResp.Code != 0 {
		return
	}

	var loginResp LoginResponse
	if err := json.Unmarshal(apiResp.Data, &loginResp); err != nil {
		return
	}

	c.token = loginResp.Token
	if loginResp.Expire > 0 {
		c.tokenExpire = time.Now().Unix() + int64(loginResp.Expire)
	}
}

func (c *ConfigClient) getAllConfig() {
	payload := map[string]interface{}{
		"AppId":    c.AppID,
		"Secret":   c.Secret,
		"ClientId": c.ClientID,
		"Version":  c.version,
	}

	body, err := json.Marshal(payload)
	if err != nil {
		return
	}

	reqURL := fmt.Sprintf("%s/Config/GetAll?Token=%s", c.Server, url.QueryEscape(c.token))
	resp, err := c.client.Post(reqURL, "application/json", bytes.NewReader(body))
	if err != nil {
		fmt.Fprintf(os.Stderr, "[Stardust] Config GetAll failed: %v\n", err)
		return
	}
	defer resp.Body.Close()

	respBody, _ := io.ReadAll(resp.Body)
	var apiResp APIResponse
	if err := json.Unmarshal(respBody, &apiResp); err != nil || apiResp.Code != 0 {
		return
	}

	var configInfo ConfigInfo
	if err := json.Unmarshal(apiResp.Data, &configInfo); err != nil {
		return
	}

	// 版本没变化，不更新
	if c.version > 0 && configInfo.Version == c.version && configInfo.Configs == nil {
		return
	}

	// 更新配置
	if configInfo.Configs != nil {
		c.mu.Lock()
		c.configs = configInfo.Configs
		c.version = configInfo.Version
		c.mu.Unlock()

		// 触发回调
		if c.onChange != nil {
			go c.onChange(configInfo.Configs)
		}
	}
}

func (c *ConfigClient) pollLoop() {
	// 首次立即获取配置
	c.getAllConfig()

	ticker := time.NewTicker(30 * time.Second)
	defer ticker.Stop()
	for {
		select {
		case <-ticker.C:
			c.getAllConfig()
		case <-c.stopCh:
			return
		}
	}
}
