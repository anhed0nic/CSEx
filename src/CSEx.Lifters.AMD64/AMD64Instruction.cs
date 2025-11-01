using System;

namespace CSEx.Lifters.AMD64
{
    /// <summary>
    /// AMD64 instruction representation with x86-64 specific features
    /// Extends x86 instruction format with REX prefix and 64-bit addressing
    /// </summary>
    public class AMD64Instruction
    {
        /// <summary>
        /// Instruction mnemonic (e.g., "mov", "add", "syscall")
        /// </summary>
        public string Mnemonic { get; set; } = "";
        
        /// <summary>
        /// Total instruction length in bytes
        /// </summary>
        public int Length { get; set; }
        
        /// <summary>
        /// Raw instruction bytes
        /// </summary>
        public byte[] Bytes { get; set; } = Array.Empty<byte>();
        
        /// <summary>
        /// Instruction operands
        /// </summary>
        public AMD64Operand[] Operands { get; set; } = Array.Empty<AMD64Operand>();
        
        /// <summary>
        /// REX prefix information (if present)
        /// </summary>
        public REXPrefix REXPrefix { get; set; } = new REXPrefix();
        
        /// <summary>
        /// Whether this instruction has a REX prefix
        /// </summary>
        public bool HasREXPrefix { get; set; }
        
        /// <summary>
        /// Whether this instruction is AMD64-only (not available in 32-bit mode)
        /// </summary>
        public bool IsAMD64Only { get; set; }
        
        /// <summary>
        /// Whether this instruction uses RIP-relative addressing
        /// </summary>
        public bool HasRIPRelativeAddressing { get; set; }
        
        /// <summary>
        /// RIP-relative displacement value
        /// </summary>
        public int RIPDisplacement { get; set; }
        
        /// <summary>
        /// Default operand size for this instruction
        /// </summary>
        public OperandSize DefaultOperandSize { get; set; } = OperandSize.Size32;
        
        /// <summary>
        /// Address size for this instruction
        /// </summary>
        public OperandSize AddressSize { get; set; } = OperandSize.Size64;
    }

    /// <summary>
    /// REX prefix structure for AMD64 instructions
    /// </summary>
    public struct REXPrefix
    {
        /// <summary>
        /// REX.W - 64-bit operand size
        /// </summary>
        public bool W { get; set; }
        
        /// <summary>
        /// REX.R - Extension of ModR/M reg field
        /// </summary>
        public bool R { get; set; }
        
        /// <summary>
        /// REX.X - Extension of SIB index field
        /// </summary>
        public bool X { get; set; }
        
        /// <summary>
        /// REX.B - Extension of ModR/M r/m field, SIB base field, or opcode reg field
        /// </summary>
        public bool B { get; set; }
        
        /// <summary>
        /// Raw REX byte value
        /// </summary>
        public byte RawValue { get; set; }
        
        public REXPrefix(byte rexByte)
        {
            RawValue = rexByte;
            W = (rexByte & 0x08) != 0;
            R = (rexByte & 0x04) != 0;
            X = (rexByte & 0x02) != 0;
            B = (rexByte & 0x01) != 0;
        }
    }

    /// <summary>
    /// AMD64 instruction operand
    /// </summary>
    public class AMD64Operand
    {
        /// <summary>
        /// Operand type
        /// </summary>
        public AMD64OperandType Type { get; set; }
        
        /// <summary>
        /// Operand size
        /// </summary>
        public OperandSize Size { get; set; }
        
        /// <summary>
        /// Register name (for register operands)
        /// </summary>
        public string Register { get; set; } = "";
        
        /// <summary>
        /// Immediate value (for immediate operands)
        /// </summary>
        public long ImmediateValue { get; set; }
        
        /// <summary>
        /// Memory operand information
        /// </summary>
        public MemoryOperand? Memory { get; set; }
        
        /// <summary>
        /// Whether this operand uses extended register (R8-R15)
        /// </summary>
        public bool IsExtendedRegister { get; set; }
    }

    /// <summary>
    /// AMD64 operand types
    /// </summary>
    public enum AMD64OperandType
    {
        None,
        Register,
        Immediate,
        Memory,
        RIPRelative
    }

    /// <summary>
    /// Memory operand structure
    /// </summary>
    public struct MemoryOperand
    {
        /// <summary>
        /// Base register
        /// </summary>
        public string Base { get; set; }
        
        /// <summary>
        /// Index register
        /// </summary>
        public string Index { get; set; }
        
        /// <summary>
        /// Scale factor (1, 2, 4, 8)
        /// </summary>
        public int Scale { get; set; }
        
        /// <summary>
        /// Displacement value
        /// </summary>
        public long Displacement { get; set; }
        
        /// <summary>
        /// Whether this uses RIP-relative addressing
        /// </summary>
        public bool IsRIPRelative { get; set; }
    }

    /// <summary>
    /// Operand size enumeration
    /// </summary>
    public enum OperandSize
    {
        Size8 = 1,
        Size16 = 2,
        Size32 = 4,
        Size64 = 8,
        Size128 = 16,
        Size256 = 32,
        Size512 = 64
    }
}