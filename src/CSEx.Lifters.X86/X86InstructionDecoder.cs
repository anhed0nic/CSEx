using System;
using System.Collections.Generic;
using CSEx.Core;
using CSEx.IR;
using CSEx.Guests;
using CSEx.Guests.X86;

namespace CSEx.Lifters.X86
{
    /// <summary>
    /// x86 instruction decoder that parses instruction bytes into structured instruction objects.
    /// Implements the decoding patterns from VEX guest_x86_toIR.c
    /// </summary>
    public class X86InstructionDecoder
    {
        private readonly byte[] _code;
        private int _position;

        public X86InstructionDecoder(byte[] code)
        {
            _code = code ?? throw new ArgumentNullException(nameof(code));
            _position = 0;
        }

        /// <summary>
        /// Decode a single x86 instruction at the current position
        /// </summary>
        public X86Decoder.X86Instruction? DecodeInstruction()
        {
            if (_position >= _code.Length)
                return null;

            int startPosition = _position;
            var instruction = new X86Decoder.X86Instruction();
            
            // Parse prefixes
            var prefixes = ParsePrefixes(instruction);
            
            // Handle VEX-encoded instructions
            if (instruction.HasVEXPrefix)
            {
                return DecodeVEXInstruction(instruction, startPosition);
            }
            
            // Handle EVEX-encoded instructions (AVX-512)
            if (instruction.HasEVEXPrefix)
            {
                return DecodeEVEXInstruction(instruction, startPosition);
            }
            
            // Get the opcode for legacy-encoded instructions
            if (_position >= _code.Length)
                return null;
                
            byte opcode = _code[_position++];
            instruction.Opcode = opcode;

            // Handle 2-byte opcodes (0x0F escape)
            if (opcode == 0x0F)
            {
                if (_position >= _code.Length)
                    return null;
                instruction.SecondaryOpcode = _code[_position++];
                // Note: Don't overwrite opcode - let DecodeInstructionByOpcode handle case 0x0F
            }

            // Decode the instruction based on opcode
            if (!DecodeInstructionByOpcode(instruction, opcode))
                return null;

            instruction.Length = _position - startPosition;
            return instruction;
        }

        /// <summary>
        /// Parse instruction prefixes
        /// </summary>
        private X86Decoder.X86Prefixes ParsePrefixes(X86Decoder.X86Instruction instruction)
        {
            var prefixes = X86Decoder.X86Prefixes.None;
            
            while (_position < _code.Length)
            {
                byte b = _code[_position];
                
                // Check for VEX prefixes first (C4h = 3-byte VEX, C5h = 2-byte VEX)
                if (b == 0xC4 || b == 0xC5)
                {
                    var vex = ParseVEXPrefix(b == 0xC4);
                    if (vex != null)
                    {
                        instruction.HasVEXPrefix = true;
                        instruction.VEX = vex;
                        instruction.InstructionType = X86Decoder.InstructionType.SSE; // AVX instructions are SSE type
                        return vex.GetImpliedPrefix(); // VEX replaces legacy prefixes
                    }
                    // If VEX parsing failed, treat as regular instruction
                }
                
                // Check for EVEX prefix (62h = 4-byte EVEX for AVX-512)
                if (b == 0x62)
                {
                    var evex = ParseEVEXPrefix();
                    if (evex != null)
                    {
                        instruction.HasEVEXPrefix = true;
                        instruction.EVEX = evex;
                        instruction.InstructionType = X86Decoder.InstructionType.SSE; // AVX-512 instructions are SSE type
                        return evex.GetImpliedPrefix(); // EVEX replaces legacy prefixes
                    }
                    // If EVEX parsing failed, treat as regular instruction
                }
                
                switch (b)
                {
                    case 0xF0: // LOCK
                        instruction.HasLockPrefix = true;
                        prefixes |= X86Decoder.X86Prefixes.Lock;
                        _position++;
                        break;
                    case 0xF2: // REPNE/REPNZ
                        prefixes |= X86Decoder.X86Prefixes.RepNE;
                        _position++;
                        break;
                    case 0xF3: // REP/REPE/REPZ
                        prefixes |= X86Decoder.X86Prefixes.Rep;
                        _position++;
                        break;
                    case 0x2E: // CS override
                        instruction.SegmentOverride = b;
                        prefixes |= X86Decoder.X86Prefixes.CS;
                        _position++;
                        break;
                    case 0x36: // SS override
                        instruction.SegmentOverride = b;
                        prefixes |= X86Decoder.X86Prefixes.SS;
                        _position++;
                        break;
                    case 0x3E: // DS override
                        instruction.SegmentOverride = b;
                        prefixes |= X86Decoder.X86Prefixes.DS;
                        _position++;
                        break;
                    case 0x26: // ES override
                        instruction.SegmentOverride = b;
                        prefixes |= X86Decoder.X86Prefixes.ES;
                        _position++;
                        break;
                    case 0x64: // FS override
                        instruction.SegmentOverride = b;
                        prefixes |= X86Decoder.X86Prefixes.FS;
                        _position++;
                        break;
                    case 0x65: // GS override
                        instruction.SegmentOverride = b;
                        prefixes |= X86Decoder.X86Prefixes.GS;
                        _position++;
                        break;
                    case 0x66: // Operand size override
                        instruction.Has66Prefix = true;
                        prefixes |= X86Decoder.X86Prefixes.OpSize;
                        _position++;
                        break;
                    case 0x67: // Address size override
                        instruction.Has67Prefix = true;
                        prefixes |= X86Decoder.X86Prefixes.AddrSize;
                        _position++;
                        break;
                    default:
                        return prefixes; // Not a prefix, return
                }
            }
            
            return prefixes;
        }

        /// <summary>
        /// Parse VEX prefix (2-byte or 3-byte format)
        /// </summary>
        private X86Decoder.VEXPrefix? ParseVEXPrefix(bool is3Byte)
        {
            if (is3Byte)
            {
                // 3-byte VEX: C4 <byte1> <byte2>
                if (_position + 2 >= _code.Length)
                    return null;
                    
                _position++; // Skip C4
                byte byte1 = _code[_position++];
                byte byte2 = _code[_position++];
                
                return new X86Decoder.VEXPrefix
                {
                    Is3Byte = true,
                    R = (byte1 & 0x80) == 0,           // Bit 7 inverted
                    X = (byte1 & 0x40) == 0,           // Bit 6 inverted
                    B = (byte1 & 0x20) == 0,           // Bit 5 inverted
                    MapSelect = (byte)(byte1 & 0x1F),  // Bits 4:0
                    W = (byte2 & 0x80) != 0,           // Bit 7
                    vvvv = (byte)((byte2 >> 3) & 0x0F), // Bits 6:3 (inverted)
                    L = (byte2 & 0x04) != 0,           // Bit 2
                    pp = (byte)(byte2 & 0x03)          // Bits 1:0
                };
            }
            else
            {
                // 2-byte VEX: C5 <byte1>
                if (_position + 1 >= _code.Length)
                    return null;
                    
                _position++; // Skip C5
                byte byte1 = _code[_position++];
                
                return new X86Decoder.VEXPrefix
                {
                    Is3Byte = false,
                    R = (byte1 & 0x80) == 0,           // Bit 7 inverted
                    X = false,                         // Not present in 2-byte
                    B = false,                         // Not present in 2-byte
                    MapSelect = 0x01,                  // Implied 0x0F map
                    W = false,                         // Not present in 2-byte
                    vvvv = (byte)((byte1 >> 3) & 0x0F), // Bits 6:3 (inverted)
                    L = (byte1 & 0x04) != 0,           // Bit 2
                    pp = (byte)(byte1 & 0x03)          // Bits 1:0
                };
            }
        }

        /// <summary>
        /// Parse EVEX prefix (4-byte format for AVX-512)
        /// </summary>
        private X86Decoder.EVEXPrefix? ParseEVEXPrefix()
        {
            // 4-byte EVEX: 62 <P0> <P1> <P2>
            if (_position + 3 >= _code.Length)
                return null;
                
            _position++; // Skip 62h
            byte p0 = _code[_position++];
            byte p1 = _code[_position++];
            byte p2 = _code[_position++];
            
            // Validate EVEX encoding requirements
            // P0[1:0] must be 00b for EVEX
            if ((p0 & 0x03) != 0x00)
                return null;
            
            return new X86Decoder.EVEXPrefix
            {
                // P0 byte
                R = (p0 & 0x80) == 0,               // Bit 7 inverted
                X = (p0 & 0x40) == 0,               // Bit 6 inverted
                B = (p0 & 0x20) == 0,               // Bit 5 inverted
                R2 = (p0 & 0x10) == 0,              // Bit 4 inverted (high-16 reg)
                MapSelect = (byte)((p0 >> 2) & 0x03), // Bits 3:2
                
                // P1 byte
                W = (p1 & 0x80) != 0,               // Bit 7
                vvvv = (byte)((p1 >> 3) & 0x0F),    // Bits 6:3 (inverted)
                V2 = (p1 & 0x04) == 0,              // Bit 2 inverted (high-16 reg)
                pp = (byte)(p1 & 0x03),             // Bits 1:0
                
                // P2 byte
                z = (p2 & 0x80) != 0,               // Bit 7 (zeroing)
                LL = (byte)((p2 >> 5) & 0x03),      // Bits 6:5 (vector length)
                b = (p2 & 0x10) != 0,               // Bit 4 (broadcast/RC/SAE)
                V3 = (p2 & 0x08) == 0,              // Bit 3 inverted (high-16 reg)
                aaa = (byte)(p2 & 0x07)             // Bits 2:0 (mask register)
            };
        }

        /// <summary>
        /// Decode VEX-encoded AVX instruction
        /// </summary>
        private X86Decoder.X86Instruction? DecodeVEXInstruction(X86Decoder.X86Instruction instruction, int startPosition)
        {
            if (instruction.VEX == null)
                return null;
                
            // Set current instruction for mnemonic selection
            _currentInstruction = instruction;
                
            // Get the opcode byte that follows VEX prefix
            if (_position >= _code.Length)
                return null;
                
            byte opcode = _code[_position++];
            instruction.Opcode = opcode;
            
            // VEX-encoded instructions use the map select field to determine opcode space
            // Map 1 = 0x0F (2-byte opcodes), Map 2 = 0x0F38, Map 3 = 0x0F3A
            switch (instruction.VEX.MapSelect)
            {
                case 0x01: // 0x0F map (most common AVX instructions)
                    if (!DecodeVEXOpcode0F(instruction, opcode))
                        return null;
                    break;
                case 0x02: // 0x0F38 map  
                    if (!DecodeVEXOpcode0F38(instruction, opcode))
                        return null;
                    break;
                case 0x03: // 0x0F3A map
                    if (!DecodeVEXOpcode0F3A(instruction, opcode))
                        return null;
                    break;
                default:
                    return null; // Unsupported map
            }
            
            instruction.Length = _position - startPosition;
            return instruction;
        }

        /// <summary>
        /// Decode EVEX-encoded AVX-512 instruction
        /// </summary>
        private X86Decoder.X86Instruction? DecodeEVEXInstruction(X86Decoder.X86Instruction instruction, int startPosition)
        {
            if (instruction.EVEX == null)
                return null;
                
            // Set current instruction for mnemonic selection
            _currentInstruction = instruction;
                
            // Get the opcode byte that follows EVEX prefix
            if (_position >= _code.Length)
                return null;
                
            byte opcode = _code[_position++];
            instruction.Opcode = opcode;
            
            // EVEX-encoded instructions use the map select field to determine opcode space
            // Map 1 = 0x0F (2-byte opcodes), Map 2 = 0x0F38, Map 3 = 0x0F3A
            switch (instruction.EVEX.MapSelect)
            {
                case 0x01: // 0x0F map (most common AVX-512 instructions)
                    if (!DecodeEVEXOpcode0F(instruction, opcode))
                        return null;
                    break;
                case 0x02: // 0x0F38 map  
                    if (!DecodeEVEXOpcode0F38(instruction, opcode))
                        return null;
                    break;
                case 0x03: // 0x0F3A map
                    if (!DecodeEVEXOpcode0F3A(instruction, opcode))
                        return null;
                    break;
                default:
                    return null; // Unsupported map
            }
            
            instruction.Length = _position - startPosition;
            return instruction;
        }

