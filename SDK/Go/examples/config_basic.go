package main

import (
	"fmt"
	"time"

	"github.com/NewLifeX/Stardust/SDK/Go/stardust"
)

func main() {
	// 创建配置客户端
	config := stardust.NewConfigClient("http://localhost:6600", "MyGoApp", "MySecret")
	config.Start()
	defer config.Stop()

	fmt.Println("配置中心客户端已启动...")

	// 监听配置变更
	config.OnChange(func(configs map[string]string) {
		fmt.Println("\n=== 配置已更新 ===")
		for k, v := range configs {
			fmt.Printf("  %s = %s\n", k, v)
		}
		fmt.Println("==================\n")
	})

	// 等待配置加载
	time.Sleep(time.Second * 2)

	// 读取配置
	fmt.Println("当前配置:")
	for k, v := range config.GetAll() {
		fmt.Printf("  %s = %s\n", k, v)
	}

	// 读取单个配置
	dbHost := config.Get("database.host")
	dbPort := config.Get("database.port")
	fmt.Printf("\n数据库配置: %s:%s\n", dbHost, dbPort)

	// 持续运行
	fmt.Println("\n按 Ctrl+C 退出...")
	select {}
}
