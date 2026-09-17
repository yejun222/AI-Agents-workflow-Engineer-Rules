using Xunit;

// 集成测试共用开发态 MySQL / Redis 容器与同一测试库，串行执行避免相互污染（规范 7.1 方案二）。
[assembly: CollectionBehavior(DisableTestParallelization = true)]