        /// <summary>
        /// Decode instruction based on primary opcode
        /// </summary>
        private bool DecodeInstructionByOpcode(X86Decoder.X86Instruction instruction, byte opcode)
        {
            // Determine operand size (default 4 bytes, 2 bytes with 0x66 prefix)
            int operandSize = instruction.Has66Prefix ? 2 : 4;
            
            switch (opcode)
            {
                // ADD instructions
                case 0x00: return DecodeModRMInstruction(instruction, "add", 1, false); // ADD Eb,Gb
                case 0x01: return DecodeModRMInstruction(instruction, "add", operandSize, false); // ADD Ev,Gv
                case 0x02: return DecodeModRMInstruction(instruction, "add", 1, true); // ADD Gb,Eb  
                case 0x03: return DecodeModRMInstruction(instruction, "add", operandSize, true); // ADD Gv,Ev
                case 0x04: return DecodeImmediateALInstruction(instruction, "add", 1); // ADD AL,Ib
                case 0x05: return DecodeImmediateAXInstruction(instruction, "add", operandSize); // ADD eAX,Iv

                // OR instructions
                case 0x08: return DecodeModRMInstruction(instruction, "or", 1, false); // OR Eb,Gb
                case 0x09: return DecodeModRMInstruction(instruction, "or", operandSize, false); // OR Ev,Gv
                case 0x0A: return DecodeModRMInstruction(instruction, "or", 1, true); // OR Gb,Eb
                case 0x0B: return DecodeModRMInstruction(instruction, "or", operandSize, true); // OR Gv,Ev
                case 0x0C: return DecodeImmediateALInstruction(instruction, "or", 1); // OR AL,Ib
                case 0x0D: return DecodeImmediateAXInstruction(instruction, "or", operandSize); // OR eAX,Iv

                // ADC instructions
                case 0x10: return DecodeModRMInstruction(instruction, "adc", 1, false); // ADC Eb,Gb
                case 0x11: return DecodeModRMInstruction(instruction, "adc", operandSize, false); // ADC Ev,Gv
                case 0x12: return DecodeModRMInstruction(instruction, "adc", 1, true); // ADC Gb,Eb
                case 0x13: return DecodeModRMInstruction(instruction, "adc", operandSize, true); // ADC Gv,Ev
                case 0x14: return DecodeImmediateALInstruction(instruction, "adc", 1); // ADC AL,Ib
                case 0x15: return DecodeImmediateAXInstruction(instruction, "adc", operandSize); // ADC eAX,Iv

                // SBB instructions
                case 0x18: return DecodeModRMInstruction(instruction, "sbb", 1, false); // SBB Eb,Gb
                case 0x19: return DecodeModRMInstruction(instruction, "sbb", operandSize, false); // SBB Ev,Gv
                case 0x1A: return DecodeModRMInstruction(instruction, "sbb", 1, true); // SBB Gb,Eb
                case 0x1B: return DecodeModRMInstruction(instruction, "sbb", operandSize, true); // SBB Gv,Ev
                case 0x1C: return DecodeImmediateALInstruction(instruction, "sbb", 1); // SBB AL,Ib
                case 0x1D: return DecodeImmediateAXInstruction(instruction, "sbb", operandSize); // SBB eAX,Iv

                // AND instructions
                case 0x20: return DecodeModRMInstruction(instruction, "and", 1, false); // AND Eb,Gb
                case 0x21: return DecodeModRMInstruction(instruction, "and", operandSize, false); // AND Ev,Gv
                case 0x22: return DecodeModRMInstruction(instruction, "and", 1, true); // AND Gb,Eb
                case 0x23: return DecodeModRMInstruction(instruction, "and", operandSize, true); // AND Gv,Ev
                case 0x24: return DecodeImmediateALInstruction(instruction, "and", 1); // AND AL,Ib
                case 0x25: return DecodeImmediateAXInstruction(instruction, "and", operandSize); // AND eAX,Iv

                // SUB instructions
                case 0x28: return DecodeModRMInstruction(instruction, "sub", 1, false); // SUB Eb,Gb
                case 0x29: return DecodeModRMInstruction(instruction, "sub", operandSize, false); // SUB Ev,Gv
                case 0x2A: return DecodeModRMInstruction(instruction, "sub", 1, true); // SUB Gb,Eb
                case 0x2B: return DecodeModRMInstruction(instruction, "sub", operandSize, true); // SUB Gv,Ev
                case 0x2C: return DecodeImmediateALInstruction(instruction, "sub", 1); // SUB AL,Ib
                case 0x2D: return DecodeImmediateAXInstruction(instruction, "sub", operandSize); // SUB eAX,Iv

                // XOR instructions
                case 0x30: return DecodeModRMInstruction(instruction, "xor", 1, false); // XOR Eb,Gb
                case 0x31: return DecodeModRMInstruction(instruction, "xor", operandSize, false); // XOR Ev,Gv
                case 0x32: return DecodeModRMInstruction(instruction, "xor", 1, true); // XOR Gb,Eb
                case 0x33: return DecodeModRMInstruction(instruction, "xor", operandSize, true); // XOR Gv,Ev
                case 0x34: return DecodeImmediateALInstruction(instruction, "xor", 1); // XOR AL,Ib
                case 0x35: return DecodeImmediateAXInstruction(instruction, "xor", operandSize); // XOR eAX,Iv

                // CMP instructions
                case 0x38: return DecodeModRMInstruction(instruction, "cmp", 1, false); // CMP Eb,Gb
                case 0x39: return DecodeModRMInstruction(instruction, "cmp", operandSize, false); // CMP Ev,Gv
                case 0x3A: return DecodeModRMInstruction(instruction, "cmp", 1, true); // CMP Gb,Eb
                case 0x3B: return DecodeModRMInstruction(instruction, "cmp", operandSize, true); // CMP Gv,Ev
                case 0x3C: return DecodeImmediateALInstruction(instruction, "cmp", 1); // CMP AL,Ib
                case 0x3D: return DecodeImmediateAXInstruction(instruction, "cmp", operandSize); // CMP eAX,Iv

                // INC/DEC instructions
                case 0x40: case 0x41: case 0x42: case 0x43: // INC reg32
                case 0x44: case 0x45: case 0x46: case 0x47:
                    return DecodeRegisterInstruction(instruction, "inc", (byte)(opcode & 0x07), operandSize);
                case 0x48: case 0x49: case 0x4A: case 0x4B: // DEC reg32
                case 0x4C: case 0x4D: case 0x4E: case 0x4F:
                    return DecodeRegisterInstruction(instruction, "dec", (byte)(opcode & 0x07), operandSize);

                // PUSH/POP instructions
                case 0x50: case 0x51: case 0x52: case 0x53: // PUSH reg32
                case 0x54: case 0x55: case 0x56: case 0x57:
                    return DecodeRegisterInstruction(instruction, "push", (byte)(opcode & 0x07), operandSize);
                case 0x58: case 0x59: case 0x5A: case 0x5B: // POP reg32
                case 0x5C: case 0x5D: case 0x5E: case 0x5F:
                    return DecodeRegisterInstruction(instruction, "pop", (byte)(opcode & 0x07), operandSize);

                // Conditional jumps (short)
                case 0x70: return DecodeRelativeInstruction(instruction, "jo", 1); // JO rel8
                case 0x71: return DecodeRelativeInstruction(instruction, "jno", 1); // JNO rel8
                case 0x72: return DecodeRelativeInstruction(instruction, "jb", 1); // JB/JNAE/JC rel8
                case 0x73: return DecodeRelativeInstruction(instruction, "jnb", 1); // JNB/JAE/JNC rel8
                case 0x74: return DecodeRelativeInstruction(instruction, "je", 1); // JE/JZ rel8
                case 0x75: return DecodeRelativeInstruction(instruction, "jne", 1); // JNE/JNZ rel8
                case 0x76: return DecodeRelativeInstruction(instruction, "jbe", 1); // JBE/JNA rel8
                case 0x77: return DecodeRelativeInstruction(instruction, "ja", 1); // JA/JNBE rel8
                case 0x78: return DecodeRelativeInstruction(instruction, "js", 1); // JS rel8
                case 0x79: return DecodeRelativeInstruction(instruction, "jns", 1); // JNS rel8
                case 0x7A: return DecodeRelativeInstruction(instruction, "jp", 1); // JP/JPE rel8
                case 0x7B: return DecodeRelativeInstruction(instruction, "jnp", 1); // JNP/JPO rel8
                case 0x7C: return DecodeRelativeInstruction(instruction, "jl", 1); // JL/JNGE rel8
                case 0x7D: return DecodeRelativeInstruction(instruction, "jnl", 1); // JNL/JGE rel8
                case 0x7E: return DecodeRelativeInstruction(instruction, "jle", 1); // JLE/JNG rel8
                case 0x7F: return DecodeRelativeInstruction(instruction, "jg", 1); // JG/JNLE rel8

                // TEST instructions
                case 0x84: return DecodeModRMInstruction(instruction, "test", 1, true); // TEST Eb,Gb
                case 0x85: return DecodeModRMInstruction(instruction, "test", operandSize, true); // TEST Ev,Gv
                case 0xA8: return DecodeImmediateALInstruction(instruction, "test", 1); // TEST AL,Ib
                case 0xA9: return DecodeImmediateAXInstruction(instruction, "test", operandSize); // TEST eAX,Iv

                // Sign extension instructions
                case 0x99: return DecodeSimpleInstruction(instruction, "cdq"); // CDQ - Convert doubleword to quadword

                // MOV instructions
                case 0x88: return DecodeModRMInstruction(instruction, "mov", 1, false); // MOV Eb,Gb
                case 0x89: return DecodeModRMInstruction(instruction, "mov", operandSize, false); // MOV Ev,Gv
                case 0x8A: return DecodeModRMInstruction(instruction, "mov", 1, true); // MOV Gb,Eb
                case 0x8B: return DecodeModRMInstruction(instruction, "mov", operandSize, true); // MOV Gv,Ev
                
                // LEA instruction - Load Effective Address
                case 0x8D: return DecodeModRMInstruction(instruction, "lea", operandSize, true); // LEA Gv,Ev

                // Single-byte register MOV instructions (0xB0-0xBF)
                case 0xB0: case 0xB1: case 0xB2: case 0xB3: // MOV reg8, imm8
                case 0xB4: case 0xB5: case 0xB6: case 0xB7:
                    return DecodeRegisterImmediateInstruction(instruction, "mov", (byte)(opcode & 0x07), 1);
                    
                case 0xB8: case 0xB9: case 0xBA: case 0xBB: // MOV reg32, imm32
                case 0xBC: case 0xBD: case 0xBE: case 0xBF:
                    return DecodeRegisterImmediateInstruction(instruction, "mov", (byte)(opcode & 0x07), operandSize);

                // Control flow instructions
                case 0xC3: return DecodeSimpleInstruction(instruction, "ret"); // RET
                case 0xC2: return DecodeRetInstruction(instruction, "ret"); // RET imm16
                case 0xE8: return DecodeRelativeInstruction(instruction, "call", operandSize); // CALL rel32
                case 0xE9: return DecodeRelativeInstruction(instruction, "jmp", operandSize); // JMP rel32
                case 0xEB: return DecodeRelativeInstruction(instruction, "jmp", 1); // JMP rel8

                // Group 2 instructions (0xD0-0xD3 - shift and rotate)
                case 0xD0: return DecodeGroup2Instruction(instruction, 1); // Group 2 Eb, 1
                case 0xD1: return DecodeGroup2Instruction(instruction, operandSize); // Group 2 Ev, 1
                case 0xD2: return DecodeGroup2InstructionCL(instruction, 1); // Group 2 Eb, CL
                case 0xD3: return DecodeGroup2InstructionCL(instruction, operandSize); // Group 2 Ev, CL

                // Group 3 instructions (0xF6, 0xF7)
                case 0xF6: return DecodeGroup3Instruction(instruction, 1); // Group 3 Eb
                case 0xF7: return DecodeGroup3Instruction(instruction, operandSize); // Group 3 Ev

                // System instructions
                case 0xF4: return DecodeSimpleInstruction(instruction, "hlt"); // HLT - Halt

                // x87 FPU instructions (0xD8-0xDF)
                case 0xD8: return DecodeX87Instruction(instruction, 0xD8, operandSize);
                case 0xD9: return DecodeX87Instruction(instruction, 0xD9, operandSize);
                case 0xDA: return DecodeX87Instruction(instruction, 0xDA, operandSize);
                case 0xDB: return DecodeX87Instruction(instruction, 0xDB, operandSize);
                case 0xDC: return DecodeX87Instruction(instruction, 0xDC, operandSize);
                case 0xDD: return DecodeX87Instruction(instruction, 0xDD, operandSize);
                case 0xDE: return DecodeX87Instruction(instruction, 0xDE, operandSize);
                case 0xDF: return DecodeX87Instruction(instruction, 0xDF, operandSize);

                // Two-byte opcodes (0x0F prefix)
                case 0x0F: return DecodeTwoByteInstruction(instruction, operandSize);

                default:
                    // Unsupported opcode
                    return false;
            }
        }

        /// <summary>
        /// Decode instruction with ModR/M byte (most common x86 pattern)
        /// </summary>
        private bool DecodeModRMInstruction(X86Decoder.X86Instruction instruction, string mnemonic, int size, bool regFirst)
        {
            if (_position >= _code.Length)
                return false;

            instruction.Mnemonic = mnemonic;
            byte modRM = _code[_position++];
            instruction.ModRM = modRM;

            var (mod, reg, rm) = X86Decoder.DecodeModRM(modRM);

            // Create register operand
            var regOperand = new X86Decoder.X86Operand(X86Decoder.OperandType.Register, size)
            {
                Register = X86Decoder.GetRegister(reg, size)
            };

            // Create second operand (register or memory)
            X86Decoder.X86Operand? secondOperand;
            if (mod == 3) // Register mode
            {
                secondOperand = new X86Decoder.X86Operand(X86Decoder.OperandType.Register, size)
                {
                    Register = X86Decoder.GetRegister(rm, size)
                };
            }
            else // Memory mode
            {
                secondOperand = DecodeMemoryOperand(mod, rm, size);
                if (secondOperand == null)
                    return false;
            }

            // Add operands in correct order
            if (regFirst)
            {
                instruction.Operands.Add(regOperand);
                instruction.Operands.Add(secondOperand);
            }
            else
            {
                instruction.Operands.Add(secondOperand);
                instruction.Operands.Add(regOperand);
            }

            return true;
        }

