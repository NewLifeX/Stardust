<?php
/**
 * Swoole 常驻进程模式示例
 * 
 * 演示如何在 Swoole / Workerman 等常驻进程框架中使用星尘监控
 * 
 * 特点：
 * - 进程启动时登录一次
 * - 定时上报数据
 * - 定时心跳保活
 */

require_once __DIR__ . '/../src/StardustTracer.php';

// 初始化追踪器（禁用自动关闭函数）
$tracer = new StardustTracer(
    'http://star.newlifex.com:6600',
    'MySwooleApp',
    '',
    false  // 禁用自动关闭函数
);

$tracer->setDebug(true);

// 登录
if (!$tracer->login()) {
    die("登录失败\n");
}

echo "登录成功，启动 Swoole 服务器...\n";

// 创建 HTTP 服务器
$http = new Swoole\Http\Server("0.0.0.0", 9501);

// 配置
$http->set([
    'worker_num' => 4,
    'daemonize' => false,
]);

// 服务器启动时的回调
$http->on('start', function ($server) use ($tracer) {
    echo "Swoole HTTP Server 已启动: http://0.0.0.0:9501\n";
    
    // 定时上报数据（每60秒）
    Swoole\Timer::tick(60000, function () use ($tracer) {
        echo "[" . date('Y-m-d H:i:s') . "] 定时上报数据\n";
        $tracer->flush();
    });
    
    // 定时心跳（每30秒）
    Swoole\Timer::tick(30000, function () use ($tracer) {
        echo "[" . date('Y-m-d H:i:s') . "] 发送心跳\n";
        $tracer->ping();
    });
});

// 处理请求
$http->on('request', function ($request, $response) use ($tracer) {
    $path = $request->server['request_uri'];
    $method = $request->server['request_method'];
    
    // 创建追踪片段
    $span = $tracer->newSpan("{$method} {$path}");
    $span->tag = "{$method} {$path}";
    
    try {
        // 路由处理
        if ($path === '/') {
            $response->header('Content-Type', 'text/html; charset=utf-8');
            $response->end('<h1>欢迎使用星尘监控</h1><p>这是一个 Swoole 示例</p>');
        } elseif ($path === '/hello') {
            // 模拟业务逻辑
            usleep(50000); // 50ms
            
            $response->header('Content-Type', 'application/json');
            $response->end(json_encode([
                'code' => 0,
                'message' => 'success',
                'data' => 'Hello, Stardust!'
            ]));
        } elseif ($path === '/error') {
            // 模拟错误
            throw new Exception("测试错误");
        } else {
            $response->status(404);
            $response->end('Not Found');
            $span->setError('HTTP 404');
        }
    } catch (Exception $e) {
        $span->setError($e);
        $response->status(500);
        $response->end('Internal Server Error: ' . $e->getMessage());
    } finally {
        $span->finish();
    }
});

// 启动服务器
$http->start();
