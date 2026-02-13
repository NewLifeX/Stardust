package main

import (
	"fmt"
	"time"

	"github.com/NewLifeX/Stardust/SDK/Go/stardust"
)

func main() {
	// 同时启动 APM 和配置中心
	tracer := stardust.NewTracer("http://localhost:6600", "MyGoApp", "MySecret")
	tracer.Start()
	defer tracer.Stop()

	config := stardust.NewConfigClient("http://localhost:6600", "MyGoApp", "MySecret")
	config.Start()
	defer config.Stop()

	fmt.Println("APM 和配置中心已启动...")

	// 监听配置变更，并记录到 APM
	config.OnChange(func(configs map[string]string) {
		span := tracer.NewSpan("配置更新", "")
		span.Tag = fmt.Sprintf("配置项数量: %d", len(configs))
		span.Finish()

		fmt.Println("\n配置已更新:")
		for k, v := range configs {
			fmt.Printf("  %s = %s\n", k, v)
		}
	})

	// 等待配置加载
	time.Sleep(time.Second * 2)

	// 业务循环：根据配置执行业务逻辑
	for i := 0; i < 10; i++ {
		span := tracer.NewSpan("业务处理", "")

		// 读取配置
		dbHost := config.Get("database.host")
		dbPort := config.Get("database.port")

		span.Tag = fmt.Sprintf("DB: %s:%s, 批次: %d", dbHost, dbPort, i+1)

		// 模拟业务处理
		time.Sleep(time.Second * 2)

		span.Finish()

		fmt.Printf("业务批次 %d 完成\n", i+1)
	}

	fmt.Println("所有业务完成，等待数据上报...")
	time.Sleep(time.Second * 5)
}