        /// <summary>
        /// Decode memory operand from ModR/M and SIB bytes
        /// </summary>
        private X86Decoder.X86Operand? DecodeMemoryOperand(byte mod, byte rm, int size)
        {
            var operand = new X86Decoder.X86Operand(X86Decoder.OperandType.Memory, size)
            {
                Memory = new X86Decoder.X86MemoryOperand()
            };

            // Handle SIB byte case (rm == 4 in 32-bit mode)
            if (rm == 4 && mod != 3)
            {
                if (_position >= _code.Length)
                    return null;

                byte sib = _code[_position++];
                var (scale, index, @base) = X86Decoder.DecodeSIB(sib);

                operand.Memory.Scale = 1 << scale; // Convert to actual scale value
                if (index != 4) // ESP cannot be used as index
                    operand.Memory.Index = (X86Decoder.X86Register)index;
                if (@base != 5 || mod != 0) // Special case for EBP
                    operand.Memory.Base = (X86Decoder.X86Register)@base;
            }
            else
            {
                // Direct register base (no SIB)
                if (!(rm == 5 && mod == 0)) // Special case for disp32
                    operand.Memory.Base = (X86Decoder.X86Register)rm;
            }

            // Handle displacement
            switch (mod)
            {
                case 0: // No displacement (except special cases)
                    if (rm == 5) // disp32 only
                    {
                        operand.Memory.Displacement = ReadInt32();
                    }
                    break;
                case 1: // 8-bit displacement
                    operand.Memory.Displacement = ReadInt8();
                    break;
                case 2: // 32-bit displacement
                    operand.Memory.Displacement = ReadInt32();
                    break;
            }

            return operand;
        }

        /// <summary>
        /// Decode immediate to AL instruction (e.g., ADD AL, imm8)
        /// </summary>
        private bool DecodeImmediateALInstruction(X86Decoder.X86Instruction instruction, string mnemonic, int size)
        {
            instruction.Mnemonic = mnemonic;
            
            // AL register operand
            var regOperand = new X86Decoder.X86Operand(X86Decoder.OperandType.Register, size)
            {
                Register = X86Decoder.X86Register.AL
            };

            // Immediate operand
            var immOperand = new X86Decoder.X86Operand(X86Decoder.OperandType.Immediate, size);
            if (size == 1)
                immOperand.Immediate = ReadUInt8();
            else
                return false; // This method is only for 8-bit instructions

            instruction.Operands.Add(regOperand);
            instruction.Operands.Add(immOperand);
            return true;
        }

        /// <summary>
        /// Decode immediate to AX/EAX instruction (e.g., ADD EAX, imm32)
        /// </summary>
        private bool DecodeImmediateAXInstruction(X86Decoder.X86Instruction instruction, string mnemonic, int size)
        {
            instruction.Mnemonic = mnemonic;
            
            // AX/EAX register operand
            var regOperand = new X86Decoder.X86Operand(X86Decoder.OperandType.Register, size)
            {
                Register = size == 2 ? X86Decoder.X86Register.AX : X86Decoder.X86Register.EAX
            };

            // Immediate operand
            var immOperand = new X86Decoder.X86Operand(X86Decoder.OperandType.Immediate, size);
            if (size == 2)
                immOperand.Immediate = ReadUInt16();
            else if (size == 4)
                immOperand.Immediate = ReadUInt32();
            else
                return false;

            instruction.Operands.Add(regOperand);
            instruction.Operands.Add(immOperand);
            return true;
        }

        /// <summary>
        /// Decode register+immediate instruction (e.g., MOV EAX, imm32)
        /// </summary>
        private bool DecodeRegisterImmediateInstruction(X86Decoder.X86Instruction instruction, string mnemonic, byte reg, int size)
        {
            instruction.Mnemonic = mnemonic;
            
            // Register operand
            var regOperand = new X86Decoder.X86Operand(X86Decoder.OperandType.Register, size)
            {
                Register = X86Decoder.GetRegister(reg, size)
            };

            // Immediate operand
            var immOperand = new X86Decoder.X86Operand(X86Decoder.OperandType.Immediate, size);
            if (size == 1)
                immOperand.Immediate = ReadUInt8();
            else if (size == 2)
                immOperand.Immediate = ReadUInt16();
            else if (size == 4)
                immOperand.Immediate = ReadUInt32();
            else
                return false;

            instruction.Operands.Add(regOperand);
            instruction.Operands.Add(immOperand);
            return true;
        }

        // Helper methods for reading multi-byte values
        private byte ReadUInt8()
        {
            if (_position >= _code.Length)
                throw new InvalidOperationException("Unexpected end of instruction stream");
            return _code[_position++];
        }

        private ushort ReadUInt16()
        {
            if (_position + 1 >= _code.Length)
                throw new InvalidOperationException("Unexpected end of instruction stream");
            ushort value = (ushort)(_code[_position] | (_code[_position + 1] << 8));
            _position += 2;
            return value;
        }

        private uint ReadUInt32()
        {
            if (_position + 3 >= _code.Length)
                throw new InvalidOperationException("Unexpected end of instruction stream");
            uint value = (uint)(_code[_position] | (_code[_position + 1] << 8) | 
                               (_code[_position + 2] << 16) | (_code[_position + 3] << 24));
            _position += 4;
            return value;
        }

        private int ReadInt8()
        {
            return (sbyte)ReadUInt8();
        }

        private int ReadInt32()
        {
            return (int)ReadUInt32();
        }

        /// <summary>
        /// Decode simple instruction with no operands (like RET)
        /// </summary>
        private bool DecodeSimpleInstruction(X86Decoder.X86Instruction instruction, string mnemonic)
        {
            instruction.Mnemonic = mnemonic;
            instruction.Operands = new List<X86Decoder.X86Operand>();
            return true;
        }

        /// <summary>
        /// Decode single register instruction (like INC/DEC/PUSH/POP reg)
        /// </summary>
        private bool DecodeRegisterInstruction(X86Decoder.X86Instruction instruction, string mnemonic, byte reg, int size)
        {
            instruction.Mnemonic = mnemonic;
            
            var regOperand = new X86Decoder.X86Operand(X86Decoder.OperandType.Register, size)
            {
                Register = X86Decoder.GetRegister(reg, size)
            };
            
            instruction.Operands = new List<X86Decoder.X86Operand> { regOperand };
            return true;
        }

        /// <summary>
        /// Decode relative jump/call instruction
        /// </summary>
        private bool DecodeRelativeInstruction(X86Decoder.X86Instruction instruction, string mnemonic, int size)
        {
            instruction.Mnemonic = mnemonic;
            
            var relOperand = new X86Decoder.X86Operand(X86Decoder.OperandType.Relative, size);
            if (size == 1)
                relOperand.Immediate = (uint)ReadInt8();
            else if (size == 4)
                relOperand.Immediate = (uint)ReadInt32();
            else
                return false;
                
            instruction.Operands = new List<X86Decoder.X86Operand> { relOperand };
            return true;
        }

        /// <summary>
        /// Decode RET instruction with optional immediate
        /// </summary>
        private bool DecodeRetInstruction(X86Decoder.X86Instruction instruction, string mnemonic)
        {
            instruction.Mnemonic = mnemonic;
            
            // RET imm16 has a 16-bit immediate operand
            var immOperand = new X86Decoder.X86Operand(X86Decoder.OperandType.Immediate, 2)
            {
                Immediate = ReadUInt16()
            };
            
            instruction.Operands = new List<X86Decoder.X86Operand> { immOperand };
            return true;
        }

        /// <summary>
        /// Decode Group 3 instructions (0xF6/0xF7 - TEST, NOT, NEG, MUL, IMUL, DIV, IDIV)
        /// </summary>
        private bool DecodeGroup3Instruction(X86Decoder.X86Instruction instruction, int size)
        {
            if (_position >= _code.Length)
                return false;

            byte modRM = _code[_position++];
            instruction.ModRM = modRM;

            var (mod, reg, rm) = X86Decoder.DecodeModRM(modRM);

            // Determine instruction based on reg field (bits 3-5 of ModR/M)
            string mnemonic = reg switch
            {
                0 => "test", // TEST
                1 => "test", // TEST (undocumented, same as 0)
                2 => "not",  // NOT
                3 => "neg",  // NEG
                4 => "mul",  // MUL
                5 => "imul", // IMUL
                6 => "div",  // DIV
                7 => "idiv", // IDIV
                _ => null
            };

            if (mnemonic == null)
                return false;

            instruction.Mnemonic = mnemonic;

            // Create operand (register or memory)
            X86Decoder.X86Operand operand;
            if (mod == 3) // Register mode
            {
                operand = new X86Decoder.X86Operand(X86Decoder.OperandType.Register, size)
                {
                    Register = X86Decoder.GetRegister(rm, size)
                };
            }
            else // Memory mode
            {
                operand = DecodeMemoryOperand(mod, rm, size);
                if (operand == null)
                    return false;
            }

            instruction.Operands = new List<X86Decoder.X86Operand> { operand };

            // TEST instruction needs an immediate operand as second operand
            if (mnemonic == "test")
            {
                var immOperand = new X86Decoder.X86Operand(X86Decoder.OperandType.Immediate, size);
                if (size == 1)
                    immOperand.Immediate = ReadUInt8();
                else if (size == 2)
                    immOperand.Immediate = ReadUInt16();
                else if (size == 4)
                    immOperand.Immediate = ReadUInt32();
                else
                    return false;

                instruction.Operands.Add(immOperand);
            }

            return true;
        }

        /// <summary>
        /// Decode Group 2 instructions (0xD0/0xD1 - shift and rotate by 1)
        /// </summary>
        private bool DecodeGroup2Instruction(X86Decoder.X86Instruction instruction, int size)
        {
            if (_position >= _code.Length)
                return false;

            byte modRM = _code[_position++];
            instruction.ModRM = modRM;

            var (mod, reg, rm) = X86Decoder.DecodeModRM(modRM);

            // Determine instruction based on reg field (bits 3-5 of ModR/M)
            string mnemonic = reg switch
            {
                0 => "rol",  // ROL
                1 => "ror",  // ROR
                2 => "rcl",  // RCL
                3 => "rcr",  // RCR
                4 => "shl",  // SHL/SAL
                5 => "shr",  // SHR
                6 => "sal",  // SAL (same as SHL)
                7 => "sar",  // SAR
                _ => null
            };

            if (mnemonic == null)
                return false;

            instruction.Mnemonic = mnemonic;

            // Create operand (register or memory)
            X86Decoder.X86Operand operand;
            if (mod == 3) // Register mode
            {
                operand = new X86Decoder.X86Operand(X86Decoder.OperandType.Register, size)
                {
                    Register = X86Decoder.GetRegister(rm, size)
                };
            }
            else // Memory mode
            {
                operand = DecodeMemoryOperand(mod, rm, size);
                if (operand == null)
                    return false;
            }

            // Add immediate operand (always 1 for 0xD0/0xD1 instructions)
            var immOperand = new X86Decoder.X86Operand(X86Decoder.OperandType.Immediate, 1)
            {
                Immediate = 1
            };

            instruction.Operands = new List<X86Decoder.X86Operand> { operand, immOperand };
            return true;
        }

        /// <summary>
        /// Decode Group 2 instructions (0xD2/0xD3 - shift and rotate by CL)
        /// </summary>
        private bool DecodeGroup2InstructionCL(X86Decoder.X86Instruction instruction, int size)
        {
            if (_position >= _code.Length)
                return false;

            byte modRM = _code[_position++];
            instruction.ModRM = modRM;

            var (mod, reg, rm) = X86Decoder.DecodeModRM(modRM);

            // Determine instruction based on reg field (bits 3-5 of ModR/M)
            string mnemonic = reg switch
            {
                0 => "rol",  // ROL
                1 => "ror",  // ROR
                2 => "rcl",  // RCL
                3 => "rcr",  // RCR
                4 => "shl",  // SHL/SAL
                5 => "shr",  // SHR
                6 => "sal",  // SAL (same as SHL)
                7 => "sar",  // SAR
                _ => null
            };

            if (mnemonic == null)
                return false;

            instruction.Mnemonic = mnemonic;

            // Create operand (register or memory)
            X86Decoder.X86Operand operand;
            if (mod == 3) // Register mode
            {
                operand = new X86Decoder.X86Operand(X86Decoder.OperandType.Register, size)
                {
                    Register = X86Decoder.GetRegister(rm, size)
                };
            }
            else // Memory mode
            {
                operand = DecodeMemoryOperand(mod, rm, size);
                if (operand == null)
                    return false;
            }

            // Add CL register operand
            var clOperand = new X86Decoder.X86Operand(X86Decoder.OperandType.Register, 1)
            {
                Register = X86Decoder.X86Register.CL
            };

            instruction.Operands = new List<X86Decoder.X86Operand> { operand, clOperand };
            return true;
        }

