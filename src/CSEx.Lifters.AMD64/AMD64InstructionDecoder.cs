using System;
using System.Collections.Generic;

namespace CSEx.Lifters.AMD64
{
    /// <summary>
    /// AMD64 instruction decoder - extends x86 decoding with 64-bit support
    /// Handles REX prefixes, RIP-relative addressing, and AMD64-specific instructions
    /// Based on Intel 64 and IA-32 Architecture Software Developer's Manual
    /// </summary>
    public class AMD64InstructionDecoder
    {
        private byte[] _code = Array.Empty<byte>();
        private int _position;

        /// <summary>
        /// Decode an AMD64 instruction at the specified position
        /// </summary>
        public AMD64Instruction? DecodeInstruction(byte[] code, int position)
        {
            _code = code ?? throw new ArgumentNullException(nameof(code));
            _position = position;

            if (_position >= _code.Length)
                return null;

            try
            {
                var instruction = new AMD64Instruction();
                int startPosition = _position;

                // Parse prefixes (including REX)
                ParsePrefixes(instruction);

                // Parse opcode
                if (!ParseOpcode(instruction))
                    return null;

                // Parse ModR/M and operands
                ParseOperands(instruction);

                // Set instruction length
                instruction.Length = _position - startPosition;
                
                // Copy raw bytes
                var length = instruction.Length;
                instruction.Bytes = new byte[length];
                Array.Copy(_code, startPosition, instruction.Bytes, 0, length);

                return instruction;
            }
            catch (Exception)
            {
                // Decoding failed
                return null;
            }
        }

        /// <summary>
        /// Parse instruction prefixes including REX
        /// </summary>
        private void ParsePrefixes(AMD64Instruction instruction)
        {
            bool foundREX = false;
            
            while (_position < _code.Length && !foundREX)
            {
                byte b = _code[_position];
                
                // Check for REX prefix (0x40-0x4F)
                if ((b & 0xF0) == 0x40)
                {
                    instruction.REXPrefix = new REXPrefix(b);
                    instruction.HasREXPrefix = true;
                    foundREX = true;
                    _position++;
                    
                    // Set default operand size based on REX.W
                    if (instruction.REXPrefix.W)
                    {
                        instruction.DefaultOperandSize = OperandSize.Size64;
                    }
                }
                // Check for other prefixes (operand size, address size, etc.)
                else if (IsPrefix(b))
                {
                    HandlePrefix(b, instruction);
                    _position++;
                }
                else
                {
                    break;
                }
            }
        }

        /// <summary>
        /// Check if byte is a prefix
        /// </summary>
        private bool IsPrefix(byte b)
        {
            return b switch
            {
                0x66 => true, // Operand size override
                0x67 => true, // Address size override
                0xF0 => true, // LOCK
                0xF2 => true, // REPNE
                0xF3 => true, // REP/REPE
                0x2E or 0x36 or 0x3E or 0x26 or 0x64 or 0x65 => true, // Segment overrides
                _ => false
            };
        }

        /// <summary>
        /// Handle non-REX prefixes
        /// </summary>
        private void HandlePrefix(byte prefix, AMD64Instruction instruction)
        {
            switch (prefix)
            {
                case 0x66: // Operand size override
                    if (instruction.DefaultOperandSize == OperandSize.Size32)
                        instruction.DefaultOperandSize = OperandSize.Size16;
                    break;
                case 0x67: // Address size override
                    instruction.AddressSize = OperandSize.Size32;
                    break;
                // Handle other prefixes as needed
            }
        }

