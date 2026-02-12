# 星尘监控 PHP SDK

适用于 PHP 7.4+，提供星尘 APM 监控和配置中心的接入能力。

## 功能特性

- ✅ **APM 监控**：应用性能追踪、调用链分析、错误监控
- ✅ **配置中心**：集中配置管理、配置热更新、多环境支持
- ✅ **零依赖**：仅依赖 PHP 内置的 cURL 和 JSON 扩展
- ✅ **轻量级**：核心代码约 500 行，易于集成和维护
- ✅ **易于使用**：简洁的 API，几行代码即可完成接入

## 依赖

- PHP 7.4+
- cURL 扩展（通常默认安装）
- JSON 扩展（通常默认安装）

## SDK 文件位置

SDK 源码位于项目的 `SDK/PHP/` 目录：

```
SDK/PHP/
├── src/
│   ├── StardustTracer.php      # APM 监控核心类
│   └── StardustConfig.php      # 配置中心客户端
├── examples/
│   ├── apm_basic.php           # APM 基础使用示例
│   ├── config_basic.php        # 配置中心使用示例
│   ├── laravel_middleware.php  # Laravel 中间件集成
│   └── swoole_server.php       # Swoole 常驻进程示例
└── README.md                   # 详细文档
```

## 快速开始

### APM 监控

```php
require_once 'SDK/PHP/src/StardustTracer.php';

$tracer = new StardustTracer('http://star.example.com:6600', 'MyPHPApp', 'MySecret');
$tracer->login();

// 创建追踪片段
$span = $tracer->newSpan('业务操作');
$span->tag = '参数信息';
try {
    doSomething();
} catch (Exception $e) {
    $span->setError($e);
} finally {
    $span->finish();
}

// 请求结束时上报（自动调用，也可手动调用）
$tracer->flush();
```

### 配置中心

```php
require_once 'SDK/PHP/src/StardustConfig.php';

// 初始化配置客户端
$config = new StardustConfig('http://star.example.com:6600', 'MyPHPApp', '', 'dev');
$config->login();

// 获取所有配置
$configs = $config->getAll();

// 获取单个配置项
$dbHost = $config->get('database.host', 'localhost');
$dbPort = $config->get('database.port', 3306);

// 检查配置更新
if ($config->hasNewVersion()) {
    echo "有新版本配置等待发布\n";
}
```

## 完整文档

详细使用文档请参考：[SDK/PHP/README.md](../../SDK/PHP/README.md)

内容包括：
- 核心类说明（StardustTracer、StardustConfig）
- 使用场景（PHP-FPM、Laravel、Swoole）
- 高级特性（嵌套追踪、错误处理、自定义标签）
- 性能优化建议
- 故障排查指南

## 示例代码

完整示例请查看 `SDK/PHP/examples/` 目录：

- `apm_basic.php` - APM 基础使用示例
- `config_basic.php` - 配置中心使用示例
- `laravel_middleware.php` - Laravel 中间件集成示例
- `swoole_server.php` - Swoole 常驻进程示例

运行示例：

```bash
# APM 基础示例
php SDK/PHP/examples/apm_basic.php

# 配置中心示例
php SDK/PHP/examples/config_basic.php

# Swoole 服务器示例（需要安装 Swoole 扩展）
php SDK/PHP/examples/swoole_server.php
```

## 核心类简介

### StardustTracer - APM 追踪器

```php
// 构造函数
public function __construct(
    string $server,        // 星尘服务器地址
    string $appId,         // 应用标识
    string $secret = '',   // 应用密钥
    bool $autoShutdown = true  // 是否自动注册关闭函数上报
)

// 主要方法
$tracer->login();              // 登录获取令牌
$tracer->ping();               // 心跳保活
$span = $tracer->newSpan($name); // 创建追踪片段
$tracer->flush();              // 上报数据
$tracer->setDebug(true);       // 开启调试模式
```

### StardustConfig - 配置中心客户端

