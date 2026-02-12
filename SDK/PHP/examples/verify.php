<?php
/**
 * PHP SDK 验证脚本
 * 
 * 测试 SDK 的基本功能和结构
 */

echo "=== 星尘 PHP SDK 验证测试 ===\n\n";

// 测试1：加载 StardustTracer
echo "测试1：加载 StardustTracer\n";
require_once __DIR__ . '/../src/StardustTracer.php';

if (class_exists('StardustTracer')) {
    echo "  ✓ StardustTracer 类加载成功\n";
} else {
    echo "  ✗ StardustTracer 类加载失败\n";
    exit(1);
}

if (class_exists('StardustSpan')) {
    echo "  ✓ StardustSpan 类加载成功\n";
} else {
    echo "  ✗ StardustSpan 类加载失败\n";
    exit(1);
}

if (class_exists('StardustSpanBuilder')) {
    echo "  ✓ StardustSpanBuilder 类加载成功\n";
} else {
    echo "  ✗ StardustSpanBuilder 类加载失败\n";
    exit(1);
}

// 测试2：加载 StardustConfig
echo "\n测试2：加载 StardustConfig\n";
require_once __DIR__ . '/../src/StardustConfig.php';

if (class_exists('StardustConfig')) {
    echo "  ✓ StardustConfig 类加载成功\n";
} else {
    echo "  ✗ StardustConfig 类加载失败\n";
    exit(1);
}

// 测试3：创建 StardustTracer 实例
echo "\n测试3：创建 StardustTracer 实例\n";
try {
    $tracer = new StardustTracer('http://test.example.com:6600', 'TestApp', 'secret');
    echo "  ✓ StardustTracer 实例创建成功\n";
    
    // 测试方法是否存在
    $methods = ['login', 'ping', 'newSpan', 'flush', 'setDebug'];
    foreach ($methods as $method) {
        if (method_exists($tracer, $method)) {
            echo "  ✓ 方法 {$method}() 存在\n";
        } else {
            echo "  ✗ 方法 {$method}() 不存在\n";
            exit(1);
        }
    }
} catch (Exception $e) {
    echo "  ✗ 创建实例失败: " . $e->getMessage() . "\n";
    exit(1);
}

// 测试4：创建 StardustConfig 实例
echo "\n测试4：创建 StardustConfig 实例\n";
try {
    $config = new StardustConfig('http://test.example.com:6600', 'TestApp', 'secret', 'dev');
    echo "  ✓ StardustConfig 实例创建成功\n";
    
    // 测试方法是否存在
    $methods = ['login', 'getAll', 'get', 'getVersion', 'hasNewVersion', 'setScope', 'setDebug'];
    foreach ($methods as $method) {
        if (method_exists($config, $method)) {
            echo "  ✓ 方法 {$method}() 存在\n";
        } else {
            echo "  ✗ 方法 {$method}() 不存在\n";
            exit(1);
        }
    }
} catch (Exception $e) {
    echo "  ✗ 创建实例失败: " . $e->getMessage() . "\n";
    exit(1);
}

// 测试5：创建 Span 并完成
echo "\n测试5：创建 Span 并完成\n";
try {
    $span = $tracer->newSpan('测试操作');
    echo "  ✓ Span 创建成功\n";
    
    if (isset($span->id) && !empty($span->id)) {
        echo "  ✓ Span ID 已生成: {$span->id}\n";
    } else {
        echo "  ✗ Span ID 未生成\n";
        exit(1);
    }
    
    $span->tag = 'test tag';
    $span->setError('test error');
    $span->finish();
    echo "  ✓ Span 完成\n";
    
    if ($span->endTime > 0) {
        echo "  ✓ Span 结束时间已记录\n";
    } else {
        echo "  ✗ Span 结束时间未记录\n";
        exit(1);
    }
} catch (Exception $e) {
    echo "  ✗ Span 操作失败: " . $e->getMessage() . "\n";
    exit(1);
}

// 测试6：SpanBuilder
echo "\n测试6：测试 SpanBuilder\n";
try {
    $builder = new StardustSpanBuilder('TestOperation', 5, 10);
    echo "  ✓ SpanBuilder 创建成功\n";
    
    // 添加几个 span
    for ($i = 0; $i < 3; $i++) {
        $testSpan = $tracer->newSpan('op' . $i);
        usleep(10000); // 10ms
        $testSpan->finish();
        $builder->addSpan($testSpan);
    }
    
    if ($builder->total === 3) {
        echo "  ✓ SpanBuilder 统计正确: total={$builder->total}\n";
    } else {
        echo "  ✗ SpanBuilder 统计错误: total={$builder->total}\n";
        exit(1);
    }
    
    $data = $builder->toArray();
    if (isset($data['Name']) && isset($data['Total']) && isset($data['Cost'])) {
        echo "  ✓ SpanBuilder toArray() 成功\n";
    } else {
        echo "  ✗ SpanBuilder toArray() 失败\n";
        exit(1);
    }
} catch (Exception $e) {
    echo "  ✗ SpanBuilder 测试失败: " . $e->getMessage() . "\n";
    exit(1);
}

// 测试7：检查 PHP 版本
echo "\n测试7：检查 PHP 版本\n";
$phpVersion = phpversion();
echo "  当前 PHP 版本: {$phpVersion}\n";
if (version_compare($phpVersion, '7.4.0', '>=')) {
    echo "  ✓ PHP 版本符合要求 (>= 7.4)\n";
} else {
    echo "  ✗ PHP 版本过低，需要 7.4+\n";
    exit(1);
}

// 测试8：检查必需的扩展
echo "\n测试8：检查必需的扩展\n";
$requiredExtensions = ['curl', 'json'];
foreach ($requiredExtensions as $ext) {
    if (extension_loaded($ext)) {
        echo "  ✓ 扩展 {$ext} 已加载\n";
    } else {
        echo "  ✗ 扩展 {$ext} 未加载\n";
        exit(1);
    }
}

echo "\n=== 所有测试通过 ✓ ===\n";
echo "\nPHP SDK 已准备就绪，可以正常使用。\n";
echo "请参考 README.md 和 examples/ 目录中的示例代码。\n";