        /// <summary>
        /// Parse instruction opcode with comprehensive AMD64 support
        /// </summary>
        private bool ParseOpcode(AMD64Instruction instruction)
        {
            if (_position >= _code.Length)
                return false;

            byte opcode = _code[_position++];
            
            // Handle one-byte opcodes
            instruction.Mnemonic = opcode switch
            {
                // Data movement
                0x88 => "mov", // MOV r/m8, r8
                0x89 => "mov", // MOV r/m16/32/64, r16/32/64
                0x8A => "mov", // MOV r8, r/m8
                0x8B => "mov", // MOV r16/32/64, r/m16/32/64
                0x8C => "mov", // MOV r/m16, Sreg
                0x8E => "mov", // MOV Sreg, r/m16
                0xA0 => "mov", // MOV AL, moffs8
                0xA1 => "mov", // MOV rAX, moffs16/32/64
                0xA2 => "mov", // MOV moffs8, AL
                0xA3 => "mov", // MOV moffs16/32/64, rAX
                0xB0 => "mov", // MOV AL, imm8
                0xB1 => "mov", // MOV CL, imm8
                0xB2 => "mov", // MOV DL, imm8
                0xB3 => "mov", // MOV BL, imm8
                0xB4 => "mov", // MOV AH, imm8
                0xB5 => "mov", // MOV CH, imm8
                0xB6 => "mov", // MOV DH, imm8
                0xB7 => "mov", // MOV BH, imm8
                0xB8 => "mov", // MOV eAX, imm32
                0xB9 => "mov", // MOV eCX, imm32
                0xBA => "mov", // MOV eDX, imm32
                0xBB => "mov", // MOV eBX, imm32
                0xBC => "mov", // MOV eSP, imm32
                0xBD => "mov", // MOV eBP, imm32
                0xBE => "mov", // MOV eSI, imm32
                0xBF => "mov", // MOV eDI, imm32
                0xC6 => "mov", // MOV r/m8, imm8
                0xC7 => "mov", // MOV r/m16/32/64, imm16/32
                
                // Arithmetic operations
                0x00 => "add", // ADD r/m8, r8
                0x01 => "add", // ADD r/m16/32/64, r16/32/64
                0x02 => "add", // ADD r8, r/m8
                0x03 => "add", // ADD r16/32/64, r/m16/32/64
                0x04 => "add", // ADD AL, imm8
                0x05 => "add", // ADD rAX, imm16/32
                0x80 => ParseGroup1(instruction), // Group 1 instructions (ADD, SUB, etc.)
                0x81 => ParseGroup1(instruction),
                0x82 => ParseGroup1(instruction),
                0x83 => ParseGroup1(instruction),
                
                0x28 => "sub", // SUB r/m16/32/64, r16/32/64
                0x29 => "sub", // SUB r/m16/32/64, r16/32/64
                0x2A => "sub", // SUB r8, r/m8
                0x2B => "sub", // SUB r16/32/64, r/m16/32/64
                0x2C => "sub", // SUB AL, imm8
                0x2D => "sub", // SUB rAX, imm16/32
                
                0x38 => "cmp", // CMP r/m8, r8
                0x39 => "cmp", // CMP r/m16/32/64, r16/32/64
                0x3A => "cmp", // CMP r8, r/m8
                0x3B => "cmp", // CMP r16/32/64, r/m16/32/64
                0x3C => "cmp", // CMP AL, imm8
                0x3D => "cmp", // CMP rAX, imm16/32
                
                0x20 => "and", // AND r/m8, r8
                0x21 => "and", // AND r/m16/32/64, r16/32/64
                0x22 => "and", // AND r8, r/m8
                0x23 => "and", // AND r16/32/64, r/m16/32/64
                0x24 => "and", // AND AL, imm8
                0x25 => "and", // AND rAX, imm16/32
                
                0x08 => "or", // OR r/m8, r8
                0x09 => "or", // OR r/m16/32/64, r16/32/64
                0x0A => "or", // OR r8, r/m8
                0x0B => "or", // OR r16/32/64, r/m16/32/64
                0x0C => "or", // OR AL, imm8
                0x0D => "or", // OR rAX, imm16/32
                
                0x30 => "xor", // XOR r/m8, r8
                0x31 => "xor", // XOR r/m16/32/64, r16/32/64
                0x32 => "xor", // XOR r8, r/m8
                0x33 => "xor", // XOR r16/32/64, r/m16/32/64
                0x34 => "xor", // XOR AL, imm8
                0x35 => "xor", // XOR rAX, imm16/32
                
                0x84 => "test", // TEST r/m8, r8
                0x85 => "test", // TEST r/m16/32/64, r16/32/64
                0xA8 => "test", // TEST AL, imm8
                0xA9 => "test", // TEST rAX, imm16/32
                0xF6 => ParseGroup3a(instruction), // Group 3a (TEST, NOT, NEG, etc.)
                0xF7 => ParseGroup3b(instruction), // Group 3b
                
                // Stack operations
                0x50 => "push", // PUSH rAX
                0x51 => "push", // PUSH rCX
                0x52 => "push", // PUSH rDX
                0x53 => "push", // PUSH rBX
                0x54 => "push", // PUSH rSP
                0x55 => "push", // PUSH rBP
                0x56 => "push", // PUSH rSI
                0x57 => "push", // PUSH rDI
                0x58 => "pop",  // POP rAX
                0x59 => "pop",  // POP rCX
                0x5A => "pop",  // POP rDX
                0x5B => "pop",  // POP rBX
                0x5C => "pop",  // POP rSP
                0x5D => "pop",  // POP rBP
                0x5E => "pop",  // POP rSI
                0x5F => "pop",  // POP rDI
                0x68 => "push", // PUSH imm32
                0x6A => "push", // PUSH imm8
                0x8F => "pop",  // POP r/m16/32/64
                0xFF => ParseGroup5(instruction), // Group 5 (PUSH, POP, CALL, JMP, etc.)
                
                // Control flow
                0xC2 => "ret",  // RET imm16
                0xC3 => "ret",  // RET
                0xCA => "retf", // RETF imm16
                0xCB => "retf", // RETF
                
                // Shift and rotate instructions
                0xD0 => ParseGroup2(instruction), // Group 2 (8-bit shift/rotate by 1)
                0xD1 => ParseGroup2(instruction), // Group 2 (16/32/64-bit shift/rotate by 1)
                0xD2 => ParseGroup2(instruction), // Group 2 (8-bit shift/rotate by CL)
                0xD3 => ParseGroup2(instruction), // Group 2 (16/32/64-bit shift/rotate by CL)
                
                0xE8 => "call", // CALL rel32
                0x9A => "callf", // CALLF ptr16:32
                
                0xEB => "jmp",  // JMP rel8
                0xE9 => "jmp",  // JMP rel32
                0xEA => "jmpf", // JMPF ptr16:32
                
                // Conditional jumps
                0x70 => "jo",   // JO rel8
                0x71 => "jno",  // JNO rel8
                0x72 => "jb",   // JB rel8
                0x73 => "jae",  // JAE rel8
                0x74 => "je",   // JE rel8
                0x75 => "jne",  // JNE rel8
                0x76 => "jbe",  // JBE rel8
                0x77 => "ja",   // JA rel8
                0x78 => "js",   // JS rel8
                0x79 => "jns",  // JNS rel8
                0x7A => "jp",   // JP rel8
                0x7B => "jnp",  // JNP rel8
                0x7C => "jl",   // JL rel8
                0x7D => "jge",  // JGE rel8
                0x7E => "jle",  // JLE rel8
                0x7F => "jg",   // JG rel8
                
                // String operations
                0xA4 => "movsb", // MOVSB
                0xA5 => "movs",  // MOVSW/MOVSD/MOVSQ
                0xA6 => "cmpsb", // CMPSB
                0xA7 => "cmps",  // CMPSW/CMPSD/CMPSQ
                0xAA => "stosb", // STOSB
                0xAB => "stos",  // STOSW/STOSD/STOSQ
                0xAC => "lodsb", // LODSB
                0xAD => "lods",  // LODSW/LODSD/LODSQ
                0xAE => "scasb", // SCASB
                0xAF => "scas",  // SCASW/SCASD/SCASQ
                
                // Misc operations
                0x90 => "nop",  // NOP (XCHG rAX, rAX)
                0x91 => "xchg", // XCHG rAX, rCX
                0x92 => "xchg", // XCHG rAX, rDX
                0x93 => "xchg", // XCHG rAX, rBX
                0x94 => "xchg", // XCHG rAX, rSP
                0x95 => "xchg", // XCHG rAX, rBP
                0x96 => "xchg", // XCHG rAX, rSI
                0x97 => "xchg", // XCHG rAX, rDI
                0x86 => "xchg", // XCHG r/m8, r8
                0x87 => "xchg", // XCHG r/m16/32/64, r16/32/64
                
                0x8D => "lea",  // LEA r16/32/64, m
                
                // Processor control
                0xF4 => "hlt",  // HLT
                0xFA => "cli",  // CLI
                0xFB => "sti",  // STI
                0xFC => "cld",  // CLD
                0xFD => "std",  // STD
                0xF8 => "clc",  // CLC
                0xF9 => "stc",  // STC
                0x9C => "pushf", // PUSHF
                0x9D => "popf",  // POPF
                
                // Two-byte opcodes
                0x0F => ParseTwoByteOpcode(instruction),
                
                _ => "unknown"
            };

            return instruction.Mnemonic != "unknown";
        }

