using System;

namespace CSEx.IR
{
    /// <summary>
    /// IR type indicating size and kind of values (equivalent to VEX IRType)
    /// A type indicates the size of a value, and whether it's an integer, a float, or a vector (SIMD) value.
    /// </summary>
    public enum IRType : uint
    {
        Invalid = 0x1100,
        I1,     // 1-bit value (boolean)
        I8,     // 8-bit integer
        I16,    // 16-bit integer
        I32,    // 32-bit integer 
        I64,    // 64-bit integer
        I128,   // 128-bit scalar
        F16,    // 16-bit float (half precision)
        F32,    // IEEE 754 float
        F64,    // IEEE 754 double
        D32,    // 32-bit Decimal floating point
        D64,    // 64-bit Decimal floating point
        D128,   // 128-bit Decimal floating point
        F128,   // 128-bit floating point; implementation defined
        V128,   // 128-bit SIMD
        V256,   // 256-bit SIMD
        V512    // 512-bit SIMD (AVX-512)
    }

    /// <summary>
    /// Types of constants (equivalent to VEX IRConstTag)
    /// </summary>
    public enum IRConstTag : uint
    {
        U1 = 0x1300,
        U8,
        U16,
        U32,
        U64,
        F32,    // 32-bit IEEE754 floating
        F32i,   // 32-bit unsigned int to be interpreted literally as IEEE754 single
        F64,    // 64-bit IEEE754 floating  
        F64i,   // 64-bit unsigned int to be interpreted literally as IEEE754 double
        V128,   // 128-bit restricted vector constant, 1 bit repeated 8 times for each 16 lanes
        V256,   // 256-bit restricted vector constant, 1 bit repeated 8 times for each 32 lanes
        V512    // 512-bit restricted vector constant, 1 bit repeated 8 times for each 64 lanes
    }

    /// <summary>
    /// IR temporary handle (equivalent to VEX IRTemp)
    /// The IR optimiser relies on IRTemps being 32-bit ints.
    /// </summary>
    public readonly struct IRTemp : IEquatable<IRTemp>
    {
        public const uint INVALID = 0xFFFFFFFF;
        
        private readonly uint _value;

        public IRTemp(uint value)
        {
            _value = value;
        }

        public uint Value => _value;
        public bool IsValid => _value != INVALID;

        public static implicit operator uint(IRTemp temp) => temp._value;
        public static implicit operator IRTemp(uint value) => new(value);

        public static IRTemp Invalid => new(INVALID);

        public bool Equals(IRTemp other) => _value == other._value;
        public override bool Equals(object? obj) => obj is IRTemp other && Equals(other);
        public override int GetHashCode() => _value.GetHashCode();
        public override string ToString() => IsValid ? $"t{_value}" : "t_INVALID";

        public static bool operator ==(IRTemp left, IRTemp right) => left.Equals(right);
        public static bool operator !=(IRTemp left, IRTemp right) => !left.Equals(right);
    }

    /// <summary>
    /// Extension methods for IR types
    /// </summary>
    public static class IRTypeExtensions
    {
        /// <summary>
        /// Get the size (in bytes) of an IRType
        /// </summary>
        public static int SizeOf(this IRType type) => type switch
        {
            IRType.I1 => 1,      // Special case - 1 bit stored in 1 byte
            IRType.I8 => 1,
            IRType.I16 => 2,
            IRType.I32 => 4,
            IRType.I64 => 8,
            IRType.I128 => 16,
            IRType.F16 => 2,
            IRType.F32 => 4,
            IRType.F64 => 8,
            IRType.D32 => 4,
            IRType.D64 => 8,
            IRType.D128 => 16,
            IRType.F128 => 16,
            IRType.V128 => 16,
            IRType.V256 => 32,
            IRType.V512 => 64,
            _ => throw new ArgumentException($"Invalid IRType: {type}")
        };

        /// <summary>
        /// Get the size (in bits) of an IRType
        /// </summary>
        public static int SizeOfBits(this IRType type) => type switch
        {
            IRType.I1 => 1,
            _ => SizeOf(type) * 8
        };

