<?php
/**
 * Laravel 中间件集成示例
 * 
 * 将星尘 APM 监控集成到 Laravel 应用
 * 
 * 使用方法：
 * 1. 将此文件放到 app/Http/Middleware/StardustMiddleware.php
 * 2. 在 app/Http/Kernel.php 的 $middleware 数组中添加此中间件
 * 3. 在 config/stardust.php 中配置服务器地址和应用信息
 */

namespace App\Http\Middleware;

use Closure;
use Illuminate\Http\Request;

// 引入 SDK（根据实际路径调整）
require_once base_path('vendor/stardust/sdk/StardustTracer.php');

class StardustMiddleware
{
    private static ?StardustTracer $tracer = null;

    /**
     * 获取追踪器单例
     * 
     * @return StardustTracer
     */
    private static function getTracer(): StardustTracer
    {
        if (self::$tracer === null) {
            self::$tracer = new StardustTracer(
                config('stardust.server', 'http://star.example.com:6600'),
                config('stardust.app_id', 'MyLaravelApp'),
                config('stardust.secret', ''),
                false // 禁用自动关闭函数，改为手动上报
            );
            
            // 设置调试模式
            if (config('stardust.debug', false)) {
                self::$tracer->setDebug(true);
            }
            
            // 登录
            self::$tracer->login();
        }
        return self::$tracer;
    }

    /**
     * 处理请求
     *
     * @param  \Illuminate\Http\Request  $request
     * @param  \Closure  $next
     * @return mixed
     */
    public function handle(Request $request, Closure $next)
    {
        $tracer = self::getTracer();
        
        // 创建追踪片段
        $name = $request->method() . ' ' . $request->path();
        $span = $tracer->newSpan($name);
        $span->tag = $request->method() . ' ' . $request->fullUrl();

        try {
            $response = $next($request);
            
            // 检查响应状态码
            if ($response->getStatusCode() >= 400) {
                $span->setError('HTTP ' . $response->getStatusCode());
            }
            
            return $response;
        } catch (\Throwable $e) {
            $span->setError($e);
            throw $e;
        } finally {
            $span->finish();
            
            // 每次请求结束时上报
            $tracer->flush();
        }
    }
}
