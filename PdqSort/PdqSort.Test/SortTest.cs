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
	}
}