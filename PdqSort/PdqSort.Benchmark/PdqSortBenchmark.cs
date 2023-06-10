using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

using BenchmarkDotNet.Attributes;

using Gal.Core;

namespace Sort.Benchmark
{
	[MemoryDiagnoser()]
	public class PdqSortBenchmark
	{
		[Params(10, 100, 1000, 10000, 100000, 1000000)]
		public int size;

		private int[] allEqual, randomNear, random, sequence, reverse, peek, reversePeek;
		private int[] a_allEqual, a_randomNear, a_random, a_sequence, a_reverse, a_peek, a_reversePeek;
		private int[] p1_allEqual, p1_randomNear, p1_random, p1_sequence, p1_reverse, p1_peek, p1_reversePeek;
		private int[] p2_allEqual, p2_randomNear, p2_random, p2_sequence, p2_reverse, p2_peek, p2_reversePeek;
		private int[] p3_allEqual, p3_randomNear, p3_random, p3_sequence, p3_reverse, p3_peek, p3_reversePeek;

		[GlobalSetup]
		public void Startup() {
			a_allEqual = new int[size];
			a_randomNear = new int[size];
			a_random = new int[size];
			a_sequence = new int[size];
			a_reverse = new int[size];
			a_peek = new int[size];
			a_reversePeek = new int[size];

			p1_allEqual = new int[size];
			p1_randomNear = new int[size];
			p1_random = new int[size];
			p1_sequence = new int[size];
			p1_reverse = new int[size];
			p1_peek = new int[size];
			p1_reversePeek = new int[size];

			p2_allEqual = new int[size];
			p2_randomNear = new int[size];
			p2_random = new int[size];
			p2_sequence = new int[size];
			p2_reverse = new int[size];
			p2_peek = new int[size];
			p2_reversePeek = new int[size];

			p3_allEqual = new int[size];
			p3_randomNear = new int[size];
			p3_random = new int[size];
			p3_sequence = new int[size];
			p3_reverse = new int[size];
			p3_peek = new int[size];
			p3_reversePeek = new int[size];

			allEqual = Enumerable.Repeat(1, size).ToArray();
			randomNear = GetRandomSequence(size, true).ToArray();
			random = GetRandomSequence(size, false).ToArray();
			sequence = GetOrderSequence(size).ToArray();
			reverse = GetReverseSequence(size).ToArray();
			peek = GetPeekSequence(size).ToArray();
			reversePeek = GetReversePeekSequence(size).ToArray();
		}

		[IterationSetup]
		public void InitIteration() {
			allEqual.CopyTo(a_allEqual, 0);
			random.CopyTo(a_random, 0);
			randomNear.CopyTo(a_randomNear, 0);
			sequence.CopyTo(a_sequence, 0);
			reverse.CopyTo(a_reverse, 0);
			peek.CopyTo(a_peek, 0);
			reversePeek.CopyTo(a_reversePeek, 0);

			allEqual.CopyTo(p1_allEqual, 0);
			random.CopyTo(p1_random, 0);
			randomNear.CopyTo(p1_randomNear, 0);
			sequence.CopyTo(p1_sequence, 0);
			reverse.CopyTo(p1_reverse, 0);
			peek.CopyTo(p1_peek, 0);
			reversePeek.CopyTo(p1_reversePeek, 0);

			allEqual.CopyTo(p2_allEqual, 0);
			random.CopyTo(p2_random, 0);
			randomNear.CopyTo(p2_randomNear, 0);
			sequence.CopyTo(p2_sequence, 0);
			reverse.CopyTo(p2_reverse, 0);
			peek.CopyTo(p2_peek, 0);
			reversePeek.CopyTo(p2_reversePeek, 0);

			allEqual.CopyTo(p3_allEqual, 0);
			random.CopyTo(p3_random, 0);
			randomNear.CopyTo(p3_randomNear, 0);
			sequence.CopyTo(p3_sequence, 0);
			reverse.CopyTo(p3_reverse, 0);
			peek.CopyTo(p3_peek, 0);
			reversePeek.CopyTo(p3_reversePeek, 0);
		}

		#region allEqual
		[Benchmark(Baseline = true)]
		public void BuildInSort_AllEqual() => Array.Sort(a_allEqual, new MyComparer<int>());

		[Benchmark]
		public void PdqSort_Span_AllEqual() => PdqSortHelper.Sort<int>(p1_allEqual, (a, b) => a > b ? 1 : a < b ? -1 : 0);

		[Benchmark]
		public void PdqSort_Unguarded_AllEqual() => PdqSortHelper_Unguarded.Sort<int>(p2_allEqual, (a, b) => a > b ? 1 : a < b ? -1 : 0);

		[Benchmark]
		public void PdqSort_Unmanaged_AllEqual() => PdqSortHelper_Unmanaged.Sort<int>(p3_allEqual, (a, b) => a > b ? 1 : a < b ? -1 : 0);
		#endregion

		#region random
		[Benchmark()]
		public void BuildInSort_Random() => Array.Sort(a_random, new MyComparer<int>());

		[Benchmark]
		public void PdqSort_Span_Random() => PdqSortHelper.Sort<int>(p1_random, (a, b) => a > b ? 1 : a < b ? -1 : 0);

		[Benchmark]
		public void PdqSort_Unguarded_Random() => PdqSortHelper_Unguarded.Sort<int>(p2_random, (a, b) => a > b ? 1 : a < b ? -1 : 0);

