"""基础使用示例"""

import time
from stardust_sdk import StardustClient


def main():
    # 创建客户端（同时启用 APM 和配置）
    client = StardustClient(
        server="http://localhost:6600",  # 修改为你的星尘服务器地址
        app_id="PythonDemo",
        secret="",  # 首次连接可以为空，服务器会自动注册并返回密钥
        scope="dev",
    )

    # 启动客户端
    print("启动星尘客户端...")
    client.start()

    # 监听配置变更
    def on_config_changed(configs):
        print(f"\n配置已更新，共 {len(configs)} 项:")
        for key, value in configs.items():
            print(f"  {key} = {value}")

    client.on_config_change(on_config_changed)

    # 等待配置加载
    time.sleep(2)

    # 读取配置
    print("\n=== 配置中心示例 ===")
    db_host = client.get_config("database.host", "localhost")
    db_port = client.get_config_int("database.port", 3306)
    debug = client.get_config_bool("debug", False)

    print(f"数据库地址: {db_host}:{db_port}")
    print(f"调试模式: {debug}")
    print(f"当前配置版本: {client.config_version}")

    # APM 监控示例
    print("\n=== APM 监控示例 ===")

    # 使用上下文管理器
    with client.new_span("数据查询") as span:
        span.tag = "查询用户列表"
        print("执行数据查询...")
        time.sleep(0.1)

    # 使用装饰器
    @client.trace("计算任务")
    def complex_calculation():
        print("执行复杂计算...")
        time.sleep(0.2)

    complex_calculation()

    # 模拟错误
    with client.new_span("错误示例") as span:
        span.tag = "这是一个错误测试"
        try:
            raise ValueError("模拟错误")
        except Exception as ex:
            span.set_error(ex)
            print(f"捕获到错误: {ex}")

    print("\n程序运行中，等待 60 秒后退出（期间会上报监控数据）...")
    time.sleep(60)

    # 停止客户端
    print("\n停止星尘客户端...")
    client.stop()
    print("完成！")


if __name__ == "__main__":
    main()
