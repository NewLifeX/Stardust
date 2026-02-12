<?php
/**
 * 星尘监控 PHP SDK
 * Stardust APM Monitoring SDK for PHP
 * 
 * 适用于 PHP 7.4+
 * 提供星尘 APM 监控的接入能力
 * 
 * @version 1.0.0
 * @link https://github.com/NewLifeX/Stardust
 */

/**
 * 追踪片段类
 * 
 * 表示一个操作的追踪信息
 */
class StardustSpan
{
    public string $id;
    public string $parentId;
    public string $traceId;
    public int $startTime;
    public int $endTime = 0;
    public string $tag = '';
    public string $error = '';

    private string $name;
    private StardustTracer $tracer;

    public function __construct(string $name, StardustTracer $tracer, string $parentId = '')
    {
        $this->id = bin2hex(random_bytes(8));
        $this->parentId = $parentId;
        $this->traceId = bin2hex(random_bytes(16));
        $this->name = $name;
        $this->startTime = intval(microtime(true) * 1000);
        $this->tracer = $tracer;
    }

    /**
     * 设置错误信息
     * 
     * @param mixed $error 异常对象或错误信息
     */
    public function setError($error): void
    {
        if ($error instanceof Throwable) {
            $this->error = get_class($error) . ': ' . $error->getMessage();
        } else {
            $this->error = (string)$error;
        }
    }

    /**
     * 完成追踪片段
     */
    public function finish(): void
    {
        $this->endTime = intval(microtime(true) * 1000);
        $this->tracer->finishSpan($this->name, $this);
    }

    /**
     * 转换为数组格式
     * 
     * @return array
     */
    public function toArray(): array
    {
        return [
            'Id' => $this->id,
            'ParentId' => $this->parentId,
            'TraceId' => $this->traceId,
            'StartTime' => $this->startTime,
            'EndTime' => $this->endTime,
            'Tag' => $this->tag,
            'Error' => $this->error,
        ];
    }
}

/**
 * 追踪片段构建器类
 * 
 * 用于聚合同一操作的多个追踪片段
 */
class StardustSpanBuilder
{
    public string $name;
    public int $startTime;
    public int $endTime = 0;
    public int $total = 0;
    public int $errors = 0;
    public int $cost = 0;
    public int $maxCost = 0;
    public int $minCost = 0;
    public array $samples = [];
    public array $errorSamples = [];

    private int $maxSamplesLimit;
    private int $maxErrorsLimit;

    public function __construct(string $name, int $maxSamples = 1, int $maxErrors = 10)
    {
        $this->name = $name;
        $this->startTime = intval(microtime(true) * 1000);
        $this->maxSamplesLimit = $maxSamples;
        $this->maxErrorsLimit = $maxErrors;
    }

    /**
     * 添加一个追踪片段到构建器
     * 
     * @param StardustSpan $span
     */
    public function addSpan(StardustSpan $span): void
    {
        $elapsed = $span->endTime - $span->startTime;

        $this->total++;
        $this->cost += $elapsed;
        if ($this->maxCost === 0 || $elapsed > $this->maxCost) $this->maxCost = $elapsed;
        if ($this->minCost === 0 || $elapsed < $this->minCost) $this->minCost = $elapsed;

        if (!empty($span->error)) {
            $this->errors++;
            if (count($this->errorSamples) < $this->maxErrorsLimit) {
                $this->errorSamples[] = $span;
            }
        } else {
            if (count($this->samples) < $this->maxSamplesLimit) {
                $this->samples[] = $span;
            }
        }
        $this->endTime = intval(microtime(true) * 1000);
    }

    /**
     * 转换为数组格式
     * 
     * @return array
     */
    public function toArray(): array
    {
        return [
            'Name' => $this->name,
            'StartTime' => $this->startTime,
            'EndTime' => $this->endTime,
            'Total' => $this->total,
            'Errors' => $this->errors,
            'Cost' => $this->cost,
            'MaxCost' => $this->maxCost,
            'MinCost' => $this->minCost,
            'Samples' => array_map(fn($s) => $s->toArray(), $this->samples),
            'ErrorSamples' => array_map(fn($s) => $s->toArray(), $this->errorSamples),
        ];
    }
}