        /// <summary>
        /// Parse Group 1 instructions (arithmetic with immediate)
        /// </summary>
        private string ParseGroup1(AMD64Instruction instruction)
        {
            if (_position >= _code.Length)
                return "unknown";

            byte modRM = _code[_position];
            byte reg = (byte)((modRM >> 3) & 0x7);
            
            return reg switch
            {
                0 => "add",
                1 => "or",
                2 => "adc",
                3 => "sbb",
                4 => "and",
                5 => "sub",
                6 => "xor",
                7 => "cmp",
                _ => "unknown"
            };
        }

        /// <summary>
        /// Parse Group 2 instructions (shift and rotate operations)
        /// </summary>
        private string ParseGroup2(AMD64Instruction instruction)
        {
            if (_position >= _code.Length)
                return "unknown";

            byte modRM = _code[_position];
            byte reg = (byte)((modRM >> 3) & 0x7);
            
            return reg switch
            {
                0 => "rol",  // ROL
                1 => "ror",  // ROR  
                2 => "rcl",  // RCL (rotate through carry left)
                3 => "rcr",  // RCR (rotate through carry right)
                4 => "shl",  // SHL/SAL (shift left)
                5 => "shr",  // SHR (shift right)
                6 => "shl",  // SAL (same as SHL)
                7 => "sar",  // SAR (shift arithmetic right)
                _ => "unknown"
            };
        }

        /// <summary>
        /// Parse Group 3a instructions (unary operations on r/m8)
        /// </summary>
        private string ParseGroup3a(AMD64Instruction instruction)
        {
            if (_position >= _code.Length)
                return "unknown";

            byte modRM = _code[_position];
            byte reg = (byte)((modRM >> 3) & 0x7);
            
            return reg switch
            {
                0 => "test",
                1 => "test", // Same as 0
                2 => "not",
                3 => "neg",
                4 => "mul",
                5 => "imul",
                6 => "div",
                7 => "idiv",
                _ => "unknown"
            };
        }

        /// <summary>
        /// Parse Group 3b instructions (unary operations on r/m16/32/64)
        /// </summary>
        private string ParseGroup3b(AMD64Instruction instruction)
        {
            return ParseGroup3a(instruction); // Same opcodes
        }

        /// <summary>
        /// Parse Group 5 instructions (inc, dec, call, jmp, push)
        /// </summary>
        private string ParseGroup5(AMD64Instruction instruction)
        {
            if (_position >= _code.Length)
                return "unknown";

            byte modRM = _code[_position];
            byte reg = (byte)((modRM >> 3) & 0x7);
            
            return reg switch
            {
                0 => "inc",
                1 => "dec",
                2 => "call",
                3 => "callf",
                4 => "jmp",
                5 => "jmpf",
                6 => "push",
                7 => "unknown", // Invalid
                _ => "unknown"
            };
        }