```php
// 构造函数
public function __construct(
    string $server,     // 星尘服务器地址
    string $appId,      // 应用标识
    string $secret = '',    // 应用密钥
    string $scope = ''      // 作用域（dev/test/prod）
)

// 主要方法
$config->login();                     // 登录获取令牌
$configs = $config->getAll();         // 获取所有配置
$value = $config->get($key, $default); // 获取单个配置项
$version = $config->getVersion();     // 获取当前配置版本
$hasNew = $config->hasNewVersion();   // 检查是否有新版本
$config->setScope('prod');            // 设置作用域
$config->setDebug(true);              // 开启调试模式
```

## 使用场景

### 场景1：PHP-FPM / Apache mod_php

标准的请求-响应模式，每次请求独立：

```php
// 初始化（启用自动关闭函数）
$tracer = new StardustTracer($server, $appId, $secret, true);
$tracer->login();

// 创建追踪
$span = $tracer->newSpan('处理请求');
// ... 业务逻辑
$span->finish();

// 请求结束时自动上报（通过 register_shutdown_function）
```

### 场景2：Laravel / Symfony 框架

通过中间件集成：

```php
class StardustMiddleware
{
    public function handle(Request $request, Closure $next)
    {
        $span = $tracer->newSpan($request->path());
        try {
            return $next($request);
        } finally {
            $span->finish();
            $tracer->flush();
        }
    }
}
```

### 场景3：Swoole / Workerman 常驻进程

需要定时上报和心跳：

```php
// 初始化（禁用自动关闭函数）
$tracer = new StardustTracer($server, $appId, $secret, false);
$tracer->login();

// 定时上报（每60秒）
Swoole\Timer::tick(60000, function () use ($tracer) {
    $tracer->flush();
});

// 定时心跳（每30秒）
Swoole\Timer::tick(30000, function () use ($tracer) {
    $tracer->ping();
});
```

## 附录：完整 SDK 源码

