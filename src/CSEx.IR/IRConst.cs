using System;
using CSEx.Core;

namespace CSEx.IR
{
    /// <summary>
    /// Base class for IR constants (equivalent to VEX IRConst)
    /// IRConsts are used within 'Const' and 'Exit' IRExprs.
    /// </summary>
    public abstract class IRConst : IEquatable<IRConst>
    {
        public abstract IRConstTag Tag { get; }

        public abstract bool Equals(IRConst? other);
        public abstract override bool Equals(object? obj);
        public abstract override int GetHashCode();
        public abstract override string ToString();

        /// <summary>
        /// Deep copy the constant
        /// </summary>
        public abstract IRConst DeepCopy();

        /// <summary>
        /// Pretty print the constant
        /// </summary>
        public virtual string PrettyPrint() => ToString();
    }

    /// <summary>
    /// 1-bit boolean constant
    /// </summary>
    public sealed class IRConstU1 : IRConst
    {
        public bool Value { get; }
        public override IRConstTag Tag => IRConstTag.U1;

        public IRConstU1(bool value)
        {
            Value = value;
        }

        public override bool Equals(IRConst? other) => 
            other is IRConstU1 u1 && Value == u1.Value;

        public override bool Equals(object? obj) => 
            obj is IRConstU1 other && Equals(other);

        public override int GetHashCode() => Value.GetHashCode();

        public override string ToString() => Value ? "0x1:I1" : "0x0:I1";

        public override IRConst DeepCopy() => new IRConstU1(Value);
    }

    /// <summary>
    /// 8-bit unsigned integer constant
    /// </summary>
    public sealed class IRConstU8 : IRConst
    {
        public byte Value { get; }
        public override IRConstTag Tag => IRConstTag.U8;

        public IRConstU8(byte value)
        {
            Value = value;
        }

        public override bool Equals(IRConst? other) => 
            other is IRConstU8 u8 && Value == u8.Value;

        public override bool Equals(object? obj) => 
            obj is IRConstU8 other && Equals(other);

        public override int GetHashCode() => Value.GetHashCode();

        public override string ToString() => $"0x{Value:X2}:I8";

        public override IRConst DeepCopy() => new IRConstU8(Value);
    }

    /// <summary>
    /// 16-bit unsigned integer constant
    /// </summary>
    public sealed class IRConstU16 : IRConst
    {
        public ushort Value { get; }
        public override IRConstTag Tag => IRConstTag.U16;

        public IRConstU16(ushort value)
        {
            Value = value;
        }

        public override bool Equals(IRConst? other) => 
            other is IRConstU16 u16 && Value == u16.Value;

        public override bool Equals(object? obj) => 
            obj is IRConstU16 other && Equals(other);

        public override int GetHashCode() => Value.GetHashCode();

        public override string ToString() => $"0x{Value:X4}:I16";

        public override IRConst DeepCopy() => new IRConstU16(Value);
    }

    /// <summary>
    /// 32-bit unsigned integer constant
    /// </summary>
    public sealed class IRConstU32 : IRConst
    {
        public uint Value { get; }
        public override IRConstTag Tag => IRConstTag.U32;

        public IRConstU32(uint value)
        {
            Value = value;
        }

        public override bool Equals(IRConst? other) => 
            other is IRConstU32 u32 && Value == u32.Value;

        public override bool Equals(object? obj) => 
            obj is IRConstU32 other && Equals(other);

        public override int GetHashCode() => Value.GetHashCode();

        public override string ToString() => $"0x{Value:X8}:I32";

        public override IRConst DeepCopy() => new IRConstU32(Value);
    }

    /// <summary>
    /// 64-bit unsigned integer constant
    /// </summary>
    public sealed class IRConstU64 : IRConst
    {
        public ulong Value { get; }
        public override IRConstTag Tag => IRConstTag.U64;

        public IRConstU64(ulong value)
        {
            Value = value;
        }

        public override bool Equals(IRConst? other) => 
            other is IRConstU64 u64 && Value == u64.Value;

        public override bool Equals(object? obj) => 
            obj is IRConstU64 other && Equals(other);

        public override int GetHashCode() => Value.GetHashCode();

        public override string ToString() => $"0x{Value:X16}:I64";

        public override IRConst DeepCopy() => new IRConstU64(Value);
    }

    /// <summary>
    /// 32-bit IEEE754 floating point constant
    /// </summary>
    public sealed class IRConstF32 : IRConst
    {
        public float Value { get; }
        public override IRConstTag Tag => IRConstTag.F32;

        public IRConstF32(float value)
        {
            Value = value;
        }

        public override bool Equals(IRConst? other) => 
            other is IRConstF32 f32 && Value.Equals(f32.Value);

        public override bool Equals(object? obj) => 
            obj is IRConstF32 other && Equals(other);

        public override int GetHashCode() => Value.GetHashCode();

        public override string ToString() => $"F32({Value})";

        public override IRConst DeepCopy() => new IRConstF32(Value);
    }

    /// <summary>
    /// 32-bit unsigned int to be interpreted literally as IEEE754 single
    /// </summary>
    public sealed class IRConstF32i : IRConst
    {
        public uint Value { get; }
        public override IRConstTag Tag => IRConstTag.F32i;

        public IRConstF32i(uint value)
        {
            Value = value;
        }

