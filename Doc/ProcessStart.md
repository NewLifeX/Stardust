Process启动进程研究

## 研究目标

A应用通过Process类启动B应用，研究不同参数设置下的测试结果。



## 测试准备

.Net版本：net8.0

星尘代理：/root/agent

A目录：/root/testA

B目录：/root/testB

跟随退出：随着A应用退出，B应用跟随退出



## 分类测试

根据不同类型的B应用，分类测试。

#### NewLife应用测试

要求B应用必须引入NewLife.Core，它能收到环境变量BasePath并自动调整当前目录。

| 启动目录    | ShellExecute | WorkingDirectory | Environment | 合并输出 | 跟随退出 | 结果 |
| ----------- | :----------: | ---------------- | ----------- | -------- | -------- | ---- |
| /root/testA |    false     |                  |             |          |          |      |
| /root/testA |    false     |                  |             |          |          |      |
| /root/testA |     true     |                  |             |          |          |      |
| /root/testA |     true     |                  |             |          |          |      |



#### Net8应用测试

要求B应用是普通net8应用，禁止引入NewLife.Core。



#### 非托管应用测试

要求B应用必须是非托管应用。