        /// <summary>
        /// Parse two-byte opcodes (0x0F prefix) with comprehensive AMD64 support
        /// </summary>
        private string ParseTwoByteOpcode(AMD64Instruction instruction)
        {
            if (_position >= _code.Length)
                return "unknown";

            byte opcode2 = _code[_position++];
            
            return opcode2 switch
            {
                // System instructions (AMD64-specific)
                0x05 => ParseSyscall(instruction),
                0x07 => ParseSysret(instruction),
                0x01 => ParseGroup7(instruction), // Group 7 instructions (SWAPGS, etc.)
                
                // Conditional moves
                0x40 => "cmovo",   // CMOVO r16/32/64, r/m16/32/64
                0x41 => "cmovno",  // CMOVNO r16/32/64, r/m16/32/64
                0x42 => "cmovb",   // CMOVB r16/32/64, r/m16/32/64
                0x43 => "cmovae",  // CMOVAE r16/32/64, r/m16/32/64
                0x44 => "cmove",   // CMOVE r16/32/64, r/m16/32/64
                0x45 => "cmovne",  // CMOVNE r16/32/64, r/m16/32/64
                0x46 => "cmovbe",  // CMOVBE r16/32/64, r/m16/32/64
                0x47 => "cmova",   // CMOVA r16/32/64, r/m16/32/64
                0x48 => "cmovs",   // CMOVS r16/32/64, r/m16/32/64
                0x49 => "cmovns",  // CMOVNS r16/32/64, r/m16/32/64
                0x4A => "cmovp",   // CMOVP r16/32/64, r/m16/32/64
                0x4B => "cmovnp",  // CMOVNP r16/32/64, r/m16/32/64
                0x4C => "cmovl",   // CMOVL r16/32/64, r/m16/32/64
                0x4D => "cmovge",  // CMOVGE r16/32/64, r/m16/32/64
                0x4E => "cmovle",  // CMOVLE r16/32/64, r/m16/32/64
                0x4F => "cmovg",   // CMOVG r16/32/64, r/m16/32/64
                
                // Extended conditional jumps
                0x80 => "jo",      // JO rel32
                0x81 => "jno",     // JNO rel32
                0x82 => "jb",      // JB rel32
                0x83 => "jae",     // JAE rel32
                0x84 => "je",      // JE rel32
                0x85 => "jne",     // JNE rel32
                0x86 => "jbe",     // JBE rel32
                0x87 => "ja",      // JA rel32
                0x88 => "js",      // JS rel32
                0x89 => "jns",     // JNS rel32
                0x8A => "jp",      // JP rel32
                0x8B => "jnp",     // JNP rel32
                0x8C => "jl",      // JL rel32
                0x8D => "jge",     // JGE rel32
                0x8E => "jle",     // JLE rel32
                0x8F => "jg",      // JG rel32
                
                // Set byte on condition
                0x90 => "seto",    // SETO r/m8
                0x91 => "setno",   // SETNO r/m8
                0x92 => "setb",    // SETB r/m8
                0x93 => "setae",   // SETAE r/m8
                0x94 => "sete",    // SETE r/m8
                0x95 => "setne",   // SETNE r/m8
                0x96 => "setbe",   // SETBE r/m8
                0x97 => "seta",    // SETA r/m8
                0x98 => "sets",    // SETS r/m8
                0x99 => "setns",   // SETNS r/m8
                0x9A => "setp",    // SETP r/m8
                0x9B => "setnp",   // SETNP r/m8
                0x9C => "setl",    // SETL r/m8
                0x9D => "setge",   // SETGE r/m8
                0x9E => "setle",   // SETLE r/m8
                0x9F => "setg",    // SETG r/m8
                
                // Bit manipulation
                0xA3 => "bt",      // BT r/m16/32/64, r16/32/64
                0xAB => "bts",     // BTS r/m16/32/64, r16/32/64
                0xB3 => "btr",     // BTR r/m16/32/64, r16/32/64
                0xBB => "btc",     // BTC r/m16/32/64, r16/32/64
                0xBA => ParseGroup8(instruction), // Group 8 (BT, BTS, BTR, BTC with immediate)
                0xBC => "bsf",     // BSF r16/32/64, r/m16/32/64
                0xBD => "bsr",     // BSR r16/32/64, r/m16/32/64
                
                // Data movement extensions
                0xB6 => "movzx",   // MOVZX r16/32/64, r/m8
                0xB7 => "movzx",   // MOVZX r32/64, r/m16
                0xBE => "movsx",   // MOVSX r16/32/64, r/m8
                0xBF => "movsx",   // MOVSX r32/64, r/m16
                
                // Extended arithmetic
                0xA4 => "shld",    // SHLD r/m16/32/64, r16/32/64, imm8
                0xA5 => "shld",    // SHLD r/m16/32/64, r16/32/64, CL
                0xAC => "shrd",    // SHRD r/m16/32/64, r16/32/64, imm8
                0xAD => "shrd",    // SHRD r/m16/32/64, r16/32/64, CL
                0xAF => "imul",    // IMUL r16/32/64, r/m16/32/64
                
                // Atomic operations
                0xB0 => "cmpxchg", // CMPXCHG r/m8, r8
                0xB1 => "cmpxchg", // CMPXCHG r/m16/32/64, r16/32/64
                0xC0 => "xadd",    // XADD r/m8, r8
                0xC1 => "xadd",    // XADD r/m16/32/64, r16/32/64
                0xC7 => ParseGroup9(instruction), // Group 9 (CMPXCHG8B/16B)
                
                // Processor identification
                0xA2 => "cpuid",   // CPUID
                
                // Performance monitoring
                0x30 => "wrmsr",   // WRMSR
                0x31 => "rdtsc",   // RDTSC
                0x32 => "rdmsr",   // RDMSR
                0x33 => "rdpmc",   // RDPMC
                
                // Cache control
                0x08 => "invd",    // INVD
                0x09 => "wbinvd",  // WBINVD
                0x18 => ParseGroup16(instruction), // Group 16 (PREFETCH)
                0xAE => ParseGroup15(instruction), // Group 15 (CLFLUSH, etc.)
                
                // Memory ordering
                0x38 => ParseThreeByteOpcode38(instruction), // Three-byte opcodes 0x0F 0x38
                0x3A => ParseThreeByteOpcode3A(instruction), // Three-byte opcodes 0x0F 0x3A
                
                _ => "unknown"
            };
        }

        /// <summary>
        /// Parse Group 8 instructions (bit test with immediate)
        /// </summary>
        private string ParseGroup8(AMD64Instruction instruction)
        {
            if (_position >= _code.Length)
                return "unknown";

            byte modRM = _code[_position];
            byte reg = (byte)((modRM >> 3) & 0x7);
            
            return reg switch
            {
                4 => "bt",
                5 => "bts",
                6 => "btr",
                7 => "btc",
                _ => "unknown"
            };
        }

        /// <summary>
        /// Parse Group 9 instructions (CMPXCHG8B/16B)
        /// </summary>
        private string ParseGroup9(AMD64Instruction instruction)
        {
            if (_position >= _code.Length)
                return "unknown";

            byte modRM = _code[_position];
            byte reg = (byte)((modRM >> 3) & 0x7);
            
            return reg switch
            {
                1 => instruction.HasREXPrefix && instruction.REXPrefix.W ? "cmpxchg16b" : "cmpxchg8b",
                _ => "unknown"
            };
        }

        /// <summary>
        /// Parse Group 15 instructions (cache control)
        /// </summary>
        private string ParseGroup15(AMD64Instruction instruction)
        {
            if (_position >= _code.Length)
                return "unknown";

            byte modRM = _code[_position];
            byte reg = (byte)((modRM >> 3) & 0x7);
            
            return reg switch
            {
                0 => "fxsave",
                1 => "fxrstor",
                2 => "ldmxcsr",
                3 => "stmxcsr",
                4 => "xsave",
                5 => "xrstor",
                6 => "xsaveopt",
                7 => "clflush",
                _ => "unknown"
            };
        }

        /// <summary>
        /// Parse Group 16 instructions (prefetch)
        /// </summary>
        private string ParseGroup16(AMD64Instruction instruction)
        {
            if (_position >= _code.Length)
                return "unknown";

            byte modRM = _code[_position];
            byte reg = (byte)((modRM >> 3) & 0x7);
            
            return reg switch
            {
                0 => "prefetchnta",
                1 => "prefetcht0",
                2 => "prefetcht1",
                3 => "prefetcht2",
                _ => "unknown"
            };
        }

