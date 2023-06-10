using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Gal.Core
{
	/// <summary>
	/// Pdq排序,基于https://gist.github.com/hez2010/6b52929ee1755788c34818972c46aefb的实现
	/// </summary>
	/// <para>author gouanlin</para>
	public class PdqSortHelper_Unguarded
	{
		// Partitions below this size are sorted using insertion sort.
		public const int INSERTION_SORT_THRESHOLD = 24;

		// Partitions above this size use Tukey's ninther to select the pivot.
		public const int NINTHER_THRESHOLD = 128;

		// When we detect an already sorted partition, attempt an insertion sort that allows this
		// amount of element moves before giving up.
		public const int PARTIAL_INSERTION_SORT_LIMIT = 8;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static ref T UnguardedAccess<T>(Span<T> span, int index) => ref Unsafe.Add(ref MemoryMarshal.GetReference(span), index);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static Span<T> UnguardedSlice<T>(Span<T> span, int begin, int end) => MemoryMarshal.CreateSpan(ref UnguardedAccess(span, begin), end - begin);

		/// <summary>
		/// 插入排序
		/// </summary>
		/// <param name="span"></param>
		/// <param name="begin"></param>
		/// <param name="end"></param>
		/// <param name="comparison"></param>
		/// <typeparam name="T"></typeparam>
		private static void InsertionSort<T>(Span<T> span, int begin, int end, Comparison<T> comparison) {
			if (begin == end) return;
			for (var i = begin + 1; i < end; i++) {
				var current = i;
				var previous = i - 1;

				// 先进行比较，这样可以避免对已正确定位的元素进行两次移动。
				var t = UnguardedAccess(span, current);
				if (comparison(t, UnguardedAccess(span, previous)) >= 0) continue;
				do {
					UnguardedAccess(span, current--) = UnguardedAccess(span, previous);
				} while (current != begin && comparison(t, UnguardedAccess(span, --previous)) < 0);
				UnguardedAccess(span, current) = t;
			}
		}

		/// <summary>
		/// 不做越界检查的插入排序(假定span[begin-1]的值存在,且小于此分区内的所以元素)
		/// </summary>
		/// <param name="span"></param>
		/// <param name="begin"></param>
		/// <param name="end"></param>
		/// <param name="comparison"></param>
		/// <typeparam name="T"></typeparam>
		private static void UnguardedInsertionSort<T>(Span<T> span, int begin, int end, Comparison<T> comparison) {
			if (begin == end) return;
			for (var i = begin + 1; i < end; i++) {
				var current = i;
				var previous = i - 1;

				// 先进行比较，这样可以避免对已正确定位的元素进行两次移动。
				var t = UnguardedAccess(span, current);
				if (comparison(t, UnguardedAccess(span, previous)) >= 0) continue;
				do {
					UnguardedAccess(span, current--) = UnguardedAccess(span, previous);
				} while (comparison(t, UnguardedAccess(span, --previous)) < 0);
				UnguardedAccess(span, current) = t;
			}
		}

		/// <summary>
		/// 尝试在[begin，end）上使用插入排序。如果超过 PARTIAL_INSERTION_SORT_LIMIT 个元素被移动，并中止排序。否则它会成功排序并返回true。 
		/// </summary>
		/// <param name="span"></param>
		/// <param name="begin"></param>
		/// <param name="end"></param>
		/// <param name="comparison"></param>
		/// <typeparam name="T"></typeparam>
		/// <returns></returns>
		private static bool PartialInsertionSort<T>(Span<T> span, int begin, int end, Comparison<T> comparison) {
			if (begin == end) return true;
			var limit = 0;
			for (var i = begin + 1; i < end; i++) {
				var current = i;
				var previous = i - 1;

				// 先进行比较，这样可以避免对已正确定位的元素进行两次移动。
				var t = UnguardedAccess(span, current);
				if (comparison(t, UnguardedAccess(span, previous)) < 0) {
					do {
						UnguardedAccess(span, current--) = UnguardedAccess(span, previous);
					} while (current != begin && comparison(t, UnguardedAccess(span, --previous)) < 0);
					UnguardedAccess(span, current) = t;
					limit += i - current;
				}
				if (limit > PARTIAL_INSERTION_SORT_LIMIT) return false;
			}
			return true;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static void Sort2<T>(ref T a, ref T b, Comparison<T> comparison) {
			if (comparison(b, a) < 0) Swap(ref a, ref b);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static void Sort3<T>(ref T a, ref T b, ref T c, Comparison<T> comparison) {
			Sort2(ref a, ref b, comparison);
			Sort2(ref b, ref c, comparison);
			Sort2(ref a, ref b, comparison);
		}

		// Partitions [begin, end) around pivot *begin using comparison function comp. Elements equal
		// to the pivot are put in the right-hand partition. Returns the position of the pivot after
		// partitioning and whether the passed sequence already was correctly partitioned. Assumes the
		// pivot is a median of at least 3 elements and that [begin, end) is at least
		// insertion_sort_threshold long.
		private static (int Pivot, bool HasPartitioned) PartitionRight<T>(Span<T> span, int begin, int end, Comparison<T> comparison) {
			// Move pivot into local for speed.
			var pivot = UnguardedAccess(span, begin);
			var first = begin;
			var last = end;

			// Find the first element greater than or equal than the pivot (the median of 3 guarantees
			// this exists).
			while (comparison(UnguardedAccess(span, ++first), pivot) < 0) { }

			// Find the first element strictly smaller than the pivot. We have to guard this search if
			// there was no element before *first.
			if (first - 1 == 0)
				while (first < last && comparison(UnguardedAccess(span, --last), pivot) >= 0) { }
			else
				while (comparison(UnguardedAccess(span, --last), pivot) >= 0) { }

			// If the first pair of elements that should be swapped to partition are the same element,
			// the passed in sequence already was correctly partitioned.
			var hasPartitioned = first >= last;

			// Keep swapping pairs of elements that are on the wrong side of the pivot. Previously
			// swapped pairs guard the searches, which is why the first iteration is special-cased
			// above.
			while (first < last) {
				Swap(ref UnguardedAccess(span, first), ref UnguardedAccess(span, last));
				while (comparison(UnguardedAccess(span, ++first), pivot) < 0) { }
				while (comparison(UnguardedAccess(span, --last), pivot) >= 0) { }
			}

			// Put the pivot in the right place.
			var pivotPosition = first - 1;
			UnguardedAccess(span, begin) = UnguardedAccess(span, pivotPosition);
			UnguardedAccess(span, pivotPosition) = pivot;
			return (pivotPosition, hasPartitioned);
		}

		// Similar function to the one above, except elements equal to the pivot are put to the left of
		// the pivot and it doesn't check or return if the passed sequence already was partitioned.
		// Since this is rarely used (the many equal case), and in that case pdqsort already has O(n)
		// performance, no block quicksort is applied here for simplicity.
		private static int PartitionLeft<T>(Span<T> span, int begin, int end, Comparison<T> comparison) {
			var pivot = UnguardedAccess(span, begin);
			var first = begin;
			var last = end;
			while (comparison(pivot, UnguardedAccess(span, --last)) < 0) { }
			if (last + 1 == end)
				while (first < last && comparison(pivot, UnguardedAccess(span, ++first)) >= 0) { }
			else
				while (comparison(pivot, UnguardedAccess(span, ++first)) >= 0) { }
			while (first < last) {
				Swap(ref UnguardedAccess(span, first), ref UnguardedAccess(span, last));
				while (comparison(pivot, UnguardedAccess(span, --last)) < 0) { }
				while (comparison(pivot, UnguardedAccess(span, ++first)) >= 0) { }
			}
			var pivotPosition = last;
			UnguardedAccess(span, begin) = UnguardedAccess(span, pivotPosition);
			UnguardedAccess(span, pivotPosition) = pivot;
			return pivotPosition;
		}

		private static void HeapSort<T>(Span<T> span, Comparison<T> comparison) {
			if (span.Length == 0) return;
			var n = span.Length;
			for (var i = n >> 1; i >= 1; i--) DownHeap(span, i, n, comparison);
			for (var i = n; i > 1; i--) {
				Swap(ref UnguardedAccess(span, 0), ref UnguardedAccess(span, i - 1));
				DownHeap(span, 1, i - 1, comparison);
			}
		}

		private static void DownHeap<T>(Span<T> span, int i, int n, Comparison<T> comparison) {
			var d = UnguardedAccess(span, i - 1);
			while (i <= n >> 1) {
				var child = 2 * i;
				if (child < n && comparison(UnguardedAccess(span, child - 1), UnguardedAccess(span, child)) < 0) child++;
				if (comparison(d, UnguardedAccess(span, child - 1)) >= 0) break;
				UnguardedAccess(span, i - 1) = UnguardedAccess(span, child - 1);
				i = child;
			}
			UnguardedAccess(span, i - 1) = d;
		}

		private static unsafe void HeapSort1<T>(Span<T> span, Comparison<T> comparison) {
			var n = span.Length;
			if (n == 0) return;
			for (var i = (n >> 1) - 1; i >= 0; i--) Heapify(span, i, n, comparison);
			for (var i = n - 1; i > 0; i--) {
				Swap(ref UnguardedAccess(span, 0), ref UnguardedAccess(span, i));
				Heapify(span, 0, i, comparison);
			}
		}

		private static void Heapify<T>(Span<T> span, int i, int n, Comparison<T> comparison) {
			//当前值
			var current = UnguardedAccess(span, i);
			var half = n >> 1;
			while (i < half) {
				//右子节点索引
				var t = (i << 1) + 1;
				var child = UnguardedAccess(span, t);

				//如果有右子节点,且左子节点小于右子节点,则i置为右子节点的索引
				if (++t < n && comparison(child, UnguardedAccess(span, t)) < 0)
					child = UnguardedAccess(span, t);
				else
					t--;

				//如果当前节点不小于子节点,结束循环
				if ((comparison(current, child) >= 0)) break;

				//将子节点移动到父节点位置
				UnguardedAccess(span, i) = child;

				//父节点的值在current中,用于后续比较,无需处理

				//父节点指针指向子节点
				i = t;
			}

			//将当前值存回当前节点
			UnguardedAccess(span, i) = current;
		}

		private static void Sort<T>(Span<T> span, int begin, int end, Comparison<T> comparison, int badAllowed, bool leftmost = true) {
			while (true) {
				var size = end - begin;

				// 对小数组排序,插入排序是最快的
				if (size < INSERTION_SORT_THRESHOLD) {
					if (leftmost) InsertionSort(span, begin, end, comparison);
					else UnguardedInsertionSort(span, begin, end, comparison);
					return;
				}

				// Choose pivot as median of 3 or pseudomedian of 9.
				var mid = size / 2;
				if (size > NINTHER_THRESHOLD) {
					Sort3(ref UnguardedAccess(span, begin), ref UnguardedAccess(span, begin + mid), ref UnguardedAccess(span, end - 1), comparison);
					Sort3(ref UnguardedAccess(span, begin + 1), ref UnguardedAccess(span, begin + mid - 1), ref UnguardedAccess(span, end - 2), comparison);
					Sort3(ref UnguardedAccess(span, begin + 2), ref UnguardedAccess(span, begin + mid + 1), ref UnguardedAccess(span, end - 3), comparison);
					Sort3(ref UnguardedAccess(span, begin + mid - 1), ref UnguardedAccess(span, begin + mid), ref UnguardedAccess(span, begin + mid + 1), comparison);
					Swap(ref UnguardedAccess(span, begin), ref UnguardedAccess(span, begin + mid));
				} else {
					Sort3(ref UnguardedAccess(span, begin + mid), ref UnguardedAccess(span, begin), ref UnguardedAccess(span, end - 1), comparison);
				}

				// If *(begin - 1) is the end of the right partition of a previous partition operation
				// there is no element in [begin, end) that is smaller than *(begin - 1). Then if our
				// pivot compares equal to *(begin - 1) we change strategy, putting equal elements in
				// the left partition, greater elements in the right partition. We do not have to
				// recurse on the left partition, since it's sorted (all equal).
				if (!leftmost && comparison(UnguardedAccess(span, begin - 1), UnguardedAccess(span, begin)) >= 0) {
					begin = PartitionLeft(span, begin, end, comparison) + 1;
					continue;
				}
				var (pivot, hasPartitioned) = PartitionRight(span, begin, end, comparison);

				// Check for a highly unbalanced partition.
				var leftSize = pivot - begin;
				var rightSize = end - (pivot + 1);
				var highlyUnbalanced = leftSize < size / 8 || rightSize < size / 8;

				// If we got a highly unbalanced partition we shuffle elements to break many patterns.
				if (highlyUnbalanced) {
					// If we had too many bad partitions, switch to heapsort to guarantee O(n log n).
					if (--badAllowed == 0) {
						//HeapSort(UnguardedSlice(span, begin, end), comparison);
						HeapSort1(UnguardedSlice(span, begin, end), comparison);
						return;
					}
					if (leftSize >= INSERTION_SORT_THRESHOLD) {
						Swap(ref UnguardedAccess(span, begin), ref UnguardedAccess(span, begin + leftSize / 4));
						Swap(ref UnguardedAccess(span, pivot - 1), ref UnguardedAccess(span, pivot - leftSize / 4));
						if (leftSize > NINTHER_THRESHOLD) {
							Swap(ref UnguardedAccess(span, begin + 1), ref UnguardedAccess(span, begin + leftSize / 4 + 1));
							Swap(ref UnguardedAccess(span, begin + 2), ref UnguardedAccess(span, begin + leftSize / 4 + 2));
							Swap(ref UnguardedAccess(span, pivot - 2), ref UnguardedAccess(span, pivot - (leftSize / 4 + 1)));
							Swap(ref UnguardedAccess(span, pivot - 3), ref UnguardedAccess(span, pivot - (leftSize / 4 + 2)));
						}
					}
					if (rightSize >= INSERTION_SORT_THRESHOLD) {
						Swap(ref UnguardedAccess(span, pivot + 1), ref UnguardedAccess(span, pivot + 1 + rightSize / 4));
						Swap(ref UnguardedAccess(span, end - 1), ref UnguardedAccess(span, end - rightSize / 4));
						if (rightSize > NINTHER_THRESHOLD) {
							Swap(ref UnguardedAccess(span, pivot + 2), ref UnguardedAccess(span, pivot + 2 + rightSize / 4));
							Swap(ref UnguardedAccess(span, pivot + 3), ref UnguardedAccess(span, pivot + 3 + rightSize / 4));
							Swap(ref UnguardedAccess(span, end - 2), ref UnguardedAccess(span, end - (1 + rightSize / 4)));
							Swap(ref UnguardedAccess(span, end - 3), ref UnguardedAccess(span, end - (2 + rightSize / 4)));
						}
					}
				} else {
					// If we were decently balanced and we tried to sort an already partitioned
					// sequence try to use insertion sort.
					if (hasPartitioned &&
						PartialInsertionSort(span, begin, pivot, comparison) && PartialInsertionSort(span, pivot + 1, end, comparison)) {
						return;
					}
				}

				// Sort the left partition first using recursion and do tail recursion elimination for
				// the right-hand partition.
				Sort(span, begin, pivot, comparison, badAllowed, leftmost);
				begin = pivot + 1;
				leftmost = false;
			}
		}

		public static void Sort<T>(Span<T> span, Comparison<T> comparison) => Sort(span, 0, span.Length, comparison, BitOperations.Log2((uint)span.Length));

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static void Swap<T>(ref T a, ref T b) => (a, b) = (b, a);
	}
}