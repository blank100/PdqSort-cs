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

		// Must be multiple of 8 due to loop unrolling, and < 256 to fit in unsigned char.
		public const int BLOCK_SIZE = 64;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static nint ElementCount<T>(ref T begin, ref T end)
			=> (nint)((nuint)Unsafe.ByteOffset(ref begin, ref end) / (nuint)Unsafe.SizeOf<T>());

		/// <summary>
		/// 插入排序
		/// </summary>
		private static void InsertionSort<T>(ref T begin, ref T end, Comparison<T> comparison) {
			for (ref T itor = ref Unsafe.Add(ref begin, 1); Unsafe.IsAddressLessThan(ref itor, ref end); itor = ref Unsafe.Add(ref itor, 1)) {
				ref T current = ref itor;
				ref T previous = ref Unsafe.Subtract(ref itor, 1);

				// 先进行比较，这样可以避免对已正确定位的元素进行两次移动。
				var t = current;
				if (comparison(t, previous) >= 0) continue;
				do {
					current = previous;
					current = ref previous;
					if (Unsafe.AreSame(ref current, ref begin)) break;
					previous = ref Unsafe.Subtract(ref previous, 1);
				} while (comparison(t, previous) < 0);
				current = t;
			}
		}

		/// <summary>
		/// 不做越界检查的插入排序(假定span[begin-1]的值存在,且小于此分区内的所以元素)
		/// </summary>
		private static void UnguardedInsertionSort<T>(ref T begin, ref T end, Comparison<T> comparison) {
			for (ref T itor = ref Unsafe.Add(ref begin, 1); Unsafe.IsAddressLessThan(ref itor, ref end); itor = ref Unsafe.Add(ref itor, 1)) {
				ref T current = ref itor;
				ref T previous = ref Unsafe.Subtract(ref itor, 1);

				// 先进行比较，这样可以避免对已正确定位的元素进行两次移动。
				var t = current;
				if (comparison(t, previous) >= 0) continue;
				do {
					current = previous;
					current = ref previous;
					previous = ref Unsafe.Subtract(ref previous, 1);
				} while (comparison(t, previous) < 0);
				current = t;
			}
		}

		/// <summary>
		/// 尝试在[begin，end）上使用插入排序。如果超过 PARTIAL_INSERTION_SORT_LIMIT 个元素被移动，并中止排序。否则它会成功排序并返回true。 
		/// </summary>
		private static bool PartialInsertionSort<T>(ref T begin, ref T end, Comparison<T> comparison) {
			nint limit = 0;
			nint maxByteLimit = (nint)(PARTIAL_INSERTION_SORT_LIMIT * Unsafe.SizeOf<T>());
			for (ref T itor = ref Unsafe.Add(ref begin, 1); Unsafe.IsAddressLessThan(ref itor, ref end); itor = ref Unsafe.Add(ref itor, 1)) {
				if (limit > maxByteLimit) return false;

				ref T current = ref itor;
				ref T previous = ref Unsafe.Subtract(ref itor, 1);

				// 先进行比较，这样可以避免对已正确定位的元素进行两次移动。
				var t = current;
				if (comparison(t, previous) < 0) {
					do {
						current = previous;
						current = ref previous;
						if (Unsafe.AreSame(ref current, ref begin)) break;
						previous = ref Unsafe.Subtract(ref previous, 1);
					} while (comparison(t, previous) < 0);
					current = t;
					limit += Unsafe.ByteOffset(ref current, ref itor);
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
		private static unsafe void SwapOffsets<T>(ref T first, ref T last, byte* offsetsL, byte* offsetsR, int num, bool useSwaps) {
			if (useSwaps) {
				for (var i = 0; i < num; i++) {
					Swap(ref Unsafe.Add(ref first, offsetsL[i]), ref Unsafe.Subtract(ref last, offsetsR[i]));
				}
			} else {
				ref T l = ref Unsafe.Add(ref first, offsetsL[0]);
				ref T r = ref Unsafe.Subtract(ref last, offsetsR[0]);
				var tmp = l;
				l = r;
				for (var i = 1; i < num; i++) {
					l = ref Unsafe.Add(ref first, offsetsL[i]);
					r = l;
					r = ref Unsafe.Subtract(ref last, offsetsR[i]);
					l = r;
				}
				r = tmp;
			}
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		private static ref T PartitionRightHoare<T>(ref T first, ref T last, T pivot, Comparison<T> comparison) {
			first = ref Unsafe.Subtract(ref first, 1);
			while (true) {
				do { first = ref Unsafe.Add(ref first, 1); }
				while (comparison(first, pivot) < 0);
				do { last = ref Unsafe.Subtract(ref last, 1); }
				while (comparison(last, pivot) >= 0);
				if (!Unsafe.IsAddressLessThan(ref first, ref last)) break;
				Swap(ref first, ref last);
			}
			return ref first;
		}

		[SkipLocalsInit]
		[MethodImpl(MethodImplOptions.NoInlining)]
		private static unsafe ref T PartitionRightBranchlessSlow<T>(ref T first, ref T last, T pivot, Comparison<T> comparison) {
			byte* offsetsL = stackalloc byte[BLOCK_SIZE];
			byte* offsetsR = stackalloc byte[BLOCK_SIZE];

			ref T firstCur = ref first;
			ref T lastCur = ref last;
			int numL = 0, numR = 0, startL = 0, startR = 0;

			nint blockThreshold = (nint)(2 * BLOCK_SIZE * Unsafe.SizeOf<T>());
			while (Unsafe.ByteOffset(ref firstCur, ref lastCur) > blockThreshold) {
				if (numL == 0) {
					startL = 0;
					ref T it = ref firstCur;
					for (byte i = 0; i < BLOCK_SIZE;) {
						offsetsL[numL] = i++; numL += comparison(it, pivot) >= 0 ? 1 : 0; it = ref Unsafe.Add(ref it, 1);
						offsetsL[numL] = i++; numL += comparison(it, pivot) >= 0 ? 1 : 0; it = ref Unsafe.Add(ref it, 1);
						offsetsL[numL] = i++; numL += comparison(it, pivot) >= 0 ? 1 : 0; it = ref Unsafe.Add(ref it, 1);
						offsetsL[numL] = i++; numL += comparison(it, pivot) >= 0 ? 1 : 0; it = ref Unsafe.Add(ref it, 1);
						offsetsL[numL] = i++; numL += comparison(it, pivot) >= 0 ? 1 : 0; it = ref Unsafe.Add(ref it, 1);
						offsetsL[numL] = i++; numL += comparison(it, pivot) >= 0 ? 1 : 0; it = ref Unsafe.Add(ref it, 1);
						offsetsL[numL] = i++; numL += comparison(it, pivot) >= 0 ? 1 : 0; it = ref Unsafe.Add(ref it, 1);
						offsetsL[numL] = i++; numL += comparison(it, pivot) >= 0 ? 1 : 0; it = ref Unsafe.Add(ref it, 1);
					}
					if (numL == 0) {
						firstCur = ref Unsafe.Add(ref firstCur, BLOCK_SIZE);
						return ref PartitionRightHoare(ref firstCur, ref lastCur, pivot, comparison);
					}
				}
				if (numR == 0) {
					startR = 0;
					ref T it = ref lastCur;
					for (byte i = 0; i < BLOCK_SIZE;) {
						it = ref Unsafe.Subtract(ref it, 1); offsetsR[numR] = ++i; numR += comparison(it, pivot) < 0 ? 1 : 0;
						it = ref Unsafe.Subtract(ref it, 1); offsetsR[numR] = ++i; numR += comparison(it, pivot) < 0 ? 1 : 0;
						it = ref Unsafe.Subtract(ref it, 1); offsetsR[numR] = ++i; numR += comparison(it, pivot) < 0 ? 1 : 0;
						it = ref Unsafe.Subtract(ref it, 1); offsetsR[numR] = ++i; numR += comparison(it, pivot) < 0 ? 1 : 0;
						it = ref Unsafe.Subtract(ref it, 1); offsetsR[numR] = ++i; numR += comparison(it, pivot) < 0 ? 1 : 0;
						it = ref Unsafe.Subtract(ref it, 1); offsetsR[numR] = ++i; numR += comparison(it, pivot) < 0 ? 1 : 0;
						it = ref Unsafe.Subtract(ref it, 1); offsetsR[numR] = ++i; numR += comparison(it, pivot) < 0 ? 1 : 0;
						it = ref Unsafe.Subtract(ref it, 1); offsetsR[numR] = ++i; numR += comparison(it, pivot) < 0 ? 1 : 0;
					}
					if (numR == 0) {
						lastCur = ref Unsafe.Subtract(ref lastCur, BLOCK_SIZE);
						return ref PartitionRightHoare(ref firstCur, ref lastCur, pivot, comparison);
					}
				}

				var bothFull = numL == BLOCK_SIZE && numR == BLOCK_SIZE;
				var num = Math.Min(numL, numR);
				SwapOffsets(ref firstCur, ref lastCur, offsetsL + startL, offsetsR + startR, num, numL == numR);
				numL -= num;
				numR -= num;
				startL += num;
				startR += num;

				if (numL == 0) firstCur = ref Unsafe.Add(ref firstCur, BLOCK_SIZE);
				if (numR == 0) lastCur = ref Unsafe.Subtract(ref lastCur, BLOCK_SIZE);

				if (bothFull) {
					return ref PartitionRightHoare(ref firstCur, ref lastCur, pivot, comparison);
				}
			}

			if (numL != 0) {
				ref T leftBlockEnd = ref Unsafe.Add(ref firstCur, BLOCK_SIZE);
				while (numL > 0) {
					leftBlockEnd = ref Unsafe.Subtract(ref leftBlockEnd, 1);
					Swap(ref Unsafe.Add(ref firstCur, offsetsL[startL + --numL]), ref leftBlockEnd);
				}
				firstCur = ref leftBlockEnd;
			} else if (numR != 0) {
				ref T rightBlockBegin = ref Unsafe.Subtract(ref lastCur, BLOCK_SIZE);
				while (numR > 0) {
					Swap(ref Unsafe.Subtract(ref lastCur, offsetsR[startR + --numR]), ref rightBlockBegin);
					rightBlockBegin = ref Unsafe.Add(ref rightBlockBegin, 1);
				}
				lastCur = ref rightBlockBegin;
			}

			return ref PartitionRightHoare(ref firstCur, ref lastCur, pivot, comparison);
		}

		// Partitions [begin, end) around pivot *begin using comparison function comp. Elements equal
		// to the pivot are put in the right-hand partition. Returns the position of the pivot after
		// partitioning and whether the passed sequence already was correctly partitioned. Assumes the
		// pivot is a median of at least 3 elements and that [begin, end) is at least
		// insertion_sort_threshold long.
		private static ref T PartitionRight<T>(ref T begin, ref T end, Comparison<T> comparison, bool branchless, out bool hasPartitioned) {
			// Move pivot into local for speed.
			var pivot = begin;
			ref T first = ref begin;
			ref T last = ref end;

			// Find the first element greater than or equal than the pivot (the median of 3 guarantees
			// this exists).
			do { first = ref Unsafe.Add(ref first, 1); }
			while (comparison(first, pivot) < 0);

			// Find the first element strictly smaller than the pivot. We have to guard this search if
			// there was no element before *first.
			if (Unsafe.AreSame(ref Unsafe.Subtract(ref first, 1), ref begin)) {
				while (Unsafe.IsAddressLessThan(ref first, ref last)) {
					last = ref Unsafe.Subtract(ref last, 1);
					if (comparison(last, pivot) < 0) break;
				}
			} else {
				do { last = ref Unsafe.Subtract(ref last, 1); }
				while (comparison(last, pivot) >= 0);
			}

			// If the first pair of elements that should be swapped to partition are the same element,
			// the passed in sequence already was correctly partitioned.
			hasPartitioned = !Unsafe.IsAddressLessThan(ref first, ref last);

			if (!hasPartitioned) {
				Swap(ref first, ref last);
				if (branchless && Unsafe.ByteOffset(ref first, ref last) > (nint)(2 * BLOCK_SIZE * Unsafe.SizeOf<T>())) {
					first = ref Unsafe.Add(ref first, 1);
					first = ref PartitionRightBranchlessSlow(ref first, ref last, pivot, comparison);
				} else {
					while (true) {
						do { first = ref Unsafe.Add(ref first, 1); }
						while (comparison(first, pivot) < 0);
						do { last = ref Unsafe.Subtract(ref last, 1); }
						while (comparison(last, pivot) >= 0);
						if (!Unsafe.IsAddressLessThan(ref first, ref last)) break;
						Swap(ref first, ref last);
					}
				}
			}

			// Put the pivot in the right place.
			ref T pivotPosition = ref Unsafe.Subtract(ref first, 1);
			begin = pivotPosition;
			pivotPosition = pivot;
			return ref pivotPosition;
		}

		// Similar function to the one above, except elements equal to the pivot are put to the left of
		// the pivot and it doesn't check or return if the passed sequence already was partitioned.
		// Since this is rarely used (the many equal case), and in that case pdqsort already has O(n)
		// performance, no block quicksort is applied here for simplicity.
		private static ref T PartitionLeft<T>(ref T begin, ref T end, Comparison<T> comparison) {
			var pivot = begin;
			ref T first = ref begin;
			ref T last = ref end;

			do { last = ref Unsafe.Subtract(ref last, 1); }
			while (comparison(pivot, last) < 0);

			if (Unsafe.AreSame(ref Unsafe.Add(ref last, 1), ref end)) {
				while (Unsafe.IsAddressLessThan(ref first, ref last)) {
					first = ref Unsafe.Add(ref first, 1);
					if (comparison(pivot, first) < 0) break;
				}
			} else {
				do { first = ref Unsafe.Add(ref first, 1); }
				while (comparison(pivot, first) >= 0);
			}

			while (Unsafe.IsAddressLessThan(ref first, ref last)) {
				Swap(ref first, ref last);
				do { last = ref Unsafe.Subtract(ref last, 1); }
				while (comparison(pivot, last) < 0);
				do { first = ref Unsafe.Add(ref first, 1); }
				while (comparison(pivot, first) >= 0);
			}

			begin = last;
			last = pivot;
			return ref last;
		}

		private static void HeapSort<T>(ref T begin, nint n, Comparison<T> comparison) {
			for (nint i = (n >> 1) - 1; i >= 0; i--) Heapify(ref begin, i, n, comparison);
			for (nint i = n - 1; i > 0; i--) {
				Swap(ref begin, ref Unsafe.Add(ref begin, i));
				Heapify(ref begin, 0, i, comparison);
			}
		}

		private static void Heapify<T>(ref T begin, nint i, nint n, Comparison<T> comparison) {
			//当前值
			var current = Unsafe.Add(ref begin, i);
			nint half = n >> 1;
			while (i < half) {
				//右子节点索引
				nint t = (i << 1) + 1;
				var child = Unsafe.Add(ref begin, t);

				//如果有右子节点,且左子节点小于右子节点,则i置为右子节点的索引
				if (++t < n && comparison(child, Unsafe.Add(ref begin, t)) < 0)
					child = Unsafe.Add(ref begin, t);
				else
					t--;

				//如果当前节点不小于子节点,结束循环
				if (comparison(current, child) >= 0) break;

				//将子节点移动到父节点位置
				Unsafe.Add(ref begin, i) = child;

				//父节点的值在current中,用于后续比较,无需处理

				//父节点指针指向子节点
				i = t;
			}

			//将当前值存回当前节点
			Unsafe.Add(ref begin, i) = current;
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		private static void BreakPatterns<T>(ref T begin, ref T pivot, ref T end, nint leftSize, nint rightSize) {
			if (leftSize >= INSERTION_SORT_THRESHOLD) {
				nint q = leftSize >> 2;
				Swap(ref begin, ref Unsafe.Add(ref begin, q));
				Swap(ref Unsafe.Subtract(ref pivot, 1), ref Unsafe.Subtract(ref pivot, q));
				if (leftSize > NINTHER_THRESHOLD) {
					Swap(ref Unsafe.Add(ref begin, 1), ref Unsafe.Add(ref begin, q + 1));
					Swap(ref Unsafe.Add(ref begin, 2), ref Unsafe.Add(ref begin, q + 2));
					Swap(ref Unsafe.Subtract(ref pivot, 2), ref Unsafe.Subtract(ref pivot, q + 1));
					Swap(ref Unsafe.Subtract(ref pivot, 3), ref Unsafe.Subtract(ref pivot, q + 2));
				}
			}
			if (rightSize >= INSERTION_SORT_THRESHOLD) {
				nint q = rightSize >> 2;
				Swap(ref Unsafe.Add(ref pivot, 1), ref Unsafe.Add(ref pivot, 1 + q));
				Swap(ref Unsafe.Subtract(ref end, 1), ref Unsafe.Subtract(ref end, q));
				if (rightSize > NINTHER_THRESHOLD) {
					Swap(ref Unsafe.Add(ref pivot, 2), ref Unsafe.Add(ref pivot, 2 + q));
					Swap(ref Unsafe.Add(ref pivot, 3), ref Unsafe.Add(ref pivot, 3 + q));
					Swap(ref Unsafe.Subtract(ref end, 2), ref Unsafe.Subtract(ref end, 1 + q));
					Swap(ref Unsafe.Subtract(ref end, 3), ref Unsafe.Subtract(ref end, 2 + q));
				}
			}
		}

		private static void Sort<T>(ref T begin, ref T end, Comparison<T> comparison, int badAllowed, bool leftmost = true, bool branchless = true) {
			while (true) {
				nint size = ElementCount(ref begin, ref end);

				// 对小数组排序,插入排序是最快的
				if (size < INSERTION_SORT_THRESHOLD) {
					if (leftmost) InsertionSort(ref begin, ref end, comparison);
					else UnguardedInsertionSort(ref begin, ref end, comparison);
					return;
				}

				// Choose pivot as median of 3 or pseudomedian of 9.
				nint mid = size >> 1;
				if (size > NINTHER_THRESHOLD) {
					Sort3(ref begin, ref Unsafe.Add(ref begin, mid), ref Unsafe.Subtract(ref end, 1), comparison);
					Sort3(ref Unsafe.Add(ref begin, 1), ref Unsafe.Add(ref begin, mid - 1), ref Unsafe.Subtract(ref end, 2), comparison);
					Sort3(ref Unsafe.Add(ref begin, 2), ref Unsafe.Add(ref begin, mid + 1), ref Unsafe.Subtract(ref end, 3), comparison);
					Sort3(ref Unsafe.Add(ref begin, mid - 1), ref Unsafe.Add(ref begin, mid), ref Unsafe.Add(ref begin, mid + 1), comparison);
					Swap(ref begin, ref Unsafe.Add(ref begin, mid));
				} else {
					Sort3(ref Unsafe.Add(ref begin, mid), ref begin, ref Unsafe.Subtract(ref end, 1), comparison);
				}

				// If *(begin - 1) is the end of the right partition of a previous partition operation
				// there is no element in [begin, end) that is smaller than *(begin - 1). Then if our
				// pivot compares equal to *(begin - 1) we change strategy, putting equal elements in
				// the left partition, greater elements in the right partition. We do not have to
				// recurse on the left partition, since it's sorted (all equal).
				if (!leftmost && comparison(Unsafe.Subtract(ref begin, 1), begin) >= 0) {
					begin = ref Unsafe.Add(ref PartitionLeft(ref begin, ref end, comparison), 1);
					continue;
				}

				ref T pivot = ref PartitionRight(ref begin, ref end, comparison, branchless, out bool hasPartitioned);

				// Check for a highly unbalanced partition.
				nint leftSize = ElementCount(ref begin, ref pivot);
				nint rightSize = size - leftSize - 1;
				var highlyUnbalanced = leftSize < (size >> 3) || rightSize < (size >> 3);

				// If we got a highly unbalanced partition we shuffle elements to break many patterns.
				if (highlyUnbalanced) {
					branchless = false;
					// If we had too many bad partitions, switch to heapsort to guarantee O(n log n).
					if (--badAllowed == 0) {
						HeapSort(ref begin, size, comparison);
						return;
					}
					BreakPatterns(ref begin, ref pivot, ref end, leftSize, rightSize);
				} else {
					// If we were decently balanced and we tried to sort an already partitioned
					// sequence try to use insertion sort.
					if (hasPartitioned &&
						PartialInsertionSort(ref begin, ref pivot, comparison) &&
						PartialInsertionSort(ref Unsafe.Add(ref pivot, 1), ref end, comparison)) {
						return;
					}
				}

				// Sort the left partition first using recursion and do tail recursion elimination for
				// the right-hand partition.
				Sort(ref begin, ref pivot, comparison, badAllowed, leftmost, branchless);
				begin = ref Unsafe.Add(ref pivot, 1);
				leftmost = false;
			}
		}

		public static void Sort<T>(Span<T> span, Comparison<T> comparison) {
			ref T begin = ref MemoryMarshal.GetReference(span);
			Sort(ref begin, ref Unsafe.Add(ref begin, span.Length), comparison, BitOperations.Log2((uint)span.Length));
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static void Swap<T>(ref T a, ref T b) => (a, b) = (b, a);
	}
}