		[Benchmark]
		public void PdqSort_Unmanaged_Random() => PdqSortHelper_Unmanaged.Sort<int>(p3_random, (a, b) => a > b ? 1 : a < b ? -1 : 0);
		#endregion

		#region randomNear
		[Benchmark()]
		public void BuildInSort_RandomNear() => Array.Sort(a_randomNear, new MyComparer<int>());

		[Benchmark]
		public void PdqSort_Span_RandomNear() => PdqSortHelper.Sort<int>(p1_randomNear, (a, b) => a > b ? 1 : a < b ? -1 : 0);

		[Benchmark]
		public void PdqSort_Unguarded_RandomNear() => PdqSortHelper_Unguarded.Sort<int>(p2_randomNear, (a, b) => a > b ? 1 : a < b ? -1 : 0);

		[Benchmark]
		public void PdqSort_Unmanaged_RandomNear() => PdqSortHelper_Unmanaged.Sort<int>(p3_randomNear, (a, b) => a > b ? 1 : a < b ? -1 : 0);
		#endregion

		#region sequence
		[Benchmark()]
		public void BuildInSort_Sequence() => Array.Sort(a_sequence, new MyComparer<int>());

		[Benchmark]
		public void PdqSort_Span_Sequence() => PdqSortHelper.Sort<int>(p1_sequence, (a, b) => a > b ? 1 : a < b ? -1 : 0);

		[Benchmark]
		public void PdqSort_Unguarded_Sequence() => PdqSortHelper_Unguarded.Sort<int>(p2_sequence, (a, b) => a > b ? 1 : a < b ? -1 : 0);

		[Benchmark]
		public void PdqSort_Unmanaged_Sequence() => PdqSortHelper_Unmanaged.Sort<int>(p3_sequence, (a, b) => a > b ? 1 : a < b ? -1 : 0);
		#endregion

		#region reverse
		[Benchmark()]
		public void BuildInSort_Reverse() => Array.Sort(a_reverse, new MyComparer<int>());

		[Benchmark]
		public void PdqSort_Span_Reverse() => PdqSortHelper.Sort<int>(p1_reverse, (a, b) => a > b ? 1 : a < b ? -1 : 0);

		[Benchmark]
		public void PdqSort_Unguarded_Reverse() => PdqSortHelper_Unguarded.Sort<int>(p2_reverse, (a, b) => a > b ? 1 : a < b ? -1 : 0);

		[Benchmark]
		public void PdqSort_Unmanaged_Reverse() => PdqSortHelper_Unmanaged.Sort<int>(p3_reverse, (a, b) => a > b ? 1 : a < b ? -1 : 0);
		#endregion

		#region peek
		[Benchmark()]
		public void BuildInSort_Peek() => Array.Sort(a_peek, new MyComparer<int>());

		[Benchmark]
		public void PdqSort_Span_Peek() => PdqSortHelper.Sort<int>(p1_peek, (a, b) => a > b ? 1 : a < b ? -1 : 0);

		[Benchmark]
		public void PdqSort_Unguarded_Peek() => PdqSortHelper_Unguarded.Sort<int>(p2_peek, (a, b) => a > b ? 1 : a < b ? -1 : 0);

		[Benchmark]
		public void PdqSort_Unmanaged_Peek() => PdqSortHelper_Unmanaged.Sort<int>(p3_peek, (a, b) => a > b ? 1 : a < b ? -1 : 0);
		#endregion

		#region reversePeek
		[Benchmark()]
		public void BuildInSort_ReversePeek() => Array.Sort(a_reversePeek, new MyComparer<int>());

		[Benchmark]
		public void PdqSort_Span_ReversePeek() => PdqSortHelper.Sort<int>(p1_reversePeek, (a, b) => a > b ? 1 : a < b ? -1 : 0);

		[Benchmark]
		public void PdqSort_Unguarded_ReversePeek() => PdqSortHelper_Unguarded.Sort<int>(p2_reversePeek, (a, b) => a > b ? 1 : a < b ? -1 : 0);

		[Benchmark]
		public void PdqSort_Unmanaged_ReversePeek() => PdqSortHelper_Unmanaged.Sort<int>(p3_reversePeek, (a, b) => a > b ? 1 : a < b ? -1 : 0);
		#endregion

		static IEnumerable<int> GetRandomSequence(int count, bool near) {
			while (count-- > 0) yield return near ? Random.Shared.Next(0, count / 4) : Random.Shared.Next();
		}

		static IEnumerable<int> GetReverseSequence(int count) {
			while (count-- > 0) yield return count;
		}

		static IEnumerable<int> GetOrderSequence(int count) {
			var i = 0;
			while (count-- > 0) yield return i++;
		}

		static IEnumerable<int> GetPeekSequence(int count) {
			var mid = count / 2;
			var i = 0;
			while (mid-- > 0) {
				count--;
				yield return i++;
			}

			while (count-- > 0) yield return i--;
		}

		static IEnumerable<int> GetReversePeekSequence(int count) {
			var mid = count / 2;
			while (mid-- > 0) {
				count--;
				yield return mid;
			}

			var i = 0;
			while (count-- > 0) yield return i++;
		}

		struct MyComparer<T> : IComparer<T>
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public int Compare(T? x, T? y) {
				return Comparer<T>.Default.Compare(x, y);
			}
		}
	}
}