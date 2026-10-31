using System;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace Gal.Core
{
	/// <summary>
	/// Pdq排序,基于https://gist.github.com/hez2010/6b52929ee1755788c34818972c46aefb的实现
	/// </summary>
	/// <para>author gouanlin</para>
	public class PdqSortHelper
	{
		// Partitions below this size are sorted using insertion sort.
		public const int INSERTION_SORT_THRESHOLD = 24;

		// Partitions above this size use Tukey's ninther to select the pivot.
		public const int NINTHER_THRESHOLD = 128;

		// When we detect an already sorted partition, attempt an insertion sort that allows this
		// amount of element moves before giving up.
		public const int PARTIAL_INSERTION_SORT_LIMIT = 8;

		// Must be multiple of 8 due to loop unrolling, and < 256 to fit in unsigned char.
		public const int BLOCK_SIZE = 64;

		/// <summary>
		/// 插入排序
		/// </summary>
		/// <param name="span"></param>
		/// <param name="begin"></param>
		/// <param name="end"></param>
		/// <param name="comparison"></param>
		/// <typeparam name="T"></typeparam>
		private static void InsertionSort<T>(Span<T> span, int begin, int end, Comparison<T> comparison) {
			for (var i = begin + 1; i < end; i++) {
				var current = i;
				var previous = i - 1;

				// 先进行比较，这样可以避免对已正确定位的元素进行两次移动。
				var t = span[current];
				if (comparison(t, span[previous]) >= 0) continue;
				do {
					span[current] = span[previous];
					current = previous;
					if (current == begin) break;
					--previous;
				} while (comparison(t, span[previous]) < 0);
				span[current] = t;
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
			for (var i = begin + 1; i < end; i++) {
				var current = i;
				var previous = i - 1;

				// 先进行比较，这样可以避免对已正确定位的元素进行两次移动。
				var t = span[current];
				if (comparison(t, span[previous]) >= 0) continue;
				do {
					span[current] = span[previous];
					current = previous;
					--previous;
				} while (comparison(t, span[previous]) < 0);
				span[current] = t;
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
			var limit = 0;
			for (var i = begin + 1; i < end; i++) {
				if (limit > PARTIAL_INSERTION_SORT_LIMIT) return false;

				var current = i;
				var previous = i - 1;

				// 先进行比较，这样可以避免对已正确定位的元素进行两次移动。
				var t = span[current];
				if (comparison(t, span[previous]) < 0) {
					do {
						span[current] = span[previous];
						current = previous;
						if (current == begin) break;
						--previous;
					} while (comparison(t, span[previous]) < 0);
					span[current] = t;
					limit += i - current;
				}
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

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static unsafe void SwapOffsets<T>(Span<T> span, int first, int last, byte* offsetsL, byte* offsetsR, int num, bool useSwaps) {
			if (useSwaps) {
				for (var i = 0; i < num; i++) {
					Swap(ref span[first + offsetsL[i]], ref span[last - offsetsR[i]]);
				}
			} else {
				var l = first + offsetsL[0];
				var r = last - offsetsR[0];
				var tmp = span[l];
				span[l] = span[r];
				for (var i = 1; i < num; i++) {
					l = first + offsetsL[i];
					span[r] = span[l];
					r = last - offsetsR[i];
					span[l] = span[r];
				}
				span[r] = tmp;
			}
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		private static int PartitionRightHoare<T>(Span<T> span, int first, int last, T pivot, Comparison<T> comparison) {
			--first;
			while (true) {
				do { ++first; } while (comparison(span[first], pivot) < 0);
				do { --last; } while (comparison(span[last], pivot) >= 0);
				if (first >= last) break;
				Swap(ref span[first], ref span[last]);
			}
			return first;
		}

		[SkipLocalsInit]
		[MethodImpl(MethodImplOptions.NoInlining)]
		private static unsafe int PartitionRightBranchlessSlow<T>(Span<T> span, int first, int last, T pivot, Comparison<T> comparison) {
			byte* offsetsL = stackalloc byte[BLOCK_SIZE];
			byte* offsetsR = stackalloc byte[BLOCK_SIZE];

			int firstCur = first;
			int lastCur = last;
			int numL = 0, numR = 0, startL = 0, startR = 0;

			while (lastCur - firstCur > 2 * BLOCK_SIZE) {
				if (numL == 0) {
					startL = 0;
					var it = firstCur;
					for (byte i = 0; i < BLOCK_SIZE;) {
						offsetsL[numL] = i++; numL += comparison(span[it], pivot) >= 0 ? 1 : 0; ++it;
						offsetsL[numL] = i++; numL += comparison(span[it], pivot) >= 0 ? 1 : 0; ++it;
						offsetsL[numL] = i++; numL += comparison(span[it], pivot) >= 0 ? 1 : 0; ++it;
						offsetsL[numL] = i++; numL += comparison(span[it], pivot) >= 0 ? 1 : 0; ++it;
						offsetsL[numL] = i++; numL += comparison(span[it], pivot) >= 0 ? 1 : 0; ++it;
						offsetsL[numL] = i++; numL += comparison(span[it], pivot) >= 0 ? 1 : 0; ++it;
						offsetsL[numL] = i++; numL += comparison(span[it], pivot) >= 0 ? 1 : 0; ++it;
						offsetsL[numL] = i++; numL += comparison(span[it], pivot) >= 0 ? 1 : 0; ++it;
					}
					if (numL == 0) {
						firstCur += BLOCK_SIZE;
						return PartitionRightHoare(span, firstCur, lastCur, pivot, comparison);
					}
				}
				if (numR == 0) {
					startR = 0;
					var it = lastCur;
					for (byte i = 0; i < BLOCK_SIZE;) {
						--it; offsetsR[numR] = ++i; numR += comparison(span[it], pivot) < 0 ? 1 : 0;
						--it; offsetsR[numR] = ++i; numR += comparison(span[it], pivot) < 0 ? 1 : 0;
						--it; offsetsR[numR] = ++i; numR += comparison(span[it], pivot) < 0 ? 1 : 0;
						--it; offsetsR[numR] = ++i; numR += comparison(span[it], pivot) < 0 ? 1 : 0;
						--it; offsetsR[numR] = ++i; numR += comparison(span[it], pivot) < 0 ? 1 : 0;
						--it; offsetsR[numR] = ++i; numR += comparison(span[it], pivot) < 0 ? 1 : 0;
						--it; offsetsR[numR] = ++i; numR += comparison(span[it], pivot) < 0 ? 1 : 0;
						--it; offsetsR[numR] = ++i; numR += comparison(span[it], pivot) < 0 ? 1 : 0;
					}
					if (numR == 0) {
						lastCur -= BLOCK_SIZE;
						return PartitionRightHoare(span, firstCur, lastCur, pivot, comparison);
					}
				}

				var bothFull = numL == BLOCK_SIZE && numR == BLOCK_SIZE;
				var num = Math.Min(numL, numR);
				SwapOffsets(span, firstCur, lastCur, offsetsL + startL, offsetsR + startR, num, numL == numR);
				numL -= num;
				numR -= num;
				startL += num;
				startR += num;

				if (numL == 0) firstCur += BLOCK_SIZE;
				if (numR == 0) lastCur -= BLOCK_SIZE;

				if (bothFull) {
					return PartitionRightHoare(span, firstCur, lastCur, pivot, comparison);
				}
			}

			if (numL != 0) {
				var leftBlockEnd = firstCur + BLOCK_SIZE;
				while (numL > 0) {
					--leftBlockEnd;
					Swap(ref span[firstCur + offsetsL[startL + --numL]], ref span[leftBlockEnd]);
				}
				firstCur = leftBlockEnd;
			} else if (numR != 0) {
				var rightBlockBegin = lastCur - BLOCK_SIZE;
				while (numR > 0) {
					Swap(ref span[lastCur - offsetsR[startR + --numR]], ref span[rightBlockBegin]);
					++rightBlockBegin;
				}
				lastCur = rightBlockBegin;
			}

			return PartitionRightHoare(span, firstCur, lastCur, pivot, comparison);
		}

		// Partitions [begin, end) around pivot *begin using comparison function comp. Elements equal
		// to the pivot are put in the right-hand partition. Returns the position of the pivot after
		// partitioning and whether the passed sequence already was correctly partitioned. Assumes the
		// pivot is a median of at least 3 elements and that [begin, end) is at least
		// insertion_sort_threshold long.
		private static int PartitionRight<T>(Span<T> span, int begin, int end, Comparison<T> comparison, bool branchless, out bool hasPartitioned) {
			// Move pivot into local for speed.
			var pivot = span[begin];
			var first = begin;
			var last = end;

			// Find the first element greater than or equal than the pivot (the median of 3 guarantees
			// this exists).
			do { ++first; }
			while (comparison(span[first], pivot) < 0);

			// Find the first element strictly smaller than the pivot. We have to guard this search if
			// there was no element before *first.
			if (first - 1 == begin) {
				while (first < last) {
					--last;
					if (comparison(span[last], pivot) < 0) break;
				}
			} else {
				do { --last; }
				while (comparison(span[last], pivot) >= 0);
			}

			// If the first pair of elements that should be swapped to partition are the same element,
			// the passed in sequence already was correctly partitioned.
			hasPartitioned = first >= last;

			if (!hasPartitioned) {
				Swap(ref span[first], ref span[last]);
				if (branchless && last - first > 2 * BLOCK_SIZE) {
					++first;
					first = PartitionRightBranchlessSlow(span, first, last, pivot, comparison);
				} else {
					while (true) {
						do { ++first; }
						while (comparison(span[first], pivot) < 0);
						do { --last; }
						while (comparison(span[last], pivot) >= 0);
						if (first >= last) break;
						Swap(ref span[first], ref span[last]);
					}
				}
			}

			// Put the pivot in the right place.
			var pivotPosition = first - 1;
			span[begin] = span[pivotPosition];
			span[pivotPosition] = pivot;
			return pivotPosition;
		}

		// Similar function to the one above, except elements equal to the pivot are put to the left of
		// the pivot and it doesn't check or return if the passed sequence already was partitioned.
		// Since this is rarely used (the many equal case), and in that case pdqsort already has O(n)
		// performance, no block quicksort is applied here for simplicity.
		private static int PartitionLeft<T>(Span<T> span, int begin, int end, Comparison<T> comparison) {
			var pivot = span[begin];
			var first = begin;
			var last = end;
			do { --last; }
			while (comparison(pivot, span[last]) < 0);
			if (last + 1 == end) {
				while (first < last) {
					++first;
					if (comparison(pivot, span[first]) < 0) break;
				}
			} else {
				do { ++first; }
				while (comparison(pivot, span[first]) >= 0);
			}
			while (first < last) {
				Swap(ref span[first], ref span[last]);
				do { --last; }
				while (comparison(pivot, span[last]) < 0);
				do { ++first; }
				while (comparison(pivot, span[first]) >= 0);
			}
			var pivotPosition = last;
			span[begin] = span[pivotPosition];
			span[pivotPosition] = pivot;
			return pivotPosition;
		}

		private static void HeapSort<T>(Span<T> span, Comparison<T> comparison) {
			var n = span.Length;
			for (var i = (n >> 1) - 1; i >= 0; i--) Heapify(span, i, n, comparison);
			for (var i = n - 1; i > 0; i--) {
				Swap(ref span[0], ref span[i]);
				Heapify(span, 0, i, comparison);
			}
		}

		private static void Heapify<T>(Span<T> span, int i, int n, Comparison<T> comparison) {
			//当前值
			var current = span[i];
			var half = n >> 1;
			while (i < half) {
				//右子节点索引
				var t = (i << 1) + 1;
				var child = span[t];

				//如果有右子节点,且左子节点小于右子节点,则i置为右子节点的索引
				if (++t < n && comparison(child, span[t]) < 0)
					child = span[t];
				else
					t--;

				//如果当前节点不小于子节点,结束循环
				if (comparison(current, child) >= 0) break;

				//将子节点移动到父节点位置
				span[i] = child;

				//父节点的值在current中,用于后续比较,无需处理

				//父节点指针指向子节点
				i = t;
			}

			//将当前值存回当前节点
			span[i] = current;
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		private static void BreakPatterns<T>(Span<T> span, int begin, int pivot, int end, int leftSize, int rightSize) {
			if (leftSize >= INSERTION_SORT_THRESHOLD) {
				var q = leftSize >> 2;
				Swap(ref span[begin], ref span[begin + q]);
				Swap(ref span[pivot - 1], ref span[pivot - q]);
				if (leftSize > NINTHER_THRESHOLD) {
					Swap(ref span[begin + 1], ref span[begin + q + 1]);
					Swap(ref span[begin + 2], ref span[begin + q + 2]);
					Swap(ref span[pivot - 2], ref span[pivot - (q + 1)]);
					Swap(ref span[pivot - 3], ref span[pivot - (q + 2)]);
				}
			}
			if (rightSize >= INSERTION_SORT_THRESHOLD) {
				var q = rightSize >> 2;
				Swap(ref span[pivot + 1], ref span[pivot + 1 + q]);
				Swap(ref span[end - 1], ref span[end - q]);
				if (rightSize > NINTHER_THRESHOLD) {
					Swap(ref span[pivot + 2], ref span[pivot + 2 + q]);
					Swap(ref span[pivot + 3], ref span[pivot + 3 + q]);
					Swap(ref span[end - 2], ref span[end - (1 + q)]);
					Swap(ref span[end - 3], ref span[end - (2 + q)]);
				}
			}
		}

		private static void Sort<T>(Span<T> span, int begin, int end, Comparison<T> comparison, int badAllowed, bool leftmost = true, bool branchless = true) {
			while (true) {
				var size = end - begin;

				// 对小数组排序,插入排序是最快的
				if (size < INSERTION_SORT_THRESHOLD) {
					if (leftmost) InsertionSort(span, begin, end, comparison);
					else UnguardedInsertionSort(span, begin, end, comparison);
					return;
				}

				// Choose pivot as median of 3 or pseudomedian of 9.
				var mid = size >> 1;
				if (size > NINTHER_THRESHOLD) {
					Sort3(ref span[begin], ref span[begin + mid], ref span[end - 1], comparison);
					Sort3(ref span[begin + 1], ref span[begin + mid - 1], ref span[end - 2], comparison);
					Sort3(ref span[begin + 2], ref span[begin + mid + 1], ref span[end - 3], comparison);
					Sort3(ref span[begin + mid - 1], ref span[begin + mid], ref span[begin + mid + 1], comparison);
					Swap(ref span[begin], ref span[begin + mid]);
				} else {
					Sort3(ref span[begin + mid], ref span[begin], ref span[end - 1], comparison);
				}

				// If *(begin - 1) is the end of the right partition of a previous partition operation
				// there is no element in [begin, end) that is smaller than *(begin - 1). Then if our
				// pivot compares equal to *(begin - 1) we change strategy, putting equal elements in
				// the left partition, greater elements in the right partition. We do not have to
				// recurse on the left partition, since it's sorted (all equal).
				if (!leftmost && comparison(span[begin - 1], span[begin]) >= 0) {
					begin = PartitionLeft(span, begin, end, comparison) + 1;
					continue;
				}
				var pivot = PartitionRight(span, begin, end, comparison, branchless, out bool hasPartitioned);

				// Check for a highly unbalanced partition.
				var leftSize = pivot - begin;
				var rightSize = size - leftSize - 1;
				var highlyUnbalanced = leftSize < (size >> 3) || rightSize < (size >> 3);

				// If we got a highly unbalanced partition we shuffle elements to break many patterns.
				if (highlyUnbalanced) {
					branchless = false;
					// If we had too many bad partitions, switch to heapsort to guarantee O(n log n).
					if (--badAllowed == 0) {
						HeapSort(span.Slice(begin, size), comparison);
						return;
					}
					BreakPatterns(span, begin, pivot, end, leftSize, rightSize);
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
				Sort(span, begin, pivot, comparison, badAllowed, leftmost, branchless);
				begin = pivot + 1;
				leftmost = false;
			}
		}

		public static void Sort<T>(Span<T> span, Comparison<T> comparison) => Sort(span, 0, span.Length, comparison, BitOperations.Log2((uint)span.Length));

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static void Swap<T>(ref T a, ref T b) => (a, b) = (b, a);
	}
}
