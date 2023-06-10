using System;
using System.Collections.Generic;

namespace Gal.Core
{
	/// <summary>
	/// 模糊的二分搜索,在列表中搜索最接近目标的元素的索引
	/// <para>注意:计算距离时需要考虑数据计算越界的问题哦,比如 100 - int.MinValue 的结果是一个负数,int值的安全区间是 int.MinValue/2 至 int.MaxValue/2 </para>
	/// </summary>
	/// <para>author gouanlin</para>
	public static class FuzzyBinarySearch
	{
		/// <summary>
		/// 模糊的二分搜索(在有序的列表中以二分法搜索目标,若目标不存在,则返回目标所在区间的前一个索引)
		/// <para>不能返回离目标最近的元素的索引,因为这涉及到计算元素距离的问题,而涉及到距离就可能存在数值计算越界的问题,
		/// 比如: 100 - int.MinValue 就越界了,返回值应该是一个正数,但因为越界了,返回的还是一个负数,解决方法就是将 int 改为 long
		/// 来计算: 100L - (long)int.MinValue, 以此类推,可能又需要用 decimal 来计算,这对计算性能是不利的,所以此类问题应该逻辑层来解决
		/// </para>
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="value"></param>
		/// <param name="list"></param>
		/// <param name="minIndex"></param>
		/// <param name="maxIndex"></param>
		/// <param name="comparison">一定要考虑数值越界的问题哦</param>
		/// <returns>目标在列表中的索引,若目标不在列表中,则返回目标所在区间的前一个索引</returns>
		public static int Exec<T>(T value, IReadOnlyList<T> list, int minIndex, int maxIndex, Comparison<T> comparison) {
			if (comparison(value, list[minIndex]) <= 0) return minIndex;
			if (comparison(value, list[maxIndex]) >= 0) return maxIndex;
			while (true) {
				if (maxIndex - minIndex == 1) return comparison(value, list[maxIndex]) == 0 ? maxIndex : minIndex;
				var index = (minIndex + maxIndex) >> 1;
				switch (comparison(value, list[index])) {
					case 0: return index;
					case < 0:
						maxIndex = index;
						break;
					default:
						minIndex = index;
						break;
				}
			}
		}

		/// <summary>
		/// 模糊的二分搜索(在有序的列表中以二分法搜索目标,若目标不存在,则返回目标所在区间的前一个索引)
		/// <para>不能返回离目标最近的元素的索引,因为这涉及到计算元素距离的问题,而涉及到距离就可能存在数值计算越界的问题,
		/// 比如: 100 - int.MinValue 就越界了,返回值应该是一个正数,但因为越界了,返回的还是一个负数,解决方法就是将 int 改为 long
		/// 来计算: 100L - (long)int.MinValue, 以此类推,可能又需要用 decimal 来计算,这对计算性能是不利的,所以此类问题应该逻辑层来解决
		/// </para>
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="value"></param>
		/// <param name="span"></param>
		/// <param name="comparison">一定要考虑数值越界的问题哦</param>
		/// <returns>目标在列表中的索引,若目标不在列表中,则返回目标所在区间的前一个索引</returns>
		public static int Exec<T>(T value, ReadOnlySpan<T> span, Comparison<T> comparison) {
			int minIndex = 0, maxIndex = span.Length - 1;

			if (comparison(value, span[minIndex]) <= 0) return minIndex;
			if (comparison(value, span[maxIndex]) >= 0) return maxIndex;
			while (true) {
				if (maxIndex - minIndex == 1) return comparison(value, span[maxIndex]) == 0 ? maxIndex : minIndex;
				var index = (minIndex + maxIndex) >> 1;
				switch (comparison(value, span[index])) {
					case 0: return index;
					case < 0:
						maxIndex = index;
						break;
					default:
						minIndex = index;
						break;
				}
			}
		}
	}
}