        /// <summary>
        /// Parse three-byte opcodes 0x0F 0x38
        /// </summary>
        private string ParseThreeByteOpcode38(AMD64Instruction instruction)
        {
            if (_position >= _code.Length)
                return "unknown";

            byte opcode3 = _code[_position++];
            
            return opcode3 switch
            {
                // SSE4.1 instructions
                0x00 => "pshufb",
                0x01 => "phaddw",
                0x02 => "phaddd",
                0x03 => "phaddsw",
                0x04 => "pmaddubsw",
                0x05 => "phsubw",
                0x06 => "phsubd",
                0x07 => "phsubsw",
                0x08 => "psignb",
                0x09 => "psignw",
                0x0A => "psignd",
                0x0B => "pmulhrsw",
                0x10 => "pblendvb",
                0x14 => "blendvps",
                0x15 => "blendvpd",
                0x17 => "ptest",
                0x1C => "pabsb",
                0x1D => "pabsw",
                0x1E => "pabsd",
                0x20 => "pmovsxbw",
                0x21 => "pmovsxbd",
                0x22 => "pmovsxbq",
                0x23 => "pmovsxwd",
                0x24 => "pmovsxwq",
                0x25 => "pmovsxdq",
                0x28 => "pmuldq",
                0x29 => "pcmpeqq",
                0x2A => "movntdqa",
                0x2B => "packusdw",
                0x30 => "pmovzxbw",
                0x31 => "pmovzxbd",
                0x32 => "pmovzxbq",
                0x33 => "pmovzxwd",
                0x34 => "pmovzxwq",
                0x35 => "pmovzxdq",
                0x37 => "pcmpgtq",
                0x38 => "pminsb",
                0x39 => "pminsd",
                0x3A => "pminuw",
                0x3B => "pminud",
                0x3C => "pmaxsb",
                0x3D => "pmaxsd",
                0x3E => "pmaxuw",
                0x3F => "pmaxud",
                0x40 => "pmulld",
                0x41 => "phminposuw",
                _ => "unknown"
            };
        }

        /// <summary>
        /// Parse three-byte opcodes 0x0F 0x3A
        /// </summary>
        private string ParseThreeByteOpcode3A(AMD64Instruction instruction)
        {
            if (_position >= _code.Length)
                return "unknown";

            byte opcode3 = _code[_position++];
            
            return opcode3 switch
            {
                // SSE4.1 with immediate
                0x08 => "roundps",
                0x09 => "roundpd",
                0x0A => "roundss",
                0x0B => "roundsd",
                0x0C => "blendps",
                0x0D => "blendpd",
                0x0E => "pblendw",
                0x0F => "palignr",
                0x14 => "pextrb",
                0x15 => "pextrw",
                0x16 => "pextrd",
                0x17 => "extractps",
                0x20 => "pinsrb",
                0x21 => "insertps",
                0x22 => "pinsrd",
                0x40 => "dpps",
                0x41 => "dppd",
                0x42 => "mpsadbw",
                0x60 => "pcmpestrm",
                0x61 => "pcmpestri",
                0x62 => "pcmpistrm",
                0x63 => "pcmpistri",
                _ => "unknown"
            };
        }

        /// <summary>
        /// Parse SYSCALL instruction (AMD64-only)
        /// </summary>
        private string ParseSyscall(AMD64Instruction instruction)
        {
            instruction.IsAMD64Only = true;
            return "syscall";
        }

        /// <summary>
        /// Parse SYSRET instruction (AMD64-only)
        /// </summary>
        private string ParseSysret(AMD64Instruction instruction)
        {
            instruction.IsAMD64Only = true;
            return "sysret";
        }

        /// <summary>
        /// Parse Group 7 instructions (0x0F 0x01)
        /// </summary>
        private string ParseGroup7(AMD64Instruction instruction)
        {
            if (_position >= _code.Length)
                return "unknown";

            byte modRM = _code[_position];
            
            // Check for SWAPGS (0x0F 0x01 0xF8)
            if (modRM == 0xF8)
            {
                _position++;
                instruction.IsAMD64Only = true;
                return "swapgs";
            }
            
            return "unknown";
        }

        /// <summary>
        /// Parse instruction operands with proper ModR/M and SIB byte handling
        /// </summary>
        private void ParseOperands(AMD64Instruction instruction)
        {
            var operands = new List<AMD64Operand>();
            
            // Parse operands based on instruction format
            switch (instruction.Mnemonic)
            {
                case "mov":
                    ParseMovOperands(instruction, operands);
                    break;
                case "add":
                case "sub":
                case "cmp":
                case "and":
                case "or":
                case "xor":
                    ParseArithmeticOperands(instruction, operands);
                    break;
                case "shl":
                case "sal":
                case "shr":
                case "sar":
                case "rol":
                case "ror":
                case "rcl":
                case "rcr":
                    ParseShiftOperands(instruction, operands);
                    break;
                case "push":
                case "pop":
                    ParseStackOperands(instruction, operands);
                    break;
                case "call":
                case "jmp":
                case "je":
                case "jne":
                case "jz":
                case "jnz":
                case "jl":
                case "jg":
                case "jle":
                case "jge":
                case "jb":
                case "ja":
                case "jbe":
                case "jae":
                    ParseControlFlowOperands(instruction, operands);
                    break;
                case "syscall":
                case "sysret":
                case "swapgs":
                    // These instructions have no operands
                    break;
                default:
                    // Try generic ModR/M parsing
                    ParseGenericOperands(instruction, operands);
                    break;
            }
            
            instruction.Operands = operands.ToArray();
        }

        /// <summary>
        /// Parse generic operands using ModR/M byte
        /// </summary>
        private void ParseGenericOperands(AMD64Instruction instruction, List<AMD64Operand> operands)
        {
            if (_position < _code.Length && NeedsModRM(instruction.Mnemonic))
            {
                var modRMByte = ParseModRM();
                var operand1 = ParseRMOperand(modRMByte, instruction);
                var operand2 = ParseRegOperand(modRMByte, instruction);
                
                operands.Add(operand1);
                operands.Add(operand2);
            }
        }

        /// <summary>
        /// Check if instruction needs ModR/M byte parsing
        /// </summary>
        private bool NeedsModRM(string mnemonic)
        {
            return mnemonic switch
            {
                "mov" or "add" or "sub" or "cmp" or "and" or "or" or "xor" or
                "test" or "lea" or "mul" or "div" or "imul" or "idiv" => true,
                _ => false
            };
        }

