#if !DEBUG
using BenchmarkDotNet.Running;
#endif

namespace Sort.Benchmark
{
	public class Program
	{
		public static void Main(string[] args) {
#if DEBUG
			DebugRunner.Run();
#else
			BenchmarkRunner.Run<PdqSortBenchmark>();
#endif
		}
	}
}