        /// <summary>
        /// Decode two-byte opcodes (0x0F prefix)
        /// </summary>
        private bool DecodeTwoByteInstruction(X86Decoder.X86Instruction instruction, int operandSize)
        {
            // Secondary opcode was already read and stored in DecodeInstruction()
            if (!instruction.SecondaryOpcode.HasValue)
                return false;
                
            byte secondOpcode = instruction.SecondaryOpcode.Value;
            
            switch (secondOpcode)
            {
                // Conditional jumps (near)
                case 0x80: return DecodeRelativeInstruction(instruction, "jo", 4); // JO rel32
                case 0x81: return DecodeRelativeInstruction(instruction, "jno", 4); // JNO rel32
                case 0x82: return DecodeRelativeInstruction(instruction, "jb", 4); // JB/JNAE/JC rel32
                case 0x83: return DecodeRelativeInstruction(instruction, "jnb", 4); // JNB/JAE/JNC rel32
                case 0x84: return DecodeRelativeInstruction(instruction, "je", 4); // JE/JZ rel32
                case 0x85: return DecodeRelativeInstruction(instruction, "jne", 4); // JNE/JNZ rel32
                case 0x86: return DecodeRelativeInstruction(instruction, "jbe", 4); // JBE/JNA rel32
                case 0x87: return DecodeRelativeInstruction(instruction, "ja", 4); // JA/JNBE rel32
                case 0x88: return DecodeRelativeInstruction(instruction, "js", 4); // JS rel32
                case 0x89: return DecodeRelativeInstruction(instruction, "jns", 4); // JNS rel32
                case 0x8A: return DecodeRelativeInstruction(instruction, "jp", 4); // JP/JPE rel32
                case 0x8B: return DecodeRelativeInstruction(instruction, "jnp", 4); // JNP/JPO rel32
                case 0x8C: return DecodeRelativeInstruction(instruction, "jl", 4); // JL/JNGE rel32
                case 0x8D: return DecodeRelativeInstruction(instruction, "jnl", 4); // JNL/JGE rel32
                case 0x8E: return DecodeRelativeInstruction(instruction, "jle", 4); // JLE/JNG rel32
                case 0x8F: return DecodeRelativeInstruction(instruction, "jg", 4); // JG/JNLE rel32
                
                // MOVZX/MOVSX instructions
                case 0xB6: return DecodeModRMInstruction(instruction, "movzx", 1, true); // MOVZX Gv,Eb
                case 0xB7: return DecodeModRMInstruction(instruction, "movzx", 2, true); // MOVZX Gv,Ew
                case 0xBE: return DecodeModRMInstruction(instruction, "movsx", 1, true); // MOVSX Gv,Eb
                case 0xBF: return DecodeModRMInstruction(instruction, "movsx", 2, true); // MOVSX Gv,Ew
                
                // IMUL instruction (two-operand form)
                case 0xAF: return DecodeModRMInstruction(instruction, "imul", operandSize, true); // IMUL Gv,Ev
                
                // SSE1/SSE2 instructions (with prefix variations)
                case 0x10: return DecodeSSEInstruction(instruction, GetSSEMnemonic("movups", "movupd", "movss", "movsd")); 
                case 0x11: return DecodeSSEInstruction(instruction, GetSSEMnemonic("movups", "movupd", "movss", "movsd")); 
                case 0x12: return DecodeSSEInstruction(instruction, GetSSEMnemonic("movlps", "movlpd", "movsldup", "movddup")); 
                case 0x13: return DecodeSSEInstruction(instruction, GetSSEMnemonic("movlps", "movlpd", "", "")); 
                case 0x14: return DecodeSSEInstruction(instruction, GetSSEMnemonic("unpcklps", "unpcklpd", "", "")); 
                case 0x15: return DecodeSSEInstruction(instruction, GetSSEMnemonic("unpckhps", "unpckhpd", "", "")); 
                case 0x16: return DecodeSSEInstruction(instruction, GetSSEMnemonic("movhps", "movhpd", "movshdup", "")); 
                case 0x17: return DecodeSSEInstruction(instruction, GetSSEMnemonic("movhps", "movhpd", "", "")); 
                case 0x28: return DecodeSSEInstruction(instruction, GetSSEMnemonic("movaps", "movapd", "", "")); 
                case 0x29: return DecodeSSEInstruction(instruction, GetSSEMnemonic("movaps", "movapd", "", "")); 
                case 0x2A: return DecodeSSEInstruction(instruction, GetSSEMnemonic("cvtpi2ps", "cvtpi2pd", "cvtsi2ss", "cvtsi2sd")); 
                case 0x2B: return DecodeSSEInstruction(instruction, GetSSEMnemonic("movntps", "movntpd", "", "")); 
                case 0x2C: return DecodeSSEInstruction(instruction, GetSSEMnemonic("cvttps2pi", "cvttpd2pi", "cvttss2si", "cvttsd2si")); 
                case 0x2D: return DecodeSSEInstruction(instruction, GetSSEMnemonic("cvtps2pi", "cvtpd2pi", "cvtss2si", "cvtsd2si")); 
                case 0x2E: return DecodeSSEInstruction(instruction, GetSSEMnemonic("ucomiss", "ucomisd", "", "")); 
                case 0x2F: return DecodeSSEInstruction(instruction, GetSSEMnemonic("comiss", "comisd", "", "")); 
                case 0x50: return DecodeSSEInstruction(instruction, GetSSEMnemonic("movmskps", "movmskpd", "", "")); 
                case 0x51: return DecodeSSEInstruction(instruction, GetSSEMnemonic("sqrtps", "sqrtpd", "sqrtss", "sqrtsd")); 
                case 0x52: return DecodeSSEInstruction(instruction, GetSSEMnemonic("rsqrtps", "", "rsqrtss", "")); 
                case 0x53: return DecodeSSEInstruction(instruction, GetSSEMnemonic("rcpps", "", "rcpss", "")); 
                case 0x54: return DecodeSSEInstruction(instruction, GetSSEMnemonic("andps", "andpd", "", "")); 
                case 0x55: return DecodeSSEInstruction(instruction, GetSSEMnemonic("andnps", "andnpd", "", "")); 
                case 0x56: return DecodeSSEInstruction(instruction, GetSSEMnemonic("orps", "orpd", "", "")); 
                case 0x57: return DecodeSSEInstruction(instruction, GetSSEMnemonic("xorps", "xorpd", "", "")); 
                case 0x58: return DecodeSSEInstruction(instruction, GetSSEMnemonic("addps", "addpd", "addss", "addsd")); 
                case 0x59: return DecodeSSEInstruction(instruction, GetSSEMnemonic("mulps", "mulpd", "mulss", "mulsd")); 
                case 0x5A: return DecodeSSEInstruction(instruction, GetSSEMnemonic("cvtps2pd", "cvtpd2ps", "cvtss2sd", "cvtsd2ss")); 
                case 0x5B: return DecodeSSEInstruction(instruction, GetSSEMnemonic("cvtdq2ps", "cvtps2dq", "cvttps2dq", "")); 
                case 0x5C: return DecodeSSEInstruction(instruction, GetSSEMnemonic("subps", "subpd", "subss", "subsd")); 
                case 0x5D: return DecodeSSEInstruction(instruction, GetSSEMnemonic("minps", "minpd", "minss", "minsd")); 
                case 0x5E: return DecodeSSEInstruction(instruction, GetSSEMnemonic("divps", "divpd", "divss", "divsd")); 
                case 0x5F: return DecodeSSEInstruction(instruction, GetSSEMnemonic("maxps", "maxpd", "maxss", "maxsd"));
                
                // MMX/SSE2 instructions (MMX when no prefix, SSE2 XMM with 66h prefix)
                case 0x60: return DecodeSSEInstruction(instruction, GetSSEMnemonic("punpcklbw", "punpcklbw", "", "")); 
                case 0x61: return DecodeSSEInstruction(instruction, GetSSEMnemonic("punpcklwd", "punpcklwd", "", "")); 
                case 0x62: return DecodeSSEInstruction(instruction, GetSSEMnemonic("punpckldq", "punpckldq", "", "")); 
                case 0x63: return DecodeSSEInstruction(instruction, GetSSEMnemonic("packsswb", "packsswb", "", "")); 
                case 0x64: return DecodeSSEInstruction(instruction, GetSSEMnemonic("pcmpgtb", "pcmpgtb", "", "")); 
                case 0x65: return DecodeSSEInstruction(instruction, GetSSEMnemonic("pcmpgtw", "pcmpgtw", "", "")); 
                case 0x66: return DecodeSSEInstruction(instruction, GetSSEMnemonic("pcmpgtd", "pcmpgtd", "", "")); 
                case 0x67: return DecodeSSEInstruction(instruction, GetSSEMnemonic("packuswb", "packuswb", "", "")); 
                case 0x68: return DecodeSSEInstruction(instruction, GetSSEMnemonic("punpckhbw", "punpckhbw", "", "")); 
                case 0x69: return DecodeSSEInstruction(instruction, GetSSEMnemonic("punpckhwd", "punpckhwd", "", "")); 
                case 0x6A: return DecodeSSEInstruction(instruction, GetSSEMnemonic("punpckhdq", "punpckhdq", "", "")); 
                case 0x6B: return DecodeSSEInstruction(instruction, GetSSEMnemonic("packssdw", "packssdw", "", "")); 
                case 0x6C: return DecodeSSEInstruction(instruction, GetSSEMnemonic("", "punpcklqdq", "", "")); // SSE2 only
                case 0x6D: return DecodeSSEInstruction(instruction, GetSSEMnemonic("", "punpckhqdq", "", "")); // SSE2 only
                case 0x6E: return DecodeSSEInstruction(instruction, GetSSEMnemonic("movd", "movd", "", "")); 
                case 0x6F: return DecodeSSEInstruction(instruction, GetSSEMnemonic("movq", "movdqa", "movdqu", ""));
                case 0x70: return DecodeSSEInstruction(instruction, GetSSEMnemonic("pshufw", "pshufd", "pshufhw", "pshuflw")); // Shuffle operations
                case 0x71: return DecodeSSEShiftInstruction(instruction); // PSLLW/PSRLW/PSRAW with immediate
                case 0x72: return DecodeSSEShiftInstruction(instruction); // PSLLD/PSRLD/PSRAD with immediate  
                case 0x73: return DecodeSSEShiftInstruction(instruction); // PSLLQ/PSRLQ with immediate
                case 0x74: return DecodeSSEInstruction(instruction, GetSSEMnemonic("pcmpeqb", "pcmpeqb", "", "")); 
                case 0x75: return DecodeSSEInstruction(instruction, GetSSEMnemonic("pcmpeqw", "pcmpeqw", "", "")); 
                case 0x76: return DecodeSSEInstruction(instruction, GetSSEMnemonic("pcmpeqd", "pcmpeqd", "", "")); 
                case 0x77: return DecodeSSEInstruction(instruction, GetSSEMnemonic("emms", "", "", "")); // MMX only
                case 0x7C: return DecodeSSEInstruction(instruction, GetSSEMnemonic("", "", "haddps", "haddpd")); // SSE3
                case 0x7D: return DecodeSSEInstruction(instruction, GetSSEMnemonic("", "", "hsubps", "hsubpd")); // SSE3
                case 0x7E: return DecodeSSEInstruction(instruction, GetSSEMnemonic("movd", "movd", "movq", "")); 
                case 0x7F: return DecodeSSEInstruction(instruction, GetSSEMnemonic("movq", "movdqa", "movdqu", ""));
                
                // SSE2 comparison operations  
                case 0xC2: return DecodeSSEInstruction(instruction, GetSSEMnemonic("cmpps", "cmppd", "cmpss", "cmpsd")); // Compare with immediate
                
                // SSE2/SSE3/SSSE3 additional instructions
                case 0xD0: return DecodeSSEInstruction(instruction, GetSSEMnemonic("", "", "addsubps", "addsubpd")); // SSE3
                case 0xD1: return DecodeSSEInstruction(instruction, GetSSEMnemonic("psrlw", "psrlw", "", ""));
                case 0xD2: return DecodeSSEInstruction(instruction, GetSSEMnemonic("psrld", "psrld", "", ""));
                case 0xD3: return DecodeSSEInstruction(instruction, GetSSEMnemonic("psrlq", "psrlq", "", ""));
                case 0xD4: return DecodeSSEInstruction(instruction, GetSSEMnemonic("paddq", "paddq", "", "")); // SSE2
                case 0xD5: return DecodeSSEInstruction(instruction, GetSSEMnemonic("pmullw", "pmullw", "", ""));
                case 0xD6: return DecodeSSEInstruction(instruction, GetSSEMnemonic("", "movq", "movq2dq", "movdq2q")); // SSE2
                case 0xD7: return DecodeSSEInstruction(instruction, GetSSEMnemonic("pmovmskb", "pmovmskb", "", "")); // SSE2
                case 0xD8: return DecodeSSEInstruction(instruction, GetSSEMnemonic("psubusb", "psubusb", "", ""));
                case 0xD9: return DecodeSSEInstruction(instruction, GetSSEMnemonic("psubusw", "psubusw", "", ""));
                case 0xDA: return DecodeSSEInstruction(instruction, GetSSEMnemonic("pminub", "pminub", "", "")); // SSE2
                case 0xDB: return DecodeSSEInstruction(instruction, GetSSEMnemonic("pand", "pand", "", ""));
                case 0xDC: return DecodeSSEInstruction(instruction, GetSSEMnemonic("paddusb", "paddusb", "", ""));
                case 0xDD: return DecodeSSEInstruction(instruction, GetSSEMnemonic("paddusw", "paddusw", "", ""));
                case 0xDE: return DecodeSSEInstruction(instruction, GetSSEMnemonic("pmaxub", "pmaxub", "", "")); // SSE2
                case 0xDF: return DecodeSSEInstruction(instruction, GetSSEMnemonic("pandn", "pandn", "", ""));
                case 0xE0: return DecodeSSEInstruction(instruction, GetSSEMnemonic("pavgb", "pavgb", "", "")); // SSE2
                case 0xE1: return DecodeSSEInstruction(instruction, GetSSEMnemonic("psraw", "psraw", "", ""));
                case 0xE2: return DecodeSSEInstruction(instruction, GetSSEMnemonic("psrad", "psrad", "", ""));
                case 0xE3: return DecodeSSEInstruction(instruction, GetSSEMnemonic("pavgw", "pavgw", "", "")); // SSE2
                case 0xE4: return DecodeSSEInstruction(instruction, GetSSEMnemonic("pmulhuw", "pmulhuw", "", "")); // SSE2
                case 0xE5: return DecodeSSEInstruction(instruction, GetSSEMnemonic("pmulhw", "pmulhw", "", ""));
                case 0xE6: return DecodeSSEInstruction(instruction, GetSSEMnemonic("", "cvttpd2dq", "cvtdq2pd", "cvtpd2dq")); // SSE2
                case 0xE7: return DecodeSSEInstruction(instruction, GetSSEMnemonic("movntq", "movntdq", "", "")); // SSE2
                case 0xE8: return DecodeSSEInstruction(instruction, GetSSEMnemonic("psubsb", "psubsb", "", ""));
                case 0xE9: return DecodeSSEInstruction(instruction, GetSSEMnemonic("psubsw", "psubsw", "", ""));
                case 0xEA: return DecodeSSEInstruction(instruction, GetSSEMnemonic("pminsw", "pminsw", "", "")); // SSE2
                case 0xEB: return DecodeSSEInstruction(instruction, GetSSEMnemonic("por", "por", "", ""));
                case 0xEC: return DecodeSSEInstruction(instruction, GetSSEMnemonic("paddsb", "paddsb", "", ""));
                case 0xED: return DecodeSSEInstruction(instruction, GetSSEMnemonic("paddsw", "paddsw", "", ""));
                case 0xEE: return DecodeSSEInstruction(instruction, GetSSEMnemonic("pmaxsw", "pmaxsw", "", "")); // SSE2
                case 0xEF: return DecodeSSEInstruction(instruction, GetSSEMnemonic("pxor", "pxor", "", ""));
                case 0xF0: return DecodeSSEInstruction(instruction, GetSSEMnemonic("", "", "", "lddqu")); // SSE3
                case 0xF1: return DecodeSSEInstruction(instruction, GetSSEMnemonic("psllw", "psllw", "", ""));
                case 0xF2: return DecodeSSEInstruction(instruction, GetSSEMnemonic("pslld", "pslld", "", ""));
                case 0xF3: return DecodeSSEInstruction(instruction, GetSSEMnemonic("psllq", "psllq", "", ""));
                case 0xF4: return DecodeSSEInstruction(instruction, GetSSEMnemonic("pmuludq", "pmuludq", "", "")); // SSE2
                case 0xF5: return DecodeSSEInstruction(instruction, GetSSEMnemonic("pmaddwd", "pmaddwd", "", ""));
                case 0xF6: return DecodeSSEInstruction(instruction, GetSSEMnemonic("psadbw", "psadbw", "", "")); // SSE2
                case 0xF7: return DecodeSSEInstruction(instruction, GetSSEMnemonic("maskmovq", "maskmovdqu", "", "")); // SSE2
                case 0xF8: return DecodeSSEInstruction(instruction, GetSSEMnemonic("psubb", "psubb", "", ""));
                case 0xF9: return DecodeSSEInstruction(instruction, GetSSEMnemonic("psubw", "psubw", "", ""));
                case 0xFA: return DecodeSSEInstruction(instruction, GetSSEMnemonic("psubd", "psubd", "", ""));
                case 0xFB: return DecodeSSEInstruction(instruction, GetSSEMnemonic("psubq", "psubq", "", "")); // SSE2
                case 0xFC: return DecodeSSEInstruction(instruction, GetSSEMnemonic("paddb", "paddb", "", ""));
                case 0xFD: return DecodeSSEInstruction(instruction, GetSSEMnemonic("paddw", "paddw", "", ""));
                case 0xFE: return DecodeSSEInstruction(instruction, GetSSEMnemonic("paddd", "paddd", "", ""));
                
                default:
                    // Unsupported two-byte opcode
                    return false;
            }
        }

