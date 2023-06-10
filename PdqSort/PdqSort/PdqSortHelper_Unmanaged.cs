using System;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace Gal.Core
{
	/// <summary>
	/// Pdq排序_非托管对象,基于https://gist.github.com/hez2010/6b52929ee1755788c34818972c46aefb的实现
	/// </summary>
    /// <para>author gouanlin</para>
	public static class PdqSortHelper_Unmanaged
    {
        /// <summary>
        /// Partitions below this size are sorted using insertion sort.
        /// </summary>
        public const int INSERTION_SORT_THRESHOLD = 24;
        /// <summary>
        /// Partitions above this size use Tukey's ninther to select the pivot.
        /// </summary>
        public const int NINTHER_THRESHOLD = 128;
        /// <summary>
        /// When we detect an already sorted partition, attempt an insertion sort that allows this amount of element moves before giving up.
        /// </summary>
        public const int PARTIAL_INSERTION_SORT_LIMIT = 8;

        /// <summary>
        /// 插入排序,数组范围为 [begin,end)
        /// </summary>
        /// <param name="begin"></param>
        /// <param name="end"></param>
        /// <param name="comparison"></param>
        /// <typeparam name="T"></typeparam>
        private static unsafe void InsertionSort<T>(T* begin, T* end, Comparison<T> comparison) where T : unmanaged {
            if (begin == end) return;

            //从第二个元素开始,依次与前一个元素比较,如果当前值小于比较值,则比较值向后移动一位,直到比较失败,将当前值存入当前位置
            for (var itor = begin + 1; itor != end; ++itor) {
                var current = itor;
                var previous = itor - 1;

                // 先进行比较，这样可以避免对已正确定位的元素进行两次移动。
                var value = *current;
                if (comparison(value, *previous) >= 0) continue;
                do {
                    *current-- = *previous;
                } while (current != begin && comparison(value, *--previous) < 0);
                *current = value;
            }
        }

        /// <summary>
        /// 不做越界检测的插入排序(假定 *(begin-1) 小于数组中的索引元素),数组范围为 [begin,end)
        /// </summary>
        /// <param name="begin"></param>
        /// <param name="end"></param>
        /// <param name="comparison"></param>
        /// <typeparam name="T"></typeparam>
        private static unsafe void UnguardedInsertionSort<T>(T* begin, T* end, Comparison<T> comparison)
            where T : unmanaged {
            if (begin == end) return;

            //从第二个元素开始,依次与前一个元素比较,如果当前值小于比较值,则比较值向后移动一位,直到比较失败,将当前值存入当前位置
            for (var itor = begin + 1; itor != end; ++itor) {
                var current = itor;
                var previous = itor - 1;

                // 先进行比较，这样可以避免对已正确定位的元素进行两次移动。
                var value = *current;
                if (comparison(value, *previous) >= 0) continue;
                do {
                    *current-- = *previous;
                } while (comparison(value, *--previous) < 0);
                *current = value;
            }
        }

        /// <summary>
        /// 尝试在[begin，end）上使用插入排序。如果超过 PARTIAL_INSERTION_SORT_LIMIT 个元素被移动，并中止排序。否则它会成功排序并返回true。 
        /// </summary>
        /// <param name="begin"></param>
        /// <param name="end"></param>
        /// <param name="comparison"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        private static unsafe bool PartialInsertionSort<T>(T* begin, T* end, Comparison<T> comparison)
            where T : unmanaged {
            if (begin == end) return true;
            long limit = 0;
            for (var itor = begin + 1; itor != end; ++itor) {
                var current = itor;
                var previous = itor - 1;

                // 先进行比较，这样可以避免对已正确定位的元素进行两次移动。
                var value = *current;
                if (comparison(value, *previous) < 0) {
                    do {
                        *current-- = *previous;
                    } while (current != begin && comparison(value, *--previous) < 0);
                    *current = value;
                    limit += itor - current;
                }
                if (limit > PARTIAL_INSERTION_SORT_LIMIT) return false;
            }
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe void Sort2<T>(T* a, T* b, Comparison<T> comparison) where T : unmanaged {
            if (comparison(*b, *a) < 0) Swap(a, b);
        }

        /// <summary>
        /// Sorts the elements *a, *b and *c using comparison function comp.
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <param name="c"></param>
        /// <param name="comparison"></param>
        /// <typeparam name="T"></typeparam>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe void Sort3<T>(T* a, T* b, T* c, Comparison<T> comparison) where T : unmanaged {
            Sort2(a, b, comparison);
            Sort2(b, c, comparison);
            Sort2(a, b, comparison);
        }

        private static unsafe bool PartitionRight<T>(T* begin, T* end, Comparison<T> comparison, out T* outResult)
            where T : unmanaged {
            // Move pivot into local for speed.
            var pivot = *begin;
            var first = begin;
            var last = end;

            // Find the first element greater than or equal than the pivot (the median of 3 guarantees
            // this exists).
            while (comparison(*++first, pivot) < 0) {
            }

            // Find the first element strictly smaller than the pivot. We have to guard this search if
            // there was no element before *first.
            if (first - 1 == begin)
                while (first < last && !(comparison(*--last, pivot) < 0)) {
                }
            else
                while (!(comparison(*--last, pivot) < 0)) {
                }

            // If the first pair of elements that should be swapped to partition are the same element,
            // the passed in sequence already was correctly partitioned.
            var alreadyPartitioned = first >= last;

            // Keep swapping pairs of elements that are on the wrong side of the pivot. Previously
            // swapped pairs guard the searches, which is why the first iteration is special-cased
            // above.
            while (first < last) {
                Swap(first, last);
                while (comparison(*++first, pivot) < 0) {
                }
                while (!(comparison(*--last, pivot) < 0)) {
                }
            }

            // Put the pivot in the right place.
            var pivotPos = first - 1;
            *begin = *pivotPos;
            *pivotPos = pivot;
            outResult = pivotPos;
            return alreadyPartitioned;
        }

        /// <summary>
        /// Similar function to the one above, except elements equal to the pivot are put to the left of
        /// the pivot and it doesn't check or return if the passed sequence already was partitioned.
        ///	Since this is rarely used (the many equal case), and in that case pdqsort already has      O(n)
        /// performance, no block quicksort is applied here for simplicity.
        /// </summary>
        /// <param name="begin"></param>
        /// <param name="end"></param>
        /// <param name="comparison"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        private static unsafe T* PartitionLeft<T>(T* begin, T* end, Comparison<T> comparison) where T : unmanaged {
            var pivot = *begin;
            var first = begin;
            var last = end;
            while (comparison(pivot, *--last) < 0) {
            }
            if (last + 1 == end)
                while (first < last && !(comparison(pivot, *++first) < 0)) {
                }
            else
                while (!(comparison(pivot, *++first) < 0)) {
                }
            while (first < last) {
                Swap(first, last);
                while (comparison(pivot, *--last) < 0) {
                }
                while (!(comparison(pivot, *++first) < 0)) {
                }
            }
            var pivotPos = last;
            *begin = *pivotPos;
            *pivotPos = pivot;
            return pivotPos;
        }

        private static unsafe void HeapSort<T>(T* begin, T* end, Comparison<T> comparison) where T : unmanaged {
            if (begin == end) return;
            var n = (int)(end - begin);
            for (var i = (n >> 1) - 1; i >= 0; i--) Heapify(begin, n, i, comparison);
            for (var i = n - 1; i > 0; i--) {
                Swap(begin, begin + i);
                Heapify(begin, i, 0, comparison);
            }
        }

        private static unsafe void Heapify<T>(T* begin, int n, int i, Comparison<T> comparison) where T : unmanaged {
            //当前值
            var current = begin[i];
            var half = n >> 1;
            while (i < half) {
                //右子节点索引
                var t = (i << 1) + 1;
                var child = begin[t];

                //如果有右子节点,且左子节点小于右子节点,则i置为右子节点的索引
                if (++t < n && comparison(child, begin[t]) < 0)
                    child = begin[t];
                else
                    t--;

                //如果当前节点不小于子节点,结束循环
                if ((comparison(current, child) >= 0)) break;

                //将子节点移动到父节点位置
                begin[i] = child;

                //父节点的值在current中,用于后续比较,无需处理

                //父节点指针指向子节点
                i = t;
            }

            //将当前值存回当前节点
            begin[i] = current;
        }

        private static unsafe void PdqSort<T>(T* begin, T* end, Comparison<T> comparison, int badAllowed,
                                              bool leftmost = true) where T : unmanaged {
            // Use a while loop for tail recursion elimination.
            while (true) {
                var size = (int)(end - begin);

                // 对小数组排序,插入排序是最快的
                if (size < INSERTION_SORT_THRESHOLD) {
                    if (leftmost)
                        InsertionSort(begin, end, comparison);
                    else
                        UnguardedInsertionSort(begin, end, comparison);
                    return;
                }

                // Choose pivot as median of 3 or pseudomedian of 9.
                var s2 = size >> 1;
                if (size > NINTHER_THRESHOLD) {
                    Sort3(begin, begin + s2, end - 1, comparison);
                    Sort3(begin + 1, begin + (s2 - 1), end - 2, comparison);
                    Sort3(begin + 2, begin + (s2 + 1), end - 3, comparison);
                    Sort3(begin + (s2 - 1), begin + s2, begin + (s2 + 1), comparison);
                    (*begin, *(begin + s2)) = (*(begin + s2), *begin);
                } else
                    Sort3(begin + s2, begin, end - 1, comparison);

                // If *(begin - 1) is the end of the right partition of a previous partition operation
                // there is no element in [begin, end) that is smaller than *(begin - 1). Then if our
                // pivot compares equal to *(begin - 1) we change strategy, putting equal elements in
                // the left partition, greater elements in the right partition. We do not have to
                // recurse on the left partition, since it's sorted (all equal).
                if (!leftmost && !(comparison(*(begin - 1), *begin) < 0)) {
                    begin = PartitionLeft(begin, end, comparison) + 1;
                    continue;
                }

                // Partition and get results.
                var alreadyPartitioned = PartitionRight(begin, end, comparison, out var pivotPos);

                // Check for a highly unbalanced partition.
                var lSize = pivotPos - begin;
                var rSize = end - (pivotPos + 1);
                var highlyUnbalanced = lSize < size >> 3 || rSize < size >> 3;

                // If we got a highly unbalanced partition we shuffle elements to break many patterns.
                if (highlyUnbalanced) {
                    // If we had too many bad partitions, switch to heapsort to guarantee O(n log n).
                    if (--badAllowed == 0) {
                        // heap_sort(begin, end, comparison);
                        HeapSort(begin, end, comparison);
                        return;
                    }
                    if (lSize >= INSERTION_SORT_THRESHOLD) {
                        Swap(begin, begin + lSize / 4);
                        Swap(pivotPos - 1, pivotPos - lSize / 4);
                        if (lSize > NINTHER_THRESHOLD) {
                            Swap(begin + 1, begin + (lSize / 4 + 1));
                            Swap(begin + 2, begin + (lSize / 4 + 2));
                            Swap(pivotPos - 2, pivotPos - (lSize / 4 + 1));
                            Swap(pivotPos - 3, pivotPos - (lSize / 4 + 2));
                        }
                    }
                    if (rSize >= INSERTION_SORT_THRESHOLD) {
                        Swap(pivotPos + 1, pivotPos + (1 + rSize / 4));
                        Swap(end - 1, end - rSize / 4);
                        if (rSize > NINTHER_THRESHOLD) {
                            Swap(pivotPos + 2, pivotPos + (2 + rSize / 4));
                            Swap(pivotPos + 3, pivotPos + (3 + rSize / 4));
                            Swap(end - 2, end - (1 + rSize / 4));
                            Swap(end - 3, end - (2 + rSize / 4));
                        }
                    }
                } else {
                    // If we were decently balanced and we tried to sort an already partitioned
                    // sequence try to use insertion sort.
                    if (alreadyPartitioned && PartialInsertionSort(begin, pivotPos, comparison) &&
                        PartialInsertionSort(pivotPos + 1, end, comparison)) return;
                }

                // Sort the left partition first using recursion and do tail recursion elimination for
                // the right-hand partition.
                PdqSort(begin, pivotPos, comparison, badAllowed, leftmost);
                begin = pivotPos + 1;
                leftmost = false;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe void Swap<T>(T* a, T* b) where T : unmanaged => (*a, *b) = (*b, *a);

        public static unsafe void Sort<T>(Span<T> span, Comparison<T> comparison) where T : unmanaged {
            fixed (T* begin = span) {
                PdqSort(begin, begin + span.Length, comparison, BitOperations.Log2((uint)span.Length));
            }
        }
    }
}