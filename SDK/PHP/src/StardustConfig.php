<?php
/**
 * 星尘配置中心 PHP SDK
 * Stardust Config Center SDK for PHP
 * 
 * 适用于 PHP 7.4+
 * 提供星尘配置中心的接入能力
 * 
 * @version 1.0.0
 * @link https://github.com/NewLifeX/Stardust
 */

/**
 * 星尘配置中心客户端
 * 
 * 用于从星尘配置中心拉取应用配置
 */
class StardustConfig
{
    private string $server;
    private string $appId;
    private string $secret;
    private string $clientId;
    private string $scope = '';

    private string $token = '';
    private int $tokenExpire = 0;

    private int $version = 0;
    private array $configs = [];
    private int $nextVersion = 0;
    private string $nextPublish = '';
    private string $updateTime = '';
    
    private bool $debug = false;

    /**
     * 构造函数
     * 
     * @param string $server 星尘服务器地址，如 http://star.example.com:6600
     * @param string $appId 应用标识
     * @param string $secret 应用密钥
     * @param string $scope 作用域，如 dev/test/prod，默认空
     */
    public function __construct(string $server, string $appId, string $secret = '', string $scope = '')
    {
        $this->server = rtrim($server, '/');
        $this->appId = $appId;
        $this->secret = $secret;
        $this->clientId = $this->getLocalIP() . '@' . getmypid();
        $this->scope = $scope;
    }

    /**
     * 设置调试模式
     * 
     * @param bool $debug
     */
    public function setDebug(bool $debug): void
    {
        $this->debug = $debug;
    }

    /**
     * 设置作用域
     * 
     * @param string $scope
     */
    public function setScope(string $scope): void
    {
        $this->scope = $scope;
    }

    /**
     * 登录获取令牌
     * 
     * @return bool 是否成功
     */
    public function login(): bool
    {
        $url = $this->server . '/App/Login';
        $payload = [
            'AppId' => $this->appId,
            'Secret' => $this->secret,
            'ClientId' => $this->clientId,
            'AppName' => $this->appId,
        ];

        $data = $this->postJson($url, $payload);
        if ($data !== null) {
            $this->token = $data['Token'] ?? '';
            $this->tokenExpire = time() + 7200; // 2小时过期
            if (!empty($data['Code'])) $this->appId = $data['Code'];
            if (!empty($data['Secret'])) $this->secret = $data['Secret'];
            
            if ($this->debug) {
                error_log("[StardustConfig] Login success, appId={$this->appId}");
            }
            
            return true;
        }
        
        if ($this->debug) {
            error_log("[StardustConfig] Login failed");
        }
        
        return false;
    }

    /**
     * 确保令牌有效
     */
    private function ensureToken(): void
    {
        // 如果令牌为空或即将过期（提前5分钟刷新），则重新登录
        if (empty($this->token) || $this->tokenExpire - time() < 300) {
            $this->login();
        }
    }

    /**
     * 获取所有配置
     * 
     * @param bool $force 是否强制刷新，不考虑版本
     * @return array|null 配置字典，失败返回 null
     */
    public function getAll(bool $force = false): ?array
    {
        $this->ensureToken();

        $url = $this->server . '/Config/GetAll?Token=' . urlencode($this->token);
        $payload = [
            'AppId' => $this->appId,
            'Secret' => $this->secret,
            'ClientId' => $this->clientId,
            'Scope' => $this->scope,
            'Version' => $force ? 0 : $this->version,
        ];

        $data = $this->postJson($url, $payload);
        if ($data !== null) {
            $newVersion = $data['Version'] ?? 0;
            
            // 版本相同且不强制刷新，返回缓存的配置
            if (!$force && $newVersion > 0 && $newVersion === $this->version) {
                if ($this->debug) {
                    error_log("[StardustConfig] Config version not changed: {$this->version}");
                }
                return $this->configs;
            }

            // 更新配置
            $this->version = $newVersion;
            $this->configs = $data['Configs'] ?? [];
            $this->nextVersion = $data['NextVersion'] ?? 0;
            $this->nextPublish = $data['NextPublish'] ?? '';
            $this->updateTime = $data['UpdateTime'] ?? '';
            
            if ($this->debug) {
                error_log("[StardustConfig] Config loaded: version={$this->version}, count=" . count($this->configs));
            }
            
            return $this->configs;
        }
        
        if ($this->debug) {
            error_log("[StardustConfig] Get config failed");
        }
        
        return null;
    }

    /**
     * 获取指定配置项
     * 
     * @param string $key 配置键
     * @param mixed $default 默认值
     * @return mixed
     */
    public function get(string $key, $default = null)
    {
        // 如果配置为空，尝试加载
        if (empty($this->configs)) {
            $this->getAll();
        }

        return $this->configs[$key] ?? $default;
    }

    /**
     * 获取当前配置版本
     * 
     * @return int
     */
    public function getVersion(): int
    {
        return $this->version;
    }

    /**
     * 获取下一个版本号
     * 
     * @return int
     */
    public function getNextVersion(): int
    {
        return $this->nextVersion;
    }

    /**
     * 获取下次发布时间
     * 
     * @return string
     */
    public function getNextPublish(): string
    {
        return $this->nextPublish;
    }

    /**
     * 获取配置更新时间
     * 
     * @return string
     */
    public function getUpdateTime(): string
    {
        return $this->updateTime;
    }

    /**
     * 检查是否有新版本配置等待发布
     * 
     * @return bool
     */
    public function hasNewVersion(): bool
    {
        return $this->nextVersion > 0 && $this->nextVersion != $this->version;
    }

    // ========== HTTP 工具方法 ==========

    /**
     * POST JSON 数据
     * 
     * @param string $url
     * @param array $payload
     * @return array|null
     */
    private function postJson(string $url, array $payload): ?array
    {
        $body = json_encode($payload, JSON_UNESCAPED_UNICODE);

        $ch = curl_init($url);
        curl_setopt_array($ch, [
            CURLOPT_POST => true,
            CURLOPT_POSTFIELDS => $body,
            CURLOPT_HTTPHEADER => ['Content-Type: application/json; charset=utf-8'],
            CURLOPT_RETURNTRANSFER => true,
            CURLOPT_TIMEOUT => 10,
            CURLOPT_CONNECTTIMEOUT => 5,
        ]);

        $response = curl_exec($ch);
        $httpCode = curl_getinfo($ch, CURLINFO_HTTP_CODE);
        $error = curl_error($ch);
        curl_close($ch);

        if ($response === false || $httpCode >= 400) {
            if ($this->debug) {
                error_log("[StardustConfig] HTTP request failed: url={$url}, code={$httpCode}, error={$error}");
            }
            return null;
        }

        $json = json_decode($response, true);
        if ($json !== null && ($json['code'] ?? -1) === 0) {
            return $json['data'] ?? null;
        }
        
        if ($this->debug) {
            error_log("[StardustConfig] API response error: " . json_encode($json));
        }
        
        return null;
    }

    /**
     * 获取本机IP地址
     * 
     * @return string
     */
    private function getLocalIP(): string
    {
        $hostname = gethostname();
        $ip = gethostbyname($hostname);
        return $ip ?: '127.0.0.1';
    }
}