        /// <summary>
        /// Decode x87 FPU instruction (0xD8-0xDF)
        /// </summary>
        private bool DecodeX87Instruction(X86Decoder.X86Instruction instruction, byte primaryOpcode, int operandSize)
        {
            if (_position >= _code.Length)
                return false;

            byte modRM = _code[_position++];
            instruction.ModRM = modRM;
            instruction.InstructionType = X86Decoder.InstructionType.X87;

            var (mod, reg, rm) = X86Decoder.DecodeModRM(modRM);

            // x87 instructions have different formats depending on primary opcode and mod field
            if (mod == 3) // Register form (ST(i) operations)
            {
                return DecodeX87RegisterForm(instruction, primaryOpcode, reg, rm);
            }
            else // Memory form
            {
                return DecodeX87MemoryForm(instruction, primaryOpcode, reg, modRM);
            }
        }

        /// <summary>
        /// Decode x87 register-to-register operations (mod=3)
        /// </summary>
        private bool DecodeX87RegisterForm(X86Decoder.X86Instruction instruction, byte primaryOpcode, int reg, int rm)
        {
            switch (primaryOpcode)
            {
                case 0xD8: // Basic arithmetic operations
                    switch (reg)
                    {
                        case 0: instruction.Mnemonic = "fadd"; break;  // FADD ST(0),ST(i)
                        case 1: instruction.Mnemonic = "fmul"; break;  // FMUL ST(0),ST(i)
                        case 2: instruction.Mnemonic = "fcom"; break;  // FCOM ST(i)
                        case 3: instruction.Mnemonic = "fcomp"; break; // FCOMP ST(i)
                        case 4: instruction.Mnemonic = "fsub"; break;  // FSUB ST(0),ST(i)
                        case 5: instruction.Mnemonic = "fsubr"; break; // FSUBR ST(0),ST(i)
                        case 6: instruction.Mnemonic = "fdiv"; break;  // FDIV ST(0),ST(i)
                        case 7: instruction.Mnemonic = "fdivr"; break; // FDIVR ST(0),ST(i)
                        default: return false;
                    }
                    break;

                case 0xD9: // Data transfer and control
                    if (reg == 0) // FLD ST(i)
                    {
                        instruction.Mnemonic = "fld";
                    }
                    else if (reg == 2) // FST ST(i)
                    {
                        instruction.Mnemonic = "fst";
                    }
                    else if (reg == 3) // FSTP ST(i)
                    {
                        instruction.Mnemonic = "fstp";
                    }
                    else if (reg == 4) // Various operations based on modrm
                    {
                        switch (rm)
                        {
                            case 0: instruction.Mnemonic = "fchs"; break;   // FCHS
                            case 1: instruction.Mnemonic = "fabs"; break;   // FABS
                            case 4: instruction.Mnemonic = "ftst"; break;   // FTST
                            case 5: instruction.Mnemonic = "fxam"; break;   // FXAM
                            default: return false;
                        }
                    }
                    else if (reg == 5) // Load constants
                    {
                        switch (rm)
                        {
                            case 0: instruction.Mnemonic = "fld1"; break;   // FLD1
                            case 1: instruction.Mnemonic = "fldl2t"; break; // FLDL2T
                            case 2: instruction.Mnemonic = "fldl2e"; break; // FLDL2E
                            case 3: instruction.Mnemonic = "fldpi"; break;  // FLDPI
                            case 4: instruction.Mnemonic = "fldlg2"; break; // FLDLG2
                            case 5: instruction.Mnemonic = "fldln2"; break; // FLDLN2
                            case 6: instruction.Mnemonic = "fldz"; break;   // FLDZ
                            default: return false;
                        }
                    }
                    else if (reg == 6) // Various math operations
                    {
                        switch (rm)
                        {
                            case 0: instruction.Mnemonic = "f2xm1"; break;  // F2XM1
                            case 1: instruction.Mnemonic = "fyl2x"; break;  // FYL2X
                            case 2: instruction.Mnemonic = "fptan"; break;  // FPTAN
                            case 3: instruction.Mnemonic = "fpatan"; break; // FPATAN
                            case 4: instruction.Mnemonic = "fxtract"; break; // FXTRACT
                            case 5: instruction.Mnemonic = "fprem1"; break; // FPREM1
                            case 6: instruction.Mnemonic = "fdecstp"; break; // FDECSTP
                            case 7: instruction.Mnemonic = "fincstp"; break; // FINCSTP
                            default: return false;
                        }
                    }
                    else if (reg == 7) // More math operations
                    {
                        switch (rm)
                        {
                            case 0: instruction.Mnemonic = "fprem"; break;  // FPREM
                            case 1: instruction.Mnemonic = "fyl2xp1"; break; // FYL2XP1
                            case 2: instruction.Mnemonic = "fsqrt"; break;  // FSQRT
                            case 3: instruction.Mnemonic = "fsincos"; break; // FSINCOS
                            case 4: instruction.Mnemonic = "frndint"; break; // FRNDINT
                            case 5: instruction.Mnemonic = "fscale"; break; // FSCALE
                            case 6: instruction.Mnemonic = "fsin"; break;   // FSIN
                            case 7: instruction.Mnemonic = "fcos"; break;   // FCOS
                            default: return false;
                        }
                    }
                    else
                    {
                        return false;
                    }
                    break;

                case 0xDA: // Integer operations and comparison
                    switch (reg)
                    {
                        case 0: instruction.Mnemonic = "fiadd"; break;  // FIADD ST(0),ST(i)
                        case 1: instruction.Mnemonic = "fimul"; break;  // FIMUL ST(0),ST(i)
                        case 2: instruction.Mnemonic = "ficom"; break;  // FICOM ST(i)
                        case 3: instruction.Mnemonic = "ficomp"; break; // FICOMP ST(i)
                        case 4: instruction.Mnemonic = "fisub"; break;  // FISUB ST(0),ST(i)
                        case 5: instruction.Mnemonic = "fisubr"; break; // FISUBR ST(0),ST(i)
                        case 6: instruction.Mnemonic = "fidiv"; break;  // FIDIV ST(0),ST(i)
                        case 7: instruction.Mnemonic = "fidivr"; break; // FIDIVR ST(0),ST(i)
                        default: return false;
                    }
                    break;

                case 0xDB: // More specialized operations
                    if (reg == 0) // FILD
                    {
                        instruction.Mnemonic = "fild";
                    }
                    else if (reg == 2) // FIST
                    {
                        instruction.Mnemonic = "fist";
                    }
                    else if (reg == 3) // FISTP
                    {
                        instruction.Mnemonic = "fistp";
                    }
                    else if (reg == 4) // Special operations
                    {
                        switch (rm)
                        {
                            case 0: instruction.Mnemonic = "feni"; break;   // FENI (8087 only)
                            case 1: instruction.Mnemonic = "fdisi"; break;  // FDISI (8087 only)
                            case 2: instruction.Mnemonic = "fclex"; break;  // FCLEX
                            case 3: instruction.Mnemonic = "fninit"; break; // FNINIT
                            case 4: instruction.Mnemonic = "fsetpm"; break; // FSETPM (287 only)
                            default: return false;
                        }
                    }
                    else if (reg == 5) // FUCOMI
                    {
                        instruction.Mnemonic = "fucomi";
                    }
                    else if (reg == 6) // FCOMI
                    {
                        instruction.Mnemonic = "fcomi";
                    }
                    else
                    {
                        return false;
                    }
                    break;

                case 0xDC: // Reverse arithmetic operations
                    switch (reg)
                    {
                        case 0: instruction.Mnemonic = "fadd"; break;  // FADD ST(i),ST(0)
                        case 1: instruction.Mnemonic = "fmul"; break;  // FMUL ST(i),ST(0)
                        case 2: instruction.Mnemonic = "fcom"; break;  // FCOM ST(i)
                        case 3: instruction.Mnemonic = "fcomp"; break; // FCOMP ST(i)
                        case 4: instruction.Mnemonic = "fsubr"; break; // FSUBR ST(i),ST(0)
                        case 5: instruction.Mnemonic = "fsub"; break;  // FSUB ST(i),ST(0)
                        case 6: instruction.Mnemonic = "fdivr"; break; // FDIVR ST(i),ST(0)
                        case 7: instruction.Mnemonic = "fdiv"; break;  // FDIV ST(i),ST(0)
                        default: return false;
                    }
                    break;

                case 0xDD: // Data transfer and control
                    if (reg == 0) // FLD ST(i)
                    {
                        instruction.Mnemonic = "fld";
                    }
                    else if (reg == 2) // FST ST(i)
                    {
                        instruction.Mnemonic = "fst";
                    }
                    else if (reg == 3) // FSTP ST(i)
                    {
                        instruction.Mnemonic = "fstp";
                    }
                    else if (reg == 4) // FUCOM ST(i)
                    {
                        instruction.Mnemonic = "fucom";
                    }
                    else if (reg == 5) // FUCOMP ST(i)
                    {
                        instruction.Mnemonic = "fucomp";
                    }
                    else
                    {
                        return false;
                    }
                    break;

                case 0xDE: // Pop operations
                    switch (reg)
                    {
                        case 0: instruction.Mnemonic = "faddp"; break;  // FADDP ST(i),ST(0)
                        case 1: instruction.Mnemonic = "fmulp"; break;  // FMULP ST(i),ST(0)
                        case 2: instruction.Mnemonic = "fcomp"; break;  // FCOMP ST(i)
                        case 3: instruction.Mnemonic = "fcompp"; break; // FCOMPP
                        case 4: instruction.Mnemonic = "fsubrp"; break; // FSUBRP ST(i),ST(0)
                        case 5: instruction.Mnemonic = "fsubp"; break;  // FSUBP ST(i),ST(0)
                        case 6: instruction.Mnemonic = "fdivrp"; break; // FDIVRP ST(i),ST(0)
                        case 7: instruction.Mnemonic = "fdivp"; break;  // FDIVP ST(i),ST(0)
                        default: return false;
                    }
                    break;

                case 0xDF: // Various operations
                    if (reg == 0) // FILD
                    {
                        instruction.Mnemonic = "fild";
                    }
                    else if (reg == 2) // FIST
                    {
                        instruction.Mnemonic = "fist";
                    }
                    else if (reg == 3) // FISTP
                    {
                        instruction.Mnemonic = "fistp";
                    }
                    else if (reg == 4) // FBLD/FBSTP
                    {
                        instruction.Mnemonic = "fbld";
                    }
                    else if (reg == 5) // FUCOMIP
                    {
                        instruction.Mnemonic = "fucomip";
                    }
                    else if (reg == 6) // FCOMIP
                    {
                        instruction.Mnemonic = "fcomip";
                    }
                    else if (reg == 7) // FBSTP
                    {
                        instruction.Mnemonic = "fbstp";
                    }
                    else
                    {
                        return false;
                    }
                    break;

                default:
                    return false;
            }

            // Add ST register operands
            instruction.Operands.Add(new X86Decoder.X86Operand(X86Decoder.OperandType.STRegister, 10) // 80-bit
            {
                STRegister = 0 // ST(0)
            });

            if (rm != 0 || instruction.Mnemonic.EndsWith("1") || instruction.Mnemonic.EndsWith("z") ||
                instruction.Mnemonic == "fchs" || instruction.Mnemonic == "fabs" || instruction.Mnemonic == "ftst" ||
                instruction.Mnemonic == "fxam" || instruction.Mnemonic.StartsWith("fld") && instruction.Mnemonic.Length > 3)
            {
                // Single operand or constant
                if (instruction.Mnemonic.EndsWith("1") || instruction.Mnemonic.EndsWith("z") ||
                    instruction.Mnemonic == "fchs" || instruction.Mnemonic == "fabs" || instruction.Mnemonic == "ftst" ||
                    instruction.Mnemonic == "fxam" || instruction.Mnemonic.StartsWith("fld") && instruction.Mnemonic.Length > 3)
                {
                    // No additional operand needed for constants and unary ops
                }
                else
                {
                    instruction.Operands.Add(new X86Decoder.X86Operand(X86Decoder.OperandType.STRegister, 10)
                    {
                        STRegister = rm
                    });
                }
            }
            else
            {
                instruction.Operands.Add(new X86Decoder.X86Operand(X86Decoder.OperandType.STRegister, 10)
                {
                    STRegister = rm
                });
            }

            return true;
        }

