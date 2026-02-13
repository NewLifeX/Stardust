<?php
/**
 * 星尘配置中心使用示例
 * 
 * 演示如何使用 StardustConfig 从配置中心获取配置
 */

require_once __DIR__ . '/../src/StardustConfig.php';

// 初始化配置客户端
$config = new StardustConfig(
    'http://star.newlifex.com:6600',  // 星尘服务器地址
    'MyPHPApp',                        // 应用标识
    '',                                // 应用密钥（可选）
    'dev'                              // 作用域：dev/test/prod
);

// 开启调试模式（可选）
$config->setDebug(true);

// 登录
if (!$config->login()) {
    die("登录失败\n");
}

echo "登录成功\n\n";

// ========== 示例1：获取所有配置 ==========
echo "示例1：获取所有配置\n";
echo str_repeat("-", 50) . "\n";

$configs = $config->getAll();
if ($configs !== null) {
    echo "配置版本: " . $config->getVersion() . "\n";
    echo "更新时间: " . $config->getUpdateTime() . "\n";
    echo "配置数量: " . count($configs) . "\n\n";
    
    echo "配置内容:\n";
    foreach ($configs as $key => $value) {
        echo "  {$key} = {$value}\n";
    }
} else {
    echo "获取配置失败\n";
}

// ========== 示例2：获取单个配置项 ==========
echo "\n示例2：获取单个配置项\n";
echo str_repeat("-", 50) . "\n";

$dbHost = $config->get('database.host', 'localhost');
$dbPort = $config->get('database.port', 3306);
$dbName = $config->get('database.name', 'test');

echo "数据库主机: {$dbHost}\n";
echo "数据库端口: {$dbPort}\n";
echo "数据库名称: {$dbName}\n";

// ========== 示例3：检查配置更新 ==========
echo "\n示例3：检查配置更新\n";
echo str_repeat("-", 50) . "\n";

if ($config->hasNewVersion()) {
    echo "有新版本配置等待发布\n";
    echo "当前版本: " . $config->getVersion() . "\n";
    echo "新版本号: " . $config->getNextVersion() . "\n";
    echo "发布时间: " . $config->getNextPublish() . "\n";
} else {
    echo "配置已是最新版本: " . $config->getVersion() . "\n";
}

// ========== 示例4：配置热更新（轮询模式） ==========
echo "\n示例4：配置热更新（轮询模式）\n";
echo str_repeat("-", 50) . "\n";
echo "模拟每30秒检查一次配置更新...\n";

for ($i = 1; $i <= 3; $i++) {
    echo "\n第{$i}次检查:\n";
    
    $oldVersion = $config->getVersion();
    $configs = $config->getAll();
    $newVersion = $config->getVersion();
    
    if ($configs !== null) {
        if ($newVersion > $oldVersion) {
            echo "  配置已更新: {$oldVersion} -> {$newVersion}\n";
            echo "  重新加载配置...\n";
            // 这里可以重新初始化应用配置
        } else {
            echo "  配置未变化，版本: {$newVersion}\n";
        }
    }
    
    if ($i < 3) {
        echo "  等待30秒...\n";
        sleep(30);
    }
}

echo "\n示例运行完成\n";
