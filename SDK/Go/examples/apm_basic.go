package main

import (
	"fmt"
	"time"

	"github.com/NewLifeX/Stardust/SDK/Go/stardust"
)

func main() {
	// 创建追踪器
	tracer := stardust.NewTracer("http://localhost:6600", "MyGoApp", "MySecret")
	tracer.Start()
	defer tracer.Stop()

	fmt.Println("APM 追踪器已启动，开始模拟业务操作...")

	// 模拟业务操作
	for i := 0; i < 5; i++ {
		// 方式1：手动创建和结束
		span := tracer.NewSpan("业务操作", "")
		span.Tag = fmt.Sprintf("操作编号: %d", i+1)

		// 模拟业务处理
		time.Sleep(time.Millisecond * 100)

		span.Finish()

		// 方式2：使用 defer
		func() {
			span2 := tracer.NewSpan("数据库查询", "")
			span2.Tag = "SELECT * FROM users"
			defer span2.Finish()

			// 模拟数据库查询
			time.Sleep(time.Millisecond * 50)
		}()

		time.Sleep(time.Second)
	}

	fmt.Println("业务操作完成，等待数据上报...")
	time.Sleep(time.Second * 5)
}