        /// <summary>
        /// Decode x87 memory operations (mod!=3)
        /// </summary>
        private bool DecodeX87MemoryForm(X86Decoder.X86Instruction instruction, byte primaryOpcode, int reg, byte modRM)
        {
            var (mod, _, rm) = X86Decoder.DecodeModRM(modRM);
            
            // Decode memory operand
            var memoryOperand = DecodeMemoryOperand(mod, rm, 4); // Default to 4 bytes, adjust based on instruction
            
            if (memoryOperand == null)
                return false;

            // Determine operation and data size based on primary opcode and reg field
            switch (primaryOpcode)
            {
                case 0xD8: // Single-precision operations
                    switch (reg)
                    {
                        case 0: instruction.Mnemonic = "fadds"; break;  // FADD m32fp
                        case 1: instruction.Mnemonic = "fmuls"; break;  // FMUL m32fp
                        case 2: instruction.Mnemonic = "fcoms"; break;  // FCOM m32fp
                        case 3: instruction.Mnemonic = "fcomps"; break; // FCOMP m32fp
                        case 4: instruction.Mnemonic = "fsubs"; break;  // FSUB m32fp
                        case 5: instruction.Mnemonic = "fsubrs"; break; // FSUBR m32fp
                        case 6: instruction.Mnemonic = "fdivs"; break;  // FDIV m32fp
                        case 7: instruction.Mnemonic = "fdivrs"; break; // FDIVR m32fp
                        default: return false;
                    }
                    memoryOperand.Size = 4; // 32-bit float
                    break;

                case 0xD9: // Various memory operations
                    switch (reg)
                    {
                        case 0: instruction.Mnemonic = "flds"; memoryOperand.Size = 4; break;   // FLD m32fp
                        case 2: instruction.Mnemonic = "fsts"; memoryOperand.Size = 4; break;   // FST m32fp
                        case 3: instruction.Mnemonic = "fstps"; memoryOperand.Size = 4; break;  // FSTP m32fp
                        case 4: instruction.Mnemonic = "fldenv"; memoryOperand.Size = 28; break; // FLDENV m28env
                        case 5: instruction.Mnemonic = "fldcw"; memoryOperand.Size = 2; break;  // FLDCW m16
                        case 6: instruction.Mnemonic = "fstenv"; memoryOperand.Size = 28; break; // FSTENV m28env
                        case 7: instruction.Mnemonic = "fstcw"; memoryOperand.Size = 2; break;  // FSTCW m16
                        default: return false;
                    }
                    break;

                case 0xDA: // Integer operations
                    switch (reg)
                    {
                        case 0: instruction.Mnemonic = "fiadd"; break;  // FIADD m32int
                        case 1: instruction.Mnemonic = "fimul"; break;  // FIMUL m32int
                        case 2: instruction.Mnemonic = "ficom"; break;  // FICOM m32int
                        case 3: instruction.Mnemonic = "ficomp"; break; // FICOMP m32int
                        case 4: instruction.Mnemonic = "fisub"; break;  // FISUB m32int
                        case 5: instruction.Mnemonic = "fisubr"; break; // FISUBR m32int
                        case 6: instruction.Mnemonic = "fidiv"; break;  // FIDIV m32int
                        case 7: instruction.Mnemonic = "fidivr"; break; // FIDIVR m32int
                        default: return false;
                    }
                    memoryOperand.Size = 4; // 32-bit integer
                    break;

                case 0xDB: // Various formats
                    switch (reg)
                    {
                        case 0: instruction.Mnemonic = "fild"; memoryOperand.Size = 4; break;   // FILD m32int
                        case 1: instruction.Mnemonic = "fisttp"; memoryOperand.Size = 4; break; // FISTTP m32int
                        case 2: instruction.Mnemonic = "fist"; memoryOperand.Size = 4; break;   // FIST m32int
                        case 3: instruction.Mnemonic = "fistp"; memoryOperand.Size = 4; break;  // FISTP m32int
                        case 5: instruction.Mnemonic = "fld"; memoryOperand.Size = 10; break;    // FLD m80fp
                        case 7: instruction.Mnemonic = "fstp"; memoryOperand.Size = 10; break;   // FSTP m80fp
                        default: return false;
                    }
                    break;

                case 0xDC: // Double-precision operations
                    switch (reg)
                    {
                        case 0: instruction.Mnemonic = "fadd"; break;   // FADD m64fp
                        case 1: instruction.Mnemonic = "fmul"; break;   // FMUL m64fp
                        case 2: instruction.Mnemonic = "fcom"; break;   // FCOM m64fp
                        case 3: instruction.Mnemonic = "fcomp"; break;  // FCOMP m64fp
                        case 4: instruction.Mnemonic = "fsub"; break;   // FSUB m64fp
                        case 5: instruction.Mnemonic = "fsubr"; break;  // FSUBR m64fp
                        case 6: instruction.Mnemonic = "fdiv"; break;   // FDIV m64fp
                        case 7: instruction.Mnemonic = "fdivr"; break;  // FDIVR m64fp
                        default: return false;
                    }
                    memoryOperand.Size = 8; // 64-bit double
                    break;

                case 0xDD: // Double precision load/store
                    switch (reg)
                    {
                        case 0: instruction.Mnemonic = "fld"; memoryOperand.Size = 8; break;    // FLD m64fp
                        case 1: instruction.Mnemonic = "fisttp"; memoryOperand.Size = 8; break; // FISTTP m64int
                        case 2: instruction.Mnemonic = "fst"; memoryOperand.Size = 8; break;    // FST m64fp
                        case 3: instruction.Mnemonic = "fstp"; memoryOperand.Size = 8; break;   // FSTP m64fp
                        case 4: instruction.Mnemonic = "frstor"; memoryOperand.Size = 108; break; // FRSTOR m108bytes
                        case 6: instruction.Mnemonic = "fsave"; memoryOperand.Size = 108; break;  // FSAVE m108bytes
                        case 7: instruction.Mnemonic = "fstsw"; memoryOperand.Size = 2; break;  // FSTSW m16
                        default: return false;
                    }
                    break;

                case 0xDE: // Integer operations
                    switch (reg)
                    {
                        case 0: instruction.Mnemonic = "fiadd"; break;  // FIADD m16int
                        case 1: instruction.Mnemonic = "fimul"; break;  // FIMUL m16int
                        case 2: instruction.Mnemonic = "ficom"; break;  // FICOM m16int
                        case 3: instruction.Mnemonic = "ficomp"; break; // FICOMP m16int
                        case 4: instruction.Mnemonic = "fisub"; break;  // FISUB m16int
                        case 5: instruction.Mnemonic = "fisubr"; break; // FISUBR m16int
                        case 6: instruction.Mnemonic = "fidiv"; break;  // FIDIV m16int
                        case 7: instruction.Mnemonic = "fidivr"; break; // FIDIVR m16int
                        default: return false;
                    }
                    memoryOperand.Size = 2; // 16-bit integer
                    break;

                case 0xDF: // Various formats
                    switch (reg)
                    {
                        case 0: instruction.Mnemonic = "fild"; memoryOperand.Size = 2; break;   // FILD m16int
                        case 1: instruction.Mnemonic = "fisttp"; memoryOperand.Size = 2; break; // FISTTP m16int
                        case 2: instruction.Mnemonic = "fist"; memoryOperand.Size = 2; break;   // FIST m16int
                        case 3: instruction.Mnemonic = "fistp"; memoryOperand.Size = 2; break;  // FISTP m16int
                        case 4: instruction.Mnemonic = "fbld"; memoryOperand.Size = 10; break;   // FBLD m80dec
                        case 5: instruction.Mnemonic = "fild"; memoryOperand.Size = 8; break;   // FILD m64int
                        case 6: instruction.Mnemonic = "fbstp"; memoryOperand.Size = 10; break;  // FBSTP m80dec
                        case 7: instruction.Mnemonic = "fistp"; memoryOperand.Size = 8; break;  // FISTP m64int
                        default: return false;
                    }
                    break;

                default:
                    return false;
            }

            // Add memory operand
            instruction.Operands.Add(memoryOperand);

            return true;
        }

        /// <summary>
        /// Decode MMX instruction with ModR/M byte
        /// </summary>
        private bool DecodeMMXInstruction(X86Decoder.X86Instruction instruction, string mnemonic)
        {
            instruction.Mnemonic = mnemonic;
            instruction.InstructionType = X86Decoder.InstructionType.MMX;

            // Check if we have enough bytes for ModR/M
            if (_position >= _code.Length)
                return false;

            byte modRM = _code[_position++];
            int mod = (modRM >> 6) & 0x3;
            int reg = (modRM >> 3) & 0x7;
            int rm = modRM & 0x7;

            // First operand is always MMX register (reg field)
            var destOperand = new X86Decoder.X86Operand(X86Decoder.OperandType.MMXRegister, 8)
            {
                MMXRegister = reg
            };
            instruction.Operands.Add(destOperand);

            // Second operand depends on mod field
            if (mod == 3)
            {
                // Register form: second operand is also MMX register
                var srcOperand = new X86Decoder.X86Operand(X86Decoder.OperandType.MMXRegister, 8)
                {
                    MMXRegister = rm
                };
                instruction.Operands.Add(srcOperand);
            }
            else
            {
                // Memory form: decode memory operand
                var memoryOperand = DecodeMemoryOperand((byte)mod, (byte)rm, 8); // MMX operations are 64-bit
                if (memoryOperand == null)
                    return false;

                instruction.Operands.Add(memoryOperand);
            }

            return true;
        }

        /// <summary>
        /// Decode MMX shift instruction with immediate operand
        /// </summary>
        private bool DecodeMMXShiftInstruction(X86Decoder.X86Instruction instruction)
        {
            instruction.InstructionType = X86Decoder.InstructionType.MMX;

            // Check if we have enough bytes for ModR/M
            if (_position >= _code.Length)
                return false;

            byte modRM = _code[_position++];
            int mod = (modRM >> 6) & 0x3;
            int reg = (modRM >> 3) & 0x7;
            int rm = modRM & 0x7;

            // Determine instruction based on reg field
            string mnemonic;
            switch (_code[_position - 2]) // Previous byte is the opcode
            {
                case 0x71: // Word shifts
                    switch (reg)
                    {
                        case 2: mnemonic = "psrlw"; break; // PSRLW mm,imm8
                        case 4: mnemonic = "psraw"; break; // PSRAW mm,imm8
                        case 6: mnemonic = "psllw"; break; // PSLLW mm,imm8
                        default: return false;
                    }
                    break;
                case 0x72: // Dword shifts
                    switch (reg)
                    {
                        case 2: mnemonic = "psrld"; break; // PSRLD mm,imm8
                        case 4: mnemonic = "psrad"; break; // PSRAD mm,imm8
                        case 6: mnemonic = "pslld"; break; // PSLLD mm,imm8
                        default: return false;
                    }
                    break;
                case 0x73: // Qword shifts
                    switch (reg)
                    {
                        case 2: mnemonic = "psrlq"; break; // PSRLQ mm,imm8
                        case 6: mnemonic = "psllq"; break; // PSLLQ mm,imm8
                        default: return false;
                    }
                    break;
                default:
                    return false;
            }

            instruction.Mnemonic = mnemonic;

            // First operand is MMX register
            if (mod != 3) return false; // Shift instructions only support register form

            var mmxOperand = new X86Decoder.X86Operand(X86Decoder.OperandType.MMXRegister, 8)
            {
                MMXRegister = rm
            };
            instruction.Operands.Add(mmxOperand);

            // Second operand is immediate byte
            if (_position >= _code.Length)
                return false;

            var immOperand = new X86Decoder.X86Operand(X86Decoder.OperandType.Immediate, 1)
            {
                Immediate = _code[_position++]
            };
            instruction.Operands.Add(immOperand);

            return true;
        }