        /// <summary>
        /// Check if type is an integer type
        /// </summary>
        public static bool IsInteger(this IRType type) => type switch
        {
            IRType.I1 or IRType.I8 or IRType.I16 or IRType.I32 or IRType.I64 or IRType.I128 => true,
            _ => false
        };

        /// <summary>
        /// Check if type is a floating point type
        /// </summary>
        public static bool IsFloat(this IRType type) => type switch
        {
            IRType.F16 or IRType.F32 or IRType.F64 or IRType.F128 => true,
            _ => false
        };

        /// <summary>
        /// Check if type is a decimal floating point type
        /// </summary>
        public static bool IsDecimal(this IRType type) => type switch
        {
            IRType.D32 or IRType.D64 or IRType.D128 => true,
            _ => false
        };

        /// <summary>
        /// Check if type is a vector/SIMD type
        /// </summary>
        public static bool IsVector(this IRType type) => type switch
        {
            IRType.V128 or IRType.V256 or IRType.V512 => true,
            _ => false
        };

        /// <summary>
        /// Get appropriate integer IRType for given size in bytes
        /// </summary>
        public static IRType IntegerIRTypeOfSize(int sizeBytes) => sizeBytes switch
        {
            1 => IRType.I8,
            2 => IRType.I16,
            4 => IRType.I32,
            8 => IRType.I64,
            16 => IRType.I128,
            _ => throw new ArgumentException($"No integer IRType for size {sizeBytes} bytes")
        };

        /// <summary>
        /// Pretty-print an IRType
        /// </summary>
        public static string PrettyPrint(this IRType type) => type switch
        {
            IRType.Invalid => "INVALID",
            IRType.I1 => "I1",
            IRType.I8 => "I8", 
            IRType.I16 => "I16",
            IRType.I32 => "I32",
            IRType.I64 => "I64",
            IRType.I128 => "I128",
            IRType.F16 => "F16",
            IRType.F32 => "F32",
            IRType.F64 => "F64",
            IRType.D32 => "D32",
            IRType.D64 => "D64",
            IRType.D128 => "D128",
            IRType.F128 => "F128",
            IRType.V128 => "V128",
            IRType.V256 => "V256",
            IRType.V512 => "V512",
            _ => $"IRType({(uint)type})"
        };
    }

    /// <summary>
    /// Extension methods for IR endianness
    /// </summary>
    public static class IREndnessExtensions
    {
        /// <summary>
        /// Pretty-print endianness
        /// </summary>
        public static string PrettyPrint(this IREndness endness) => endness switch
        {
            IREndness.LE => "LE",
            IREndness.BE => "BE", 
            _ => $"IREndness({(uint)endness})"
        };

        /// <summary>
        /// Check if endianness matches system endianness
        /// </summary>
        public static bool IsNativeEndianness(this IREndness endness)
        {
            return BitConverter.IsLittleEndian ? endness == IREndness.LE : endness == IREndness.BE;
        }
    }

    /// <summary>
    /// Callee information for helper function calls (equivalent to VEX IRCallee)
    /// </summary>
    public sealed class IRCallee : IEquatable<IRCallee>
    {
        public int Regparms { get; }      // Register calling convention info  
        public string Name { get; }       // Function name for debugging
        public IntPtr Addr { get; }       // Function address
        public int Mcx_Mask { get; }      // Media control extension mask

        public IRCallee(int regparms, string name, IntPtr addr, int mcxMask = 0)
        {
            Regparms = regparms;
            Name = name;
            Addr = addr;
            Mcx_Mask = mcxMask;
        }

        public bool Equals(IRCallee? other) =>
            other != null && 
            Regparms == other.Regparms && 
            Name == other.Name && 
            Addr == other.Addr && 
            Mcx_Mask == other.Mcx_Mask;

        public override bool Equals(object? obj) => 
            obj is IRCallee other && Equals(other);

        public override int GetHashCode() => 
            HashCode.Combine(Regparms, Name, Addr, Mcx_Mask);

        public override string ToString() => Name;

        public string PrettyPrint() => Name;
    }
}