        /// <summary>
        /// ModR/M byte structure
        /// </summary>
        private struct ModRMByte
        {
            public byte Mod { get; init; }
            public byte Reg { get; init; }
            public byte RM { get; init; }
            
            public ModRMByte(byte value)
            {
                Mod = (byte)((value >> 6) & 0x3);
                Reg = (byte)((value >> 3) & 0x7);
                RM = (byte)(value & 0x7);
            }
        }

        /// <summary>
        /// SIB byte structure
        /// </summary>
        private struct SIBByte
        {
            public byte Scale { get; init; }
            public byte Index { get; init; }
            public byte Base { get; init; }
            
            public SIBByte(byte value)
            {
                Scale = (byte)((value >> 6) & 0x3);
                Index = (byte)((value >> 3) & 0x7);
                Base = (byte)(value & 0x7);
            }
        }

        /// <summary>
        /// Parse ModR/M byte
        /// </summary>
        private ModRMByte ParseModRM()
        {
            if (_position >= _code.Length)
                throw new InvalidOperationException("Unexpected end of instruction stream");
                
            return new ModRMByte(_code[_position++]);
        }

        /// <summary>
        /// Parse SIB byte
        /// </summary>
        private SIBByte ParseSIB()
        {
            if (_position >= _code.Length)
                throw new InvalidOperationException("Unexpected end of instruction stream");
                
            return new SIBByte(_code[_position++]);
        }

        /// <summary>
        /// Parse register operand from ModR/M byte
        /// </summary>
        private AMD64Operand ParseRegOperand(ModRMByte modRM, AMD64Instruction instruction)
        {
            var regNum = modRM.Reg;
            
            // Apply REX.R extension
            if (instruction.HasREXPrefix && instruction.REXPrefix.R)
                regNum += 8;
                
            return new AMD64Operand
            {
                Type = AMD64OperandType.Register,
                Size = instruction.DefaultOperandSize,
                Register = GetRegisterName(regNum, instruction.DefaultOperandSize, instruction.REXPrefix)
            };
        }

        /// <summary>
        /// Parse R/M operand from ModR/M byte (register or memory)
        /// </summary>
        private AMD64Operand ParseRMOperand(ModRMByte modRM, AMD64Instruction instruction)
        {
            switch (modRM.Mod)
            {
                case 0: // Memory with no displacement (or RIP-relative)
                    return ParseMemoryOperand(modRM, instruction, 0);
                    
                case 1: // Memory with 8-bit displacement
                    return ParseMemoryOperand(modRM, instruction, 1);
                    
                case 2: // Memory with 32-bit displacement
                    return ParseMemoryOperand(modRM, instruction, 4);
                    
                case 3: // Register
                    var regNum = modRM.RM;
                    if (instruction.HasREXPrefix && instruction.REXPrefix.B)
                        regNum += 8;
                        
                    return new AMD64Operand
                    {
                        Type = AMD64OperandType.Register,
                        Size = instruction.DefaultOperandSize,
                        Register = GetRegisterName(regNum, instruction.DefaultOperandSize, instruction.REXPrefix)
                    };
                    
                default:
                    throw new InvalidOperationException($"Invalid ModR/M mod field: {modRM.Mod}");
            }
        }

        /// <summary>
        /// Parse memory operand with proper SIB and displacement handling
        /// </summary>
        private AMD64Operand ParseMemoryOperand(ModRMByte modRM, AMD64Instruction instruction, int displacementSize)
        {
            var operand = new AMD64Operand
            {
                Type = AMD64OperandType.Memory,
                Size = instruction.DefaultOperandSize,
                Memory = new MemoryOperand()
            };

            var memory = operand.Memory.Value;

            // Handle RIP-relative addressing (AMD64 mode only)
            if (modRM.Mod == 0 && modRM.RM == 5)
            {
                // RIP-relative addressing
                memory.IsRIPRelative = true;
                memory.Displacement = ReadDisplacement(4); // Always 32-bit displacement for RIP-relative
                instruction.RIPDisplacement = (int)memory.Displacement;
                operand.Memory = memory;
                return operand;
            }

            // Handle SIB byte if needed
            if (modRM.RM == 4) // SIB byte present
            {
                var sib = ParseSIB();
                ParseSIBAddressing(sib, ref memory, instruction);
            }
            else
            {
                // Direct register addressing
                var baseReg = modRM.RM;
                if (instruction.HasREXPrefix && instruction.REXPrefix.B)
                    baseReg += 8;
                    
                memory.Base = GetRegisterName(baseReg, OperandSize.Size64, instruction.REXPrefix);
            }

            // Read displacement if present
            if (displacementSize > 0)
            {
                memory.Displacement = ReadDisplacement(displacementSize);
            }

            operand.Memory = memory;
            return operand;
        }

        /// <summary>
        /// Parse SIB addressing mode
        /// </summary>
        private void ParseSIBAddressing(SIBByte sib, ref MemoryOperand memory, AMD64Instruction instruction)
        {
            // Base register
            if (sib.Base != 5) // Base=5 has special meaning depending on Mod field
            {
                var baseReg = sib.Base;
                if (instruction.HasREXPrefix && instruction.REXPrefix.B)
                    baseReg += 8;
                    
                memory.Base = GetRegisterName(baseReg, OperandSize.Size64, instruction.REXPrefix);
            }

            // Index register
            if (sib.Index != 4) // Index=4 means no index register
            {
                var indexReg = sib.Index;
                if (instruction.HasREXPrefix && instruction.REXPrefix.X)
                    indexReg += 8;
                    
                memory.Index = GetRegisterName(indexReg, OperandSize.Size64, instruction.REXPrefix);
                memory.Scale = 1 << sib.Scale; // Scale is 2^(scale field)
            }
        }

        /// <summary>
        /// Read displacement value
        /// </summary>
        private long ReadDisplacement(int size)
        {
            if (_position + size > _code.Length)
                throw new InvalidOperationException("Unexpected end of instruction stream");

            long displacement = 0;
            for (int i = 0; i < size; i++)
            {
                displacement |= (long)_code[_position + i] << (i * 8);
            }

            _position += size;

            // Sign extend if needed
            if (size == 1 && (displacement & 0x80) != 0)
                displacement |= ~0xFFL; // Sign extend from 8-bit
            else if (size == 2 && (displacement & 0x8000) != 0)
                displacement |= ~0xFFFFL; // Sign extend from 16-bit
            else if (size == 4 && (displacement & 0x80000000L) != 0)
                displacement |= ~0xFFFFFFFFL; // Sign extend from 32-bit

            return displacement;
        }