        /// <summary>
        /// Decode SSE instruction with ModR/M byte
        /// </summary>
        private bool DecodeSSEInstruction(X86Decoder.X86Instruction instruction, string mnemonic)
        {
            instruction.Mnemonic = mnemonic;
            instruction.InstructionType = X86Decoder.InstructionType.SSE;

            // Check if we have enough bytes for ModR/M
            if (_position >= _code.Length)
                return false;

            byte modRM = _code[_position++];
            int mod = (modRM >> 6) & 0x3;
            int reg = (modRM >> 3) & 0x7;
            int rm = modRM & 0x7;

            // Determine operand order based on instruction
            bool reverseOperands = mnemonic switch
            {
                "movups" or "movaps" or "movlps" or "movhps" or "movntps" => 
                    instruction.SecondaryOpcode == 0x11 || instruction.SecondaryOpcode == 0x13 || 
                    instruction.SecondaryOpcode == 0x17 || instruction.SecondaryOpcode == 0x29 ||
                    instruction.SecondaryOpcode == 0x2B,
                _ => false
            };

            if (reverseOperands)
            {
                // Destination is second operand (memory/register), source is XMM register
                if (mod == 3)
                {
                    // Register form: destination is XMM register
                    var destOperand = new X86Decoder.X86Operand(X86Decoder.OperandType.XMMRegister, 16)
                    {
                        XMMRegister = rm
                    };
                    instruction.Operands.Add(destOperand);
                }
                else
                {
                    // Memory form: destination is memory
                    var memoryOperand = DecodeMemoryOperand((byte)mod, (byte)rm, 16); // SSE operations are 128-bit
                    if (memoryOperand == null)
                        return false;
                    instruction.Operands.Add(memoryOperand);
                }

                // Source is XMM register
                var srcOperand = new X86Decoder.X86Operand(X86Decoder.OperandType.XMMRegister, 16)
                {
                    XMMRegister = reg
                };
                instruction.Operands.Add(srcOperand);
            }
            else
            {
                // Normal order: first operand is XMM register (reg field)
                var destOperand = new X86Decoder.X86Operand(X86Decoder.OperandType.XMMRegister, 16)
                {
                    XMMRegister = reg
                };
                instruction.Operands.Add(destOperand);

                // Second operand depends on mod field
                if (mod == 3)
                {
                    // Register form: second operand is also XMM register
                    var srcOperand = new X86Decoder.X86Operand(X86Decoder.OperandType.XMMRegister, 16)
                    {
                        XMMRegister = rm
                    };
                    instruction.Operands.Add(srcOperand);
                }
                else
                {
                    // Memory form: decode memory operand
                    var memoryOperand = DecodeMemoryOperand((byte)mod, (byte)rm, 16); // SSE operations are 128-bit
                    if (memoryOperand == null)
                        return false;
                    instruction.Operands.Add(memoryOperand);
                }
            }

            return true;
        }

        /// <summary>
        /// Get SSE mnemonic based on prefix
        /// </summary>
        private string GetSSEMnemonic(string nonePrefix, string prefix66, string prefixF3, string prefixF2)
        {
            // Check prefixes to determine SSE variant
            if (_code.Length > 0)
            {
                // Look for prefixes before the 0x0F byte
                for (int i = 0; i < _position - 2; i++)
                {
                    switch (_code[i])
                    {
                        case 0x66: // SSE2 double-precision or integer
                            return !string.IsNullOrEmpty(prefix66) ? prefix66 : nonePrefix;
                        case 0xF3: // SSE1/SSE2 scalar or SSE3
                            return !string.IsNullOrEmpty(prefixF3) ? prefixF3 : nonePrefix;
                        case 0xF2: // SSE2 scalar double or SSE3
                            return !string.IsNullOrEmpty(prefixF2) ? prefixF2 : nonePrefix;
                    }
                }
            }
            
            // Default to no prefix (SSE1 or MMX)
            return nonePrefix;
        }

        /// <summary>
        /// Decode SSE shift instructions with immediate byte
        /// </summary>
        private bool DecodeSSEShiftInstruction(X86Decoder.X86Instruction instruction)
        {
            if (_position >= _code.Length)
                return false;

            byte modRM = _code[_position++];
            instruction.ModRM = modRM;
            instruction.InstructionType = X86Decoder.InstructionType.SSE;

            var (mod, reg, rm) = X86Decoder.DecodeModRM(modRM);

            // Determine shift operation based on reg field
            string baseMnemonic = instruction.SecondaryOpcode switch
            {
                0x71 => reg switch
                {
                    2 => "psrlw",
                    4 => "psraw", 
                    6 => "psllw",
                    _ => "unknown"
                },
                0x72 => reg switch
                {
                    2 => "psrld",
                    4 => "psrad",
                    6 => "pslld", 
                    _ => "unknown"
                },
                0x73 => reg switch
                {
                    2 => "psrlq",
                    3 => "psrldq", // SSE2 only
                    6 => "psllq",
                    7 => "pslldq", // SSE2 only
                    _ => "unknown"
                },
                _ => "unknown"
            };

            instruction.Mnemonic = baseMnemonic;

            // First operand is XMM register (or MM for MMX)
            var regOperand = new X86Decoder.X86Operand(X86Decoder.OperandType.XMMRegister, 16)
            {
                XMMRegister = rm
            };
            instruction.Operands.Add(regOperand);

            // Second operand is immediate byte
            if (_position >= _code.Length)
                return false;
                
            byte immediate = _code[_position++];
            var immOperand = new X86Decoder.X86Operand(X86Decoder.OperandType.Immediate, 1)
            {
                Immediate = immediate
            };
            instruction.Operands.Add(immOperand);

            return true;
        }

        /// <summary>
        /// Decode VEX 0x0F map instructions (most common AVX instructions)
        /// </summary>
        private bool DecodeVEXOpcode0F(X86Decoder.X86Instruction instruction, byte opcode)
        {
            // Convert legacy SSE instructions to AVX equivalents
            string baseMnemonic = opcode switch
            {
                // Basic move operations
                0x10 => GetAVXMnemonic("vmovups", "vmovupd", "vmovss", "vmovsd"),
                0x11 => GetAVXMnemonic("vmovups", "vmovupd", "vmovss", "vmovsd"),
                0x12 => GetAVXMnemonic("vmovlps", "vmovlpd", "vmovsldup", "vmovddup"), 
                0x13 => GetAVXMnemonic("vmovlps", "vmovlpd", "", ""),
                0x14 => GetAVXMnemonic("vunpcklps", "vunpcklpd", "", ""),
                0x15 => GetAVXMnemonic("vunpckhps", "vunpckhpd", "", ""),
                0x16 => GetAVXMnemonic("vmovhps", "vmovhpd", "vmovshdup", ""),
                0x17 => GetAVXMnemonic("vmovhps", "vmovhpd", "", ""),
                0x28 => GetAVXMnemonic("vmovaps", "vmovapd", "", ""),
                0x29 => GetAVXMnemonic("vmovaps", "vmovapd", "", ""),
                
                // Arithmetic operations  
                0x51 => GetAVXMnemonic("vsqrtps", "vsqrtpd", "vsqrtss", "vsqrtsd"),
                0x52 => GetAVXMnemonic("vrsqrtps", "", "vrsqrtss", ""),
                0x53 => GetAVXMnemonic("vrcpps", "", "vrcpss", ""),
                0x54 => GetAVXMnemonic("vandps", "vandpd", "", ""),
                0x55 => GetAVXMnemonic("vandnps", "vandnpd", "", ""),
                0x56 => GetAVXMnemonic("vorps", "vorpd", "", ""),
                0x57 => GetAVXMnemonic("vxorps", "vxorpd", "", ""),
                0x58 => GetAVXMnemonic("vaddps", "vaddpd", "vaddss", "vaddsd"),
                0x59 => GetAVXMnemonic("vmulps", "vmulpd", "vmulss", "vmulsd"),
                0x5A => GetAVXMnemonic("vcvtps2pd", "vcvtpd2ps", "vcvtss2sd", "vcvtsd2ss"),
                0x5B => GetAVXMnemonic("vcvtdq2ps", "vcvtps2dq", "vcvttps2dq", ""),
                0x5C => GetAVXMnemonic("vsubps", "vsubpd", "vsubss", "vsubsd"),
                0x5D => GetAVXMnemonic("vminps", "vminpd", "vminss", "vminsd"),
                0x5E => GetAVXMnemonic("vdivps", "vdivpd", "vdivss", "vdivsd"),
                0x5F => GetAVXMnemonic("vmaxps", "vmaxpd", "vmaxss", "vmaxsd"),
                
                _ => null
            };

            if (baseMnemonic == null)
                return false;

            return DecodeAVXInstruction(instruction, baseMnemonic);
        }

        /// <summary>
        /// Decode VEX 0x0F38 map instructions (AVX2 integer operations)
        /// </summary>
        private bool DecodeVEXOpcode0F38(X86Decoder.X86Instruction instruction, byte opcode)
        {
            // AVX2 integer vector operations and other enhanced instructions
            string baseMnemonic = opcode switch
            {
                // Integer vector operations (AVX2)
                0x00 => GetAVXMnemonic("vpshufb", "vpshufb", "", ""),
                0x01 => GetAVXMnemonic("vphaddw", "vphaddw", "", ""),
                0x02 => GetAVXMnemonic("vphaddd", "vphaddd", "", ""),
                0x03 => GetAVXMnemonic("vphaddsw", "vphaddsw", "", ""),
                0x04 => GetAVXMnemonic("vpmaddubsw", "vpmaddubsw", "", ""),
                0x05 => GetAVXMnemonic("vphsubw", "vphsubw", "", ""),
                0x06 => GetAVXMnemonic("vphsubd", "vphsubd", "", ""),
                0x07 => GetAVXMnemonic("vphsubsw", "vphsubsw", "", ""),
                0x08 => GetAVXMnemonic("vpsignb", "vpsignb", "", ""),
                0x09 => GetAVXMnemonic("vpsignw", "vpsignw", "", ""),
                0x0A => GetAVXMnemonic("vpsignd", "vpsignd", "", ""),
                0x0B => GetAVXMnemonic("vpmulhrsw", "vpmulhrsw", "", ""),
                
                // AVX2 Permute and broadcast operations
                0x0C => GetAVXMnemonic("vpermilps", "vpermilps", "", ""),
                0x0D => GetAVXMnemonic("vpermilpd", "vpermilpd", "", ""),
                0x0E => GetAVXMnemonic("vtestps", "vtestpd", "", ""),
                
                // AVX2 Integer arithmetic
                0x13 => GetAVXMnemonic("vcvtph2ps", "vcvtph2ps", "", ""),
                0x16 => GetAVXMnemonic("vpermps", "vpermps", "", ""),
                0x17 => GetAVXMnemonic("vptest", "vptest", "", ""),
                
                // Broadcast operations (AVX2)
                0x18 => GetAVXMnemonic("vbroadcastss", "vbroadcastss", "", ""),
                0x19 => GetAVXMnemonic("vbroadcastsd", "vbroadcastsd", "", ""),
                0x1A => GetAVXMnemonic("vbroadcastf128", "vbroadcastf128", "", ""),
                
                // More integer operations
                0x1C => GetAVXMnemonic("vpabsb", "vpabsb", "", ""),
                0x1D => GetAVXMnemonic("vpabsw", "vpabsw", "", ""),
                0x1E => GetAVXMnemonic("vpabsd", "vpabsd", "", ""),
                
                // Advanced operations
                0x20 => GetAVXMnemonic("vpmovsxbw", "vpmovsxbw", "", ""),
                0x21 => GetAVXMnemonic("vpmovsxbd", "vpmovsxbd", "", ""),
                0x22 => GetAVXMnemonic("vpmovsxbq", "vpmovsxbq", "", ""),
                0x23 => GetAVXMnemonic("vpmovsxwd", "vpmovsxwd", "", ""),
                0x24 => GetAVXMnemonic("vpmovsxwq", "vpmovsxwq", "", ""),
                0x25 => GetAVXMnemonic("vpmovsxdq", "vpmovsxdq", "", ""),
                
                // Zero extend operations
                0x30 => GetAVXMnemonic("vpmovzxbw", "vpmovzxbw", "", ""),
                0x31 => GetAVXMnemonic("vpmovzxbd", "vpmovzxbd", "", ""),
                0x32 => GetAVXMnemonic("vpmovzxbq", "vpmovzxbq", "", ""),
                0x33 => GetAVXMnemonic("vpmovzxwd", "vpmovzxwd", "", ""),
                0x34 => GetAVXMnemonic("vpmovzxwq", "vpmovzxwq", "", ""),
                0x35 => GetAVXMnemonic("vpmovzxdq", "vpmovzxdq", "", ""),
                
                // Comparison operations
                0x37 => GetAVXMnemonic("vpcmpgtq", "vpcmpgtq", "", ""),
                0x38 => GetAVXMnemonic("vpminsb", "vpminsb", "", ""),
                0x39 => GetAVXMnemonic("vpminsd", "vpminsd", "", ""),
                0x3A => GetAVXMnemonic("vpminuw", "vpminuw", "", ""),
                0x3B => GetAVXMnemonic("vpminud", "vpminud", "", ""),
                0x3C => GetAVXMnemonic("vpmaxsb", "vpmaxsb", "", ""),
                0x3D => GetAVXMnemonic("vpmaxsd", "vpmaxsd", "", ""),
                0x3E => GetAVXMnemonic("vpmaxuw", "vpmaxuw", "", ""),
                0x3F => GetAVXMnemonic("vpmaxud", "vpmaxud", "", ""),
                
                // Multiplication operations
                0x40 => GetAVXMnemonic("vpmulld", "vpmulld", "", ""),
                0x41 => GetAVXMnemonic("vphminposuw", "vphminposuw", "", ""),
                
                // AVX2 broadcast operations for integer types
                0x58 => GetAVXMnemonic("vpbroadcastd", "vpbroadcastd", "", ""),
                0x59 => GetAVXMnemonic("vpbroadcastq", "vpbroadcastq", "", ""),
                0x5A => GetAVXMnemonic("vbroadcasti128", "vbroadcasti128", "", ""),
                
                // AVX2 Gather operations (complex addressing)
                0x90 => GetAVXMnemonic("vpgatherdd", "vpgatherdd", "", ""),
                0x91 => GetAVXMnemonic("vpgatherqd", "vpgatherqd", "", ""),
                0x92 => GetAVXMnemonic("vgatherdps", "vgatherdps", "", ""),
                0x93 => GetAVXMnemonic("vgatherqps", "vgatherqps", "", ""),
                
                _ => null
            };

            if (baseMnemonic == null)
                return false;

            return DecodeAVXInstruction(instruction, baseMnemonic);
        }