/**
 * 星尘追踪器主类
 * 
 * 用于创建追踪片段并上报到星尘监控中心
 */
class StardustTracer
{
    private string $server;
    private string $appId;
    private string $appName;
    private string $secret;
    private string $clientId;

    private string $token = '';
    private int $tokenExpire = 0;

    // 采样参数
    private int $period = 60;
    private int $maxSamples = 1;
    private int $maxErrors = 10;
    private int $timeout = 5000;
    private int $maxTagLength = 1024;
    private int $requestTagLength = 1024;
    private bool $enableMeter = true;
    private array $excludes = [];

    /** @var StardustSpanBuilder[] */
    private array $builders = [];

    private bool $autoShutdown = true;
    private bool $debug = false;

    /**
     * 构造函数
     * 
     * @param string $server 星尘服务器地址，如 http://star.example.com:6600
     * @param string $appId 应用标识
     * @param string $secret 应用密钥
     * @param bool $autoShutdown 是否自动注册关闭函数上报数据，默认 true
     */
    public function __construct(string $server, string $appId, string $secret = '', bool $autoShutdown = true)
    {
        $this->server = rtrim($server, '/');
        $this->appId = $appId;
        $this->appName = $appId;
        $this->secret = $secret;
        $this->clientId = $this->getLocalIP() . '@' . getmypid();
        $this->autoShutdown = $autoShutdown;

        // 注册关闭函数，自动上报
        if ($this->autoShutdown) {
            register_shutdown_function([$this, 'flush']);
        }
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
            'AppName' => $this->appName,
        ];

        $data = $this->postJson($url, $payload);
        if ($data !== null) {
            $this->token = $data['Token'] ?? '';
            $this->tokenExpire = time() + 7200; // 2小时过期
            if (!empty($data['Code'])) $this->appId = $data['Code'];
            if (!empty($data['Secret'])) $this->secret = $data['Secret'];
            
            if ($this->debug) {
                error_log("[StardustTracer] Login success, appId={$this->appId}, token=" . substr($this->token, 0, 20) . "...");
            }
            
            return true;
        }
        
        if ($this->debug) {
            error_log("[StardustTracer] Login failed");
        }
        