        /// <summary>
        /// Get the float interpretation of the bits
        /// </summary>
        public unsafe float AsFloat 
        { 
            get 
            { 
                uint temp = Value;
                return *(float*)&temp; 
            } 
        }

        public override bool Equals(IRConst? other) => 
            other is IRConstF32i f32i && Value == f32i.Value;

        public override bool Equals(object? obj) => 
            obj is IRConstF32i other && Equals(other);

        public override int GetHashCode() => Value.GetHashCode();

        public override string ToString() => $"F32i(0x{Value:X8})";

        public override IRConst DeepCopy() => new IRConstF32i(Value);
    }

    /// <summary>
    /// 64-bit IEEE754 floating point constant
    /// </summary>
    public sealed class IRConstF64 : IRConst
    {
        public double Value { get; }
        public override IRConstTag Tag => IRConstTag.F64;

        public IRConstF64(double value)
        {
            Value = value;
        }

        public override bool Equals(IRConst? other) => 
            other is IRConstF64 f64 && Value.Equals(f64.Value);

        public override bool Equals(object? obj) => 
            obj is IRConstF64 other && Equals(other);

        public override int GetHashCode() => Value.GetHashCode();

        public override string ToString() => $"F64({Value})";

        public override IRConst DeepCopy() => new IRConstF64(Value);
    }

    /// <summary>
    /// 64-bit unsigned int to be interpreted literally as IEEE754 double
    /// </summary>
    public sealed class IRConstF64i : IRConst
    {
        public ulong Value { get; }
        public override IRConstTag Tag => IRConstTag.F64i;

        public IRConstF64i(ulong value)
        {
            Value = value;
        }

        /// <summary>
        /// Get the double interpretation of the bits
        /// </summary>
        public unsafe double AsDouble 
        { 
            get 
            { 
                ulong temp = Value;
                return *(double*)&temp; 
            } 
        }

        public override bool Equals(IRConst? other) => 
            other is IRConstF64i f64i && Value == f64i.Value;

        public override bool Equals(object? obj) => 
            obj is IRConstF64i other && Equals(other);

        public override int GetHashCode() => Value.GetHashCode();

        public override string ToString() => $"F64i(0x{Value:X16})";

        public override IRConst DeepCopy() => new IRConstF64i(Value);
    }

    /// <summary>
    /// 128-bit restricted vector constant
    /// with 1 bit (repeated 8 times) for each of the 16 x 1-byte lanes
    /// </summary>
    public sealed class IRConstV128 : IRConst
    {
        public ushort Value { get; }
        public override IRConstTag Tag => IRConstTag.V128;

        public IRConstV128(ushort value)
        {
            Value = value;
        }

        public override bool Equals(IRConst? other) => 
            other is IRConstV128 v128 && Value == v128.Value;

        public override bool Equals(object? obj) => 
            obj is IRConstV128 other && Equals(other);

        public override int GetHashCode() => Value.GetHashCode();

        public override string ToString() => $"V128(0x{Value:X4})";

        public override IRConst DeepCopy() => new IRConstV128(Value);
    }

    /// <summary>
    /// 256-bit restricted vector constant
    /// with 1 bit (repeated 8 times) for each of the 32 x 1-byte lanes
    /// </summary>
    public sealed class IRConstV256 : IRConst
    {
        public uint Value { get; }
        public override IRConstTag Tag => IRConstTag.V256;

        public IRConstV256(uint value)
        {
            Value = value;
        }

        public override bool Equals(IRConst? other) => 
            other is IRConstV256 v256 && Value == v256.Value;

        public override bool Equals(object? obj) => 
            obj is IRConstV256 other && Equals(other);

        public override int GetHashCode() => Value.GetHashCode();

        public override string ToString() => $"V256(0x{Value:X8})";

        public override IRConst DeepCopy() => new IRConstV256(Value);
    }

    /// <summary>
    /// 512-bit vector constant (AVX-512 ZMM)
    /// </summary>
    public sealed class IRConstV512 : IRConst
    {
        public ulong Value { get; }
        public override IRConstTag Tag => IRConstTag.V512;

        public IRConstV512(ulong value)
        {
            Value = value;
        }

        public override bool Equals(IRConst? other) => 
            other is IRConstV512 v512 && Value == v512.Value;

        public override bool Equals(object? obj) => 
            obj is IRConstV512 other && Equals(other);

        public override int GetHashCode() => Value.GetHashCode();

        public override string ToString() => $"V512(0x{Value:X16})";

        public override IRConst DeepCopy() => new IRConstV512(Value);
    }

    /// <summary>
    /// Factory methods for creating IR constants
    /// </summary>
    public static class IRConstFactory
    {
        public static IRConstU1 U1(bool value) => new(value);
        public static IRConstU8 U8(byte value) => new(value);
        public static IRConstU16 U16(ushort value) => new(value);
        public static IRConstU32 U32(uint value) => new(value);
        public static IRConstU64 U64(ulong value) => new(value);
        public static IRConstF32 F32(float value) => new(value);
        public static IRConstF32i F32i(uint value) => new(value);
        public static IRConstF64 F64(double value) => new(value);
        public static IRConstF64i F64i(ulong value) => new(value);
        public static IRConstV128 V128(ushort value) => new(value);
        public static IRConstV256 V256(uint value) => new(value);
        public static IRConstV512 V512(ulong value) => new(value);
    }
}