        /// <summary>
        /// Decode VEX 0x0F3A map instructions (AVX2 with immediate operands)
        /// </summary>
        private bool DecodeVEXOpcode0F3A(X86Decoder.X86Instruction instruction, byte opcode)
        {
            // AVX2 instructions with immediate byte operands
            string baseMnemonic = opcode switch
            {
                // Permute operations with immediate
                0x00 => GetAVXMnemonic("vpermq", "vpermq", "", ""),
                0x01 => GetAVXMnemonic("vpermpd", "vpermpd", "", ""),
                0x02 => GetAVXMnemonic("vpblendd", "vpblendd", "", ""),
                
                // Shift operations with immediate
                0x04 => GetAVXMnemonic("vpermilps", "vpermilps", "", ""),
                0x05 => GetAVXMnemonic("vpermilpd", "vpermilpd", "", ""),
                0x06 => GetAVXMnemonic("vperm2f128", "vperm2f128", "", ""),
                
                // Round operations  
                0x08 => GetAVXMnemonic("vroundps", "vroundpd", "", ""),
                0x09 => GetAVXMnemonic("vroundpd", "vroundpd", "", ""),
                0x0A => GetAVXMnemonic("vroundss", "vroundss", "", ""),
                0x0B => GetAVXMnemonic("vroundsd", "vroundsd", "", ""),
                
                // Blend operations
                0x0C => GetAVXMnemonic("vblendps", "vblendps", "", ""),
                0x0D => GetAVXMnemonic("vblendpd", "vblendpd", "", ""),
                0x0E => GetAVXMnemonic("vpblendw", "vpblendw", "", ""),
                0x0F => GetAVXMnemonic("vpalignr", "vpalignr", "", ""),
                
                // Extract and insert operations
                0x14 => GetAVXMnemonic("vpextrb", "vpextrb", "", ""),
                0x15 => GetAVXMnemonic("vpextrw", "vpextrw", "", ""),
                0x16 => GetAVXMnemonic("vpextrd", "vpextrd", "", ""),
                0x17 => GetAVXMnemonic("vextractps", "vextractps", "", ""),
                0x18 => GetAVXMnemonic("vinsertf128", "vinsertf128", "", ""),
                0x19 => GetAVXMnemonic("vextractf128", "vextractf128", "", ""),
                
                // More insert operations
                0x20 => GetAVXMnemonic("vpinsrb", "vpinsrb", "", ""),
                0x21 => GetAVXMnemonic("vinsertps", "vinsertps", "", ""),
                0x22 => GetAVXMnemonic("vpinsrd", "vpinsrd", "", ""),
                
                // AVX2 specific operations
                0x38 => GetAVXMnemonic("vinserti128", "vinserti128", "", ""),
                0x39 => GetAVXMnemonic("vextracti128", "vextracti128", "", ""),
                
                // Compare operations with immediate
                0x40 => GetAVXMnemonic("vdpps", "vdpps", "", ""),
                0x41 => GetAVXMnemonic("vdppd", "vdppd", "", ""),
                0x42 => GetAVXMnemonic("vmpsadbw", "vmpsadbw", "", ""),
                
                // More permute operations
                0x46 => GetAVXMnemonic("vperm2i128", "vperm2i128", "", ""),
                
                // Shift operations
                0x4A => GetAVXMnemonic("vblendvps", "vblendvps", "", ""),
                0x4B => GetAVXMnemonic("vblendvpd", "vblendvpd", "", ""),
                0x4C => GetAVXMnemonic("vpblendvb", "vpblendvb", "", ""),
                
                // Additional compare operations
                0x60 => GetAVXMnemonic("vpcmpestrm", "vpcmpestrm", "", ""),
                0x61 => GetAVXMnemonic("vpcmpestri", "vpcmpestri", "", ""),
                0x62 => GetAVXMnemonic("vpcmpistrm", "vpcmpistrm", "", ""),
                0x63 => GetAVXMnemonic("vpcmpistri", "vpcmpistri", "", ""),
                
                _ => null
            };

            if (baseMnemonic == null)
                return false;

            // These instructions typically have immediate operands
            bool hasImmediate = opcode switch
            {
                0x00 or 0x01 or 0x02 or 0x04 or 0x05 or 0x06 or 
                0x08 or 0x09 or 0x0A or 0x0B or 0x0C or 0x0D or 0x0E or 0x0F or
                0x14 or 0x15 or 0x16 or 0x18 or 0x19 or 0x20 or 0x21 or 0x22 or
                0x38 or 0x39 or 0x40 or 0x41 or 0x42 or 0x46 or 0x60 or 0x61 or 0x62 or 0x63 => true,
                _ => false
            };

            return DecodeAVXInstructionWithImmediate(instruction, baseMnemonic, hasImmediate);
        }

        /// <summary>
        /// Get AVX mnemonic based on VEX.pp field (equivalent to legacy prefixes)
        /// </summary>
        private string GetAVXMnemonic(string nonePrefix, string prefix66, string prefixF3, string prefixF2)
        {
            // This should be called on instructions that have VEX prefix
            // The pp field in VEX determines which variant to use
            if (_currentInstruction?.VEX == null)
                return nonePrefix;

            return _currentInstruction.VEX.pp switch
            {
                0x00 => nonePrefix,  // No prefix equivalent
                0x01 => prefix66,    // 0x66 prefix equivalent  
                0x02 => prefixF3,    // 0xF3 prefix equivalent
                0x03 => prefixF2,    // 0xF2 prefix equivalent
                _ => nonePrefix
            };
        }

        /// <summary>
        /// Decode AVX instruction with 3-operand format
        /// </summary>
        private bool DecodeAVXInstruction(X86Decoder.X86Instruction instruction, string mnemonic)
        {
            if (string.IsNullOrEmpty(mnemonic) || instruction.VEX == null)
                return false;

            instruction.Mnemonic = mnemonic;

            // Parse ModR/M byte
            if (_position >= _code.Length)
                return false;

            byte modRM = _code[_position++];
            instruction.ModRM = modRM;
            var (mod, reg, rm) = X86Decoder.DecodeModRM(modRM);

            // Determine register size based on VEX.L bit
            bool is256Bit = instruction.VEX.L;
            int registerSize = is256Bit ? 32 : 16; // 256-bit or 128-bit
            var registerType = is256Bit ? X86Decoder.OperandType.YMMRegister : X86Decoder.OperandType.XMMRegister;

            // AVX instructions typically have 3 operands: dest, src1, src2
            // Destination: ModR/M.reg field
            var destOperand = new X86Decoder.X86Operand(registerType, registerSize);
            if (is256Bit)
                destOperand.YMMRegister = reg;
            else
                destOperand.XMMRegister = reg;
            instruction.Operands.Add(destOperand);

            // Source 1: VEX.vvvv field (inverted)
            if (instruction.VEX.vvvv != 0xF) // vvvv = 1111 means no second operand
            {
                var src1Operand = new X86Decoder.X86Operand(registerType, registerSize);
                int vvvvReg = instruction.VEX.GetVRegister();
                if (is256Bit)
                    src1Operand.YMMRegister = vvvvReg;
                else
                    src1Operand.XMMRegister = vvvvReg;
                instruction.Operands.Add(src1Operand);
            }

            // Source 2: ModR/M.rm field (register or memory)
            if (mod == 3) // Register
            {
                var src2Operand = new X86Decoder.X86Operand(registerType, registerSize);
                if (is256Bit)
                    src2Operand.YMMRegister = rm;
                else
                    src2Operand.XMMRegister = rm;
                instruction.Operands.Add(src2Operand);
            }
            else // Memory operand
            {
                var memOperand = DecodeMemoryOperand(mod, rm, registerSize);
                if (memOperand != null)
                    instruction.Operands.Add(memOperand);
            }

            return true;
        }

        /// <summary>
        /// Decode AVX instruction with immediate operand
        /// </summary>
        private bool DecodeAVXInstructionWithImmediate(X86Decoder.X86Instruction instruction, string mnemonic, bool hasImmediate)
        {
            // First decode the standard AVX instruction format
            if (!DecodeAVXInstruction(instruction, mnemonic))
                return false;

            // Add immediate operand if required
            if (hasImmediate)
            {
                if (_position >= _code.Length)
                    return false;

                byte immediate = _code[_position++];
                var immOperand = new X86Decoder.X86Operand(X86Decoder.OperandType.Immediate, 1);
                immOperand.Immediate = immediate;
                instruction.Operands.Add(immOperand);
                instruction.Length++;
            }

            return true;
        }

        // Keep track of current instruction for mnemonic selection
        private X86Decoder.X86Instruction? _currentInstruction;

        /// <summary>
        /// Decode EVEX 0x0F map opcodes (most common AVX-512 instructions)
        /// </summary>
        private bool DecodeEVEXOpcode0F(X86Decoder.X86Instruction instruction, byte opcode)
        {
            switch (opcode)
            {
                case 0x6F: // VMOVDQU32/64
                    instruction.Mnemonic = instruction.EVEX?.W == true ? "vmovdqu64" : "vmovdqu32";
                    return DecodeEVEXInstruction(instruction, "vmovdqu");
                    
                case 0x7F: // VMOVDQU32/64 (store)
                    instruction.Mnemonic = instruction.EVEX?.W == true ? "vmovdqu64" : "vmovdqu32";
                    return DecodeEVEXInstruction(instruction, "vmovdqu");
                    
                case 0xFE: // VPADDD/Q
                    instruction.Mnemonic = instruction.EVEX?.W == true ? "vpaddq" : "vpaddd";
                    return DecodeEVEXInstruction(instruction, "vpadd");
                    
                case 0xDB: // VPANDD/Q
                    instruction.Mnemonic = instruction.EVEX?.W == true ? "vpandq" : "vpandd";
                    return DecodeEVEXInstruction(instruction, "vpand");
                    
                case 0xEB: // VPORD/Q
                    instruction.Mnemonic = instruction.EVEX?.W == true ? "vporq" : "vpord";
                    return DecodeEVEXInstruction(instruction, "vpor");
                    
                case 0xEF: // VPXORD/Q
                    instruction.Mnemonic = instruction.EVEX?.W == true ? "vpxorq" : "vpxord";
                    return DecodeEVEXInstruction(instruction, "vpxor");
                
                default:
                    return false; // Unsupported opcode
            }
        }

        /// <summary>
        /// Decode EVEX 0x0F38 map opcodes
        /// </summary>
        private bool DecodeEVEXOpcode0F38(X86Decoder.X86Instruction instruction, byte opcode)
        {
            switch (opcode)
            {
                case 0x40: // VPMULLD/Q
                    instruction.Mnemonic = instruction.EVEX?.W == true ? "vpmullq" : "vpmulld";
                    return DecodeEVEXInstruction(instruction, "vpmull");
                    
                default:
                    return false; // Unsupported opcode
            }
        }

        /// <summary>
        /// Decode EVEX 0x0F3A map opcodes
        /// </summary>
        private bool DecodeEVEXOpcode0F3A(X86Decoder.X86Instruction instruction, byte opcode)
        {
            // Most 0x0F3A instructions have immediate operands
            switch (opcode)
            {
                default:
                    return false; // Unsupported opcode
            }
        }

        /// <summary>
        /// Decode EVEX-encoded AVX-512 instruction operands
        /// </summary>
        private bool DecodeEVEXInstruction(X86Decoder.X86Instruction instruction, string baseMnemonic)
        {
            if (instruction.EVEX == null)
                return false;
                
            // Parse ModR/M byte if present (most AVX-512 instructions have it)
            if (_position >= _code.Length)
                return false;
                
            byte modRM = _code[_position++];
            instruction.ModRM = modRM;
            
            // Extract ModR/M fields
            int mod = (modRM >> 6) & 0x03;
            int reg = (modRM >> 3) & 0x07;
            int rm = modRM & 0x07;
            
            // Get vector length from EVEX.LL field
            int vectorLength = instruction.EVEX.GetVectorLength();
            int operandSize = vectorLength / 8; // Convert bits to bytes
            
            // Decode first operand (destination - always ZMM register)
            var destOperand = new X86Decoder.X86Operand(X86Decoder.OperandType.ZMMRegister, operandSize);
            destOperand.ZMMRegister = reg; // Extended by EVEX.R
            instruction.Operands.Add(destOperand);
            
            // Decode second operand (vvvv field - ZMM register)
            var src1Operand = new X86Decoder.X86Operand(X86Decoder.OperandType.ZMMRegister, operandSize);
            src1Operand.ZMMRegister = instruction.EVEX.GetVRegister();
            instruction.Operands.Add(src1Operand);
            
            // Decode third operand (ModR/M r/m field)
            if (mod == 0x03) // Register mode
            {
                var src2Operand = new X86Decoder.X86Operand(X86Decoder.OperandType.ZMMRegister, operandSize);
                src2Operand.ZMMRegister = rm; // Extended by EVEX.B
                instruction.Operands.Add(src2Operand);
            }
            else // Memory mode
            {
                var memOperand = new X86Decoder.X86Operand(X86Decoder.OperandType.Memory, operandSize);
                // Simplified memory operand - would need full SIB decoding for production
                memOperand.Memory = new X86Decoder.X86MemoryOperand
                {
                    Base = (X86Decoder.X86Register)rm,
                    Displacement = 0 // Simplified
                };
                instruction.Operands.Add(memOperand);
                
                // Would need to parse displacement bytes here for full implementation
            }
            
            return true;
        }
    }
}