        /// <summary>
        /// Parse MOV instruction operands with comprehensive addressing mode support
        /// </summary>
        private void ParseMovOperands(AMD64Instruction instruction, List<AMD64Operand> operands)
        {
            // Handle immediate-to-register MOV instructions (0xB0-0xBF)
            var lastOpcode = _code[_position - 1];
            if (lastOpcode >= 0xB0 && lastOpcode <= 0xBF)
            {
                // MOV reg, imm
                var regNum = lastOpcode & 0x7;
                if (instruction.HasREXPrefix && instruction.REXPrefix.B)
                    regNum += 8;

                var size = (lastOpcode >= 0xB8) ? instruction.DefaultOperandSize : OperandSize.Size8;
                
                operands.Add(new AMD64Operand
                {
                    Type = AMD64OperandType.Register,
                    Size = size,
                    Register = GetRegisterName(regNum, size, instruction.REXPrefix)
                });
                
                operands.Add(new AMD64Operand
                {
                    Type = AMD64OperandType.Immediate,
                    Size = size,
                    ImmediateValue = ReadImmediate(size)
                });
                return;
            }
            
            // Handle ModR/M based MOV instructions
            if (NeedsModRM(instruction.Mnemonic))
            {
                var modRM = ParseModRM();
                
                // Determine operand order based on opcode
                bool regToRM = lastOpcode == 0x88 || lastOpcode == 0x89 || lastOpcode == 0x8C;
                
                var regOperand = ParseRegOperand(modRM, instruction);
                var rmOperand = ParseRMOperand(modRM, instruction);
                
                if (regToRM)
                {
                    operands.Add(rmOperand);  // destination
                    operands.Add(regOperand); // source
                }
                else
                {
                    operands.Add(regOperand); // destination
                    operands.Add(rmOperand);  // source
                }
            }
        }

        /// <summary>
        /// Parse arithmetic instruction operands
        /// </summary>
        private void ParseArithmeticOperands(AMD64Instruction instruction, List<AMD64Operand> operands)
        {
            var lastOpcode = _code[_position - 1];
            
            // Handle immediate to accumulator operations (AL/AX/EAX/RAX)
            if ((lastOpcode & 0xFC) == 0x04) // 0x04, 0x05 for ADD; similar patterns for other ops
            {
                var size = (lastOpcode & 1) != 0 ? instruction.DefaultOperandSize : OperandSize.Size8;
                
                operands.Add(new AMD64Operand
                {
                    Type = AMD64OperandType.Register,
                    Size = size,
                    Register = size == OperandSize.Size8 ? "al" : 
                              size == OperandSize.Size16 ? "ax" :
                              size == OperandSize.Size32 ? "eax" : "rax"
                });
                
                operands.Add(new AMD64Operand
                {
                    Type = AMD64OperandType.Immediate,
                    Size = size,
                    ImmediateValue = ReadImmediate(size)
                });
                return;
            }
            
            // Handle Group 1 instructions (0x80-0x83)
            if (lastOpcode >= 0x80 && lastOpcode <= 0x83)
            {
                var modRM = ParseModRM();
                var rmOperand = ParseRMOperand(modRM, instruction);
                
                operands.Add(rmOperand);
                
                // Add immediate operand
                var immSize = (lastOpcode == 0x81) ? instruction.DefaultOperandSize : OperandSize.Size8;
                operands.Add(new AMD64Operand
                {
                    Type = AMD64OperandType.Immediate,
                    Size = immSize,
                    ImmediateValue = ReadImmediate(immSize)
                });
                return;
            }
            
            // Handle standard ModR/M arithmetic operations
            if (NeedsModRM(instruction.Mnemonic))
            {
                var modRM = ParseModRM();
                
                // Determine operand order: opcodes with bit 1 set have reg as source
                bool regToRM = (lastOpcode & 0x02) == 0;
                
                var regOperand = ParseRegOperand(modRM, instruction);
                var rmOperand = ParseRMOperand(modRM, instruction);
                
                if (regToRM)
                {
                    operands.Add(rmOperand);  // destination
                    operands.Add(regOperand); // source
                }
                else
                {
                    operands.Add(regOperand); // destination
                    operands.Add(rmOperand);  // source
                }
            }
        }

        /// <summary>
        /// Parse shift/rotate instruction operands (Group 2)
        /// </summary>
        private void ParseShiftOperands(AMD64Instruction instruction, List<AMD64Operand> operands)
        {
            var lastOpcode = _code[_position - 1];
            
            // Handle Group 2 instructions (0xD0-0xD3)
            if (lastOpcode >= 0xD0 && lastOpcode <= 0xD3)
            {
                var modRM = ParseModRM();
                var rmOperand = ParseRMOperand(modRM, instruction);
                operands.Add(rmOperand); // destination
                
                // Add shift count operand
                if (lastOpcode == 0xD0 || lastOpcode == 0xD1)
                {
                    // Shift by 1
                    operands.Add(new AMD64Operand
                    {
                        Type = AMD64OperandType.Immediate,
                        Size = OperandSize.Size8,
                        ImmediateValue = 1
                    });
                }
                else if (lastOpcode == 0xD2 || lastOpcode == 0xD3)
                {
                    // Shift by CL register
                    operands.Add(new AMD64Operand
                    {
                        Type = AMD64OperandType.Register,
                        Size = OperandSize.Size8,
                        Register = "cl"
                    });
                }
            }
            else
            {
                // Handle immediate shift instructions (0xC0-0xC1)
                if (lastOpcode == 0xC0 || lastOpcode == 0xC1)
                {
                    var modRM = ParseModRM();
                    var rmOperand = ParseRMOperand(modRM, instruction);
                    operands.Add(rmOperand); // destination
                    
                    // Add immediate shift count
                    operands.Add(new AMD64Operand
                    {
                        Type = AMD64OperandType.Immediate,
                        Size = OperandSize.Size8,
                        ImmediateValue = ReadImmediate(OperandSize.Size8)
                    });
                }
            }
        }

