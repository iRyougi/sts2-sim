// 这个测试程序集里的用例共享 ModelDb 等进程级静态状态（大量用例会 ResetForTests / Init），必须整体串行执行。
//
// 这一行特意放在一个不引用任何其他类型的独立文件里：公开导出（tools/sim_export.py）会按依赖剔除
// 引用了"不随导出提供的类型"的测试文件。它原先写在 Demo/RunReplayDemoTests.cs 里，演示代码移出 Core
// 之后那个文件被整体剔除，公开仓库的测试程序集因此失去这一行，用例并行执行、互相踩踏 ModelDb。
[assembly: CollectionBehavior(DisableTestParallelization = true)]