```php
<?php
/**
 * 星尘监控 PHP SDK
 *
 * 由于 PHP 通常为请求-响应模式（非常驻进程），
 * 推荐在每次请求结束时调用 flush() 上报数据，
 * 或使用 register_shutdown_function 自动上报。
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

    public function setError($error): void
    {
        if ($error instanceof Throwable) {
            $this->error = get_class($error) . ': ' . $error->getMessage();
        } else {
            $this->error = (string)$error;
        }
    }

    public function finish(): void
    {
        $this->endTime = intval(microtime(true) * 1000);
        $this->tracer->finishSpan($this->name, $this);
    }

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

class StardustTracer
{
    private string $server;
    private string $appId;
    private string $appName;
    private string $secret;
    private string $clientId;

    private string $token = '';

    // 采样参数
    private int $maxSamples = 1;
    private int $maxErrors = 10;
    private int $maxTagLength = 1024;
    private array $excludes = [];

    /** @var StardustSpanBuilder[] */
    private array $builders = [];

    public function __construct(string $server, string $appId, string $secret = '')
    {
        $this->server = rtrim($server, '/');
        $this->appId = $appId;
        $this->appName = $appId;
        $this->secret = $secret;
        $this->clientId = $this->getLocalIP() . '@' . getmypid();

        // 注册关闭函数，自动上报
        register_shutdown_function([$this, 'flush']);
    }

    /**
     * 登录获取令牌
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
            if (!empty($data['Code'])) $this->appId = $data['Code'];
            if (!empty($data['Secret'])) $this->secret = $data['Secret'];
            return true;
        }
        return false;
    }

    /**
     * 心跳保活
     */
    public function ping(): void
    {
        $url = $this->server . '/App/Ping?Token=' . urlencode($this->token);
        $payload = [
            'Id' => getmypid(),
            'Name' => $this->appName,
            'Time' => intval(microtime(true) * 1000),
        ];

        $data = $this->postJson($url, $payload);
        if ($data !== null && !empty($data['Token'])) {
            $this->token = $data['Token'];
        }
    }

    /**
     * 创建追踪片段
     */
    public function newSpan(string $name, string $parentId = ''): StardustSpan
    {
        return new StardustSpan($name, $this, $parentId);
    }

    /**
     * 完成一个 Span
     * @internal
     */
    public function finishSpan(string $name, StardustSpan $span): void
    {
        // 排除自身
        if ($name === '/Trace/Report' || $name === '/Trace/ReportRaw') return;
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
     * 上报数据
     */
    public function flush(): void
    {
        if (empty($this->builders)) return;

        $buildersData = [];
        foreach ($this->builders as $builder) {
            if ($builder->total > 0) {
                $buildersData[] = $builder->toArray();
            }
        }
        $this->builders = [];

        if (empty($buildersData)) return;

        $payload = [
            'AppId' => $this->appId,
            'AppName' => $this->appName,
            'ClientId' => $this->clientId,
            'Builders' => $buildersData,
        ];

        $body = json_encode($payload, JSON_UNESCAPED_UNICODE);

        if (strlen($body) > 1024) {
            $url = $this->server . '/Trace/ReportRaw?Token=' . urlencode($this->token);
            $data = $this->postGzip($url, $body);
        } else {
            $url = $this->server . '/Trace/Report?Token=' . urlencode($this->token);
            $data = $this->postJson($url, $payload);
        }

        if ($data !== null) {
            $this->applyResponse($data);
        }
    }

    private function applyResponse(array $result): void
    {
        if (($result['MaxSamples'] ?? 0) > 0) $this->maxSamples = $result['MaxSamples'];
        if (($result['MaxErrors'] ?? 0) > 0) $this->maxErrors = $result['MaxErrors'];
        if (($result['MaxTagLength'] ?? 0) > 0) $this->maxTagLength = $result['MaxTagLength'];
        if (!empty($result['Excludes'])) $this->excludes = $result['Excludes'];
    }

    // ========== HTTP 工具 ==========

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
        curl_close($ch);

        if ($response === false || $httpCode >= 400) return null;

        $json = json_decode($response, true);
        if ($json !== null && ($json['code'] ?? -1) === 0) {
            return $json['data'] ?? null;
        }
        return null;
    }

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
        curl_close($ch);

        if ($response === false || $httpCode >= 400) return null;

        $json = json_decode($response, true);
        if ($json !== null && ($json['code'] ?? -1) === 0) {
            return $json['data'] ?? null;
        }
        return null;
    }

    private function getLocalIP(): string
    {
        $hostname = gethostname();
        $ip = gethostbyname($hostname);
        return $ip ?: '127.0.0.1';
    }
}
```

## Laravel 中间件集成

```php
<?php

namespace App\Http\Middleware;

use Closure;
use Illuminate\Http\Request;

class StardustMiddleware
{
    private static ?StardustTracer $tracer = null;

    private static function getTracer(): StardustTracer
    {
        if (self::$tracer === null) {
            self::$tracer = new StardustTracer(
                config('stardust.server', 'http://star.example.com:6600'),
                config('stardust.app_id', 'MyLaravelApp'),
                config('stardust.secret', '')
            );
            self::$tracer->login();
        }
        return self::$tracer;
    }

    public function handle(Request $request, Closure $next)
    {
        $tracer = self::getTracer();
        $name = $request->method() . ' ' . $request->path();
        $span = $tracer->newSpan($name);
        $span->tag = $request->method() . ' ' . $request->fullUrl();

        try {
            $response = $next($request);
            if ($response->getStatusCode() >= 400) {
                $span->setError('HTTP ' . $response->getStatusCode());
            }
            return $response;
        } catch (\Throwable $e) {
            $span->setError($e);
            throw $e;
        } finally {
            $span->finish();
        }
    }
}
```

## 常驻进程模式（Swoole / Workerman）

```php
<?php
// 对于常驻进程框架，需要定时上报

$tracer = new StardustTracer('http://star.example.com:6600', 'MySwooleApp', 'secret');
$tracer->login();

// 定时上报（每60秒）
swoole_timer_tick(60000, function () use ($tracer) {
    $tracer->flush();
});

// 定时心跳（每30秒）
swoole_timer_tick(30000, function () use ($tracer) {
    $tracer->ping();
});
```
