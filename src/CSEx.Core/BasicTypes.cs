using System;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

// C-style basic types with exact VEX compatibility
using UChar = System.Byte;
using SChar = System.SByte;
using HChar = System.SByte; // signfulness depends on host - we choose signed
using UShort = System.UInt16;
using Short = System.Int16;
using UInt = System.UInt32;
using Int = System.Int32;
using ULong = System.UInt64;
using Long = System.Int64;
using SizeT = System.UIntPtr;
using Float = System.Single;
using Double = System.Double;

namespace CSEx.Core
{
    /// <summary>
    /// Basic types corresponding to VEX's libvex_basictypes.h
    /// These types maintain exact size compatibility with the original VEX
    /// </summary>
    public static class BasicTypes
    {
        // Always 8 bits
        public static byte UChar_MIN => byte.MinValue;
        public static byte UChar_MAX => byte.MaxValue;
        public static sbyte SChar_MIN => sbyte.MinValue;
        public static sbyte SChar_MAX => sbyte.MaxValue;

        // Always 16 bits  
        public static ushort UShort_MIN => ushort.MinValue;
        public static ushort UShort_MAX => ushort.MaxValue;
        public static short Short_MIN => short.MinValue;
        public static short Short_MAX => short.MaxValue;

        // Always 32 bits
        public static uint UInt_MIN => uint.MinValue;
        public static uint UInt_MAX => uint.MaxValue;
        public static int Int_MIN => int.MinValue;
        public static int Int_MAX => int.MaxValue;

        // Always 64 bits
        public static ulong ULong_MIN => ulong.MinValue;
        public static ulong ULong_MAX => ulong.MaxValue;
        public static long Long_MIN => long.MinValue;
        public static long Long_MAX => long.MaxValue;

        // Size_t equivalent - platform dependent but we use UIntPtr
        public static nuint SizeT_MIN => nuint.MinValue;
        public static nuint SizeT_MAX => nuint.MaxValue;
    }

    /// <summary>
    /// 128-bit vector type (equivalent to VEX U128[4])
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public struct U128
    {
        [FieldOffset(0)] public uint U0;
        [FieldOffset(4)] public uint U1;
        [FieldOffset(8)] public uint U2;
        [FieldOffset(12)] public uint U3;

        [FieldOffset(0)] public Vector128<uint> Vector;

        public U128(uint u0, uint u1, uint u2, uint u3)
        {
            Vector = default;
            U0 = u0;
            U1 = u1;
            U2 = u2;
            U3 = u3;
        }

        public U128(Vector128<uint> vector)
        {
            U0 = U1 = U2 = U3 = 0;
            Vector = vector;
        }

        public uint this[int index]
        {
            get => index switch
            {
                0 => U0,
                1 => U1, 
                2 => U2,
                3 => U3,
                _ => throw new IndexOutOfRangeException()
            };
            set
            {
                switch (index)
                {
                    case 0: U0 = value; break;
                    case 1: U1 = value; break;
                    case 2: U2 = value; break;
                    case 3: U3 = value; break;
                    default: throw new IndexOutOfRangeException();
                }
            }
        }
    }

    /// <summary>
    /// 256-bit vector type (equivalent to VEX U256[8])
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct U256
    {
        [FieldOffset(0)] public uint U0;
        [FieldOffset(4)] public uint U1;
        [FieldOffset(8)] public uint U2;
        [FieldOffset(12)] public uint U3;
        [FieldOffset(16)] public uint U4;
        [FieldOffset(20)] public uint U5;
        [FieldOffset(24)] public uint U6;
        [FieldOffset(28)] public uint U7;

        [FieldOffset(0)] public Vector256<uint> Vector;

        public U256(uint u0, uint u1, uint u2, uint u3, uint u4, uint u5, uint u6, uint u7)
        {
            Vector = default;
            U0 = u0; U1 = u1; U2 = u2; U3 = u3;
            U4 = u4; U5 = u5; U6 = u6; U7 = u7;
        }

        public U256(Vector256<uint> vector)
        {
            U0 = U1 = U2 = U3 = U4 = U5 = U6 = U7 = 0;
            Vector = vector;
        }

        public uint this[int index]
        {
            get => index switch
            {
                0 => U0, 1 => U1, 2 => U2, 3 => U3,
                4 => U4, 5 => U5, 6 => U6, 7 => U7,
                _ => throw new IndexOutOfRangeException()
            };
            set
            {
                switch (index)
                {
                    case 0: U0 = value; break;
                    case 1: U1 = value; break;
                    case 2: U2 = value; break;
                    case 3: U3 = value; break;
                    case 4: U4 = value; break;
                    case 5: U5 = value; break;
                    case 6: U6 = value; break;
                    case 7: U7 = value; break;
                    default: throw new IndexOutOfRangeException();
                }
            }
        }
    }

