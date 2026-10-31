#if !NET5_0_OR_GREATER
using System;
using System.Runtime.CompilerServices;

namespace System.Runtime.CompilerServices
{
	[AttributeUsage(
		AttributeTargets.Module | AttributeTargets.Class | AttributeTargets.Struct |
		AttributeTargets.Constructor | AttributeTargets.Method | AttributeTargets.Property |
		AttributeTargets.Event | AttributeTargets.Interface,
		Inherited = false)]
	internal sealed class SkipLocalsInitAttribute : Attribute
	{
	}

#pragma warning disable CS8500
	internal static unsafe class Unsafe
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int SizeOf<T>() => sizeof(T);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static ref T Add<T>(ref T source, int elementOffset) {
			fixed (T* p = &source) return ref *(p + elementOffset);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static ref T Add<T>(ref T source, IntPtr elementOffset) {
			fixed (T* p = &source) return ref *(p + (long)elementOffset);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static ref T Subtract<T>(ref T source, int elementOffset) {
			fixed (T* p = &source) return ref *(p - elementOffset);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static ref T Subtract<T>(ref T source, IntPtr elementOffset) {
			fixed (T* p = &source) return ref *(p - (long)elementOffset);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static IntPtr ByteOffset<T>(ref T origin, ref T target) {
			fixed (T* pOrigin = &origin)
			fixed (T* pTarget = &target)
				return (IntPtr)((byte*)pTarget - (byte*)pOrigin);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool AreSame<T>(ref T left, ref T right) {
			fixed (T* pLeft = &left)
			fixed (T* pRight = &right)
				return pLeft == pRight;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool IsAddressLessThan<T>(ref T left, ref T right) {
			fixed (T* pLeft = &left)
			fixed (T* pRight = &right)
				return (ulong)pLeft < (ulong)pRight;
		}
	}
#pragma warning restore CS8500
}

namespace System.Numerics
{
	internal static class BitOperations
	{
		private static ReadOnlySpan<byte> Log2DeBruijn => new byte[32] {
			00, 09, 01, 10, 13, 21, 02, 29,
			11, 14, 16, 18, 22, 25, 03, 30,
			08, 12, 20, 28, 15, 17, 24, 07,
			19, 27, 23, 06, 26, 05, 04, 31
		};

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int Log2(uint value) {
			value |= 1;
			value |= value >> 1;
			value |= value >> 2;
			value |= value >> 4;
			value |= value >> 8;
			value |= value >> 16;
			return Log2DeBruijn[(int)((value * 0x07C4ACDDu) >> 27)];
		}
	}
}
#endif
