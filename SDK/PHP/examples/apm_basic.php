<?php
/**
 * 星尘 APM 监控基础使用示例
 * 
 * 演示如何使用 StardustTracer 进行应用性能监控
 */

require_once __DIR__ . '/../src/StardustTracer.php';

// 初始化追踪器
$tracer = new StardustTracer(
    'http://star.newlifex.com:6600',  // 星尘服务器地址
    'MyPHPApp',                        // 应用标识
    ''                                 // 应用密钥（可选）
);

// 开启调试模式（可选）
$tracer->setDebug(true);

// 登录获取令牌
if (!$tracer->login()) {
    die("登录失败\n");
}

echo "登录成功\n";

// ========== 示例1：基本埋点 ==========
echo "\n示例1：基本埋点\n";

$span = $tracer->newSpan('数据库查询');
$span->tag = 'SELECT * FROM users WHERE id = 1';

// 模拟业务操作
usleep(50000); // 50ms

$span->finish();
echo "完成数据库查询埋点\n";

// ========== 示例2：错误处理 ==========
echo "\n示例2：错误处理\n";

$span = $tracer->newSpan('API调用');
$span->tag = 'GET /api/users';

try {
    // 模拟发生错误
    throw new Exception("连接超时");
} catch (Exception $e) {
    $span->setError($e);
    echo "捕获异常: " . $e->getMessage() . "\n";
} finally {
    $span->finish();
}

// ========== 示例3：嵌套追踪 ==========
echo "\n示例3：嵌套追踪\n";

$parentSpan = $tracer->newSpan('处理订单');
$parentSpan->tag = 'orderId=12345';

// 子操作1
$childSpan1 = $tracer->newSpan('验证库存', $parentSpan->id);
usleep(20000); // 20ms
$childSpan1->finish();

// 子操作2
$childSpan2 = $tracer->newSpan('扣减库存', $parentSpan->id);
usleep(30000); // 30ms
$childSpan2->finish();

$parentSpan->finish();
echo "完成嵌套追踪\n";

// ========== 示例4：批量操作 ==========
echo "\n示例4：批量操作\n";

for ($i = 1; $i <= 10; $i++) {
    $span = $tracer->newSpan('批量插入');
    $span->tag = "batch_no={$i}";
    
    usleep(10000); // 10ms
    
    // 模拟部分失败
    if ($i % 3 == 0) {
        $span->setError("批次{$i}失败");
    }
    
    $span->finish();
}
echo "完成批量操作\n";

// ========== 上报数据 ==========
echo "\n上报数据到监控中心...\n";
if ($tracer->flush()) {
    echo "上报成功！\n";
} else {
    echo "上报失败\n";
}

echo "\n示例运行完成\n";
echo "请访问星尘监控平台查看追踪数据\n";
