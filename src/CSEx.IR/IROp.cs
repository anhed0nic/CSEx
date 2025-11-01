using System;

namespace CSEx.IR
{
    /// <summary>
    /// Primitive operations used in Unop, Binop, Triop and Qop IRExprs
    /// (equivalent to VEX IROp enum)
    /// 
    /// Most instructions supported by the architectures that Vex supports
    /// (x86, PPC, etc) are represented. Some more obscure ones (eg. cpuid)
    /// are handled with dirty helpers that emulate their functionality.
    /// </summary>
    public enum IROp : uint
    {
        // IMPORTANT: Do not change this ordering. The IR generators rely on
        // (eg) Add64 == Add8 + 3.
        
        Invalid = 0x1400,

        // Basic arithmetic operations
        Add8, Add16, Add32, Add64,
        Sub8, Sub16, Sub32, Sub64,
        
        // Signless multiplication (MullS/MullU is elsewhere)
        Mul8, Mul16, Mul32, Mul64,
        
        // Bitwise operations
        Or8, Or16, Or32, Or64,
        And8, And16, And32, And64,
        Xor8, Xor16, Xor32, Xor64,
        
        // Shift operations
        Shl8, Shl16, Shl32, Shl64,   // Shift left
        Shr8, Shr16, Shr32, Shr64,   // Shift right (logical)
        Sar8, Sar16, Sar32, Sar64,   // Shift right (arithmetic)
        
        // Integer comparisons
        CmpEQ8, CmpEQ16, CmpEQ32, CmpEQ64,   // Equal
        CmpNE8, CmpNE16, CmpNE32, CmpNE64,   // Not equal
        
        // Unary operations
        Not8, Not16, Not32, Not64,   // Bitwise NOT

        // CAS (Compare-And-Swap) hint operations
        // Like CmpEQ but with hint for CAS operation optimization
        CasCmpEQ8, CasCmpEQ16, CasCmpEQ32, CasCmpEQ64,
        CasCmpNE8, CasCmpNE16, CasCmpNE32, CasCmpNE64,

        // Expensive definedness tracking operations
        ExpCmpNE8, ExpCmpNE16, ExpCmpNE32, ExpCmpNE64,

        // -- Ordering not important after here --

        // Widening multiplies
        MullS8, MullS16, MullS32, MullS64,  // Signed widening multiply
        MullU8, MullU16, MullU32, MullU64,  // Unsigned widening multiply

        // Widening operations (unsigned extension)
        Iop_8Uto16, Iop_8Uto32, Iop_8Uto64,
        Iop_16Uto32, Iop_16Uto64,
        Iop_32Uto64,

        // Widening operations (signed extension)
        Iop_8Sto16, Iop_8Sto32, Iop_8Sto64,
        Iop_16Sto32, Iop_16Sto64,
        Iop_32Sto64,

        // Narrowing operations
        Iop_64to32, Iop_64to16, Iop_64to8,
        Iop_32to16, Iop_32to8,
        Iop_16to8,

        // High/low operations
        Iop_64HLto128, Iop_128HIto64, Iop_128to64,
        Iop_32HLto64, Iop_64HIto32,

        // Bit manipulation
        Clz64, Clz32,    // Count leading zeroes (UNDEFINED for zero input)
        Ctz64, Ctz32,    // Count trailing zeros (UNDEFINED for zero input)

        // Standard integer comparisons
        CmpLT32S, CmpLT64S,  // Less than (signed)
        CmpLE32S, CmpLE64S,  // Less or equal (signed)
        CmpLT32U, CmpLT64U,  // Less than (unsigned)
        CmpLE32U, CmpLE64U,  // Less or equal (unsigned)

        // Valgrind-Memcheck support operations
        CmpNEZ8, CmpNEZ16, CmpNEZ32, CmpNEZ64,      // Compare not equal to zero
        CmpwNEZ32, CmpwNEZ64,  // all-0s -> all-0s; other -> all-1s
        Left8, Left16, Left32, Left64,  // x -> x | -x
        Max32U,  // Unsigned max

        // PowerPC-style 3-way integer comparisons
        // op(x,y) | x < y  = 0x8 else | x > y  = 0x4 else | x == y = 0x2
        CmpORD32U, CmpORD64U,  // Unsigned ordered compare
        CmpORD32S, CmpORD64S,  // Signed ordered compare

        // Division operations
        DivU32, DivS32,    // Simple division (no modulo)
        DivU64, DivS64,    // 64-bit division
        DivU64E, DivS64E,  // Extended division (64-bit dividend concat with 64 zeros)
        DivU32E, DivS32E,  // Extended division (32-bit dividend concat with 32 zeros)

        DivModU64to32, DivModS64to32,  // Division with modulo (lo=div, hi=mod)
        DivModU128to64,                // 128-bit division with modulo

