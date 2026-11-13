using Xunit;

// 全量用例共享同一个 SQLite 数据库与 XCode 元数据缓存，并行执行会在首次建库建表时互相竞争
// （实测 NodeHistory 独立库并发创建时报 no such table，节点历史写入随之丢失），
// 且不同用例会读到彼此尚未清理的哨兵数据。统一串行执行，换取稳定可复现的结果。
[assembly: CollectionBehavior(DisableTestParallelization = true)]
