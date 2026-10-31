# PdqSort-cs

Pattern-defeating Quicksort (PDQSort) 排序算法的 C# 高性能实现，另有面向有序序列范围检索的模糊二分查找 (`FuzzyBinarySearch`)。
实现参考自 Orson Peters 的 C++ `pdqsort` 以及 [hez2010/pdqsort.cs](https://gist.github.com/hez2010/6b52929ee1755788c34818972c46aefb)，并针对 .NET RyuJIT 进行了深度优化。

## 实现版本

项目在 `Gal.Core` 命名空间下提供三种 `PdqSort` 实现以及 `FuzzyBinarySearch` 工具类：

| 类型 | 适用类型约束 | 说明 |
| --- | --- | --- |
| `PdqSortHelper` | `T` (任意类型) | 基于 `Span<T>` 的安全索引实现，配合栈上偏移缓冲实现自适应 BlockQuicksort |
| `PdqSortHelper_Unguarded` | `T` (任意类型) | 基于 `MemoryMarshal.GetReference` 与 `Unsafe` 托管引用 (`ref T`) 的无边界检查实现 |
| `PdqSortHelper_Unmanaged` | `T : unmanaged` | 基于非托管指针 (`T*`) 的无边界检查实现 |
| `FuzzyBinarySearch` | `T` (任意类型) | 支持 `IReadOnlyList<T>` 与 `ReadOnlySpan<T>` 的模糊二分查找 |

## 核心特性与优化

- **自适应无分支块分区 (Adaptive BlockQuicksort)**：
  - 当元素满足无分支分区条件且分片长度较大时，采用 `64` 元素分块的 `stackalloc byte[64]` 双缓冲无分支偏移扫描与批量循环置换，消除内层循环分支预测失败开销。
  - 当单块扫描命中数全为 `0` 或 `64`（高度偏斜或包含大量重复元素）时，在完成当前剩余偏移压缩后自动无缝切换至 Hoare 双指针分区，兼顾随机数据吞吐量与重复/偏斜模式性能。
- **四分位九数取中 (Quartile Tukey's Ninther)**：
  - 大规模分片 (`size > 128`) 采用四分位点 (`q = size >> 2`) 附近的 9 个采样点计算枢轴，显著提升山峰型 (`Peek`)、逆山峰型 (`ReversePeek`) 及近似有序数据的分割均衡度。
- **模式识别与对抗退化防御**：
  - 自动检测全相等、已排序、逆序等模式并在 $O(N)$ 时间内完成处理；遇到高度不平衡分割时通过 XorShift 打乱模式，最坏情况自动退化为无递归零分配 Floyd 堆排序 (`HeapSort`)，严格保证 $O(N \log N)$ 时间复杂度与 $O(\log N)$ 栈空间复杂度。
- **RyuJIT 深度调优与 100% 测试覆盖**：
  - 消除内层循环边界检查与冗余临时拷贝，使用 `[SkipLocalsInit]` 与冷热路径分离 (`[MethodImpl(MethodImplOptions.NoInlining)]`) 防止大栈帧内联污染热路径寄存器分配。
  - 单元测试 (`PdqSort.Test`) 达到 **100% 行覆盖率与 100% 分支覆盖率**。

## 使用示例

```csharp
using System;
using Gal.Core;

Span<int> data = stackalloc int[] { 9, 3, 7, 1, 5, 8, 2, 6, 4, 0 };

// 1. Span<T> 版本
PdqSortHelper.PdqSort(data, static (a, b) => a.CompareTo(b));

// 2. Unguarded (ref T) 版本
PdqSortHelper_Unguarded.PdqSort(data, static (a, b) => a.CompareTo(b));

// 3. Unmanaged (T*) 版本
PdqSortHelper_Unmanaged.PdqSort(data, static (a, b) => a.CompareTo(b));

// 4. 模糊二分查找 (查找满足范围条件的元素索引，未命中返回 -1)
ReadOnlySpan<int> sorted = data;
int index = FuzzyBinarySearch.Exec(sorted, static x => 5.CompareTo(x));
```

## 基准测试 (Benchmark)

以下为基于 BenchmarkDotNet 在 `.NET 8.0 (X64 RyuJIT AVX2)` 下对 7 种数据分布（全相等 `AllEqual`、随机 `Random`、近似随机 `RandomNear`、升序 `Sequence`、逆序 `Reverse`、山峰 `Peek`、逆山峰 `ReversePeek`）及 6 种数据规模（`10` ~ `1,000,000`）与 `Array.Sort` (`BuildInSort`) 的对比结果：

|                        Method |    size |             Mean |           Error |          StdDev |           Median | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------ |-------- |-----------------:|----------------:|----------------:|-----------------:|------:|--------:|----------:|------------:|
|          BuildInSort_AllEqual |      10 |       1,726.4 ns |       275.17 ns |       703.92 ns |       1,379.0 ns |  1.00 |    0.00 |     824 B |        1.00 |
|         PdqSort_Span_AllEqual |      10 |         256.8 ns |        71.06 ns |       179.91 ns |         172.5 ns |  0.15 |    0.12 |     736 B |        0.89 |
|    PdqSort_Unguarded_AllEqual |      10 |         211.8 ns |        45.61 ns |       113.63 ns |         198.5 ns |  0.12 |    0.08 |     736 B |        0.89 |
|    PdqSort_Unmanaged_AllEqual |      10 |         148.1 ns |        33.98 ns |        83.28 ns |         124.0 ns |  0.09 |    0.06 |     736 B |        0.89 |
|            BuildInSort_Random |      10 |       1,386.8 ns |        85.81 ns |       210.26 ns |       1,335.0 ns |  0.80 |    0.35 |     824 B |        1.00 |
|           PdqSort_Span_Random |      10 |         219.2 ns |        28.19 ns |        69.07 ns |         205.0 ns |  0.13 |    0.07 |     736 B |        0.89 |
|      PdqSort_Unguarded_Random |      10 |         301.7 ns |        41.69 ns |       101.59 ns |         286.5 ns |  0.17 |    0.09 |     736 B |        0.89 |
|      PdqSort_Unmanaged_Random |      10 |         280.0 ns |        56.60 ns |       144.78 ns |         262.5 ns |  0.16 |    0.11 |     736 B |        0.89 |
|        BuildInSort_RandomNear |      10 |       1,326.1 ns |        74.66 ns |       177.74 ns |       1,304.5 ns |  0.77 |    0.33 |     824 B |        1.00 |
|       PdqSort_Span_RandomNear |      10 |         104.6 ns |        20.97 ns |        51.96 ns |          97.0 ns |  0.06 |    0.04 |     736 B |        0.89 |
|  PdqSort_Unguarded_RandomNear |      10 |         245.0 ns |        19.68 ns |        48.51 ns |         245.0 ns |  0.14 |    0.06 |     736 B |        0.89 |
|  PdqSort_Unmanaged_RandomNear |      10 |          65.5 ns |         4.52 ns |         3.78 ns |          65.0 ns |  0.04 |    0.02 |     736 B |        0.89 |
|          BuildInSort_Sequence |      10 |       1,507.0 ns |       132.95 ns |       322.09 ns |       1,407.5 ns |  0.87 |    0.40 |     824 B |        1.00 |
|         PdqSort_Span_Sequence |      10 |         202.1 ns |        15.63 ns |        37.65 ns |         196.5 ns |  0.12 |    0.05 |     736 B |        0.89 |
|    PdqSort_Unguarded_Sequence |      10 |         125.6 ns |        15.54 ns |        37.64 ns |         118.0 ns |  0.07 |    0.04 |     736 B |        0.89 |
|    PdqSort_Unmanaged_Sequence |      10 |         305.0 ns |        56.13 ns |       141.36 ns |         260.5 ns |  0.18 |    0.11 |     736 B |        0.89 |
|           BuildInSort_Reverse |      10 |       1,686.2 ns |       133.82 ns |       322.34 ns |       1,608.0 ns |  0.98 |    0.44 |     824 B |        1.00 |
|          PdqSort_Span_Reverse |      10 |         428.7 ns |        46.31 ns |       115.99 ns |         386.0 ns |  0.25 |    0.12 |     736 B |        0.89 |
|     PdqSort_Unguarded_Reverse |      10 |         552.2 ns |        92.48 ns |       239.01 ns |         491.0 ns |  0.32 |    0.19 |     736 B |        0.89 |
|     PdqSort_Unmanaged_Reverse |      10 |         469.2 ns |        60.12 ns |       152.20 ns |         462.5 ns |  0.27 |    0.14 |     736 B |        0.89 |
|              BuildInSort_Peek |      10 |       1,971.7 ns |       243.79 ns |       600.73 ns |       1,814.5 ns |  1.14 |    0.58 |    1496 B |        1.82 |
|             PdqSort_Span_Peek |      10 |         255.4 ns |        44.02 ns |       112.04 ns |         264.0 ns |  0.15 |    0.09 |     736 B |        0.89 |
|        PdqSort_Unguarded_Peek |      10 |         290.4 ns |        34.62 ns |        87.65 ns |         287.0 ns |  0.17 |    0.09 |     736 B |        0.89 |
|        PdqSort_Unmanaged_Peek |      10 |         374.6 ns |        51.94 ns |       133.54 ns |         380.5 ns |  0.22 |    0.12 |     736 B |        0.89 |
|       BuildInSort_ReversePeek |      10 |       1,715.8 ns |       119.09 ns |       299.91 ns |       1,720.0 ns |  0.99 |    0.44 |     824 B |        1.00 |
|      PdqSort_Span_ReversePeek |      10 |         369.2 ns |        53.28 ns |       136.99 ns |         364.5 ns |  0.21 |    0.12 |     736 B |        0.89 |
| PdqSort_Unguarded_ReversePeek |      10 |         370.2 ns |        44.35 ns |       112.29 ns |         352.0 ns |  0.21 |    0.11 |     736 B |        0.89 |
| PdqSort_Unmanaged_ReversePeek |      10 |         367.0 ns |        43.26 ns |       110.67 ns |         354.5 ns |  0.21 |    0.11 |     736 B |        0.89 |
|                               |         |                  |                 |                 |                  |       |         |           |             |
|          BuildInSort_AllEqual |     100 |       3,426.6 ns |       106.06 ns |       268.50 ns |       3,379.0 ns |  1.00 |    0.00 |     824 B |        1.00 |
|         PdqSort_Span_AllEqual |     100 |       1,070.6 ns |        67.71 ns |       173.20 ns |       1,060.0 ns |  0.31 |    0.06 |     736 B |        0.89 |
|    PdqSort_Unguarded_AllEqual |     100 |         865.5 ns |        64.24 ns |       160.92 ns |         855.5 ns |  0.25 |    0.05 |     736 B |        0.89 |
|    PdqSort_Unmanaged_AllEqual |     100 |         818.4 ns |        43.00 ns |       107.72 ns |         805.0 ns |  0.24 |    0.04 |     736 B |        0.89 |
|            BuildInSort_Random |     100 |       5,818.5 ns |       126.57 ns |       271.11 ns |       5,782.0 ns |  1.70 |    0.15 |     824 B |        1.00 |
|           PdqSort_Span_Random |     100 |       3,458.4 ns |       123.32 ns |       310.56 ns |       3,419.5 ns |  1.01 |    0.12 |     736 B |        0.89 |
|      PdqSort_Unguarded_Random |     100 |       3,462.3 ns |       161.11 ns |       412.15 ns |       3,408.0 ns |  1.01 |    0.14 |     736 B |        0.89 |
|      PdqSort_Unmanaged_Random |     100 |       3,209.8 ns |       111.58 ns |       278.00 ns |       3,201.0 ns |  0.94 |    0.11 |     736 B |        0.89 |
|        BuildInSort_RandomNear |     100 |       4,955.7 ns |       120.87 ns |       306.01 ns |       4,941.0 ns |  1.45 |    0.14 |     824 B |        1.00 |
|       PdqSort_Span_RandomNear |     100 |       2,902.8 ns |       140.93 ns |       358.67 ns |       2,869.5 ns |  0.85 |    0.12 |     736 B |        0.89 |
|  PdqSort_Unguarded_RandomNear |     100 |       2,695.5 ns |       113.98 ns |       283.97 ns |       2,656.5 ns |  0.79 |    0.10 |     736 B |        0.89 |
|  PdqSort_Unmanaged_RandomNear |     100 |       3,148.7 ns |       172.82 ns |       432.90 ns |       3,103.0 ns |  0.92 |    0.15 |     736 B |        0.89 |
|          BuildInSort_Sequence |     100 |       3,124.0 ns |        79.02 ns |       191.44 ns |       3,111.0 ns |  0.91 |    0.09 |     824 B |        1.00 |
|         PdqSort_Span_Sequence |     100 |         599.4 ns |        58.68 ns |       150.89 ns |         616.0 ns |  0.17 |    0.05 |     736 B |        0.89 |
|    PdqSort_Unguarded_Sequence |     100 |         719.6 ns |        32.81 ns |        82.62 ns |         724.5 ns |  0.21 |    0.03 |     736 B |        0.89 |
|    PdqSort_Unmanaged_Sequence |     100 |         810.5 ns |        24.23 ns |        60.03 ns |         804.0 ns |  0.24 |    0.03 |     736 B |        0.89 |
|           BuildInSort_Reverse |     100 |       4,264.0 ns |        98.37 ns |       243.74 ns |       4,243.0 ns |  1.24 |    0.12 |     824 B |        1.00 |
|          PdqSort_Span_Reverse |     100 |       1,014.5 ns |        40.06 ns |       100.34 ns |         993.5 ns |  0.30 |    0.04 |     736 B |        0.89 |
|     PdqSort_Unguarded_Reverse |     100 |       1,051.1 ns |        40.12 ns |        97.77 ns |       1,043.0 ns |  0.31 |    0.04 |     736 B |        0.89 |
|     PdqSort_Unmanaged_Reverse |     100 |       1,181.9 ns |        38.84 ns |        95.18 ns |       1,177.0 ns |  0.34 |    0.04 |     736 B |        0.89 |
|              BuildInSort_Peek |     100 |       6,558.1 ns |       148.08 ns |       314.82 ns |       6,500.5 ns |  1.91 |    0.18 |     824 B |        1.00 |
|             PdqSort_Span_Peek |     100 |       3,446.1 ns |        95.05 ns |       240.63 ns |       3,414.0 ns |  1.01 |    0.11 |     736 B |        0.89 |
|        PdqSort_Unguarded_Peek |     100 |       3,919.5 ns |       174.88 ns |       447.38 ns |       4,010.5 ns |  1.14 |    0.16 |     736 B |        0.89 |
|        PdqSort_Unmanaged_Peek |     100 |       3,808.2 ns |       191.41 ns |       494.67 ns |       3,677.0 ns |  1.11 |    0.17 |     736 B |        0.89 |
|       BuildInSort_ReversePeek |     100 |       7,294.0 ns |       285.46 ns |       730.24 ns |       7,059.5 ns |  2.13 |    0.27 |     824 B |        1.00 |
|      PdqSort_Span_ReversePeek |     100 |       3,518.6 ns |       146.81 ns |       365.75 ns |       3,441.0 ns |  1.03 |    0.13 |     736 B |        0.89 |
| PdqSort_Unguarded_ReversePeek |     100 |       3,647.6 ns |       104.67 ns |       263.59 ns |       3,637.0 ns |  1.06 |    0.11 |     736 B |        0.89 |
| PdqSort_Unmanaged_ReversePeek |     100 |       3,763.8 ns |       155.22 ns |       403.18 ns |       3,829.0 ns |  1.10 |    0.15 |     736 B |        0.89 |
|                               |         |                  |                 |                 |                  |       |         |           |             |
|          BuildInSort_AllEqual |    1000 |      27,059.0 ns |       487.84 ns |       407.35 ns |      26,905.5 ns |  1.00 |    0.00 |     824 B |        1.00 |
|         PdqSort_Span_AllEqual |    1000 |       4,284.3 ns |        98.79 ns |       195.42 ns |       4,265.5 ns |  0.16 |    0.01 |     736 B |        0.89 |
|    PdqSort_Unguarded_AllEqual |    1000 |       4,369.2 ns |        87.32 ns |       126.28 ns |       4,408.0 ns |  0.16 |    0.01 |     736 B |        0.89 |
|    PdqSort_Unmanaged_AllEqual |    1000 |       3,805.9 ns |        73.08 ns |        68.37 ns |       3,796.0 ns |  0.14 |    0.00 |     736 B |        0.89 |
|            BuildInSort_Random |    1000 |      58,984.8 ns |     1,102.79 ns |       920.84 ns |      59,020.0 ns |  2.18 |    0.05 |     824 B |        1.00 |
|           PdqSort_Span_Random |    1000 |      42,070.5 ns |       870.34 ns |     1,630.16 ns |      41,972.0 ns |  1.55 |    0.06 |     736 B |        0.89 |
|      PdqSort_Unguarded_Random |    1000 |      40,249.2 ns |       887.70 ns |     1,662.67 ns |      40,108.5 ns |  1.49 |    0.07 |     736 B |        0.89 |
|      PdqSort_Unmanaged_Random |    1000 |      39,011.1 ns |       793.64 ns |     1,071.14 ns |      38,906.0 ns |  1.44 |    0.05 |     736 B |        0.89 |
|        BuildInSort_RandomNear |    1000 |      53,889.4 ns |       809.48 ns |       675.92 ns |      53,950.5 ns |  1.99 |    0.04 |     824 B |        1.00 |
|       PdqSort_Span_RandomNear |    1000 |      35,429.2 ns |       781.95 ns |     1,464.60 ns |      35,341.0 ns |  1.31 |    0.06 |      64 B |        0.08 |
|  PdqSort_Unguarded_RandomNear |    1000 |      35,427.0 ns |       723.30 ns |     1,246.19 ns |      35,532.0 ns |  1.31 |    0.05 |     736 B |        0.89 |
|  PdqSort_Unmanaged_RandomNear |    1000 |      33,588.6 ns |       676.46 ns |     1,191.69 ns |      33,665.5 ns |  1.24 |    0.05 |     736 B |        0.89 |
|          BuildInSort_Sequence |    1000 |      23,549.8 ns |       164.68 ns |       154.06 ns |      23,523.5 ns |  0.87 |    0.01 |     824 B |        1.00 |
|         PdqSort_Span_Sequence |    1000 |       4,439.5 ns |        99.72 ns |       184.98 ns |       4,475.0 ns |  0.16 |    0.01 |     736 B |        0.89 |
|    PdqSort_Unguarded_Sequence |    1000 |       4,366.1 ns |        90.23 ns |        79.98 ns |       4,355.5 ns |  0.16 |    0.00 |     736 B |        0.89 |
|    PdqSort_Unmanaged_Sequence |    1000 |       3,950.0 ns |        87.24 ns |       134.06 ns |       3,919.0 ns |  0.15 |    0.01 |     736 B |        0.89 |
|           BuildInSort_Reverse |    1000 |      37,685.7 ns |       417.36 ns |       369.96 ns |      37,627.5 ns |  1.39 |    0.03 |     824 B |        1.00 |
|          PdqSort_Span_Reverse |    1000 |       6,753.7 ns |       143.50 ns |       200.72 ns |       6,716.0 ns |  0.25 |    0.01 |     736 B |        0.89 |
|     PdqSort_Unguarded_Reverse |    1000 |       6,771.8 ns |       136.00 ns |       120.56 ns |       6,757.0 ns |  0.25 |    0.01 |     736 B |        0.89 |
|     PdqSort_Unmanaged_Reverse |    1000 |       6,172.8 ns |        97.93 ns |        96.17 ns |       6,152.0 ns |  0.23 |    0.00 |     736 B |        0.89 |
|              BuildInSort_Peek |    1000 |      90,079.9 ns |     3,687.00 ns |     9,528.61 ns |      84,476.5 ns |  3.33 |    0.36 |     824 B |        1.00 |
|             PdqSort_Span_Peek |    1000 |      34,483.0 ns |       660.36 ns |       648.52 ns |      34,447.0 ns |  1.27 |    0.03 |     736 B |        0.89 |
|        PdqSort_Unguarded_Peek |    1000 |      36,056.0 ns |       737.02 ns |       937.83 ns |      36,101.5 ns |  1.33 |    0.04 |     736 B |        0.89 |
|        PdqSort_Unmanaged_Peek |    1000 |      32,725.0 ns |       662.56 ns |       825.33 ns |      32,807.5 ns |  1.21 |    0.04 |     736 B |        0.89 |
|       BuildInSort_ReversePeek |    1000 |      84,217.7 ns |       879.93 ns |       780.01 ns |      84,283.0 ns |  3.11 |    0.06 |     824 B |        1.00 |
|      PdqSort_Span_ReversePeek |    1000 |      36,103.8 ns |       744.69 ns |     1,005.08 ns |      36,148.5 ns |  1.33 |    0.04 |     736 B |        0.89 |
| PdqSort_Unguarded_ReversePeek |    1000 |      35,172.1 ns |       757.33 ns |     1,180.25 ns |      35,208.5 ns |  1.30 |    0.05 |     736 B |        0.89 |
| PdqSort_Unmanaged_ReversePeek |    1000 |      33,809.7 ns |       724.20 ns |     1,144.20 ns |      34,050.5 ns |  1.25 |    0.05 |     736 B |        0.89 |
|                               |         |                  |                 |                 |                  |       |         |           |             |
|          BuildInSort_AllEqual |   10000 |     392,084.7 ns |     8,857.45 ns |    22,066.94 ns |     381,610.5 ns |  1.00 |    0.00 |     824 B |        1.00 |
|         PdqSort_Span_AllEqual |   10000 |      35,700.1 ns |       454.80 ns |       425.46 ns |      35,558.5 ns |  0.09 |    0.01 |     736 B |        0.89 |
|    PdqSort_Unguarded_AllEqual |   10000 |      38,068.0 ns |       811.46 ns |     1,228.98 ns |      38,881.5 ns |  0.10 |    0.01 |     736 B |        0.89 |
|    PdqSort_Unmanaged_AllEqual |   10000 |      33,060.4 ns |       118.30 ns |        98.78 ns |      33,042.0 ns |  0.08 |    0.00 |     736 B |        0.89 |
|            BuildInSort_Random |   10000 |     821,807.0 ns |    17,517.23 ns |    26,530.43 ns |     820,751.0 ns |  2.10 |    0.14 |     824 B |        1.00 |
|           PdqSort_Span_Random |   10000 |     541,832.0 ns |    11,635.53 ns |    21,370.29 ns |     540,000.5 ns |  1.38 |    0.09 |     736 B |        0.89 |
|      PdqSort_Unguarded_Random |   10000 |     499,761.7 ns |    10,557.16 ns |    17,124.54 ns |     507,853.0 ns |  1.27 |    0.08 |     736 B |        0.89 |
|      PdqSort_Unmanaged_Random |   10000 |     451,778.2 ns |     9,859.58 ns |    18,108.51 ns |     451,048.0 ns |  1.15 |    0.08 |     736 B |        0.89 |
|        BuildInSort_RandomNear |   10000 |     802,649.5 ns |    15,378.29 ns |    13,631.91 ns |     803,121.0 ns |  2.05 |    0.12 |     824 B |        1.00 |
|       PdqSort_Span_RandomNear |   10000 |     436,604.2 ns |     9,529.94 ns |    17,677.23 ns |     437,969.0 ns |  1.11 |    0.08 |     736 B |        0.89 |
|  PdqSort_Unguarded_RandomNear |   10000 |     479,230.2 ns |    10,605.16 ns |    22,714.90 ns |     480,140.5 ns |  1.22 |    0.09 |     736 B |        0.89 |
|  PdqSort_Unmanaged_RandomNear |   10000 |     425,422.3 ns |     9,339.87 ns |    18,475.41 ns |     418,419.0 ns |  1.09 |    0.08 |     736 B |        0.89 |
|          BuildInSort_Sequence |   10000 |     335,132.4 ns |     7,373.73 ns |    13,542.89 ns |     326,681.5 ns |  0.85 |    0.06 |     824 B |        1.00 |
|         PdqSort_Span_Sequence |   10000 |      38,564.2 ns |       678.63 ns |       566.66 ns |      38,801.0 ns |  0.10 |    0.01 |     736 B |        0.89 |
|    PdqSort_Unguarded_Sequence |   10000 |      38,869.7 ns |       638.61 ns |       533.25 ns |      38,828.0 ns |  0.10 |    0.01 |     736 B |        0.89 |
|    PdqSort_Unmanaged_Sequence |   10000 |      34,357.2 ns |       724.70 ns |     1,144.98 ns |      34,150.0 ns |  0.09 |    0.01 |     736 B |        0.89 |
|           BuildInSort_Reverse |   10000 |     563,209.2 ns |    11,863.71 ns |    18,230.29 ns |     567,585.0 ns |  1.44 |    0.09 |     824 B |        1.00 |
|          PdqSort_Span_Reverse |   10000 |      55,279.2 ns |       682.69 ns |       570.05 ns |      55,134.0 ns |  0.14 |    0.01 |     736 B |        0.89 |
|     PdqSort_Unguarded_Reverse |   10000 |      60,143.1 ns |       941.29 ns |       834.39 ns |      59,752.0 ns |  0.15 |    0.01 |     736 B |        0.89 |
|     PdqSort_Unmanaged_Reverse |   10000 |      50,675.9 ns |       805.49 ns |       672.59 ns |      50,713.5 ns |  0.13 |    0.01 |     736 B |        0.89 |
|              BuildInSort_Peek |   10000 |   1,441,371.1 ns |    30,639.14 ns |    45,716.48 ns |   1,434,419.0 ns |  3.68 |    0.24 |     824 B |        1.00 |
|             PdqSort_Span_Peek |   10000 |     408,647.0 ns |    10,218.88 ns |    25,870.47 ns |     400,071.0 ns |  1.04 |    0.09 |     736 B |        0.89 |
|        PdqSort_Unguarded_Peek |   10000 |     449,166.8 ns |    26,207.09 ns |    67,729.16 ns |     415,985.5 ns |  1.15 |    0.18 |     736 B |        0.89 |
|        PdqSort_Unmanaged_Peek |   10000 |     369,397.2 ns |     7,986.86 ns |    19,680.53 ns |     365,223.0 ns |  0.94 |    0.07 |     736 B |        0.89 |
|       BuildInSort_ReversePeek |   10000 |   1,386,498.7 ns |    27,301.79 ns |    25,540.91 ns |   1,378,881.0 ns |  3.54 |    0.21 |     824 B |        1.00 |
|      PdqSort_Span_ReversePeek |   10000 |     394,710.8 ns |     8,993.71 ns |    23,125.51 ns |     387,869.5 ns |  1.01 |    0.08 |     736 B |        0.89 |
| PdqSort_Unguarded_ReversePeek |   10000 |     383,004.5 ns |     8,773.56 ns |    22,094.24 ns |     373,890.0 ns |  0.98 |    0.08 |     736 B |        0.89 |
| PdqSort_Unmanaged_ReversePeek |   10000 |     389,496.8 ns |    15,946.31 ns |    41,418.99 ns |     379,641.5 ns |  0.99 |    0.12 |     736 B |        0.89 |
|                               |         |                  |                 |                 |                  |       |         |           |             |
|          BuildInSort_AllEqual |  100000 |   4,503,705.2 ns |    32,673.75 ns |    28,963.27 ns |   4,501,056.5 ns |  1.00 |    0.00 |     824 B |        1.00 |
|         PdqSort_Span_AllEqual |  100000 |     155,309.4 ns |     2,675.97 ns |     4,504.48 ns |     154,503.0 ns |  0.03 |    0.00 |     736 B |        0.89 |
|    PdqSort_Unguarded_AllEqual |  100000 |     143,792.6 ns |     5,310.93 ns |    13,586.14 ns |     138,168.5 ns |  0.03 |    0.00 |     736 B |        0.89 |
|    PdqSort_Unmanaged_AllEqual |  100000 |     138,324.5 ns |     4,693.04 ns |    12,067.19 ns |     134,079.0 ns |  0.03 |    0.00 |     736 B |        0.89 |
|            BuildInSort_Random |  100000 |   9,758,332.0 ns |    37,880.94 ns |    33,579.12 ns |   9,754,863.0 ns |  2.17 |    0.02 |     824 B |        1.00 |
|           PdqSort_Span_Random |  100000 |   5,034,474.4 ns |    72,618.75 ns |    67,935.08 ns |   5,053,319.5 ns |  1.12 |    0.02 |     736 B |        0.89 |
|      PdqSort_Unguarded_Random |  100000 |   4,424,844.1 ns |    89,487.84 ns |   113,869.90 ns |   4,367,162.0 ns |  0.98 |    0.03 |     736 B |        0.89 |
|      PdqSort_Unmanaged_Random |  100000 |   4,461,672.9 ns |    37,058.39 ns |    32,849.99 ns |   4,456,639.5 ns |  0.99 |    0.01 |     736 B |        0.89 |
|        BuildInSort_RandomNear |  100000 |   9,244,392.1 ns |    30,579.07 ns |    27,106.47 ns |   9,238,975.0 ns |  2.05 |    0.01 |     824 B |        1.00 |
|       PdqSort_Span_RandomNear |  100000 |   4,804,270.9 ns |    59,543.64 ns |    49,719.23 ns |   4,794,992.5 ns |  1.07 |    0.01 |     736 B |        0.89 |
|  PdqSort_Unguarded_RandomNear |  100000 |   4,381,173.1 ns |    81,571.20 ns |    76,310.12 ns |   4,377,392.5 ns |  0.97 |    0.02 |     736 B |        0.89 |
|  PdqSort_Unmanaged_RandomNear |  100000 |   4,222,783.0 ns |    19,630.66 ns |    18,364.55 ns |   4,224,981.5 ns |  0.94 |    0.01 |     736 B |        0.89 |
|          BuildInSort_Sequence |  100000 |   4,077,412.4 ns |    21,910.05 ns |    17,105.85 ns |   4,080,955.0 ns |  0.91 |    0.01 |     824 B |        1.00 |
|         PdqSort_Span_Sequence |  100000 |     432,601.3 ns |    11,758.42 ns |    30,234.39 ns |     428,054.0 ns |  0.10 |    0.01 |     736 B |        0.89 |
|    PdqSort_Unguarded_Sequence |  100000 |     379,114.2 ns |     8,134.15 ns |    19,819.51 ns |     374,857.5 ns |  0.08 |    0.00 |     736 B |        0.89 |
|    PdqSort_Unmanaged_Sequence |  100000 |     402,240.9 ns |    11,665.89 ns |    29,843.03 ns |     394,755.5 ns |  0.09 |    0.01 |     736 B |        0.89 |
|           BuildInSort_Reverse |  100000 |   7,336,402.3 ns |    36,757.98 ns |    32,583.69 ns |   7,331,157.0 ns |  1.63 |    0.01 |     824 B |        1.00 |
|          PdqSort_Span_Reverse |  100000 |     534,662.2 ns |    10,222.64 ns |    10,938.46 ns |     533,594.0 ns |  0.12 |    0.00 |     736 B |        0.89 |
|     PdqSort_Unguarded_Reverse |  100000 |     477,382.5 ns |    10,120.36 ns |    16,416.00 ns |     480,037.5 ns |  0.11 |    0.00 |     736 B |        0.89 |
|     PdqSort_Unmanaged_Reverse |  100000 |     506,927.7 ns |    11,470.54 ns |    29,343.30 ns |     499,554.0 ns |  0.11 |    0.01 |     736 B |        0.89 |
|              BuildInSort_Peek |  100000 |  18,508,228.0 ns |    20,605.45 ns |    18,265.46 ns |  18,510,872.0 ns |  4.11 |    0.03 |     824 B |        1.00 |
|             PdqSort_Span_Peek |  100000 |   2,853,022.6 ns |    59,163.12 ns |    85,560.09 ns |   2,853,818.0 ns |  0.63 |    0.02 |     736 B |        0.89 |
|        PdqSort_Unguarded_Peek |  100000 |   2,163,675.7 ns |    44,291.87 ns |    59,778.52 ns |   2,156,014.5 ns |  0.48 |    0.01 |     736 B |        0.89 |
|        PdqSort_Unmanaged_Peek |  100000 |   2,199,756.4 ns |    28,556.72 ns |    25,313.78 ns |   2,199,855.0 ns |  0.49 |    0.01 |     736 B |        0.89 |
|       BuildInSort_ReversePeek |  100000 |  19,278,765.8 ns |   377,583.90 ns |   490,368.70 ns |  19,443,152.5 ns |  4.28 |    0.11 |     824 B |        1.00 |
|      PdqSort_Span_ReversePeek |  100000 |   2,923,688.3 ns |    56,785.76 ns |    53,123.26 ns |   2,927,811.0 ns |  0.65 |    0.01 |     736 B |        0.89 |
| PdqSort_Unguarded_ReversePeek |  100000 |   2,097,258.1 ns |    40,131.65 ns |    35,574.25 ns |   2,107,556.0 ns |  0.47 |    0.01 |     736 B |        0.89 |
| PdqSort_Unmanaged_ReversePeek |  100000 |   2,196,151.9 ns |    34,820.61 ns |    32,574.79 ns |   2,201,548.0 ns |  0.49 |    0.01 |     736 B |        0.89 |
|                               |         |                  |                 |                 |                  |       |         |           |             |
|          BuildInSort_AllEqual | 1000000 |  56,292,635.4 ns |   391,154.69 ns |   346,734.62 ns |  56,249,259.5 ns |  1.00 |    0.00 |     824 B |        1.00 |
|         PdqSort_Span_AllEqual | 1000000 |   1,342,051.1 ns |    30,067.39 ns |    68,973.51 ns |   1,354,347.0 ns |  0.02 |    0.00 |    1408 B |        1.71 |
|    PdqSort_Unguarded_AllEqual | 1000000 |   1,028,968.8 ns |    22,703.05 ns |    47,906.61 ns |   1,029,332.0 ns |  0.02 |    0.00 |     736 B |        0.89 |
|    PdqSort_Unmanaged_AllEqual | 1000000 |   1,151,586.9 ns |    25,125.15 ns |    47,956.21 ns |   1,145,529.5 ns |  0.02 |    0.00 |     736 B |        0.89 |
|            BuildInSort_Random | 1000000 | 117,497,794.4 ns |   427,000.03 ns |   356,547.13 ns | 117,496,818.0 ns |  2.09 |    0.01 |     824 B |        1.00 |
|           PdqSort_Span_Random | 1000000 |  60,732,140.4 ns |   569,804.83 ns |   505,097.00 ns |  60,658,694.0 ns |  1.08 |    0.01 |     736 B |        0.89 |
|      PdqSort_Unguarded_Random | 1000000 |  51,085,512.9 ns |   508,233.39 ns |   450,517.70 ns |  50,941,625.5 ns |  0.91 |    0.01 |     736 B |        0.89 |
|      PdqSort_Unmanaged_Random | 1000000 |  51,320,842.6 ns |   518,035.25 ns |   459,206.45 ns |  51,197,385.5 ns |  0.91 |    0.01 |     736 B |        0.89 |
|        BuildInSort_RandomNear | 1000000 | 116,568,068.8 ns | 2,280,863.19 ns | 2,534,944.45 ns | 116,460,229.0 ns |  2.07 |    0.05 |     824 B |        1.00 |
|       PdqSort_Span_RandomNear | 1000000 |  55,737,656.5 ns |   409,957.11 ns |   383,516.20 ns |  55,542,295.0 ns |  0.99 |    0.01 |     736 B |        0.89 |
|  PdqSort_Unguarded_RandomNear | 1000000 |  51,186,408.7 ns |   311,035.19 ns |   290,974.43 ns |  51,154,388.0 ns |  0.91 |    0.01 |     736 B |        0.89 |
|  PdqSort_Unmanaged_RandomNear | 1000000 |  50,297,277.3 ns |   366,247.03 ns |   324,655.51 ns |  50,136,805.0 ns |  0.89 |    0.01 |     736 B |        0.89 |
|          BuildInSort_Sequence | 1000000 |  49,176,188.1 ns |   407,460.56 ns |   361,188.77 ns |  49,150,400.5 ns |  0.87 |    0.01 |     824 B |        1.00 |
|         PdqSort_Span_Sequence | 1000000 |   2,282,932.2 ns |    51,422.21 ns |   120,947.51 ns |   2,321,495.5 ns |  0.04 |    0.00 |     736 B |        0.89 |
|    PdqSort_Unguarded_Sequence | 1000000 |   1,937,271.4 ns |    42,666.72 ns |    87,962.76 ns |   1,950,057.5 ns |  0.03 |    0.00 |     736 B |        0.89 |
|    PdqSort_Unmanaged_Sequence | 1000000 |   2,279,788.3 ns |    51,163.14 ns |   118,861.57 ns |   2,321,060.0 ns |  0.04 |    0.00 |     736 B |        0.89 |
|           BuildInSort_Reverse | 1000000 |  96,756,286.2 ns | 1,916,468.17 ns | 1,968,069.91 ns |  96,477,599.5 ns |  1.72 |    0.04 |     824 B |        1.00 |
|          PdqSort_Span_Reverse | 1000000 |   2,924,412.4 ns |    71,180.44 ns |   183,026.08 ns |   2,947,346.5 ns |  0.05 |    0.00 |     736 B |        0.89 |
|     PdqSort_Unguarded_Reverse | 1000000 |   2,338,425.5 ns |    53,959.75 ns |   138,036.85 ns |   2,358,399.5 ns |  0.04 |    0.00 |     736 B |        0.89 |
|     PdqSort_Unmanaged_Reverse | 1000000 |   2,307,556.4 ns |    51,572.08 ns |   113,663.28 ns |   2,334,414.0 ns |  0.04 |    0.00 |     736 B |        0.89 |
|              BuildInSort_Peek | 1000000 | 230,191,912.6 ns | 1,578,871.61 ns | 1,399,572.76 ns | 229,837,569.0 ns |  4.09 |    0.04 |     824 B |        1.00 |
|             PdqSort_Span_Peek | 1000000 |  30,190,447.5 ns |   588,724.68 ns |   654,306.82 ns |  30,079,951.5 ns |  0.54 |    0.01 |     736 B |        0.89 |
|        PdqSort_Unguarded_Peek | 1000000 |  22,721,702.4 ns |   423,806.51 ns |   396,472.36 ns |  22,656,058.5 ns |  0.40 |    0.01 |     736 B |        0.89 |
|        PdqSort_Unmanaged_Peek | 1000000 |  23,768,419.0 ns |   375,795.18 ns |   351,557.60 ns |  23,758,911.0 ns |  0.42 |    0.01 |     736 B |        0.89 |
|       BuildInSort_ReversePeek | 1000000 | 240,599,270.4 ns | 2,868,298.74 ns | 2,542,570.75 ns | 240,274,381.0 ns |  4.27 |    0.05 |     824 B |        1.00 |
|      PdqSort_Span_ReversePeek | 1000000 |  30,471,541.8 ns |   607,005.01 ns |   803,929.97 ns |  30,306,805.5 ns |  0.54 |    0.01 |     736 B |        0.89 |
| PdqSort_Unguarded_ReversePeek | 1000000 |  23,263,437.8 ns |   451,192.56 ns |   463,341.12 ns |  23,229,328.0 ns |  0.41 |    0.01 |     736 B |        0.89 |
| PdqSort_Unmanaged_ReversePeek | 1000000 |  25,058,036.2 ns |   495,298.39 ns |   603,416.98 ns |  24,956,272.0 ns |  0.45 |    0.01 |     736 B |        0.89 |