        // Floating point operations
        AddF32, AddF64,    // FP addition
        SubF32, SubF64,    // FP subtraction  
        MulF32, MulF64,    // FP multiplication
        DivF32, DivF64,    // FP division

        // FP comparisons
        CmpF32, CmpF64,    // FP compare (returns comparison result)
        CmpEQF32, CmpEQF64,
        CmpLTF32, CmpLTF64,
        CmpLEF32, CmpLEF64,

        // FP conversions
        F64toI32S, F64toI64S,  // FP to signed integer (with rounding mode)
        F64toI32U, F64toI64U,  // FP to unsigned integer
        I32StoF64, I64StoF64,  // Signed integer to FP
        I32UtoF64, I64UtoF64,  // Unsigned integer to FP
        F32toF64, F64toF32,    // FP format conversions

        // FP reinterpretation (bit patterns)
        ReinterpF64asI64, ReinterpI64asF64,
        ReinterpF32asI32, ReinterpI32asF32,

        // Vector/SIMD operations (basic set)
        Add8x16, Add16x8, Add32x4, Add64x2,     // Vector addition
        Sub8x16, Sub16x8, Sub32x4, Sub64x2,     // Vector subtraction
        Mul16x8, Mul32x4,                       // Vector multiplication

        // MMX/64-bit SIMD operations
        CmpEQ8x8, CmpEQ16x4, CmpEQ32x2,         // MMX comparisons
        CmpGTS8x8, CmpGTS16x4, CmpGTS32x2,      // MMX greater than signed
        QNarrowBin16Sx8, QNarrowBin32Sx4,       // Narrow with saturation
        InterleaveHI8x8, InterleaveHI16x4, InterleaveHI32x2,  // Interleave high
        InterleaveLO8x8, InterleaveLO16x4, InterleaveLO32x2,  // Interleave low
        ShlN16x4, ShlN32x2,                     // Shift left by N
        ShrN16x4, ShrN32x2,                     // Shift right logical by N
        SarN16x4, SarN32x2,                     // Shift right arithmetic by N
        MulHi16Sx4,                             // Multiply high signed
        Mul16x4,                                // 64-bit vector multiply
        MAddF32,                                // Multiply-add float

        // SSE/128-bit SIMD operations
        Add32Fx4, Add32F0x4,                    // SSE float addition
        Sub32Fx4, Sub32F0x4,                    // SSE float subtraction
        Mul32Fx4, Mul32F0x4,                    // SSE float multiplication
        Div32Fx4, Div32F0x4,                    // SSE float division
        Max32Fx4, Max32F0x4,                    // SSE float maximum
        Min32Fx4, Min32F0x4,                    // SSE float minimum
        Sqrt32Fx4, Sqrt32F0x4,                  // SSE float square root
        RSqrt32Fx4, RSqrt32F0x4,                // SSE float reciprocal sqrt
        Recip32Fx4, Recip32F0x4,                // SSE float reciprocal
        AndV128, OrV128, XorV128,               // SSE logical operations
        InterleaveHI32x4, InterleaveLO32x4,     // SSE interleave

        // SSE2/Double precision operations
        Add64Fx2, Add64F0x2,                    // SSE2 double addition
        Sub64Fx2, Sub64F0x2,                    // SSE2 double subtraction
        Mul64Fx2, Mul64F0x2,                    // SSE2 double multiplication
        Div64Fx2, Div64F0x2,                    // SSE2 double division
        Max64Fx2, Max64F0x2,                    // SSE2 double maximum
        Min64Fx2, Min64F0x2,                    // SSE2 double minimum
        Sqrt64Fx2, Sqrt64F0x2,                  // SSE2 double square root
        MulHi16Sx8, MulHi16Ux8,                 // SSE2 multiply high

        // AVX/256-bit SIMD operations
        Add32Fx8, Add64Fx4,                     // AVX float operations
        Sub32Fx8, Sub64Fx4,                     // AVX float operations
        Mul32Fx8, Mul64Fx4,                     // AVX float operations
        Div32Fx8, Div64Fx4,                     // AVX float operations
        AndV256, OrV256, XorV256,               // AVX logical operations

        // Special operations
        CCall,     // Call to clean helper function
        Unknown,   // Unknown/unimplemented operation
    }

