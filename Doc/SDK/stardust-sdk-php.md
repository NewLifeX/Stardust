# 星尘监控 PHP SDK

适用于 PHP 7.4+，提供星尘 APM 监控的接入能力。

## 依赖

- PHP 7.4+
- cURL 扩展（通常默认安装）
- JSON 扩展（通常默认安装）

## 快速开始

```php
require_once 'StardustTracer.php';

$tracer = new StardustTracer('http://star.example.com:6600', 'MyPHPApp', 'MySecret');
$tracer->login();

// 埋点
$span = $tracer->newSpan('业务操作');
$span->tag = '参数信息';
try {
    doSomething();
} catch (Exception $e) {
    $span->setError($e);
} finally {
    $span->finish();
}

// 请求结束时上报
$tracer->flush();
```

## 完整代码

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
