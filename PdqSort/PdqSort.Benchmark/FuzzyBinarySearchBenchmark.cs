using BenchmarkDotNet.Attributes;

using Gal.Core;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace Sort.Benchmark
{
	[MemoryDiagnoser()]
	public class FuzzyBinarySearchBenchmark
	{
		[Params(10, 100, 1000, 10000, 100000)]
		public int size;

		private int[] random;
		private int[] random_Asc;
		private int[] random_Des;
		private int[] randomSearch;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		static int comparison_Asc(int a, int b) => a > b ? 1 : a < b ? -1 : 0;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		static int comparison_Des(int a, int b) => a < b ? 1 : a > b ? -1 : 0;

		[GlobalSetup]
		public void Startup() {
			random = GetRandomSequence(size).ToArray();

			random_Asc = new int[size];
			random.CopyTo(random_Asc, 0);

			random_Des = new int[size];
			random.CopyTo(random_Des, 0);

			PdqSortHelper.Sort<int>(random_Asc, comparison_Asc);
			PdqSortHelper.Sort<int>(random_Des, comparison_Des);

			int searchLen = size / 3;
			randomSearch = new int[searchLen + 2];
			randomSearch[0] = random_Asc[0] - 1;
			randomSearch[1] = random_Asc[size - 1] + 1;
			for (int i = 2, l = searchLen + 2; i < l; i++) {
				randomSearch[i] = Random.Shared.Next(randomSearch[0], randomSearch[1]);
			}
		}

		[Benchmark(Baseline = true)]
		public void Search() {
			foreach (var t in randomSearch) Search(t, random_Asc, comparison_Asc);
			foreach (var t in randomSearch) Search(t, random_Des, comparison_Des);
		}

		[Benchmark]
		public void BinarySearch() {
			foreach (var t in randomSearch) FuzzyBinarySearch.Exec(t, random_Asc, comparison_Asc);
			foreach (var t in randomSearch) FuzzyBinarySearch.Exec(t, random_Des, comparison_Des);
		}

		static int Search<T>(T v, IReadOnlyList<T> list, Comparison<T> comparison) {
			if (comparison(v, list[0]) <= 0) return 0;
			if (comparison(v, list[list.Count - 1]) >= 0) return list.Count - 1;
			for (int i = 0; i < list.Count; i++) {
				if (comparison(v, list[i]) <= 0) return i;
			}
			return list.Count - 1;
		}

		static IEnumerable<int> GetRandomSequence(int count) {
			while (count-- > 0) yield return Random.Shared.Next();
		}
	}
}