    /// <summary>
    /// Extension methods for IROp operations
    /// </summary>
    public static class IROpExtensions
    {
        /// <summary>
        /// Get the arity (number of arguments) for an operation
        /// </summary>
        public static int GetArity(this IROp op)
        {
            // This is a simplified version - the real VEX has complex arity rules
            return op switch
            {
                // Unary operations
                IROp.Not8 or IROp.Not16 or IROp.Not32 or IROp.Not64 or
                IROp.Clz32 or IROp.Clz64 or IROp.Ctz32 or IROp.Ctz64 or
                IROp.CmpNEZ8 or IROp.CmpNEZ16 or IROp.CmpNEZ32 or IROp.CmpNEZ64 or
                IROp.Left8 or IROp.Left16 or IROp.Left32 or IROp.Left64 or
                IROp.F32toF64 or IROp.F64toF32 or
                IROp.ReinterpF64asI64 or IROp.ReinterpI64asF64 or
                IROp.ReinterpF32asI32 or IROp.ReinterpI32asF32 => 1,

                // Binary operations (most common)
                IROp.Add8 or IROp.Add16 or IROp.Add32 or IROp.Add64 or
                IROp.Sub8 or IROp.Sub16 or IROp.Sub32 or IROp.Sub64 or
                IROp.Mul8 or IROp.Mul16 or IROp.Mul32 or IROp.Mul64 or
                IROp.Or8 or IROp.Or16 or IROp.Or32 or IROp.Or64 or
                IROp.And8 or IROp.And16 or IROp.And32 or IROp.And64 or
                IROp.Xor8 or IROp.Xor16 or IROp.Xor32 or IROp.Xor64 or
                IROp.Shl8 or IROp.Shl16 or IROp.Shl32 or IROp.Shl64 or
                IROp.Shr8 or IROp.Shr16 or IROp.Shr32 or IROp.Shr64 or
                IROp.Sar8 or IROp.Sar16 or IROp.Sar32 or IROp.Sar64 or
                IROp.CmpEQ8 or IROp.CmpEQ16 or IROp.CmpEQ32 or IROp.CmpEQ64 or
                IROp.CmpNE8 or IROp.CmpNE16 or IROp.CmpNE32 or IROp.CmpNE64 or
                IROp.AddF32 or IROp.AddF64 or IROp.SubF32 or IROp.SubF64 or
                IROp.MulF32 or IROp.MulF64 or IROp.DivF32 or IROp.DivF64 or
                IROp.Max32U => 2,

                // Ternary operations (some FP operations with rounding mode)
                IROp.F64toI32S or IROp.F64toI64S or IROp.F64toI32U or IROp.F64toI64U => 2, // Actually 2 args (rounding + value)

                // Special cases
                IROp.Invalid or IROp.Unknown => 0,
                
                // Default to binary for now
                _ => 2
            };
        }

        /// <summary>
        /// Check if operation is a comparison
        /// </summary>
        public static bool IsComparison(this IROp op) => op switch
        {
            IROp.CmpEQ8 or IROp.CmpEQ16 or IROp.CmpEQ32 or IROp.CmpEQ64 or
            IROp.CmpNE8 or IROp.CmpNE16 or IROp.CmpNE32 or IROp.CmpNE64 or
            IROp.CmpLT32S or IROp.CmpLT64S or IROp.CmpLE32S or IROp.CmpLE64S or
            IROp.CmpLT32U or IROp.CmpLT64U or IROp.CmpLE32U or IROp.CmpLE64U or
            IROp.CmpNEZ8 or IROp.CmpNEZ16 or IROp.CmpNEZ32 or IROp.CmpNEZ64 or
            IROp.CmpORD32U or IROp.CmpORD64U or IROp.CmpORD32S or IROp.CmpORD64S or
            IROp.CmpF32 or IROp.CmpF64 or IROp.CmpEQF32 or IROp.CmpEQF64 or
            IROp.CmpLTF32 or IROp.CmpLTF64 or IROp.CmpLEF32 or IROp.CmpLEF64 => true,
            _ => false
        };

        /// <summary>
        /// Check if operation is floating point
        /// </summary>
        public static bool IsFloatingPoint(this IROp op) => op switch
        {
            IROp.AddF32 or IROp.AddF64 or IROp.SubF32 or IROp.SubF64 or
            IROp.MulF32 or IROp.MulF64 or IROp.DivF32 or IROp.DivF64 or
            IROp.CmpF32 or IROp.CmpF64 or IROp.CmpEQF32 or IROp.CmpEQF64 or
            IROp.CmpLTF32 or IROp.CmpLTF64 or IROp.CmpLEF32 or IROp.CmpLEF64 or
            IROp.F64toI32S or IROp.F64toI64S or IROp.F64toI32U or IROp.F64toI64U or
            IROp.I32StoF64 or IROp.I64StoF64 or IROp.I32UtoF64 or IROp.I64UtoF64 or
            IROp.F32toF64 or IROp.F64toF32 or
            IROp.ReinterpF64asI64 or IROp.ReinterpI64asF64 or
            IROp.ReinterpF32asI32 or IROp.ReinterpI32asF32 => true,
            _ => false
        };

        /// <summary>
        /// Pretty print an operation
        /// </summary>
        public static string PrettyPrint(this IROp op) => op.ToString();
    }
}