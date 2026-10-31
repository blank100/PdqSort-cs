using System;

using Gal.Core;

using Xunit;

namespace Sort.Test
{
	public class SortTest
	{
		[Fact]
		public void PdqIntRandomSort() {
			int[] sizeList = new int[] { 2, 4, 6, 8, 10, 12, 16, 32, 48, 127, 128, 255, 256, 511, 512, 1024, 4096, 8192, 16384, 30000, 40000, 50000, 60000, 70000 };

			for (var i = 0; i < 1000; i++) {
				var c = sizeList[i % sizeList.Length];

				var array = new int[c];
				for (var j = 0; j < c; j++) {
					array[j] = Random.Shared.Next(int.MinValue, int.MaxValue);
				}

				PdqSortHelper_Unmanaged.Sort(array.AsSpan(), (a, b) => a > b ? 1 : a < b ? -1 : 0);

				for (var j = 1; j < c; j++) {
					Assert.True(array[j - 1] <= array[j]);
				}
			}
		}

		[Fact]
		public void Pdq2IntRandomSort() {
			int[] sizeList = new int[] { 2, 4, 6, 8, 10, 12, 16, 32, 48, 127, 128, 255, 256, 511, 512, 1024, 4096, 8192, 16384, 30000, 40000, 50000, 60000, 70000 };

			for (var i = 0; i < 1000; i++) {
				var c = sizeList[i % sizeList.Length];

				var array = new int[c];
				for (var j = 0; j < c; j++) {
					array[j] = Random.Shared.Next(int.MinValue, int.MaxValue);
				}

				PdqSortHelper.Sort(array.AsSpan(), (a, b) => a > b ? 1 : a < b ? -1 : 0);

				for (var j = 1; j < c; j++) {
					Assert.True(array[j - 1] <= array[j]);
				}
			}
		}

		[Fact]
		public void PdqUnguardedIntRandomSort() {
			int[] sizeList = new int[] { 0, 1, 2, 4, 6, 8, 10, 12, 16, 32, 48, 127, 128, 255, 256, 511, 512, 1024, 4096, 8192, 16384, 30000, 40000, 50000, 60000, 70000 };

			for (var i = 0; i < 1000; i++) {
				var c = sizeList[i % sizeList.Length];

				var array = new int[c];
				for (var j = 0; j < c; j++) {
					array[j] = Random.Shared.Next(int.MinValue, int.MaxValue);
				}

				PdqSortHelper_Unguarded.Sort(array.AsSpan(), (a, b) => a > b ? 1 : a < b ? -1 : 0);

				for (var j = 1; j < c; j++) {
					Assert.True(array[j - 1] <= array[j]);
				}
			}
		}

		[Fact]
		public void PdqEdgeAndAdversarialPatternsSort() {
			Comparison<int> cmp = (a, b) => a > b ? 1 : a < b ? -1 : 0;
			int[] sizes = new int[] { 0, 1, 2, 15, 24, 25, 64, 128, 129, 512, 2048, 8192 };

			foreach (var n in sizes) {
				var patterns = new int[][] {
					// All equal
					new int[n],
					// Ascending
					 CreateArray(n, i => i),
					// Descending
					CreateArray(n, i => n - i),
					// Peek (organ-pipe)
					CreateArray(n, i => i < n / 2 ? i : n - i),
					// Reverse peek
					CreateArray(n, i => i < n / 2 ? n / 2 - i : i - n / 2),
					// Few unique values (triggers equal-pivot & right-partition edge cases)
					CreateArray(n, i => (i * 7) % 5),
					// Highly unbalanced / adversarial distribution to trigger HeapSort fallback
					CreateArray(n, i => i % 2 == 0 ? 0 : (i * 31337) % (n + 1))
				};

				foreach (var pat in patterns) {
					var expected = (int[])pat.Clone();
					Array.Sort(expected, cmp);

					var a1 = (int[])pat.Clone();
					PdqSortHelper.Sort(a1.AsSpan(), cmp);
					Assert.Equal(expected, a1);

					var a2 = (int[])pat.Clone();
					PdqSortHelper_Unguarded.Sort(a2.AsSpan(), cmp);
					Assert.Equal(expected, a2);

					var a3 = (int[])pat.Clone();
					PdqSortHelper_Unmanaged.Sort(a3.AsSpan(), cmp);
					Assert.Equal(expected, a3);
				}
			}
		}