    /// <summary>
    /// 128-bit vector union for convenient access (equivalent to VEX V128)
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public struct V128
    {
        [FieldOffset(0)] public unsafe fixed byte W8[16];
        [FieldOffset(0)] public unsafe fixed ushort W16[8];
        [FieldOffset(0)] public unsafe fixed uint W32[4];
        [FieldOffset(0)] public unsafe fixed ulong W64[2];

        [FieldOffset(0)] public Vector128<byte> AsByte;
        [FieldOffset(0)] public Vector128<ushort> AsUInt16;
        [FieldOffset(0)] public Vector128<uint> AsUInt32;
        [FieldOffset(0)] public Vector128<ulong> AsUInt64;

        public V128(Vector128<byte> vector)
        {
            this = default;
            AsByte = vector;
        }

        public V128(Vector128<ushort> vector)
        {
            this = default;
            AsUInt16 = vector;
        }

        public V128(Vector128<uint> vector)
        {
            this = default;
            AsUInt32 = vector;
        }

        public V128(Vector128<ulong> vector)
        {
            this = default;
            AsUInt64 = vector;
        }
    }

    /// <summary>
    /// 256-bit vector union for convenient access (equivalent to VEX V256)
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct V256
    {
        [FieldOffset(0)] public unsafe fixed byte W8[32];
        [FieldOffset(0)] public unsafe fixed ushort W16[16];
        [FieldOffset(0)] public unsafe fixed uint W32[8];
        [FieldOffset(0)] public unsafe fixed ulong W64[4];

        [FieldOffset(0)] public Vector256<byte> AsByte;
        [FieldOffset(0)] public Vector256<ushort> AsUInt16;
        [FieldOffset(0)] public Vector256<uint> AsUInt32;
        [FieldOffset(0)] public Vector256<ulong> AsUInt64;

        public V256(Vector256<byte> vector)
        {
            this = default;
            AsByte = vector;
        }

        public V256(Vector256<ushort> vector)
        {
            this = default;
            AsUInt16 = vector;
        }

        public V256(Vector256<uint> vector)
        {
            this = default;
            AsUInt32 = vector;
        }

        public V256(Vector256<ulong> vector)
        {
            this = default;
            AsUInt64 = vector;
        }
    }

    /// <summary>
    /// 512-bit vector union for convenient access (equivalent to AVX-512 ZMM)
    /// </summary>
    [StructLayout(LayoutKind.Explicit)]
    public struct V512
    {
        [FieldOffset(0)] public ulong Low;
        [FieldOffset(8)] public ulong High;
        [FieldOffset(16)] public ulong Upper1;
        [FieldOffset(24)] public ulong Upper2;
        [FieldOffset(32)] public ulong Upper3;
        [FieldOffset(40)] public ulong Upper4;
        [FieldOffset(48)] public ulong Upper5;
        [FieldOffset(56)] public ulong Upper6;

        // Vector access
        [FieldOffset(0)] public Vector512<byte> AsByte;
        [FieldOffset(0)] public Vector512<ushort> AsUInt16;
        [FieldOffset(0)] public Vector512<uint> AsUInt32;
        [FieldOffset(0)] public Vector512<ulong> AsUInt64;
        
        // Floating-point access
        [FieldOffset(0)] public Vector512<float> AsFloat32;
        [FieldOffset(0)] public Vector512<double> AsFloat64;

        // Sub-vector access for compatibility
        [FieldOffset(0)] public V256 Lower256;
        [FieldOffset(32)] public V256 Upper256;
        
        public V512(Vector512<byte> vector)
        {
            this = default;
            AsByte = vector;
        }

        public V512(Vector512<ushort> vector)
        {
            this = default;
            AsUInt16 = vector;
        }

        public V512(Vector512<uint> vector)
        {
            this = default;
            AsUInt32 = vector;
        }

        public V512(Vector512<ulong> vector)
        {
            this = default;
            AsUInt64 = vector;
        }
        
        public V512(Vector512<float> vector)
        {
            this = default;
            AsFloat32 = vector;
        }

        public V512(Vector512<double> vector)
        {
            this = default;
            AsFloat64 = vector;
        }
    }
}