        return false;
    }

    /**
     * 心跳保活，刷新令牌
     * 
     * @return bool 是否成功
     */
    public function ping(): bool
    {
        $url = $this->server . '/App/Ping?Token=' . urlencode($this->token);
        $payload = [
            'Id' => getmypid(),
            'Name' => $this->appName,
            'Time' => intval(microtime(true) * 1000),
        ];

        $data = $this->postJson($url, $payload);
        if ($data !== null) {
            if (!empty($data['Token'])) {
                $this->token = $data['Token'];
                $this->tokenExpire = time() + 7200;
            }
            
            if ($this->debug) {
                error_log("[StardustTracer] Ping success");
            }
            
            return true;
        }
        
        if ($this->debug) {
            error_log("[StardustTracer] Ping failed");
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
     * 创建追踪片段
     * 
     * @param string $name 操作名称
     * @param string $parentId 父片段ID
     * @return StardustSpan
     */
    public function newSpan(string $name, string $parentId = ''): StardustSpan
    {
        return new StardustSpan($name, $this, $parentId);
    }

    /**
     * 完成一个 Span（内部方法）
     * 
     * @internal
     * @param string $name 操作名称
     * @param StardustSpan $span 追踪片段
     */
    public function finishSpan(string $name, StardustSpan $span): void
    {
        // 排除自身上报请求
        if ($name === '/Trace/Report' || $name === '/Trace/ReportRaw') return;
        
        // 排除配置的操作
        foreach ($this->excludes as $exc) {
            if (!empty($exc) && stripos($name, $exc) !== false) return;
        }

        // 截断 Tag
        if (mb_strlen($span->tag) > $this->maxTagLength) {
            $span->tag = mb_substr($span->tag, 0, $this->maxTagLength);
        }

        if (!isset($this->builders[$name])) {
            $this->builders[$name] = new StardustSpanBuilder($name, $this->maxSamples, $this->maxErrors);
        }
        $this->builders[$name]->addSpan($span);
    }

    /**
     * 上报数据到监控中心
     * 
     * @return bool 是否成功
     */
    public function flush(): bool
    {
        if (empty($this->builders)) return true;

        $this->ensureToken();

        $buildersData = [];
        foreach ($this->builders as $builder) {
            if ($builder->total > 0) {
                $buildersData[] = $builder->toArray();
            }
        }
        $this->builders = [];

        if (empty($buildersData)) return true;

        $payload = [
            'AppId' => $this->appId,
            'AppName' => $this->appName,
            'ClientId' => $this->clientId,
            'Version' => '1.0.0',
            'Builders' => $buildersData,
        ];

        $body = json_encode($payload, JSON_UNESCAPED_UNICODE);

        // 超过1KB使用gzip压缩
        if (strlen($body) > 1024) {
            $url = $this->server . '/Trace/ReportRaw?Token=' . urlencode($this->token);
            $data = $this->postGzip($url, $body);
        } else {
            $url = $this->server . '/Trace/Report?Token=' . urlencode($this->token);
            $data = $this->postJson($url, $payload);
        }

        if ($data !== null) {
            $this->applyResponse($data);
            
            if ($this->debug) {
                error_log("[StardustTracer] Report success, builders=" . count($buildersData));
            }
            
            return true;
        }
        
        if ($this->debug) {
            error_log("[StardustTracer] Report failed");
        }
        
        return false;
    }

    /**
     * 应用服务器返回的配置
     * 
     * @param array $result
     */
    private function applyResponse(array $result): void
    {
        if (($result['Period'] ?? 0) > 0) $this->period = $result['Period'];
        if (($result['MaxSamples'] ?? 0) > 0) $this->maxSamples = $result['MaxSamples'];
        if (($result['MaxErrors'] ?? 0) > 0) $this->maxErrors = $result['MaxErrors'];
        if (($result['Timeout'] ?? 0) > 0) $this->timeout = $result['Timeout'];
        if (($result['MaxTagLength'] ?? 0) > 0) $this->maxTagLength = $result['MaxTagLength'];
        if (($result['RequestTagLength'] ?? 0) > 0) $this->requestTagLength = $result['RequestTagLength'];
        if (isset($result['EnableMeter'])) $this->enableMeter = $result['EnableMeter'];
        if (!empty($result['Excludes']) && is_array($result['Excludes'])) {
            $this->excludes = $result['Excludes'];
        }
        
        if ($this->debug) {
            error_log("[StardustTracer] Config updated: period={$this->period}, maxSamples={$this->maxSamples}, maxErrors={$this->maxErrors}");
        }
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
                error_log("[StardustTracer] HTTP request failed: url={$url}, code={$httpCode}, error={$error}");
            }
            return null;
        }

        $json = json_decode($response, true);
        if ($json !== null && ($json['code'] ?? -1) === 0) {
            return $json['data'] ?? null;
        }
        
        if ($this->debug) {
            error_log("[StardustTracer] API response error: " . json_encode($json));
        }
        
        return null;
    }

    /**
     * POST GZIP 压缩数据
     * 
     * @param string $url
     * @param string $jsonBody
     * @return array|null
     */
    private function postGzip(string $url, string $jsonBody): ?array
    {
        $compressed = gzencode($jsonBody);

        $ch = curl_init($url);
        curl_setopt_array($ch, [
            CURLOPT_POST => true,
            CURLOPT_POSTFIELDS => $compressed,
            CURLOPT_HTTPHEADER => ['Content-Type: application/x-gzip'],
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
                error_log("[StardustTracer] HTTP request failed: url={$url}, code={$httpCode}, error={$error}");
            }
            return null;
        }

        $json = json_decode($response, true);
        if ($json !== null && ($json['code'] ?? -1) === 0) {
            return $json['data'] ?? null;
        }
        
        if ($this->debug) {
            error_log("[StardustTracer] API response error: " . json_encode($json));
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