		[Fact]
		public void PdqHeapSortFallbackTest() {
			Comparison<int> cmp = (a, b) => a > b ? 1 : a < b ? -1 : 0;

			// Deterministically construct an array that exhausts badAllowed (triggers HeapSort fallback when begin > 0)
			var adversarial = BuildHeapSortTriggerArray(100);
			var expected = (int[])adversarial.Clone();
			Array.Sort(expected, cmp);

			var a1 = (int[])adversarial.Clone();
			PdqSortHelper.Sort(a1.AsSpan(), cmp);
			Assert.Equal(expected, a1);

			var a2 = (int[])adversarial.Clone();
			PdqSortHelper_Unguarded.Sort(a2.AsSpan(), cmp);
			Assert.Equal(expected, a2);

			var a3 = (int[])adversarial.Clone();
			PdqSortHelper_Unmanaged.Sort(a3.AsSpan(), cmp);
			Assert.Equal(expected, a3);
		}

		[Fact]
		public void FuzzyBinarySearchTest() {
			Comparison<int> asc = (a, b) => a > b ? 1 : a < b ? -1 : 0;
			int[] sorted = new int[] { 10, 20, 30, 40, 50, 60, 70, 80 };
			ReadOnlySpan<int> span = sorted;

			// <= min
			Assert.Equal(0, FuzzyBinarySearch.Exec(5, sorted, 0, sorted.Length - 1, asc));
			Assert.Equal(0, FuzzyBinarySearch.Exec(10, sorted, 0, sorted.Length - 1, asc));
			Assert.Equal(0, FuzzyBinarySearch.Exec(5, span, asc));
			Assert.Equal(0, FuzzyBinarySearch.Exec(10, span, asc));

			// >= max
			Assert.Equal(sorted.Length - 1, FuzzyBinarySearch.Exec(80, sorted, 0, sorted.Length - 1, asc));
			Assert.Equal(sorted.Length - 1, FuzzyBinarySearch.Exec(95, sorted, 0, sorted.Length - 1, asc));
			Assert.Equal(sorted.Length - 1, FuzzyBinarySearch.Exec(80, span, asc));
			Assert.Equal(sorted.Length - 1, FuzzyBinarySearch.Exec(95, span, asc));

			// Exact matches and fuzzy in-between values ( covering < 0, > 0, == 0 branches )
			for (var i = 0; i < sorted.Length - 1; i++) {
				var exactVal = sorted[i];
				var betweenVal = sorted[i] + 5;
				Assert.Equal(i, FuzzyBinarySearch.Exec(exactVal, sorted, 0, sorted.Length - 1, asc));
				Assert.Equal(i, FuzzyBinarySearch.Exec(betweenVal, sorted, 0, sorted.Length - 1, asc));
				Assert.Equal(i, FuzzyBinarySearch.Exec(exactVal, span, asc));
				Assert.Equal(i, FuzzyBinarySearch.Exec(betweenVal, span, asc));
			}
		}

		private static int[] BuildHeapSortTriggerArray(int n) {
			// Track original indices through simulated unbalanced partitions where leftSize == 1
			var pos = CreateArray(n, i => i);
			var values = new int[n];
			var assigned = new bool[n];
			int begin = 0, end = n;
			int badAllowed = System.Numerics.BitOperations.Log2((uint)n);
			int nextVal = 1;

			for (var step = 0; step < badAllowed; step++) {
				int size = end - begin;
				int mid = size / 2;
				// Force begin and begin + mid to hold the two smallest remaining values so pivot lands at begin + 1 (leftSize = 1)
				values[pos[begin + mid]] = nextVal++;
				assigned[pos[begin + mid]] = true;
				values[pos[begin]] = nextVal++;
				assigned[pos[begin]] = true;

				// In PartitionRight: first stops at begin + 1, last stops at begin + mid; they swap, then pivot swaps with begin + 1
				(pos[begin + 1], pos[begin + mid]) = (pos[begin + mid], pos[begin + 1]);
				(pos[begin], pos[begin + 1]) = (pos[begin + 1], pos[begin]);
				int pivot = begin + 1;
				int rightSize = end - (pivot + 1);
				if (step + 1 < badAllowed && rightSize >= PdqSortHelper.INSERTION_SORT_THRESHOLD) {
					(pos[pivot + 1], pos[pivot + 1 + rightSize / 4]) = (pos[pivot + 1 + rightSize / 4], pos[pivot + 1]);
					(pos[end - 1], pos[end - rightSize / 4]) = (pos[end - rightSize / 4], pos[end - 1]);
				}
				begin = pivot + 1;
			}

			// Fill remaining unassigned positions with larger values in reverse/scrambled order so HeapSort has full work to do
			int fillVal = nextVal + (n * 2);
			for (var i = 0; i < n; i++) {
				if (!assigned[i]) {
					values[i] = fillVal - ((i * 17) % n);
				}
			}
			return values;
		}

		private static int[] CreateArray(int n, Func<int, int> generator) {
			var arr = new int[n];
			for (var i = 0; i < n; i++) arr[i] = generator(i);
			return arr;
		}
	}
}