        /// <summary>
        /// Parse stack instruction operands
        /// </summary>
        private void ParseStackOperands(AMD64Instruction instruction, List<AMD64Operand> operands)
        {
            var lastOpcode = _code[_position - 1];
            
            // Handle register-specific PUSH/POP (0x50-0x5F)
            if ((lastOpcode >= 0x50 && lastOpcode <= 0x5F))
            {
                var regNum = lastOpcode & 0x7;
                if (instruction.HasREXPrefix && instruction.REXPrefix.B)
                    regNum += 8;
                
                operands.Add(new AMD64Operand
                {
                    Type = AMD64OperandType.Register,
                    Size = OperandSize.Size64, // Stack operations are always 64-bit in AMD64
                    Register = GetRegisterName(regNum, OperandSize.Size64, instruction.REXPrefix)
                });
                return;
            }
            
            // Handle immediate PUSH instructions
            if (lastOpcode == 0x68 || lastOpcode == 0x6A)
            {
                var immSize = lastOpcode == 0x68 ? OperandSize.Size32 : OperandSize.Size8;
                operands.Add(new AMD64Operand
                {
                    Type = AMD64OperandType.Immediate,
                    Size = immSize,
                    ImmediateValue = ReadImmediate(immSize)
                });
                return;
            }
            
            // Handle ModR/M based stack operations (Group 5)
            if (lastOpcode == 0xFF || lastOpcode == 0x8F)
            {
                var modRM = ParseModRM();
                operands.Add(ParseRMOperand(modRM, instruction));
            }
        }

        /// <summary>
        /// Parse control flow instruction operands
        /// </summary>
        private void ParseControlFlowOperands(AMD64Instruction instruction, List<AMD64Operand> operands)
        {
            var lastOpcode = _code[_position - 1];
            
            // Handle relative jumps and calls
            if (lastOpcode == 0xE8 || lastOpcode == 0xE9) // CALL rel32, JMP rel32
            {
                operands.Add(new AMD64Operand
                {
                    Type = AMD64OperandType.Immediate,
                    Size = OperandSize.Size32,
                    ImmediateValue = ReadImmediate(OperandSize.Size32)
                });
                return;
            }
            
            if (lastOpcode == 0xEB || (lastOpcode >= 0x70 && lastOpcode <= 0x7F)) // JMP rel8, Jcc rel8
            {
                operands.Add(new AMD64Operand
                {
                    Type = AMD64OperandType.Immediate,
                    Size = OperandSize.Size8,
                    ImmediateValue = ReadImmediate(OperandSize.Size8)
                });
                return;
            }
            
            // Handle two-byte conditional jumps (0x0F 0x80-0x8F)
            if (_position >= 2 && _code[_position - 2] == 0x0F && 
                lastOpcode >= 0x80 && lastOpcode <= 0x8F)
            {
                operands.Add(new AMD64Operand
                {
                    Type = AMD64OperandType.Immediate,
                    Size = OperandSize.Size32,
                    ImmediateValue = ReadImmediate(OperandSize.Size32)
                });
                return;
            }
            
            // Handle RET with immediate
            if (lastOpcode == 0xC2 || lastOpcode == 0xCA)
            {
                operands.Add(new AMD64Operand
                {
                    Type = AMD64OperandType.Immediate,
                    Size = OperandSize.Size16,
                    ImmediateValue = ReadImmediate(OperandSize.Size16)
                });
            }
        }

        /// <summary>
        /// Get register name based on encoding and REX prefix
        /// </summary>
        private string GetRegisterName(int regNum, OperandSize size, REXPrefix rex)
        {
            // Handle extended registers (R8-R15) with REX prefix
            if (rex.R && regNum < 8)
            {
                regNum += 8;
            }
            
            return size switch
            {
                OperandSize.Size8 => regNum switch
                {
                    0 => "al", 1 => "cl", 2 => "dl", 3 => "bl",
                    4 => "spl", 5 => "bpl", 6 => "sil", 7 => "dil", // REX allows access to these
                    8 => "r8b", 9 => "r9b", 10 => "r10b", 11 => "r11b",
                    12 => "r12b", 13 => "r13b", 14 => "r14b", 15 => "r15b",
                    _ => "unknown"
                },
                OperandSize.Size16 => regNum switch
                {
                    0 => "ax", 1 => "cx", 2 => "dx", 3 => "bx",
                    4 => "sp", 5 => "bp", 6 => "si", 7 => "di",
                    8 => "r8w", 9 => "r9w", 10 => "r10w", 11 => "r11w",
                    12 => "r12w", 13 => "r13w", 14 => "r14w", 15 => "r15w",
                    _ => "unknown"
                },
                OperandSize.Size32 => regNum switch
                {
                    0 => "eax", 1 => "ecx", 2 => "edx", 3 => "ebx",
                    4 => "esp", 5 => "ebp", 6 => "esi", 7 => "edi",
                    8 => "r8d", 9 => "r9d", 10 => "r10d", 11 => "r11d",
                    12 => "r12d", 13 => "r13d", 14 => "r14d", 15 => "r15d",
                    _ => "unknown"
                },
                OperandSize.Size64 => regNum switch
                {
                    0 => "rax", 1 => "rcx", 2 => "rdx", 3 => "rbx",
                    4 => "rsp", 5 => "rbp", 6 => "rsi", 7 => "rdi",
                    8 => "r8", 9 => "r9", 10 => "r10", 11 => "r11",
                    12 => "r12", 13 => "r13", 14 => "r14", 15 => "r15",
                    _ => "unknown"
                },
                _ => "unknown"
            };
        }

        /// <summary>
        /// Read immediate value from instruction stream
        /// </summary>
        private long ReadImmediate(OperandSize size)
        {
            int bytes = (int)size;
            if (_position + bytes > _code.Length)
                return 0;

            long value = 0;
            for (int i = 0; i < bytes; i++)
            {
                value |= (long)_code[_position + i] << (i * 8);
            }
            
            _position += bytes;
            return value;
        }
    }
}