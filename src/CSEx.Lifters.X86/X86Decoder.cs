using System;
using CSEx.Core;
using CSEx.IR;
using CSEx.Guests;
using CSEx.Guests.X86;

namespace CSEx.Lifters.X86
{
    /// <summary>
    /// x86 instruction decoding and operand parsing infrastructure.
    /// Based on VEX guest_x86_toIR.c patterns.
    /// </summary>
    public static class X86Decoder
    {
        /// <summary>
        /// x86 addressing modes for ModR/M byte decoding
        /// </summary>
        public enum AddressingMode
        {
            Register,           // Direct register access
            Memory,             // Memory operand 
            Immediate           // Immediate value
        }

        /// <summary>
        /// x86 instruction categories
        /// </summary>
        public enum InstructionType
        {
            General,            // General-purpose instructions
            X87,                // x87 FPU instructions
            SSE,                // SSE instructions
            MMX                 // MMX instructions
        }

        /// <summary>
        /// x86 operand types
        /// </summary>
        public enum OperandType
        {
            Register,           // Register operand
            Memory,             // Memory operand
            Immediate,          // Immediate value
            Relative,           // Relative offset (for jumps)
            STRegister,         // x87 ST register
            MMXRegister,        // MMX MM register
            XMMRegister,        // SSE XMM register
            YMMRegister,        // AVX YMM register
            ZMMRegister,        // AVX-512 ZMM register
            MaskRegister        // AVX-512 mask register (K0-K7)
        }

        /// <summary>
        /// x86 register encoding for ModR/M bytes
        /// </summary>
        public enum X86Register
        {
            EAX = 0, ECX = 1, EDX = 2, EBX = 3,
            ESP = 4, EBP = 5, ESI = 6, EDI = 7,
            AL = 0, CL = 1, DL = 2, BL = 3,
            AH = 4, CH = 5, DH = 6, BH = 7,
            AX = 0, CX = 1, DX = 2, BX = 3,
            SP = 4, BP = 5, SI = 6, DI = 7
        }

        /// <summary>
        /// Decoded x86 instruction operand
        /// </summary>
        public class X86Operand
        {
            public OperandType Type { get; set; }
            public int Size { get; set; }  // Operand size in bytes
            public X86Register? Register { get; set; }  // Register if Type == Register
            public uint Immediate { get; set; }  // Immediate value if Type == Immediate
            public X86MemoryOperand? Memory { get; set; }  // Memory operand if Type == Memory
            public int STRegister { get; set; }  // ST register number if Type == STRegister (0-7)
            public int MMXRegister { get; set; }  // MMX register number if Type == MMXRegister (0-7)
            public int XMMRegister { get; set; }  // XMM register number if Type == XMMRegister (0-7)
            public int YMMRegister { get; set; }  // YMM register number if Type == YMMRegister (0-7)
            public int ZMMRegister { get; set; }  // ZMM register number if Type == ZMMRegister (0-7)
            public int MaskRegister { get; set; }  // Mask register number if Type == MaskRegister (0-7)
            
            public X86Operand(OperandType type, int size)
            {
                Type = type;
                Size = size;
            }
        }

        /// <summary>
        /// x86 memory operand (base + index*scale + displacement)
        /// </summary>
        public class X86MemoryOperand
        {
            public X86Register? Base { get; set; }
            public X86Register? Index { get; set; }
            public int Scale { get; set; } = 1;  // 1, 2, 4, or 8
            public int Displacement { get; set; }
            public byte? SegmentOverride { get; set; }  // Segment prefix if present
        }

        /// <summary>
        /// Decoded x86 instruction
        /// </summary>
        public class X86Instruction
        {
            public byte Opcode { get; set; }
            public byte? SecondaryOpcode { get; set; }  // For 2-byte opcodes (0x0F prefix)
            public string Mnemonic { get; set; } = "";
            public List<X86Operand> Operands { get; set; } = new List<X86Operand>();
            public int Length { get; set; }  // Total instruction length in bytes
            public bool HasLockPrefix { get; set; }
            public bool Has66Prefix { get; set; }  // Operand size override
            public bool Has67Prefix { get; set; }  // Address size override
            public byte SegmentOverride { get; set; }  // Segment override prefix
            public InstructionType InstructionType { get; set; } = InstructionType.General;
            
            // VEX prefix support (AVX)
            public bool HasVEXPrefix { get; set; }
            public VEXPrefix? VEX { get; set; }
            
            // EVEX prefix support (AVX-512)
            public bool HasEVEXPrefix { get; set; }
            public EVEXPrefix? EVEX { get; set; }
            
            // ModR/M byte components (if present)
            public byte? ModRM { get; set; }
            public byte? SIB { get; set; }  // Scale-Index-Base byte
        }

        /// <summary>
        /// VEX prefix structure for AVX instructions
        /// </summary>
        public class VEXPrefix
        {
            public bool Is3Byte { get; set; }              // True for 3-byte VEX, false for 2-byte
            public byte MapSelect { get; set; }            // Map select field (implied 0x0F for 2-byte)
            public bool W { get; set; }                    // W bit (64-bit operand size when 1)
            public byte vvvv { get; set; }                 // Inverted 4-bit register specifier
            public bool L { get; set; }                    // Vector length (0=128-bit, 1=256-bit)
            public byte pp { get; set; }                   // Prefix presence (equivalent to legacy prefixes)
            
            // 3-byte VEX specific fields
            public bool R { get; set; }                    // Extension of ModR/M reg field (inverted)
            public bool X { get; set; }                    // Extension of SIB index field (inverted)
            public bool B { get; set; }                    // Extension of ModR/M r/m field (inverted)
            
            /// <summary>
            /// Get the actual register number from vvvv field (un-inverted)
            /// </summary>
            public int GetVRegister() => (~vvvv) & 0x0F;
            
            /// <summary>
            /// Convert pp field to equivalent legacy prefix
            /// </summary>
            public X86Prefixes GetImpliedPrefix() => pp switch
            {
                0x00 => X86Prefixes.None,
                0x01 => X86Prefixes.OpSize,    // 0x66
                0x02 => X86Prefixes.Rep,       // 0xF3  
                0x03 => X86Prefixes.RepNE,     // 0xF2
                _ => X86Prefixes.None
            };
        }

        /// <summary>
        /// AVX-512 EVEX prefix structure (4-byte encoding)
        /// </summary>
        public class EVEXPrefix
        {
            // Byte 0: 0x62 (EVEX escape)
            
            // Byte 1: P0 (similar to VEX byte 1)
            public bool R { get; set; }                    // Extension of ModR/M reg field (inverted)
            public bool X { get; set; }                    // Extension of SIB index field (inverted)  
            public bool B { get; set; }                    // Extension of ModR/M r/m field (inverted)
            public bool R2 { get; set; }                   // High-16 register extension (inverted)
            public byte MapSelect { get; set; }            // Opcode map select (2 bits)
            
            // Byte 2: P1 (enhanced VEX-like)
            public bool W { get; set; }                    // 64-bit operand size when 1
            public byte vvvv { get; set; }                 // Inverted 4-bit register specifier
            public bool V2 { get; set; }                   // High-16 register extension for vvvv (inverted)
            public byte pp { get; set; }                   // Prefix presence (2 bits)
            
            // Byte 3: P2 (AVX-512 specific)
            public bool z { get; set; }                    // Zeroing/merging masking
            public byte LL { get; set; }                   // Vector length (2 bits: 00=128, 01=256, 10=512)
            public bool b { get; set; }                    // Broadcast/RC/SAE context
            public bool V3 { get; set; }                   // High-16 register extension for V2 (inverted)
            public byte aaa { get; set; }                  // Embedded opmask register (3 bits)
            
            /// <summary>
            /// Get the actual register number from vvvv field (un-inverted)
            /// </summary>
            public int GetVRegister() => (~vvvv) & 0x0F;
            
            /// <summary>
            /// Get extended register number including high-16 extensions
            /// </summary>
            public int GetExtendedVRegister() => 
                ((!V3 ? 1 : 0) << 4) | ((!V2 ? 1 : 0) << 3) | ((~vvvv) & 0x07);
            
            /// <summary>
            /// Convert pp field to equivalent legacy prefix
            /// </summary>
            public X86Prefixes GetImpliedPrefix() => pp switch
            {
                0x00 => X86Prefixes.None,
                0x01 => X86Prefixes.OpSize,    // 0x66
                0x02 => X86Prefixes.Rep,       // 0xF3  
                0x03 => X86Prefixes.RepNE,     // 0xF2
                _ => X86Prefixes.None
            };
            
            /// <summary>
            /// Get vector length in bits
            /// </summary>
            public int GetVectorLength() => LL switch
            {
                0x00 => 128,
                0x01 => 256, 
                0x02 => 512,
                _ => 128  // Reserved, default to 128
            };
            
            /// <summary>
            /// Check if instruction uses masking
            /// </summary>
            public bool HasMasking() => aaa != 0;
            
            /// <summary>
            /// Get mask register number (K0-K7)
            /// </summary>
            public int GetMaskRegister() => aaa & 0x07;
        }

        /// <summary>
        /// x86 instruction prefixes
        /// </summary>
        [Flags]
        public enum X86Prefixes
        {
            None = 0,
            Lock = 0x01,        // 0xF0 - LOCK prefix
            RepNE = 0x02,       // 0xF2 - REPNE/REPNZ
            Rep = 0x04,         // 0xF3 - REP/REPE/REPZ
            CS = 0x08,          // 0x2E - CS segment override
            SS = 0x10,          // 0x36 - SS segment override  
            DS = 0x20,          // 0x3E - DS segment override
            ES = 0x40,          // 0x26 - ES segment override
            FS = 0x80,          // 0x64 - FS segment override
            GS = 0x100,         // 0x65 - GS segment override
            OpSize = 0x200,     // 0x66 - Operand size override
            AddrSize = 0x400    // 0x67 - Address size override
        }

        /// <summary>
        /// Decode ModR/M byte components
        /// </summary>
        public static (byte mod, byte reg, byte rm) DecodeModRM(byte modRM)
        {
            byte mod = (byte)((modRM >> 6) & 0x03);
            byte reg = (byte)((modRM >> 3) & 0x07);
            byte rm = (byte)(modRM & 0x07);
            return (mod, reg, rm);
        }

        /// <summary>
        /// Decode SIB (Scale-Index-Base) byte
        /// </summary>
        public static (byte scale, byte index, byte @base) DecodeSIB(byte sib)
        {
            byte scale = (byte)((sib >> 6) & 0x03);
            byte index = (byte)((sib >> 3) & 0x07);
            byte @base = (byte)(sib & 0x07);
            return (scale, index, @base);
        }

        /// <summary>
        /// Get register name for given encoding and size
        /// </summary>
        public static string GetRegisterName(byte reg, int size)
        {
            return size switch
            {
                1 => reg switch
                {
                    0 => "AL", 1 => "CL", 2 => "DL", 3 => "BL",
                    4 => "AH", 5 => "CH", 6 => "DH", 7 => "BH",
                    _ => $"?{reg}"
                },
                2 => reg switch
                {
                    0 => "AX", 1 => "CX", 2 => "DX", 3 => "BX",
                    4 => "SP", 5 => "BP", 6 => "SI", 7 => "DI",
                    _ => $"?{reg}"
                },
                4 => reg switch
                {
                    0 => "EAX", 1 => "ECX", 2 => "EDX", 3 => "EBX",
                    4 => "ESP", 5 => "EBP", 6 => "ESI", 7 => "EDI",
                    _ => $"?{reg}"
                },
                _ => $"?{reg}"
            };
        }

        /// <summary>
        /// Convert register encoding to X86Register enum
        /// </summary>
        public static X86Register GetRegister(byte reg, int size)
        {
            if (size == 1)
            {
                return (X86Register)reg;  // AL, CL, DL, BL, AH, CH, DH, BH
            }
            else if (size == 2)
            {
                return (X86Register)reg;  // AX, CX, DX, BX, SP, BP, SI, DI
            }
            else // size == 4
            {
                return (X86Register)reg;  // EAX, ECX, EDX, EBX, ESP, EBP, ESI, EDI
            }
        }
    }
}