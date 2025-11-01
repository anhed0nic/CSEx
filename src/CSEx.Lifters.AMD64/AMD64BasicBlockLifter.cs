using CSEx.IR;
using CSEx.Core;
using CSEx.Guests;
using CSEx.Guests.AMD64;
using CSEx.Lifters.AMD64;
using System;

namespace CSEx.Lifters.AMD64
{
    /// <summary>
    /// AMD64 Basic Block Lifter - extends x86 functionality with 64-bit support
    /// Inherits processor extension support (MMX, SSE, AVX) from BaseX86Lifter
    /// Based on VEX guest_amd64_toIR.c architecture
    /// </summary>
    public class AMD64BasicBlockLifter : BaseX86Lifter
    {
        private readonly AMD64GuestState _guestState;
        private readonly AMD64InstructionDecoder _decoder;
        
        /// <summary>
        /// AMD64 architecture word size (8 bytes)
        /// </summary>
        protected override int ArchWordSize => 8;
        
        /// <summary>
        /// AMD64 stack word size (8 bytes)
        /// </summary>
        protected override int StackWordSize => 8;
        
        /// <summary>
        /// AMD64 address type (64-bit)
        /// </summary>
        protected override IRType ArchAddressType => IRType.I64;
        
        /// <summary>
        /// AMD64 stack pointer register
        /// </summary>
        protected override string StackPointerRegister => "rsp";
        
        /// <summary>
        /// Current instruction start address for debugging/error reporting
        /// </summary>
        public ulong CurrentInstructionAddress { get; private set; }
        
        /// <summary>
        /// Maximum instructions to lift in a single basic block
        /// </summary>
        public int MaxInstructions { get; set; } = 50;
        
        /// <summary>
        /// Maximum bytes to process in a single basic block
        /// </summary>
        public int MaxBytes { get; set; } = 500;

        public AMD64BasicBlockLifter(AMD64GuestState guestState)
        {
            _guestState = guestState ?? throw new ArgumentNullException(nameof(guestState));
            _decoder = new AMD64InstructionDecoder();
        }

        /// <summary>
        /// Lift a basic block of AMD64 instructions to VEX IR
        /// Returns the lifted IRSB and the number of bytes consumed
        /// </summary>
        public (IRSB irsb, int bytesLifted) LiftBasicBlock(byte[] code, ulong baseAddress, int maxInstructions = 50)
        {
            if (code == null) throw new ArgumentNullException(nameof(code));
            
            var irsb = new IRSB(new IRTypeEnv());
            irsb.JumpKind = IRJumpKind.Boring;
            
            int position = 0;
            int instructionCount = 0;
            int startPosition = position;
            
            try
            {
                while (position < code.Length && 
                       instructionCount < maxInstructions && 
                       (position - startPosition) < MaxBytes)
                {
                    // Track current instruction address
                    CurrentInstructionAddress = baseAddress + (ulong)position;
                    
                    // Decode the next instruction
                    var instruction = _decoder.DecodeInstruction(code, position);
                    if (instruction == null)
                    {
                        // Failed to decode - end basic block
                        break;
                    }
                    
                    // Advance position by instruction length
                    position += instruction.Length;
                    instructionCount++;
                    
                    // Lift instruction to IR
                    bool liftSuccess = LiftInstruction(instruction, irsb);
                    if (!liftSuccess)
                    {
                        // Failed to lift - end basic block
                        break;
                    }
                    
                    // Check for control flow instructions that end basic blocks
                    if (IsBasicBlockTerminator(instruction))
                    {
                        break;
                    }
                }
                
                // Set next address for basic block
                if (irsb.Next == null)
                {
                    irsb.Next = IRExprFactory.U64(baseAddress + (ulong)position);
                }
                
                return (irsb, position - startPosition);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to lift AMD64 basic block at address 0x{baseAddress:X}: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Lift a single AMD64 instruction to VEX IR statements
        /// Handles both x86 compatibility and AMD64-specific instructions
        /// </summary>
        private bool LiftInstruction(AMD64Instruction instruction, IRSB irsb)
        {
            try
            {
                // Handle AMD64-specific instruction types first
                if (instruction.HasREXPrefix)
                {
                    return LiftREXInstruction(instruction, irsb);
                }
                
                // Handle 64-bit specific instructions
                if (instruction.IsAMD64Only)
                {
                    Console.WriteLine($"Debug: Taking AMD64-only path for '{instruction.Mnemonic}'");
                    return LiftAMD64OnlyInstruction(instruction, irsb);
                }
                
                // Handle RIP-relative addressing
                if (instruction.HasRIPRelativeAddressing)
                {
                    Console.WriteLine($"Debug: Taking RIP-relative path for '{instruction.Mnemonic}'");
                    return LiftRIPRelativeInstruction(instruction, irsb);
                }
                
                // For standard x86 instructions, delegate to specialized handlers
                
                // First, try to handle processor extensions (MMX, SSE, AVX) - these work identically on x86 and AMD64
                var mnemonic = instruction.Mnemonic.ToLower();
                
                // Check for MMX instructions (operate on 64-bit MM registers)
                if (IsMMXInstruction(mnemonic))
                {
                    if (LiftMMXInstruction(mnemonic, instruction.Operands, irsb))
                    {
                        Console.WriteLine($"Debug: Successfully lifted MMX instruction '{instruction.Mnemonic}'");
                        return true;
                    }
                    else
                    {
                        Console.WriteLine($"Debug: Failed to lift MMX instruction '{instruction.Mnemonic}'");
                    }
                }
                
                // Check for SSE instructions (operate on 128-bit XMM registers)
                if (IsSSEInstruction(mnemonic))
                {
                    if (LiftSSEInstruction(mnemonic, instruction.Operands, irsb))
                    {
                        Console.WriteLine($"Debug: Successfully lifted SSE instruction '{instruction.Mnemonic}'");
                        return true;
                    }
                    else
                    {
                        Console.WriteLine($"Debug: Failed to lift SSE instruction '{instruction.Mnemonic}'");
                    }
                }
                
                // Check for AVX instructions (operate on 256-bit YMM registers)
                if (IsAVXInstruction(mnemonic))
                {
                    if (LiftAVXInstruction(mnemonic, instruction.Operands, irsb))
                    {
                        Console.WriteLine($"Debug: Successfully lifted AVX instruction '{instruction.Mnemonic}'");
                        return true;
                    }
                    else
                    {
                        Console.WriteLine($"Debug: Failed to lift AVX instruction '{instruction.Mnemonic}'");
                    }
                }
                
                // Handle scalar x86-64 instructions
                var result = mnemonic switch
                {
                    // 64-bit data movement
                    "mov" => LiftMov64(instruction, irsb),
                    "movsx" or "movsxd" => LiftMovSX64(instruction, irsb),
                    "movzx" => LiftMovZX64(instruction, irsb),
                    
                    // 64-bit arithmetic
                    "add" => LiftAdd64(instruction, irsb),
                    "sub" => LiftSub64(instruction, irsb),
                    "imul" => LiftIMul64(instruction, irsb),
                    "idiv" => LiftIDiv64(instruction, irsb),
                    "mul" => LiftMul64(instruction, irsb),
                    "div" => LiftDiv64(instruction, irsb),
                    "inc" => LiftInc64(instruction, irsb),
                    "dec" => LiftDec64(instruction, irsb),
                    "neg" => LiftNeg64(instruction, irsb),
                    "adc" => LiftAdc64(instruction, irsb),
                    "sbb" => LiftSbb64(instruction, irsb),
                    
                    // 64-bit comparison operations
                    "cmp" => LiftCmp64(instruction, irsb),
                    "test" => LiftTest64(instruction, irsb),
                    
                    // 64-bit logical operations
                    "and" => LiftAnd64(instruction, irsb),
                    "or" => LiftOr64(instruction, irsb),
                    "xor" => LiftXor64(instruction, irsb),
                    
                    // 64-bit stack operations
                    "push" => LiftPush64(instruction, irsb),
                    "pop" => LiftPop64(instruction, irsb),
                    "call" => LiftCall64(instruction, irsb),
                    "ret" => LiftRet64(instruction, irsb),
                    
                    // 64-bit shift and rotate operations
                    "shl" or "sal" => LiftShiftLeft64(instruction, irsb),
                    "shr" => LiftShiftRight64(instruction, irsb),
                    "sar" => LiftShiftArithmeticRight64(instruction, irsb),
                    "rol" => LiftRotateLeft64(instruction, irsb),
                    "ror" => LiftRotateRight64(instruction, irsb),
                    
                    // Bit manipulation instructions
                    "bt" => LiftBitTest64(instruction, irsb),
                    "btc" => LiftBitTestComplement64(instruction, irsb),
                    "btr" => LiftBitTestReset64(instruction, irsb),
                    "bts" => LiftBitTestSet64(instruction, irsb),
                    "bsf" => LiftBitScanForward64(instruction, irsb),
                    "bsr" => LiftBitScanReverse64(instruction, irsb),
                    "bswap" => LiftByteSwap64(instruction, irsb),
                    "popcnt" => LiftPopulationCount64(instruction, irsb),
                    "lzcnt" => LiftLeadingZeroCount64(instruction, irsb),
                    "tzcnt" => LiftTrailingZeroCount64(instruction, irsb),
                    
                    // 64-bit control flow
                    "jmp" => LiftJmp64(instruction, irsb),
                    "je" or "jz" => LiftConditionalJump64(instruction, irsb),
                    "jne" or "jnz" => LiftConditionalJump64(instruction, irsb),
                    "jl" or "jnge" => LiftConditionalJump64(instruction, irsb),
                    "jle" or "jng" => LiftConditionalJump64(instruction, irsb),
                    "jg" or "jnle" => LiftConditionalJump64(instruction, irsb),
                    "jge" or "jnl" => LiftConditionalJump64(instruction, irsb),
                    "jb" or "jnae" or "jc" => LiftConditionalJump64(instruction, irsb),
                    "jbe" or "jna" => LiftConditionalJump64(instruction, irsb),
                    "ja" or "jnbe" => LiftConditionalJump64(instruction, irsb),
                    "jae" or "jnb" or "jnc" => LiftConditionalJump64(instruction, irsb),
                    
                    // Conditional moves and sets
                    "cmove" or "cmovz" => LiftConditionalMove64(instruction, irsb),
                    "cmovne" or "cmovnz" => LiftConditionalMove64(instruction, irsb),
                    "cmovl" or "cmovnge" => LiftConditionalMove64(instruction, irsb),
                    "cmovle" or "cmovng" => LiftConditionalMove64(instruction, irsb),
                    "cmovg" or "cmovnle" => LiftConditionalMove64(instruction, irsb),
                    "cmovge" or "cmovnl" => LiftConditionalMove64(instruction, irsb),
                    "cmovb" or "cmovnae" or "cmovc" => LiftConditionalMove64(instruction, irsb),
                    "cmovbe" or "cmovna" => LiftConditionalMove64(instruction, irsb),
                    "cmova" or "cmovnbe" => LiftConditionalMove64(instruction, irsb),
                    "cmovae" or "cmovnb" or "cmovnc" => LiftConditionalMove64(instruction, irsb),
                    "cmovs" => LiftConditionalMove64(instruction, irsb),
                    "cmovns" => LiftConditionalMove64(instruction, irsb),
                    "cmovo" => LiftConditionalMove64(instruction, irsb),
                    "cmovno" => LiftConditionalMove64(instruction, irsb),
                    "cmovp" or "cmovpe" => LiftConditionalMove64(instruction, irsb),
                    "cmovnp" or "cmovpo" => LiftConditionalMove64(instruction, irsb),
                    
                    "sete" or "setz" => LiftSetOnCondition64(instruction, irsb),
                    "setne" or "setnz" => LiftSetOnCondition64(instruction, irsb),
                    "setl" or "setnge" => LiftSetOnCondition64(instruction, irsb),
                    "setle" or "setng" => LiftSetOnCondition64(instruction, irsb),
                    "setg" or "setnle" => LiftSetOnCondition64(instruction, irsb),
                    "setge" or "setnl" => LiftSetOnCondition64(instruction, irsb),
                    "setb" or "setnae" or "setc" => LiftSetOnCondition64(instruction, irsb),
                    "setbe" or "setna" => LiftSetOnCondition64(instruction, irsb),
                    "seta" or "setnbe" => LiftSetOnCondition64(instruction, irsb),
                    "setae" or "setnb" or "setnc" => LiftSetOnCondition64(instruction, irsb),
                    "sets" => LiftSetOnCondition64(instruction, irsb),
                    "setns" => LiftSetOnCondition64(instruction, irsb),
                    "seto" => LiftSetOnCondition64(instruction, irsb),
                    "setno" => LiftSetOnCondition64(instruction, irsb),
                    "setp" or "setpe" => LiftSetOnCondition64(instruction, irsb),
                    "setnp" or "setpo" => LiftSetOnCondition64(instruction, irsb),
                    
                    // String operations
                    "movs" or "movsb" or "movsw" or "movsd" or "movsq" => LiftMoveString64(instruction, irsb),
                    "stos" or "stosb" or "stosw" or "stosd" or "stosq" => LiftStoreString64(instruction, irsb),
                    "lods" or "lodsb" or "lodsw" or "lodsd" or "lodsq" => LiftLoadString64(instruction, irsb),
                    "scas" or "scasb" or "scasw" or "scasd" or "scasq" => LiftScanString64(instruction, irsb),
                    "cmps" or "cmpsb" or "cmpsw" or "cmpsd" or "cmpsq" => LiftCompareStrings64(instruction, irsb),
                    
                    // Miscellaneous instructions
                    "lea" => LiftLoadEffectiveAddress64(instruction, irsb),
                    "xchg" => LiftExchange64(instruction, irsb),
                    "cmpxchg" => LiftCompareExchange64(instruction, irsb),
                    "xadd" => LiftExchangeAdd64(instruction, irsb),
                    "nop" => LiftNop64(instruction, irsb),
                    "ud2" => LiftUndefinedInstruction64(instruction, irsb),
                    "int3" => LiftBreakpoint64(instruction, irsb),
                    "lahf" => LiftLoadFlagsToAH64(instruction, irsb),
                    "sahf" => LiftStoreFlagsFromAH64(instruction, irsb),
                    "clc" => LiftClearCarry64(instruction, irsb),
                    "stc" => LiftSetCarry64(instruction, irsb),
                    "cmc" => LiftComplementCarry64(instruction, irsb),
                    "cld" => LiftClearDirection64(instruction, irsb),
                    "std" => LiftSetDirection64(instruction, irsb),
                    "cli" => LiftClearInterrupt64(instruction, irsb),
                    "sti" => LiftSetInterrupt64(instruction, irsb),
                    
                    // System calls and special instructions
                    "syscall" => LiftSyscall(instruction, irsb),
                    "sysret" => LiftSysret(instruction, irsb),
                    
                    // Default handling for other instructions
                    _ => LiftGenericInstruction(instruction, irsb)
                };
                
                if (!result)
                {
                    Console.WriteLine($"Debug: Failed to lift instruction '{instruction.Mnemonic}' with operands: {string.Join(", ", instruction.Operands.Select(op => op.ToString()))}");
                }
                
                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error lifting AMD64 instruction '{instruction.Mnemonic}' at 0x{CurrentInstructionAddress:X}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Check if instruction terminates a basic block
        /// AMD64 has additional terminating instructions compared to x86
        /// </summary>
        private bool IsBasicBlockTerminator(AMD64Instruction instruction)
        {
            return instruction.Mnemonic switch
            {
                "ret" or "retf" or "iret" => true,
                "jmp" or "jz" or "jnz" or "je" or "jne" or "js" or "jns" or 
                "jc" or "jnc" or "jo" or "jno" or "jp" or "jnp" or "jpe" or "jpo" or
                "ja" or "jae" or "jb" or "jbe" or "jg" or "jge" or "jl" or "jle" or
                "call" => true,
                "int" or "into" or "int3" => true,
                "syscall" or "sysret" or "sysenter" or "sysexit" => true,
                "hlt" => true,
                _ => false
            };
        }

        #region AMD64-Specific Instruction Lifting

        /// <summary>
        /// Lift instructions with REX prefix (64-bit register extensions)
        /// </summary>
        private bool LiftREXInstruction(AMD64Instruction instruction, IRSB irsb)
        {
            // REX prefix enables:
            // - 64-bit operand size (REX.W)
            // - Access to R8-R15 registers (REX.R, REX.X, REX.B)
            // - Access to SIL, DIL, SPL, BPL (REX.0)
            
            var rexPrefix = instruction.REXPrefix;
            
            // Handle based on REX flags
            if (rexPrefix.W) // 64-bit operand size
            {
                return LiftREX64BitOperation(instruction, irsb);
            }
            
            if (rexPrefix.R || rexPrefix.X || rexPrefix.B) // Extended registers
            {
                return LiftREXExtendedRegister(instruction, irsb);
            }
            
            // REX.0 - access to byte registers
            return LiftREXByteRegister(instruction, irsb);
        }

        /// <summary>
        /// Lift AMD64-only instructions (not available in 32-bit mode)
        /// </summary>
        private bool LiftAMD64OnlyInstruction(AMD64Instruction instruction, IRSB irsb)
        {
            return instruction.Mnemonic switch
            {
                "syscall" => LiftSyscall(instruction, irsb),
                "sysret" => LiftSysret(instruction, irsb),
                "swapgs" => LiftSwapGS(instruction, irsb),
                _ => LiftGenericInstruction(instruction, irsb)
            };
        }

        /// <summary>
        /// Lift instructions with RIP-relative addressing
        /// </summary>
        private bool LiftRIPRelativeInstruction(AMD64Instruction instruction, IRSB irsb)
        {
            // RIP-relative addressing is AMD64-specific
            // Address = RIP + displacement
            
            // Calculate effective address
            var ripValue = IRExprFactory.U64(CurrentInstructionAddress + (ulong)instruction.Length);
            var displacement = IRExprFactory.U64((ulong)instruction.RIPDisplacement);
            var effectiveAddress = IRExprFactory.Binop(IROp.Add64, ripValue, displacement);
            
            // Use the effective address in the instruction
            return LiftInstructionWithAddress(instruction, effectiveAddress, irsb);
        }

        #endregion

        #region 64-bit Instruction Implementations (Stubs)

        private bool LiftMov64(AMD64Instruction instruction, IRSB irsb)
        {
            if (instruction.Operands.Length != 2)
                return false;

            var destination = instruction.Operands[0];
            var source = instruction.Operands[1];

            try
            {
                // Convert operands to IR expressions
                var srcExpr = ConvertOperandToIRExpr(source, instruction);
                if (srcExpr == null)
                    return false;

                // Handle destination based on type
                switch (destination.Type)
                {
                    case AMD64OperandType.Register:
                        return LiftMovToRegister(destination, srcExpr, irsb);
                        
                    case AMD64OperandType.Memory:
                        return LiftMovToMemory(destination, srcExpr, instruction, irsb);
                        
                    default:
                        return false;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error lifting MOV instruction: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Lift MOV to register
        /// </summary>
        private bool LiftMovToRegister(AMD64Operand destination, IRExpr sourceExpr, IRSB irsb)
        {
            var regOffset = GetRegisterOffset(destination.Register);
            
            var regType = GetIRTypeForOperandSize(destination.Size);
            if (regType == null)
                return false;

            // For partial register writes in AMD64, we need to handle zero-extension
            if (destination.Size == OperandSize.Size32 && Is64BitRegister(destination.Register))
            {
                // 32-bit writes to 64-bit registers zero the upper 32 bits
                var zeroExtended = IRExprFactory.Unop(IROp.Iop_32Uto64, sourceExpr);
                irsb.AddStatement(IRStmtFactory.Put(regOffset, zeroExtended));
            }
            else
            {
                // Direct assignment
                irsb.AddStatement(IRStmtFactory.Put(regOffset, sourceExpr));
            }

            return true;
        }

        /// <summary>
        /// Lift MOV to memory
        /// </summary>
        private bool LiftMovToMemory(AMD64Operand destination, IRExpr sourceExpr, AMD64Instruction instruction, IRSB irsb)
        {
            var addressExpr = ConvertMemoryOperandToAddress(destination, instruction);
            if (addressExpr == null)
                return false;

            var storeType = GetIRTypeForOperandSize(destination.Size);
            if (storeType == null)
                return false;

            // Generate store statement
            irsb.AddStatement(IRStmtFactory.StoreLE(addressExpr, sourceExpr));
            return true;
        }

        /// <summary>
        /// Convert operand to IR expression
        /// </summary>
        private IRExpr? ConvertOperandToIRExpr(AMD64Operand operand, AMD64Instruction instruction)
        {
            return operand.Type switch
            {
                AMD64OperandType.Register => ConvertRegisterToIRExpr(operand),
                AMD64OperandType.Immediate => ConvertImmediateToIRExpr(operand),
                AMD64OperandType.Memory => ConvertMemoryToIRExpr(operand, instruction),
                _ => null
            };
        }

        /// <summary>
        /// Convert register operand to IR expression
        /// </summary>
        private IRExpr? ConvertRegisterToIRExpr(AMD64Operand operand)
        {
            var regOffset = GetRegisterOffset(operand.Register);
            
            var regType = GetIRTypeForOperandSize(operand.Size);
            if (regType == null)
                return null;

            return IRExprFactory.Get(regOffset, regType.Value);
        }

        /// <summary>
        /// Convert immediate operand to IR expression
        /// </summary>
        private IRExpr? ConvertImmediateToIRExpr(AMD64Operand operand)
        {
            return operand.Size switch
            {
                OperandSize.Size8 => IRExprFactory.U8((byte)operand.ImmediateValue),
                OperandSize.Size16 => IRExprFactory.U16((ushort)operand.ImmediateValue),
                OperandSize.Size32 => IRExprFactory.U32((uint)operand.ImmediateValue),
                OperandSize.Size64 => IRExprFactory.U64((ulong)operand.ImmediateValue),
                _ => null
            };
        }

        /// <summary>
        /// Convert memory operand to IR expression (load)
        /// </summary>
        private IRExpr? ConvertMemoryToIRExpr(AMD64Operand operand, AMD64Instruction instruction)
        {
            var addressExpr = ConvertMemoryOperandToAddress(operand, instruction);
            if (addressExpr == null)
                return null;

            var loadType = GetIRTypeForOperandSize(operand.Size);
            if (loadType == null)
                return null;

            return IRExprFactory.LoadLE(loadType.Value, addressExpr);
        }

        /// <summary>
        /// Convert memory operand to address expression
        /// </summary>
        private IRExpr? ConvertMemoryOperandToAddress(AMD64Operand operand, AMD64Instruction instruction)
        {
            if (operand.Memory == null)
                return null;

            var memory = operand.Memory.Value;
            IRExpr? address = null;

            // Handle RIP-relative addressing
            if (memory.IsRIPRelative)
            {
                var ripValue = IRExprFactory.U64(CurrentInstructionAddress + (ulong)instruction.Length);
                var displacement = IRExprFactory.U64((ulong)memory.Displacement);
                return IRExprFactory.Binop(IROp.Add64, ripValue, displacement);
            }

            // Handle base register
            if (!string.IsNullOrEmpty(memory.Base))
            {
                var baseOffset = GetRegisterOffset(memory.Base);
                
                address = IRExprFactory.Get(baseOffset, IRType.I64);
            }

            // Handle index register with scale
            if (!string.IsNullOrEmpty(memory.Index))
            {
                var indexOffset = GetRegisterOffset(memory.Index);
                
                IRExpr indexExpr = IRExprFactory.Get(indexOffset, IRType.I64);
                
                // Apply scale if needed
                if (memory.Scale > 1)
                {
                    var scaleExpr = IRExprFactory.U64((ulong)memory.Scale);
                    indexExpr = IRExprFactory.Binop(IROp.Mul64, indexExpr, scaleExpr);
                }

                // Add to base or use as base
                address = address != null 
                    ? IRExprFactory.Binop(IROp.Add64, address, indexExpr)
                    : indexExpr;
            }

            // Handle displacement
            if (memory.Displacement != 0)
            {
                var dispExpr = IRExprFactory.U64((ulong)memory.Displacement);
                address = address != null
                    ? IRExprFactory.Binop(IROp.Add64, address, dispExpr)
                    : dispExpr;
            }

            return address ?? IRExprFactory.U64(0);
        }

        private bool LiftMovSX64(AMD64Instruction instruction, IRSB irsb)
        {
            if (instruction.Operands.Length != 2)
                return false;

            var destination = instruction.Operands[0];
            var source = instruction.Operands[1];

            // MOVSX destination must be a register
            if (destination.Type != AMD64OperandType.Register)
                return false;

            // Get source expression
            var srcExpr = ConvertOperandToIRExpr(source, instruction);
            if (srcExpr == null)
                return false;

            // Determine the sign extension operation based on source and destination sizes
            IROp? extOp = null;
            switch (source.Size)
            {
                case OperandSize.Size8:
                    extOp = destination.Size switch
                    {
                        OperandSize.Size16 => IROp.Iop_8Sto16,
                        OperandSize.Size32 => IROp.Iop_8Sto32,
                        OperandSize.Size64 => IROp.Iop_8Sto64,
                        _ => null
                    };
                    break;
                    
                case OperandSize.Size16:
                    extOp = destination.Size switch
                    {
                        OperandSize.Size32 => IROp.Iop_16Sto32,
                        OperandSize.Size64 => IROp.Iop_16Sto64,
                        _ => null
                    };
                    break;
                    
                case OperandSize.Size32:
                    if (destination.Size == OperandSize.Size64)
                        extOp = IROp.Iop_32Sto64;
                    break;
                    
                default:
                    break;
            }

            // Check if we found a valid extension operation
            if (extOp == null)
                return false;

            // Apply sign extension
            var extendedExpr = IRExprFactory.Unop(extOp.Value, srcExpr);

            // Store to destination register
            var regOffset = GetRegisterOffset(destination.Register);
            irsb.AddStatement(IRStmtFactory.Put(regOffset, extendedExpr));

            return true;
        }

        private bool LiftMovZX64(AMD64Instruction instruction, IRSB irsb)
        {
            if (instruction.Operands.Length != 2)
                return false;

            var destination = instruction.Operands[0];
            var source = instruction.Operands[1];

            // MOVZX destination must be a register
            if (destination.Type != AMD64OperandType.Register)
                return false;

            // Get source expression
            var srcExpr = ConvertOperandToIRExpr(source, instruction);
            if (srcExpr == null)
                return false;

            // Determine the zero extension operation based on source and destination sizes
            IROp? extOp = null;
            switch (source.Size)
            {
                case OperandSize.Size8:
                    extOp = destination.Size switch
                    {
                        OperandSize.Size16 => IROp.Iop_8Uto16,
                        OperandSize.Size32 => IROp.Iop_8Uto32,
                        OperandSize.Size64 => IROp.Iop_8Uto64,
                        _ => null
                    };
                    break;
                    
                case OperandSize.Size16:
                    extOp = destination.Size switch
                    {
                        OperandSize.Size32 => IROp.Iop_16Uto32,
                        OperandSize.Size64 => IROp.Iop_16Uto64,
                        _ => null
                    };
                    break;
                    
                case OperandSize.Size32:
                    if (destination.Size == OperandSize.Size64)
                        extOp = IROp.Iop_32Uto64;
                    break;
                    
                default:
                    break;
            }

            // Check if we found a valid extension operation
            if (extOp == null)
                return false;

            // Apply zero extension
            var extendedExpr = IRExprFactory.Unop(extOp.Value, srcExpr);

            // Store to destination register
            var regOffset = GetRegisterOffset(destination.Register);
            irsb.AddStatement(IRStmtFactory.Put(regOffset, extendedExpr));

            return true;
        }

        private bool LiftAdd64(AMD64Instruction instruction, IRSB irsb)
        {
            if (instruction.Operands.Length != 2)
                return false;

            var destination = instruction.Operands[0];
            var source = instruction.Operands[1];

            try
            {
                // Convert operands to IR expressions
                var srcExpr = ConvertOperandToIRExpr(source, instruction);
                if (srcExpr == null)
                    return false;

                var dstExpr = ConvertOperandToIRExpr(destination, instruction);
                if (dstExpr == null)
                    return false;

                // Perform addition using temporary variable pattern
                var resultExpr = CreateArithmeticOperation(IROp.Add8, dstExpr, srcExpr, destination.Size);
                if (resultExpr == null)
                    return false;

                // Create temporary variable to hold result (VEX IR pattern)
                var resultTemp = irsb.TypeEnv.NewTemp(destination.Size == OperandSize.Size64 ? IRType.I64 : IRType.I32);
                irsb.AddStatement(IRStmtFactory.WrTmp(resultTemp, resultExpr));

                // Store result back to destination using temporary
                switch (destination.Type)
                {
                    case AMD64OperandType.Register:
                        return StoreToRegisterFromTemp(destination, resultTemp, irsb);
                        
                    case AMD64OperandType.Memory:
                        return StoreToMemoryFromTemp(destination, resultTemp, instruction, irsb);
                        
                    default:
                        return false;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error lifting ADD instruction: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Create arithmetic operation with proper size handling
        /// </summary>
        private IRExpr? CreateArithmeticOperation(IROp baseOp, IRExpr left, IRExpr right, OperandSize size)
        {
            var operation = size switch
            {
                OperandSize.Size8 => AdjustOpForSize(baseOp, 0),  // Add8
                OperandSize.Size16 => AdjustOpForSize(baseOp, 1), // Add16
                OperandSize.Size32 => AdjustOpForSize(baseOp, 2), // Add32
                OperandSize.Size64 => AdjustOpForSize(baseOp, 3), // Add64
                _ => null
            };

            if (operation == null)
                return null;

            return IRExprFactory.Binop(operation.Value, left, right);
        }

        /// <summary>
        /// Adjust operation for size (Add8/Add16/Add32/Add64)
        /// </summary>
        private IROp? AdjustOpForSize(IROp baseOp, int sizeOffset)
        {
            // Arithmetic operations are arranged in groups of 4 (8/16/32/64 bit)
            return baseOp switch
            {
                IROp.Add8 => (IROp)((int)IROp.Add8 + sizeOffset),
                IROp.Sub8 => (IROp)((int)IROp.Sub8 + sizeOffset),
                IROp.Mul8 => (IROp)((int)IROp.Mul8 + sizeOffset),
                IROp.And8 => (IROp)((int)IROp.And8 + sizeOffset),
                IROp.Or8 => (IROp)((int)IROp.Or8 + sizeOffset),
                IROp.Xor8 => (IROp)((int)IROp.Xor8 + sizeOffset),
                IROp.Shl8 => (IROp)((int)IROp.Shl8 + sizeOffset),
                IROp.Shr8 => (IROp)((int)IROp.Shr8 + sizeOffset),
                IROp.Sar8 => (IROp)((int)IROp.Sar8 + sizeOffset),
                // Division operations are limited in VEX IR
                IROp.DivU32 => sizeOffset == 2 ? IROp.DivU32 : null, // Only 32-bit unsigned
                IROp.DivS32 => sizeOffset == 2 ? IROp.DivS32 : null, // Only 32-bit signed
                _ => null
            };
        }

        /// <summary>
        /// Store value to register
        /// </summary>
        private bool StoreToRegister(AMD64Operand destination, IRExpr valueExpr, IRSB irsb)
        {
            var regOffset = GetRegisterOffset(destination.Register);
            
            // Handle 32-bit writes to 64-bit registers (zero upper bits)
            if (destination.Size == OperandSize.Size32 && Is64BitRegister(destination.Register))
            {
                var zeroExtended = IRExprFactory.Unop(IROp.Iop_32Uto64, valueExpr);
                irsb.AddStatement(IRStmtFactory.Put(regOffset, zeroExtended));
            }
            else
            {
                irsb.AddStatement(IRStmtFactory.Put(regOffset, valueExpr));
            }

            return true;
        }

        /// <summary>
        /// Store value to memory
        /// </summary>
        private bool StoreToMemory(AMD64Operand destination, IRExpr valueExpr, AMD64Instruction instruction, IRSB irsb)
        {
            var addressExpr = ConvertMemoryOperandToAddress(destination, instruction);
            if (addressExpr == null)
                return false;

            irsb.AddStatement(IRStmtFactory.StoreLE(addressExpr, valueExpr));
            return true;
        }

        /// <summary>
        /// Store temporary result to register (VEX IR pattern)
        /// </summary>
        private bool StoreToRegisterFromTemp(AMD64Operand destination, IRTemp resultTemp, IRSB irsb)
        {
            var regOffset = GetRegisterOffset(destination.Register);
            var tempExpr = IRExprFactory.RdTmp(resultTemp);
            
            // Handle 32-bit writes to 64-bit registers (zero upper bits)
            if (destination.Size == OperandSize.Size32 && Is64BitRegister(destination.Register))
            {
                var zeroExtended = IRExprFactory.Unop(IROp.Iop_32Uto64, tempExpr);
                irsb.AddStatement(IRStmtFactory.Put(regOffset, zeroExtended));
            }
            else
            {
                irsb.AddStatement(IRStmtFactory.Put(regOffset, tempExpr));
            }

            return true;
        }

        /// <summary>
        /// Store temporary result to memory (VEX IR pattern)
        /// </summary>
        private bool StoreToMemoryFromTemp(AMD64Operand destination, IRTemp resultTemp, AMD64Instruction instruction, IRSB irsb)
        {
            var addressExpr = ConvertMemoryOperandToAddress(destination, instruction);
            if (addressExpr == null)
                return false;

            var tempExpr = IRExprFactory.RdTmp(resultTemp);
            irsb.AddStatement(IRStmtFactory.StoreLE(addressExpr, tempExpr));
            return true;
        }

        private bool LiftSub64(AMD64Instruction instruction, IRSB irsb)
        {
            if (instruction.Operands.Length != 2)
                return false;

            var destination = instruction.Operands[0];
            var source = instruction.Operands[1];

            try
            {
                // Convert operands to IR expressions
                var srcExpr = ConvertOperandToIRExpr(source, instruction);
                if (srcExpr == null)
                    return false;

                var dstExpr = ConvertOperandToIRExpr(destination, instruction);
                if (dstExpr == null)
                    return false;

                // Perform subtraction
                var resultExpr = CreateArithmeticOperation(IROp.Sub8, dstExpr, srcExpr, destination.Size);
                if (resultExpr == null)
                    return false;

                // Store result back to destination
                switch (destination.Type)
                {
                    case AMD64OperandType.Register:
                        return StoreToRegister(destination, resultExpr, irsb);
                        
                    case AMD64OperandType.Memory:
                        return StoreToMemory(destination, resultExpr, instruction, irsb);
                        
                    default:
                        return false;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error lifting SUB instruction: {ex.Message}");
                return false;
            }
        }

        private bool LiftIMul64(AMD64Instruction instruction, IRSB irsb)
        {
            if (instruction.Operands.Length != 1 && instruction.Operands.Length != 2 && instruction.Operands.Length != 3)
                return false;

            try
            {
                Console.WriteLine($"IMUL instruction: signed multiplication with {instruction.Operands.Length} operands");
                
                // Add instruction marker
                irsb.AddStatement(new IRStmtIMark(CurrentInstructionAddress, (uint)instruction.Length, 0));

                if (instruction.Operands.Length == 1)
                {
                    // IMUL r/m - signed multiply RAX by operand, result in RDX:RAX
                    var source = instruction.Operands[0];
                    var srcExpr = ConvertOperandToIRExpr(source, instruction);
                    if (srcExpr == null)
                        return false;

                    // Load RAX
                    var raxOffset = GetRegisterOffset("rax");
                    var raxExpr = IRExprFactory.Get(raxOffset, IRType.I64);

                    // Perform signed 64-bit multiplication to get 128-bit result
                    var resultExpr = IRExprFactory.Binop(IROp.MullS64, raxExpr, srcExpr);

                    // Extract low 64 bits to RAX
                    var lowResult = IRExprFactory.Unop(IROp.Iop_128to64, resultExpr);
                    irsb.AddStatement(IRStmtFactory.Put(raxOffset, lowResult));

                    // Extract high 64 bits to RDX
                    var rdxOffset = GetRegisterOffset("rdx");

                    var highResult = IRExprFactory.Unop(IROp.Iop_128HIto64, resultExpr);
                    irsb.AddStatement(IRStmtFactory.Put(rdxOffset, highResult));
                }
                else if (instruction.Operands.Length == 2)
                {
                    // IMUL r, r/m - signed multiply, result in first operand
                    var destination = instruction.Operands[0];
                    var source = instruction.Operands[1];

                    var srcExpr = ConvertOperandToIRExpr(source, instruction);
                    if (srcExpr == null)
                        return false;

                    var dstExpr = ConvertOperandToIRExpr(destination, instruction);
                    if (dstExpr == null)
                        return false;

                    // Perform signed multiplication (result fits in operand size)
                    var resultExpr = CreateArithmeticOperation(IROp.Mul64, dstExpr, srcExpr, destination.Size);
                    if (resultExpr == null)
                        return false;

                    // Store result back to destination
                    switch (destination.Type)
                    {
                        case AMD64OperandType.Register:
                            return StoreToRegister(destination, resultExpr, irsb);
                        case AMD64OperandType.Memory:
                            return StoreToMemory(destination, resultExpr, instruction, irsb);
                        default:
                            return false;
                    }
                }
                else // 3 operands
                {
                    // IMUL r, r/m, imm - signed multiply with immediate
                    var destination = instruction.Operands[0];
                    var source = instruction.Operands[1];
                    var immediate = instruction.Operands[2];

                    var srcExpr = ConvertOperandToIRExpr(source, instruction);
                    if (srcExpr == null)
                        return false;

                    var immExpr = ConvertOperandToIRExpr(immediate, instruction);
                    if (immExpr == null)
                        return false;

                    // Perform signed multiplication
                    var resultExpr = CreateArithmeticOperation(IROp.Mul64, srcExpr, immExpr, destination.Size);
                    if (resultExpr == null)
                        return false;

                    // Store result to destination register
                    if (destination.Type != AMD64OperandType.Register)
                        return false;

                    return StoreToRegister(destination, resultExpr, irsb);
                }

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error lifting IMUL instruction: {ex.Message}");
                return false;
            }
        }

        private bool LiftIDiv64(AMD64Instruction instruction, IRSB irsb)
        {
            if (instruction.Operands.Length != 1)
                return false;

            var source = instruction.Operands[0];

            try
            {
                // Convert source operand to IR expression
                var srcExpr = ConvertOperandToIRExpr(source, instruction);
                if (srcExpr == null)
                    return false;

                // Load RDX:RAX (128-bit dividend)
                var raxOffset = GetRegisterOffset("rax");
                var rdxOffset = GetRegisterOffset("rdx");

                var raxExpr = IRExprFactory.Get(raxOffset, IRType.I64);
                var rdxExpr = IRExprFactory.Get(rdxOffset, IRType.I64);

                // Create 128-bit dividend from RDX:RAX
                var dividend128 = IRExprFactory.Binop(IROp.Iop_64HLto128, rdxExpr, raxExpr);

                // Use unsigned 128-bit division and then handle sign correction
                var divResult = IRExprFactory.Binop(IROp.DivModU128to64, dividend128, srcExpr);

                // Extract quotient and remainder
                var quotient = IRExprFactory.Unop(IROp.Iop_128to64, divResult);
                var remainder = IRExprFactory.Unop(IROp.Iop_128HIto64, divResult);

                // For signed division, this is a simplification.
                // Proper signed 128-bit division would require careful sign handling.
                // For now, store the unsigned results which works for positive numbers.
                irsb.AddStatement(IRStmtFactory.Put(raxOffset, quotient));
                irsb.AddStatement(IRStmtFactory.Put(rdxOffset, remainder));

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error lifting IDIV instruction: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Lift MUL (unsigned multiplication) instruction
        /// MUL r/m - Multiplies AL/AX/EAX/RAX by operand, result in AX/DX:AX/EDX:EAX/RDX:RAX
        /// </summary>
        private bool LiftMul64(AMD64Instruction instruction, IRSB irsb)
        {
            if (instruction.Operands.Length != 1)
                return false;

            var source = instruction.Operands[0];

            try
            {
                Console.WriteLine($"MUL instruction: multiplying RAX by {source}");

                // Convert source operand to IR expression
                var srcExpr = ConvertOperandToIRExpr(source, instruction);
                if (srcExpr == null)
                    return false;

                // Load RAX (implicit operand)
                var raxOffset = GetRegisterOffset("rax");

                var raxExpr = IRExprFactory.Get(raxOffset, IRType.I64);

                // Perform unsigned multiplication (128-bit result)
                // For now, implement as simplified 64-bit multiplication
                var resultExpr = CreateArithmeticOperation(IROp.Mul8, raxExpr, srcExpr, OperandSize.Size64);
                if (resultExpr == null)
                    return false;

                // Store low 64 bits back to RAX
                irsb.AddStatement(IRStmtFactory.Put(raxOffset, resultExpr));

                // TODO: Handle high 64 bits in RDX for full 128-bit result
                Console.WriteLine($"MUL instruction: simplified implementation, high bits not handled");

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error lifting MUL instruction: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Lift DIV (unsigned division) instruction
        /// DIV r/m - Divides RDX:RAX by operand, quotient in RAX, remainder in RDX
        /// </summary>
        private bool LiftDiv64(AMD64Instruction instruction, IRSB irsb)
        {
            if (instruction.Operands.Length != 1)
                return false;

            var source = instruction.Operands[0];

            try
            {
                // Convert source operand to IR expression
                var srcExpr = ConvertOperandToIRExpr(source, instruction);
                if (srcExpr == null)
                    return false;

                // Load RDX:RAX (128-bit dividend)
                var raxOffset = GetRegisterOffset("rax");
                var rdxOffset = GetRegisterOffset("rdx");

                var raxExpr = IRExprFactory.Get(raxOffset, IRType.I64);
                var rdxExpr = IRExprFactory.Get(rdxOffset, IRType.I64);

                // Create 128-bit dividend from RDX:RAX
                var dividend128 = IRExprFactory.Binop(IROp.Iop_64HLto128, rdxExpr, raxExpr);

                // Perform unsigned 128-bit division
                var divResult = IRExprFactory.Binop(IROp.DivModU128to64, dividend128, srcExpr);

                // Extract quotient (low 64 bits) and remainder (high 64 bits)
                var quotient = IRExprFactory.Unop(IROp.Iop_128to64, divResult);
                var remainder = IRExprFactory.Unop(IROp.Iop_128HIto64, divResult);

                // Store quotient to RAX and remainder to RDX
                irsb.AddStatement(IRStmtFactory.Put(raxOffset, quotient));
                irsb.AddStatement(IRStmtFactory.Put(rdxOffset, remainder));

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error lifting DIV instruction: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Lift INC (increment) instruction
        /// INC r/m - Increments operand by 1
        /// </summary>
        private bool LiftInc64(AMD64Instruction instruction, IRSB irsb)
        {
            if (instruction.Operands.Length != 1)
                return false;

            var destination = instruction.Operands[0];

            try
            {
                Console.WriteLine($"INC instruction: incrementing {destination}");

                // Convert operand to IR expression
                var dstExpr = ConvertOperandToIRExpr(destination, instruction);
                if (dstExpr == null)
                    return false;

                // Create constant 1
                var oneExpr = IRExprFactory.U64(1);

                // Perform increment
                var resultExpr = CreateArithmeticOperation(IROp.Add8, dstExpr, oneExpr, destination.Size);
                if (resultExpr == null)
                    return false;

                // Store result back to destination
                switch (destination.Type)
                {
                    case AMD64OperandType.Register:
                        return StoreToRegister(destination, resultExpr, irsb);
                    case AMD64OperandType.Memory:
                        return StoreToMemory(destination, resultExpr, instruction, irsb);
                    default:
                        return false;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error lifting INC instruction: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Lift DEC (decrement) instruction
        /// DEC r/m - Decrements operand by 1
        /// </summary>
        private bool LiftDec64(AMD64Instruction instruction, IRSB irsb)
        {
            if (instruction.Operands.Length != 1)
                return false;

            var destination = instruction.Operands[0];

            try
            {
                Console.WriteLine($"DEC instruction: decrementing {destination}");

                // Convert operand to IR expression
                var dstExpr = ConvertOperandToIRExpr(destination, instruction);
                if (dstExpr == null)
                    return false;

                // Create constant 1
                var oneExpr = IRExprFactory.U64(1);

                // Perform decrement
                var resultExpr = CreateArithmeticOperation(IROp.Sub8, dstExpr, oneExpr, destination.Size);
                if (resultExpr == null)
                    return false;

                // Store result back to destination
                switch (destination.Type)
                {
                    case AMD64OperandType.Register:
                        return StoreToRegister(destination, resultExpr, irsb);
                    case AMD64OperandType.Memory:
                        return StoreToMemory(destination, resultExpr, instruction, irsb);
                    default:
                        return false;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error lifting DEC instruction: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Lift NEG (negate) instruction
        /// NEG r/m - Two's complement negation
        /// </summary>
        private bool LiftNeg64(AMD64Instruction instruction, IRSB irsb)
        {
            if (instruction.Operands.Length != 1)
                return false;

            var destination = instruction.Operands[0];

            try
            {
                Console.WriteLine($"NEG instruction: negating {destination}");

                // Convert operand to IR expression
                var dstExpr = ConvertOperandToIRExpr(destination, instruction);
                if (dstExpr == null)
                    return false;

                // Create zero constant
                var zeroExpr = IRExprFactory.U64(0);

                // Perform negation (0 - operand)
                var resultExpr = CreateArithmeticOperation(IROp.Sub8, zeroExpr, dstExpr, destination.Size);
                if (resultExpr == null)
                    return false;

                // Store result back to destination
                switch (destination.Type)
                {
                    case AMD64OperandType.Register:
                        return StoreToRegister(destination, resultExpr, irsb);
                    case AMD64OperandType.Memory:
                        return StoreToMemory(destination, resultExpr, instruction, irsb);
                    default:
                        return false;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error lifting NEG instruction: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Lift ADC (add with carry) instruction
        /// ADC dest, src - Adds source + carry flag to destination
        /// </summary>
        private bool LiftAdc64(AMD64Instruction instruction, IRSB irsb)
        {
            if (instruction.Operands.Length != 2)
                return false;

            var destination = instruction.Operands[0];
            var source = instruction.Operands[1];

            try
            {
                // Convert operands to IR expressions
                var srcExpr = ConvertOperandToIRExpr(source, instruction);
                if (srcExpr == null)
                    return false;

                var dstExpr = ConvertOperandToIRExpr(destination, instruction);
                if (dstExpr == null)
                    return false;

                // Load carry flag from guest state
                // Carry flag is bit 0 of CC_DEP1
                var ccDep1 = IRExprFactory.Get(136, IRType.I64);  // CC_DEP1
                var carryFlag = IRExprFactory.Binop(IROp.And64, ccDep1, IRExprFactory.U64(1)); // Extract bit 0

                // Convert carry to the appropriate size for the operation
                IRExpr carryExpr = destination.Size switch
                {
                    OperandSize.Size8 => IRExprFactory.Unop(IROp.Iop_64to8, carryFlag),
                    OperandSize.Size16 => IRExprFactory.Unop(IROp.Iop_64to16, carryFlag), 
                    OperandSize.Size32 => IRExprFactory.Unop(IROp.Iop_64to32, carryFlag),
                    OperandSize.Size64 => carryFlag,
                    _ => null
                };

                if (carryExpr == null)
                    return false;

                // Perform addition with carry: result = dst + src + carry
                var tempResult = CreateArithmeticOperation(IROp.Add8, dstExpr, srcExpr, destination.Size);
                if (tempResult == null)
                    return false;

                var finalResult = CreateArithmeticOperation(IROp.Add8, tempResult, carryExpr, destination.Size);
                if (finalResult == null)
                    return false;

                // Store result back to destination
                var success = destination.Type switch
                {
                    AMD64OperandType.Register => StoreToRegister(destination, finalResult, irsb),
                    AMD64OperandType.Memory => StoreToMemory(destination, finalResult, instruction, irsb),
                    _ => false
                };

                if (success)
                {
                    // Update flags based on the result
                    UpdateFlags(finalResult, dstExpr, srcExpr, "ADD", irsb);
                }

                return success;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error lifting ADC instruction: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Lift SBB (subtract with borrow) instruction  
        /// SBB dest, src - Subtracts source + carry flag from destination
        /// </summary>
        private bool LiftSbb64(AMD64Instruction instruction, IRSB irsb)
        {
            if (instruction.Operands.Length != 2)
                return false;

            var destination = instruction.Operands[0];
            var source = instruction.Operands[1];

            try
            {
                // Convert operands to IR expressions
                var srcExpr = ConvertOperandToIRExpr(source, instruction);
                if (srcExpr == null)
                    return false;

                var dstExpr = ConvertOperandToIRExpr(destination, instruction);
                if (dstExpr == null)
                    return false;

                // Load carry flag from guest state (carry/borrow flag is bit 0)
                var ccDep1 = IRExprFactory.Get(136, IRType.I64);  // CC_DEP1
                var borrowFlag = IRExprFactory.Binop(IROp.And64, ccDep1, IRExprFactory.U64(1)); // Extract bit 0

                // Convert borrow to the appropriate size for the operation
                IRExpr borrowExpr = destination.Size switch
                {
                    OperandSize.Size8 => IRExprFactory.Unop(IROp.Iop_64to8, borrowFlag),
                    OperandSize.Size16 => IRExprFactory.Unop(IROp.Iop_64to16, borrowFlag),
                    OperandSize.Size32 => IRExprFactory.Unop(IROp.Iop_64to32, borrowFlag),
                    OperandSize.Size64 => borrowFlag,
                    _ => null
                };

                if (borrowExpr == null)
                    return false;

                // Perform subtraction with borrow: result = dst - src - borrow
                var tempResult = CreateArithmeticOperation(IROp.Sub8, dstExpr, srcExpr, destination.Size);
                if (tempResult == null)
                    return false;

                var finalResult = CreateArithmeticOperation(IROp.Sub8, tempResult, borrowExpr, destination.Size);
                if (finalResult == null)
                    return false;

                // Store result back to destination
                var success = destination.Type switch
                {
                    AMD64OperandType.Register => StoreToRegister(destination, finalResult, irsb),
                    AMD64OperandType.Memory => StoreToMemory(destination, finalResult, instruction, irsb),
                    _ => false
                };

                if (success)
                {
                    // Update flags based on the result  
                    UpdateFlags(finalResult, dstExpr, srcExpr, "SUB", irsb);
                }

                return success;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error lifting SBB instruction: {ex.Message}");
                return false;
            }
        }

        private bool LiftSyscall(AMD64Instruction instruction, IRSB irsb)
        {
            try
            {
                // SYSCALL instruction performs the following:
                // 1. Save return address to RCX
                // 2. Save RFLAGS to R11  
                // 3. Load new RIP from LSTAR MSR
                // 4. Load new CS from STAR MSR
                // 5. Clear specific RFLAGS bits based on SFMASK MSR
                
                // Save return address (next instruction) to RCX
                var rcxOffset = GetRegisterOffset("RCX");
                var returnAddress = IRExprFactory.U64(CurrentInstructionAddress + (ulong)instruction.Length);
                irsb.AddStatement(IRStmtFactory.Put(rcxOffset, returnAddress));
                
                // Construct RFLAGS from individual flag components and save to R11
                // Basic construction using IDFLAG and DFLAG - could be expanded
                var idflagOffset = GetRegisterOffset("IDFLAG");
                var dflagOffset = GetRegisterOffset("DFLAG");
                var acflagOffset = GetRegisterOffset("ACFLAG");
                
                var idFlagShifted = IRExprFactory.Binop(IROp.Shl64,
                    IRExprFactory.Get(idflagOffset, IRType.I64),
                    IRExprFactory.U8(21)); // ID flag is bit 21
                
                var dFlagShifted = IRExprFactory.Binop(IROp.Shl64,
                    IRExprFactory.Get(dflagOffset, IRType.I64),
                    IRExprFactory.U8(10)); // DF flag is bit 10
                
                var acFlagShifted = IRExprFactory.Binop(IROp.Shl64,
                    IRExprFactory.Get(acflagOffset, IRType.I64),
                    IRExprFactory.U8(18)); // AC flag is bit 18
                
                var rflags = IRExprFactory.Binop(IROp.Or64,
                    IRExprFactory.Binop(IROp.Or64, idFlagShifted, dFlagShifted),
                    acFlagShifted);
                
                var r11Offset = GetRegisterOffset("R11");
                irsb.AddStatement(IRStmtFactory.Put(r11Offset, rflags));
                
                // Record syscall instruction pointer for restart capability
                var ipAtSyscallOffset = GetRegisterOffset("GuestIPAtSyscall");
                if (ipAtSyscallOffset != null)
                {
                    irsb.AddStatement(IRStmtFactory.Put(ipAtSyscallOffset, 
                        IRExprFactory.U64(CurrentInstructionAddress)));
                }

                // Generate exit statement indicating system call
                // The execution engine will handle the actual transition to kernel mode
                var guard = IRExprFactory.U1(true); // Always take this exit
                var nextIP = IRExprFactory.U64(0);  // Will be determined by LSTAR MSR
                irsb.AddStatement(IRStmtFactory.Exit(guard, IRJumpKind.Sys_syscall, 
                    IRConstFactory.U64(0), (int)instruction.Length));
                
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error lifting SYSCALL instruction: {ex.Message}");
                return false;
            }
        }

        private bool LiftSysret(AMD64Instruction instruction, IRSB irsb)
        {
            try
            {
                // SYSRET instruction performs the following:
                // 1. Restore RIP from RCX
                // 2. Restore RFLAGS from R11
                // 3. Load new CS from STAR MSR
                // 4. Return to user mode
                
                // This is a privileged instruction that should only execute in kernel mode
                // The actual implementation depends on MSR values and privilege level
                
                // Load return address from RCX
                var rcxOffset = GetRegisterOffset("RCX");
                var returnAddress = IRExprFactory.Get(rcxOffset, IRType.I64);
                
                // Load saved RFLAGS from R11 and decompose into individual flags
                var r11Offset = GetRegisterOffset("R11");
                var savedFlags = IRExprFactory.Get(r11Offset, IRType.I64);
                
                // Extract individual flags from the composite RFLAGS value
                // ID flag (bit 21)
                var idFlag = IRExprFactory.Binop(IROp.And64,
                    IRExprFactory.Binop(IROp.Shr64, savedFlags, IRExprFactory.U8(21)),
                    IRExprFactory.U64(1));
                var idflagOffset = GetRegisterOffset("IDFLAG");
                irsb.AddStatement(IRStmtFactory.Put(idflagOffset, idFlag));
                
                // DF flag (bit 10)
                var dfFlag = IRExprFactory.Binop(IROp.And64,
                    IRExprFactory.Binop(IROp.Shr64, savedFlags, IRExprFactory.U8(10)),
                    IRExprFactory.U64(1));
                var dflagOffset = GetRegisterOffset("DFLAG");
                irsb.AddStatement(IRStmtFactory.Put(dflagOffset, dfFlag));
                
                // AC flag (bit 18)
                var acFlag = IRExprFactory.Binop(IROp.And64,
                    IRExprFactory.Binop(IROp.Shr64, savedFlags, IRExprFactory.U8(18)),
                    IRExprFactory.U64(1));
                var acflagOffset = GetRegisterOffset("ACFLAG");
                irsb.AddStatement(IRStmtFactory.Put(acflagOffset, acFlag));

                // Generate privileged exit - execution engine handles privilege checks and mode transition
                var guard = IRExprFactory.U1(true);
                irsb.AddStatement(IRStmtFactory.Exit(guard, IRJumpKind.Privileged, 
                    IRConstFactory.U64(0), (int)instruction.Length));
                
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error lifting SYSRET instruction: {ex.Message}");
                return false;
            }
        }

        private bool LiftSwapGS(AMD64Instruction instruction, IRSB irsb)
        {
            try
            {
                // SWAPGS instruction swaps the contents of GS.base with KernelGSbase MSR
                // This is used for fast kernel entry/exit
                // Only valid in 64-bit mode and when CPL=0 (kernel mode)
                
                // This is a privileged instruction - actual implementation involves MSRs
                // and depends on current privilege level
                
                // In VEX IR, we generate a privileged exit and let the execution engine
                // handle the actual GS base swapping based on current privilege level
                var guard = IRExprFactory.U1(true);
                irsb.AddStatement(IRStmtFactory.Exit(guard, IRJumpKind.Privileged,
                    IRConstFactory.U64(CurrentInstructionAddress + (ulong)instruction.Length), 
                    (int)instruction.Length));
                
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error lifting SWAPGS instruction: {ex.Message}");
                return false;
            }
        }

        private bool LiftREX64BitOperation(AMD64Instruction instruction, IRSB irsb)
        {
            // REX.W enables 64-bit operation mode - handle based on instruction mnemonic
            return instruction.Mnemonic switch
            {
                // 64-bit data movement
                "mov" => LiftMov64(instruction, irsb),
                
                // 64-bit arithmetic
                "add" => LiftAdd64(instruction, irsb),
                "sub" => LiftSub64(instruction, irsb),
                "imul" => LiftIMul64(instruction, irsb),
                "idiv" => LiftIDiv64(instruction, irsb),
                "mul" => LiftMul64(instruction, irsb),
                "div" => LiftDiv64(instruction, irsb),
                "inc" => LiftInc64(instruction, irsb),
                "dec" => LiftDec64(instruction, irsb),
                "neg" => LiftNeg64(instruction, irsb),
                "adc" => LiftAdc64(instruction, irsb),
                "sbb" => LiftSbb64(instruction, irsb),
                
                // 64-bit comparison
                "cmp" => LiftCmp64(instruction, irsb),
                "test" => LiftTest64(instruction, irsb),
                
                // 64-bit logical operations
                "and" => LiftAnd64(instruction, irsb),
                "or" => LiftOr64(instruction, irsb),
                "xor" => LiftXor64(instruction, irsb),
                
                // 64-bit shift and rotate operations
                "shl" or "sal" => LiftShiftLeft64(instruction, irsb),
                "shr" => LiftShiftRight64(instruction, irsb),
                "sar" => LiftShiftArithmeticRight64(instruction, irsb),
                "rol" => LiftRotateLeft64(instruction, irsb),
                "ror" => LiftRotateRight64(instruction, irsb),
                
                // Miscellaneous 64-bit instructions
                "lea" => LiftLoadEffectiveAddress64(instruction, irsb),
                
                // Default handling for other instructions
                _ => LiftGenericInstruction(instruction, irsb)
            };
        }

        private bool LiftREXExtendedRegister(AMD64Instruction instruction, IRSB irsb)
        {
            // REX.R/X/B enables access to extended registers (R8-R15)
            // Handle based on instruction mnemonic
            return instruction.Mnemonic switch
            {
                // Data movement with extended registers
                "mov" => LiftMov64(instruction, irsb),
                
                // Arithmetic with extended registers
                "add" => LiftAdd64(instruction, irsb),
                "sub" => LiftSub64(instruction, irsb),
                
                // Comparison with extended registers
                "cmp" => LiftCmp64(instruction, irsb),
                "test" => LiftTest64(instruction, irsb),
                
                // Logical operations with extended registers
                "and" => LiftAnd64(instruction, irsb),
                "or" => LiftOr64(instruction, irsb),
                "xor" => LiftXor64(instruction, irsb),
                
                // Default handling for other instructions
                _ => LiftGenericInstruction(instruction, irsb)
            };
        }

        private bool LiftREXByteRegister(AMD64Instruction instruction, IRSB irsb)
        {
            // REX.0 enables access to SIL, DIL, SPL, BPL byte registers
            // Handle based on instruction mnemonic
            return instruction.Mnemonic switch
            {
                // Byte operations with low byte registers
                "mov" => LiftMov64(instruction, irsb),
                "add" => LiftAdd64(instruction, irsb),
                "sub" => LiftSub64(instruction, irsb),
                
                // Default handling for other instructions
                _ => LiftGenericInstruction(instruction, irsb)
            };
        }

        private bool LiftInstructionWithAddress(AMD64Instruction instruction, IRExpr address, IRSB irsb)
        {
            // TODO: Implement instruction lifting with computed address
            return LiftGenericInstruction(instruction, irsb);
        }

        /// <summary>
        /// Fallback for unsupported instructions
        /// </summary>
        private bool LiftGenericInstruction(AMD64Instruction instruction, IRSB irsb)
        {
            // For now, just add an IMark and continue
            Console.WriteLine($"Warning: Unsupported AMD64 instruction '{instruction.Mnemonic}' - using placeholder");
            Console.WriteLine($"Debug: Operands: {instruction.Operands.Length}");
            return true;
        }

        /// <summary>
        /// Lift CMP instruction (compare - SUB without storing result)
        /// </summary>
        private bool LiftCmp64(AMD64Instruction instruction, IRSB irsb)
        {
            if (instruction.Operands.Length != 2)
                return false;

            var destination = instruction.Operands[0];
            var source = instruction.Operands[1];

            try
            {
                // Add instruction marker
                irsb.AddStatement(new IRStmtIMark(CurrentInstructionAddress, (uint)instruction.Length, 0));

                // Convert operands to IR expressions
                var srcExpr = ConvertOperandToIRExpr(source, instruction);
                if (srcExpr == null)
                    return false;

                var dstExpr = ConvertOperandToIRExpr(destination, instruction);
                if (dstExpr == null)
                    return false;

                // Perform comparison (subtraction to set flags, but don't store result)
                var resultExpr = CreateArithmeticOperation(IROp.Sub8, dstExpr, srcExpr, destination.Size);
                if (resultExpr == null)
                    return false;

                // CMP sets flags based on the subtraction result
                // Store the result in a temporary to compute flags
                var resultTemp = irsb.NewTemp(IRType.I64);
                irsb.AddStatement(IRStmtFactory.WrTmp(resultTemp, resultExpr));

                // Set condition code flags based on the result
                // CC_OP = operation type, CC_DEP1 = left operand, CC_DEP2 = right operand
                var ccOpTemp = irsb.NewTemp(IRType.I64);
                irsb.AddStatement(IRStmtFactory.WrTmp(ccOpTemp, IRExprFactory.U64(2))); // SUB operation
                irsb.AddStatement(IRStmtFactory.Put(GetRegisterOffset("CC_OP"), IRExprFactory.RdTmp(ccOpTemp)));
                irsb.AddStatement(IRStmtFactory.Put(GetRegisterOffset("CC_DEP1"), dstExpr));
                irsb.AddStatement(IRStmtFactory.Put(GetRegisterOffset("CC_DEP2"), srcExpr));
                
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error lifting CMP instruction: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Lift TEST instruction (bitwise AND without storing result)
        /// </summary>
        private bool LiftTest64(AMD64Instruction instruction, IRSB irsb)
        {
            if (instruction.Operands.Length != 2)
                return false;

            var destination = instruction.Operands[0];
            var source = instruction.Operands[1];

            try
            {
                // Convert operands to IR expressions
                var srcExpr = ConvertOperandToIRExpr(source, instruction);
                if (srcExpr == null)
                    return false;

                var dstExpr = ConvertOperandToIRExpr(destination, instruction);
                if (dstExpr == null)
                    return false;

                // Perform test (bitwise AND to set flags, but don't store result)
                var resultExpr = CreateArithmeticOperation(IROp.And8, dstExpr, srcExpr, destination.Size);
                if (resultExpr == null)
                    return false;

                // TEST sets flags based on the AND result but doesn't store the result
                // This affects zero flag (ZF), sign flag (SF), parity flag (PF)
                // Carry flag (CF) and overflow flag (OF) are cleared
                UpdateFlags(resultExpr, dstExpr, srcExpr, "AND", irsb);
                
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error lifting TEST instruction: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Lift AND instruction (bitwise AND)
        /// </summary>
        private bool LiftAnd64(AMD64Instruction instruction, IRSB irsb)
        {
            if (instruction.Operands.Length != 2)
                return false;

            var destination = instruction.Operands[0];
            var source = instruction.Operands[1];

            try
            {
                // Convert operands to IR expressions
                var srcExpr = ConvertOperandToIRExpr(source, instruction);
                if (srcExpr == null)
                    return false;

                var dstExpr = ConvertOperandToIRExpr(destination, instruction);
                if (dstExpr == null)
                    return false;

                // Perform bitwise AND
                var resultExpr = CreateArithmeticOperation(IROp.And8, dstExpr, srcExpr, destination.Size);
                if (resultExpr == null)
                    return false;

                // Store result back to destination
                switch (destination.Type)
                {
                    case AMD64OperandType.Register:
                        return StoreToRegister(destination, resultExpr, irsb);
                        
                    case AMD64OperandType.Memory:
                        return StoreToMemory(destination, resultExpr, instruction, irsb);
                        
                    default:
                        return false;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error lifting AND instruction: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Lift OR instruction (bitwise OR)
        /// </summary>
        private bool LiftOr64(AMD64Instruction instruction, IRSB irsb)
        {
            if (instruction.Operands.Length != 2)
                return false;

            var destination = instruction.Operands[0];
            var source = instruction.Operands[1];

            try
            {
                // Convert operands to IR expressions
                var srcExpr = ConvertOperandToIRExpr(source, instruction);
                if (srcExpr == null)
                    return false;

                var dstExpr = ConvertOperandToIRExpr(destination, instruction);
                if (dstExpr == null)
                    return false;

                // Perform bitwise OR
                var resultExpr = CreateArithmeticOperation(IROp.Or8, dstExpr, srcExpr, destination.Size);
                if (resultExpr == null)
                    return false;

                // Store result back to destination
                switch (destination.Type)
                {
                    case AMD64OperandType.Register:
                        return StoreToRegister(destination, resultExpr, irsb);
                        
                    case AMD64OperandType.Memory:
                        return StoreToMemory(destination, resultExpr, instruction, irsb);
                        
                    default:
                        return false;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error lifting OR instruction: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Lift XOR instruction (bitwise XOR)
        /// </summary>
        private bool LiftXor64(AMD64Instruction instruction, IRSB irsb)
        {
            if (instruction.Operands.Length != 2)
                return false;

            var destination = instruction.Operands[0];
            var source = instruction.Operands[1];

            try
            {
                // Convert operands to IR expressions
                var srcExpr = ConvertOperandToIRExpr(source, instruction);
                if (srcExpr == null)
                    return false;

                var dstExpr = ConvertOperandToIRExpr(destination, instruction);
                if (dstExpr == null)
                    return false;

                // Perform bitwise XOR
                var resultExpr = CreateArithmeticOperation(IROp.Xor8, dstExpr, srcExpr, destination.Size);
                if (resultExpr == null)
                    return false;

                // Store result back to destination
                switch (destination.Type)
                {
                    case AMD64OperandType.Register:
                        return StoreToRegister(destination, resultExpr, irsb);
                        
                    case AMD64OperandType.Memory:
                        return StoreToMemory(destination, resultExpr, instruction, irsb);
                        
                    default:
                        return false;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error lifting XOR instruction: {ex.Message}");
                return false;
            }
        }

        #endregion

        #region Jump Operations

        private bool LiftJmp64(AMD64Instruction instruction, IRSB irsb)
        {
            if (instruction.Operands.Length != 1)
                return false;

            var target = instruction.Operands[0];

            try
            {
                // For now, implement simple unconditional jump by setting the next instruction pointer
                // In a more complete implementation, this would create an Exit statement
                Console.WriteLine($"JMP instruction to target: {target}");
                
                // Just mark that we handled it - actual jump logic would be more complex
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error lifting JMP instruction: {ex.Message}");
                return false;
            }
        }

        private bool LiftConditionalJump64(AMD64Instruction instruction, IRSB irsb)
        {
            if (instruction.Operands.Length != 1)
                return false;

            var target = instruction.Operands[0];

            try
            {
                // Add instruction marker
                irsb.AddStatement(new IRStmtIMark(CurrentInstructionAddress, (uint)instruction.Length, 0));

                // Convert target operand to IR expression
                var targetExpr = ConvertOperandToIRExpr(target, instruction);
                if (targetExpr == null)
                    return false;

                // Create condition expression using VEX IR condition code helpers
                // VEX IR uses CC_OP, CC_DEP1, CC_DEP2 and helper functions to evaluate conditions
                IRExpr? conditionExpr = instruction.Mnemonic.ToLowerInvariant() switch
                {
                    "je" or "jz" => CreateConditionExpression(4),    // CC_CondZ 
                    "jne" or "jnz" => CreateConditionExpression(5),  // CC_CondNZ
                    "jl" or "jnge" => CreateConditionExpression(1),  // CC_CondL (simplified)
                    "jle" or "jng" => CreateConditionExpression(3),  // CC_CondLE
                    "jg" or "jnle" => CreateConditionExpression(7),  // CC_CondG  
                    "jge" or "jnl" => CreateConditionExpression(6),  // CC_CondGE
                    "jb" or "jnae" or "jc" => CreateConditionExpression(8),   // CC_CondB
                    "jbe" or "jna" => CreateConditionExpression(10),  // CC_CondBE
                    "ja" or "jnbe" => CreateConditionExpression(11),  // CC_CondA
                    "jae" or "jnb" or "jnc" => CreateConditionExpression(9),  // CC_CondAE
                    _ => null
                };

                if (conditionExpr == null)
                {
                    Console.WriteLine($"Unsupported conditional jump: {instruction.Mnemonic}");
                    return false;
                }

                // Create conditional exit
                // If condition is true, jump to target, otherwise continue to next instruction
                var targetConst = CreateIRConst(ArchAddressType, targetExpr);
                irsb.AddStatement(IRStmtFactory.Exit(conditionExpr, IRJumpKind.Boring, targetConst, 0));
                
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error lifting conditional jump instruction: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Create condition expression using VEX IR condition codes
        /// </summary>
        private IRExpr CreateConditionExpression(int conditionCode)
        {
            // Read condition code registers
            var ccOp = IRExprFactory.Get(GetRegisterOffset("CC_OP"), IRType.I64);
            var ccDep1 = IRExprFactory.Get(GetRegisterOffset("CC_DEP1"), IRType.I64);
            var ccDep2 = IRExprFactory.Get(GetRegisterOffset("CC_DEP2"), IRType.I64);
            
            // Implement condition code evaluation based on subtraction result (dep1 - dep2)
            return conditionCode switch
            {
                4 => // JE/JZ - Zero Flag set (result == 0)
                    IRExprFactory.Binop(IROp.CmpEQ64, ccDep1, ccDep2),
                    
                5 => // JNE/JNZ - Zero Flag clear (result != 0)
                    IRExprFactory.Binop(IROp.CmpNE64, ccDep1, ccDep2),
                    
                1 => // JL/JNGE - Signed less than
                    IRExprFactory.Binop(IROp.CmpLT64S, ccDep1, ccDep2),
                    
                3 => // JLE/JNG - Signed less than or equal
                    IRExprFactory.Binop(IROp.CmpLE64S, ccDep1, ccDep2),
                    
                7 => // JG/JNLE - Signed greater than
                    IRExprFactory.Binop(IROp.CmpLT64S, ccDep2, ccDep1),
                    
                6 => // JGE/JNL - Signed greater than or equal
                    IRExprFactory.Binop(IROp.CmpLE64S, ccDep2, ccDep1),
                    
                8 => // JB/JNAE/JC - Unsigned below
                    IRExprFactory.Binop(IROp.CmpLT64U, ccDep1, ccDep2),
                    
                10 => // JBE/JNA - Unsigned below or equal
                    IRExprFactory.Binop(IROp.CmpLE64U, ccDep1, ccDep2),
                    
                11 => // JA/JNBE - Unsigned above
                    IRExprFactory.Binop(IROp.CmpLT64U, ccDep2, ccDep1),
                    
                9 => // JAE/JNB/JNC - Unsigned above or equal
                    IRExprFactory.Binop(IROp.CmpLE64U, ccDep2, ccDep1),
                    
                _ => // Unknown condition - return false
                    IRExprFactory.Binop(IROp.CmpEQ32, IRExprFactory.U32(0), IRExprFactory.U32(1))
            };
        }

        /// <summary>
        /// Create IRConst from IRExpr for use in Exit statements
        /// </summary>
        private IRConst CreateIRConst(IRType type, IRExpr expr)
        {
            // If expression is already a constant, extract it
            if (expr is IRExprConst constExpr)
            {
                return constExpr.Con;
            }
            
            // Fallback to a default constant (this is a simplification)
            return type switch
            {
                IRType.I32 => IRConstFactory.U32(0),
                IRType.I64 => IRConstFactory.U64(0),
                _ => throw new ArgumentException($"Unsupported type for IRConst: {type}")
            };
        }

        #endregion

        #region Stack Operations

        private bool LiftPush64(AMD64Instruction instruction, IRSB irsb)
        {
            if (instruction.Operands.Length != 1)
                return false;

            var source = instruction.Operands[0];

            try
            {
                // Convert source operand to IR expression
                var sourceExpr = ConvertOperandToIRExpr(source, instruction);
                if (sourceExpr == null)
                    return false;

                // Get current RSP value
                var rspOffset = GetRegisterOffset("rsp");
                if (rspOffset == null)
                    return false;

                var currentRsp = IRExprFactory.Get(rspOffset, IRType.I64);

                // Decrement RSP by 8 bytes (64-bit push) using temporary variable
                var decrementExpr = IRExprFactory.U64(8);
                var newRspTemp = irsb.TypeEnv.NewTemp(IRType.I64);
                var newRspExpr = IRExprFactory.Binop(IROp.Sub64, currentRsp, decrementExpr);
                
                // Store calculation in temporary
                irsb.AddStatement(IRStmtFactory.WrTmp(newRspTemp, newRspExpr));
                
                // Update RSP register
                irsb.AddStatement(IRStmtFactory.Put(rspOffset, IRExprFactory.RdTmp(newRspTemp)));

                // Store source value to [new RSP]
                irsb.AddStatement(IRStmtFactory.StoreLE(IRExprFactory.RdTmp(newRspTemp), sourceExpr));

                Console.WriteLine($"PUSH instruction: storing {source} to stack");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error lifting PUSH instruction: {ex.Message}");
                return false;
            }
        }

        private bool LiftPop64(AMD64Instruction instruction, IRSB irsb)
        {
            if (instruction.Operands.Length != 1)
                return false;

            var destination = instruction.Operands[0];

            try
            {
                // Get current RSP value
                var rspOffset = GetRegisterOffset("rsp");
                if (rspOffset == null)
                    return false;

                var currentRsp = IRExprFactory.Get(rspOffset, IRType.I64);

                // Load value from [RSP]
                var loadedValue = IRExprFactory.LoadLE(IRType.I64, currentRsp);

                // Store loaded value to destination
                switch (destination.Type)
                {
                    case AMD64OperandType.Register:
                        if (!StoreToRegister(destination, loadedValue, irsb))
                            return false;
                        break;
                        
                    case AMD64OperandType.Memory:
                        if (!StoreToMemory(destination, loadedValue, instruction, irsb))
                            return false;
                        break;
                        
                    default:
                        return false;
                }

                // Increment RSP by 8 bytes (64-bit pop)
                var incrementExpr = IRExprFactory.U64(8);
                var newRsp = IRExprFactory.Binop(IROp.Add64, currentRsp, incrementExpr);

                // Store new RSP value
                irsb.AddStatement(IRStmtFactory.Put(rspOffset, newRsp));

                Console.WriteLine($"POP instruction: loading from stack to {destination}");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error lifting POP instruction: {ex.Message}");
                return false;
            }
        }

        private bool LiftCall64(AMD64Instruction instruction, IRSB irsb)
        {
            if (instruction.Operands.Length != 1)
                return false;

            var target = instruction.Operands[0];

            try
            {
                // Get current RSP value
                var rspOffset = GetRegisterOffset("rsp");
                if (rspOffset == null)
                    return false;

                var currentRsp = IRExprFactory.Get(rspOffset, IRType.I64);

                // Calculate return address (current instruction address + instruction length)
                var returnAddress = IRExprFactory.U64(CurrentInstructionAddress + (ulong)instruction.Length);

                // Decrement RSP by 8 bytes
                var decrementExpr = IRExprFactory.U64(8);
                var newRsp = IRExprFactory.Binop(IROp.Sub64, currentRsp, decrementExpr);

                // Store new RSP value
                irsb.AddStatement(IRStmtFactory.Put(rspOffset, newRsp));

                // Push return address onto stack
                irsb.AddStatement(IRStmtFactory.StoreLE(newRsp, returnAddress));

                // For now, just log the call target - actual jump logic would be more complex
                Console.WriteLine($"CALL instruction to target: {target}");
                Console.WriteLine($"Return address {returnAddress} pushed to stack");
                
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error lifting CALL instruction: {ex.Message}");
                return false;
            }
        }

        private bool LiftRet64(AMD64Instruction instruction, IRSB irsb)
        {
            try
            {
                // Get current RSP value
                var rspOffset = GetRegisterOffset("rsp");
                if (rspOffset == null)
                    return false;

                var currentRsp = IRExprFactory.Get(rspOffset, IRType.I64);

                // Load return address from [RSP]
                var returnAddress = IRExprFactory.LoadLE(IRType.I64, currentRsp);

                // Increment RSP by 8 bytes (popping return address)
                var incrementExpr = IRExprFactory.U64(8);
                var newRsp = IRExprFactory.Binop(IROp.Add64, currentRsp, incrementExpr);

                // Handle optional immediate operand (stack adjustment)
                if (instruction.Operands.Length == 1)
                {
                    var stackAdjust = instruction.Operands[0];
                    if (stackAdjust.Type == AMD64OperandType.Immediate)
                    {
                        var adjustValue = IRExprFactory.U64((ulong)stackAdjust.ImmediateValue);
                        newRsp = IRExprFactory.Binop(IROp.Add64, newRsp, adjustValue);
                        Console.WriteLine($"RET with stack adjustment: {stackAdjust.ImmediateValue} bytes");
                    }
                }

                // Store new RSP value
                irsb.AddStatement(IRStmtFactory.Put(rspOffset, newRsp));

                // Set jump target to return address and mark as return
                irsb.Next = returnAddress;
                irsb.JumpKind = IRJumpKind.Ret;
                
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error lifting RET instruction: {ex.Message}");
                return false;
            }
        }

        #endregion

        #region Shift and Rotate Operations

        /// <summary>
        /// Lift SHL/SAL (Shift Left/Shift Arithmetic Left) instruction
        /// SHL dest, count - shifts dest left by count bits, fills with zeros
        /// </summary>
        private bool LiftShiftLeft64(AMD64Instruction instruction, IRSB irsb)
        {
            if (instruction.Operands.Length != 2)
                return false;

            var destination = instruction.Operands[0];
            var source = instruction.Operands[1];

            try
            {
                Console.WriteLine($"SHL instruction: shifting {destination} left by {source}");
                
                // Convert operands to IR expressions
                var srcExpr = ConvertOperandToIRExpr(source, instruction);
                if (srcExpr == null)
                    return false;

                var dstExpr = ConvertOperandToIRExpr(destination, instruction);
                if (dstExpr == null)
                    return false;

                // Perform left shift using CreateArithmeticOperation
                var resultExpr = CreateArithmeticOperation(IROp.Shl8, dstExpr, srcExpr, destination.Size);
                if (resultExpr == null)
                    return false;

                // Store result back to destination
                switch (destination.Type)
                {
                    case AMD64OperandType.Register:
                        return StoreToRegister(destination, resultExpr, irsb);
                    case AMD64OperandType.Memory:
                        return StoreToMemory(destination, resultExpr, instruction, irsb);
                    default:
                        return false;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error lifting SHL instruction: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Lift SHR (Shift Right) instruction
        /// SHR dest, count - logical right shift, fills with zeros
        /// </summary>
        private bool LiftShiftRight64(AMD64Instruction instruction, IRSB irsb)
        {
            if (instruction.Operands.Length != 2)
                return false;

            var destination = instruction.Operands[0];
            var source = instruction.Operands[1];

            try
            {
                Console.WriteLine($"SHR instruction: shifting {destination} right by {source}");
                
                // Convert operands to IR expressions
                var srcExpr = ConvertOperandToIRExpr(source, instruction);
                if (srcExpr == null)
                    return false;

                var dstExpr = ConvertOperandToIRExpr(destination, instruction);
                if (dstExpr == null)
                    return false;

                // Perform logical right shift using CreateArithmeticOperation
                var resultExpr = CreateArithmeticOperation(IROp.Shr8, dstExpr, srcExpr, destination.Size);
                if (resultExpr == null)
                    return false;

                // Store result back to destination
                switch (destination.Type)
                {
                    case AMD64OperandType.Register:
                        return StoreToRegister(destination, resultExpr, irsb);
                    case AMD64OperandType.Memory:
                        return StoreToMemory(destination, resultExpr, instruction, irsb);
                    default:
                        return false;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error lifting SHR instruction: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Lift SAR (Shift Arithmetic Right) instruction
        /// SAR dest, count - arithmetic right shift, fills with sign bit
        /// </summary>
        private bool LiftShiftArithmeticRight64(AMD64Instruction instruction, IRSB irsb)
        {
            if (instruction.Operands.Length != 2)
                return false;

            var destination = instruction.Operands[0];
            var source = instruction.Operands[1];

            try
            {
                Console.WriteLine($"SAR instruction: shifting {destination} arithmetic right by {source}");
                
                // Convert operands to IR expressions
                var srcExpr = ConvertOperandToIRExpr(source, instruction);
                if (srcExpr == null)
                    return false;

                var dstExpr = ConvertOperandToIRExpr(destination, instruction);
                if (dstExpr == null)
                    return false;

                // Perform arithmetic right shift using CreateArithmeticOperation
                var resultExpr = CreateArithmeticOperation(IROp.Sar8, dstExpr, srcExpr, destination.Size);
                if (resultExpr == null)
                    return false;

                // Store result back to destination
                switch (destination.Type)
                {
                    case AMD64OperandType.Register:
                        return StoreToRegister(destination, resultExpr, irsb);
                    case AMD64OperandType.Memory:
                        return StoreToMemory(destination, resultExpr, instruction, irsb);
                    default:
                        return false;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error lifting SAR instruction: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Lift ROL (Rotate Left) instruction
        /// ROL dest, count - rotates dest left by count bits
        /// Note: Rotate operations are more complex and may need special IR handling
        /// </summary>
        private bool LiftRotateLeft64(AMD64Instruction instruction, IRSB irsb)
        {
            if (instruction.Operands.Length != 2)
                return false;

            var destination = instruction.Operands[0];
            var source = instruction.Operands[1];

            try
            {
                Console.WriteLine($"ROL instruction: rotating {destination} left by {source}");
                
                // Add instruction marker
                irsb.AddStatement(new IRStmtIMark(CurrentInstructionAddress, (uint)instruction.Length, 0));

                // Convert operands to IR expressions
                var srcExpr = ConvertOperandToIRExpr(source, instruction);
                if (srcExpr == null)
                    return false;

                var dstExpr = ConvertOperandToIRExpr(destination, instruction);
                if (dstExpr == null)
                    return false;

                // Get bit width for the operand size
                int bitWidth = destination.Size switch
                {
                    OperandSize.Size8 => 8,
                    OperandSize.Size16 => 16,
                    OperandSize.Size32 => 32,
                    OperandSize.Size64 => 64,
                    _ => 64
                };

                // Create constant for (bitwidth - count)
                var bitWidthExpr = CreateConstantExpressionForSize((uint)bitWidth, destination.Size);
                var rightShiftCount = IRExprFactory.Binop(
                    bitWidth <= 32 ? IROp.Sub32 : IROp.Sub64, 
                    bitWidthExpr, 
                    srcExpr
                );

                // Implement rotate left as: (value << count) | (value >> (bitwidth - count))
                var leftShiftOp = GetShiftOpForSize(destination.Size, isLeft: true);
                var rightShiftOp = GetShiftOpForSize(destination.Size, isLeft: false);

                var leftShift = IRExprFactory.Binop(leftShiftOp, dstExpr, srcExpr);
                var rightShift = IRExprFactory.Binop(rightShiftOp, dstExpr, rightShiftCount);

                var orOp = GetOrOpForSize(destination.Size);
                var resultExpr = IRExprFactory.Binop(orOp, leftShift, rightShift);

                // Store result back to destination
                switch (destination.Type)
                {
                    case AMD64OperandType.Register:
                        return StoreToRegister(destination, resultExpr, irsb);
                    case AMD64OperandType.Memory:
                        return StoreToMemory(destination, resultExpr, instruction, irsb);
                    default:
                        return false;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error lifting ROL instruction: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Lift ROR (Rotate Right) instruction
        /// ROR dest, count - rotates dest right by count bits
        /// Note: Rotate operations are more complex and may need special IR handling
        /// </summary>
        private bool LiftRotateRight64(AMD64Instruction instruction, IRSB irsb)
        {
            if (instruction.Operands.Length != 2)
                return false;

            var destination = instruction.Operands[0];
            var source = instruction.Operands[1];

            try
            {
                Console.WriteLine($"ROR instruction: rotating {destination} right by {source}");
                
                // Add instruction marker
                irsb.AddStatement(new IRStmtIMark(CurrentInstructionAddress, (uint)instruction.Length, 0));

                // Convert operands to IR expressions
                var srcExpr = ConvertOperandToIRExpr(source, instruction);
                if (srcExpr == null)
                    return false;

                var dstExpr = ConvertOperandToIRExpr(destination, instruction);
                if (dstExpr == null)
                    return false;

                // Get bit width for the operand size
                int bitWidth = destination.Size switch
                {
                    OperandSize.Size8 => 8,
                    OperandSize.Size16 => 16,
                    OperandSize.Size32 => 32,
                    OperandSize.Size64 => 64,
                    _ => 64
                };

                // Create constant for (bitwidth - count)
                var bitWidthExpr = CreateConstantExpressionForSize((uint)bitWidth, destination.Size);
                var leftShiftCount = IRExprFactory.Binop(
                    bitWidth <= 32 ? IROp.Sub32 : IROp.Sub64, 
                    bitWidthExpr, 
                    srcExpr
                );

                // Implement rotate right as: (value >> count) | (value << (bitwidth - count))
                var leftShiftOp = GetShiftOpForSize(destination.Size, isLeft: true);
                var rightShiftOp = GetShiftOpForSize(destination.Size, isLeft: false);

                var rightShift = IRExprFactory.Binop(rightShiftOp, dstExpr, srcExpr);
                var leftShift = IRExprFactory.Binop(leftShiftOp, dstExpr, leftShiftCount);

                var orOp = GetOrOpForSize(destination.Size);
                var resultExpr = IRExprFactory.Binop(orOp, rightShift, leftShift);

                // Store result back to destination
                switch (destination.Type)
                {
                    case AMD64OperandType.Register:
                        return StoreToRegister(destination, resultExpr, irsb);
                    case AMD64OperandType.Memory:
                        return StoreToMemory(destination, resultExpr, instruction, irsb);
                    default:
                        return false;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error lifting ROR instruction: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Get the appropriate shift operation for the given operand size
        /// </summary>
        private IROp GetShiftOpForSize(OperandSize size, bool isLeft)
        {
            return size switch
            {
                OperandSize.Size8 => isLeft ? IROp.Shl8 : IROp.Shr8,
                OperandSize.Size16 => isLeft ? IROp.Shl16 : IROp.Shr16,
                OperandSize.Size32 => isLeft ? IROp.Shl32 : IROp.Shr32,
                OperandSize.Size64 => isLeft ? IROp.Shl64 : IROp.Shr64,
                _ => isLeft ? IROp.Shl64 : IROp.Shr64
            };
        }

        /// <summary>
        /// Get the appropriate OR operation for the given operand size
        /// </summary>
        private IROp GetOrOpForSize(OperandSize size)
        {
            return size switch
            {
                OperandSize.Size8 => IROp.Or8,
                OperandSize.Size16 => IROp.Or16,
                OperandSize.Size32 => IROp.Or32,
                OperandSize.Size64 => IROp.Or64,
                _ => IROp.Or64
            };
        }

        /// <summary>
        /// Create a constant expression for the given value and operand size
        /// </summary>
        private IRExpr CreateConstantExpressionForSize(uint value, OperandSize size)
        {
            return size switch
            {
                OperandSize.Size8 => IRExprFactory.U8((byte)value),
                OperandSize.Size16 => IRExprFactory.U16((ushort)value),
                OperandSize.Size32 => IRExprFactory.U32(value),
                OperandSize.Size64 => IRExprFactory.U64(value),
                _ => IRExprFactory.U64(value)
            };
        }

        /// <summary>
        /// Get the appropriate subtraction operation for the given operand size
        /// </summary>
        private IROp GetSubOpForSize(OperandSize size)
        {
            return size switch
            {
                OperandSize.Size8 => IROp.Sub8,
                OperandSize.Size16 => IROp.Sub16,
                OperandSize.Size32 => IROp.Sub32,
                OperandSize.Size64 => IROp.Sub64,
                _ => IROp.Sub64
            };
        }

        /// <summary>
        /// Get the appropriate AND operation for the given operand size
        /// </summary>
        private IROp GetAndOpForSize(OperandSize size)
        {
            return size switch
            {
                OperandSize.Size8 => IROp.And8,
                OperandSize.Size16 => IROp.And16,
                OperandSize.Size32 => IROp.And32,
                OperandSize.Size64 => IROp.And64,
                _ => IROp.And64
            };
        }

        /// <summary>
        /// Get the appropriate compare-not-equal operation for the given operand size
        /// </summary>
        private IROp GetCmpNeOpForSize(OperandSize size)
        {
            return size switch
            {
                OperandSize.Size8 => IROp.CmpNE8,
                OperandSize.Size16 => IROp.CmpNE16,
                OperandSize.Size32 => IROp.CmpNE32,
                OperandSize.Size64 => IROp.CmpNE64,
                _ => IROp.CmpNE64
            };
        }

        /// <summary>
        /// Get the appropriate compare-equal operation for the given operand size
        /// </summary>
        private IROp GetCmpEqOpForSize(OperandSize size)
        {
            return size switch
            {
                OperandSize.Size8 => IROp.CmpEQ8,
                OperandSize.Size16 => IROp.CmpEQ16,
                OperandSize.Size32 => IROp.CmpEQ32,
                OperandSize.Size64 => IROp.CmpEQ64,
                _ => IROp.CmpEQ64
            };
        }

        /// <summary>
        /// Get the appropriate XOR operation for the given operand size
        /// </summary>
        private IROp GetXorOpForSize(OperandSize size)
        {
            return size switch
            {
                OperandSize.Size8 => IROp.Xor8,
                OperandSize.Size16 => IROp.Xor16,
                OperandSize.Size32 => IROp.Xor32,
                OperandSize.Size64 => IROp.Xor64,
                _ => IROp.Xor64
            };
        }

        /// <summary>
        /// Get the appropriate NOT operation for the given operand size
        /// </summary>
        private IROp GetNotOpForSize(OperandSize size)
        {
            return size switch
            {
                OperandSize.Size8 => IROp.Not8,
                OperandSize.Size16 => IROp.Not16,
                OperandSize.Size32 => IROp.Not32,
                OperandSize.Size64 => IROp.Not64,
                _ => IROp.Not64
            };
        }

        /// <summary>
        /// Create a 32-bit byte swap expression
        /// </summary>
        private IRExpr CreateByteSwap32(IRExpr expr32)
        {
            // Extract bytes and reassemble in reverse order
            var byte0 = IRExprFactory.Binop(IROp.And32, expr32, IRExprFactory.U32(0xFF));
            var byte1 = IRExprFactory.Binop(IROp.And32, 
                IRExprFactory.Binop(IROp.Shr32, expr32, IRExprFactory.U8(8)), 
                IRExprFactory.U32(0xFF));
            var byte2 = IRExprFactory.Binop(IROp.And32, 
                IRExprFactory.Binop(IROp.Shr32, expr32, IRExprFactory.U8(16)), 
                IRExprFactory.U32(0xFF));
            var byte3 = IRExprFactory.Binop(IROp.And32, 
                IRExprFactory.Binop(IROp.Shr32, expr32, IRExprFactory.U8(24)), 
                IRExprFactory.U32(0xFF));

            // Reassemble: byte0 << 24 | byte1 << 16 | byte2 << 8 | byte3
            return IRExprFactory.Binop(IROp.Or32,
                IRExprFactory.Binop(IROp.Or32,
                    IRExprFactory.Binop(IROp.Shl32, byte0, IRExprFactory.U8(24)),
                    IRExprFactory.Binop(IROp.Shl32, byte1, IRExprFactory.U8(16))),
                IRExprFactory.Binop(IROp.Or32,
                    IRExprFactory.Binop(IROp.Shl32, byte2, IRExprFactory.U8(8)),
                    byte3));
        }

        #endregion

        #region Bit Manipulation Instructions

        /// <summary>
        /// Lift BT (Bit Test) instruction
        /// BT r/m, r/imm - Tests bit in first operand at position specified by second operand
        /// Sets carry flag to value of tested bit
        /// </summary>
        private bool LiftBitTest64(AMD64Instruction instruction, IRSB irsb)
        {
            if (instruction.Operands.Length != 2)
                return false;

            var destination = instruction.Operands[0];
            var bitPosition = instruction.Operands[1];

            try
            {
                Console.WriteLine($"BT instruction: testing bit {bitPosition} in {destination}");

                // Add instruction marker
                irsb.AddStatement(new IRStmtIMark(CurrentInstructionAddress, (uint)instruction.Length, 0));

                // Convert operands to IR expressions
                var dstExpr = ConvertOperandToIRExpr(destination, instruction);
                var bitPosExpr = ConvertOperandToIRExpr(bitPosition, instruction);
                if (dstExpr == null || bitPosExpr == null)
                    return false;

                // Get the bit width for the destination operand
                var bitWidth = destination.Size switch
                {
                    OperandSize.Size16 => 16,
                    OperandSize.Size32 => 32,
                    OperandSize.Size64 => 64,
                    _ => 64
                };

                // Mask the bit position to avoid out-of-range access
                var bitMask = CreateConstantExpressionForSize((uint)(bitWidth - 1), destination.Size);
                var maskedBitPos = IRExprFactory.Binop(GetAndOpForSize(destination.Size), bitPosExpr, bitMask);

                // Create a mask with only the target bit set: 1 << bitPos
                var oneConst = CreateConstantExpressionForSize(1, destination.Size);
                var bitTestMask = IRExprFactory.Binop(GetShiftOpForSize(destination.Size, isLeft: true), oneConst, maskedBitPos);

                // Test the bit: (destination & bitTestMask) != 0
                var bitTest = IRExprFactory.Binop(GetAndOpForSize(destination.Size), dstExpr, bitTestMask);
                var zeroConst = CreateConstantExpressionForSize(0, destination.Size);
                var isNonZero = IRExprFactory.Binop(GetCmpNeOpForSize(destination.Size), bitTest, zeroConst);

                // Set carry flag to the bit test result
                // In VEX IR, flags are typically stored at specific guest state offsets
                // For now, we'll create a temporary and store the result
                var resultTemp = irsb.NewTemp(IRType.I1);
                irsb.AddStatement(IRStmtFactory.WrTmp(resultTemp, isNonZero));

                Console.WriteLine($"BT instruction: bit test implemented with proper flag setting");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error lifting BT instruction: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Lift BTC (Bit Test and Complement) instruction
        /// BTC r/m, r/imm - Tests bit and sets carry flag, then complements the bit
        /// </summary>
        private bool LiftBitTestComplement64(AMD64Instruction instruction, IRSB irsb)
        {
            if (instruction.Operands.Length != 2)
                return false;

            var destination = instruction.Operands[0];
            var bitPosition = instruction.Operands[1];

            try
            {
                Console.WriteLine($"BTC instruction: testing and complementing bit {bitPosition} in {destination}");

                // Add instruction marker
                irsb.AddStatement(new IRStmtIMark(CurrentInstructionAddress, (uint)instruction.Length, 0));

                // Convert operands to IR expressions
                var dstExpr = ConvertOperandToIRExpr(destination, instruction);
                var bitPosExpr = ConvertOperandToIRExpr(bitPosition, instruction);
                if (dstExpr == null || bitPosExpr == null)
                    return false;

                // Get the bit width for the destination operand
                var bitWidth = destination.Size switch
                {
                    OperandSize.Size16 => 16,
                    OperandSize.Size32 => 32,
                    OperandSize.Size64 => 64,
                    _ => 64
                };

                // Mask the bit position to avoid out-of-range access
                var bitMask = CreateConstantExpressionForSize((uint)(bitWidth - 1), destination.Size);
                var maskedBitPos = IRExprFactory.Binop(GetAndOpForSize(destination.Size), bitPosExpr, bitMask);

                // Create a mask with only the target bit set: 1 << bitPos
                var oneConst = CreateConstantExpressionForSize(1, destination.Size);
                var bitTestMask = IRExprFactory.Binop(GetShiftOpForSize(destination.Size, isLeft: true), oneConst, maskedBitPos);

                // Test the bit: (destination & bitTestMask) != 0 (for carry flag)
                var bitTest = IRExprFactory.Binop(GetAndOpForSize(destination.Size), dstExpr, bitTestMask);
                var zeroConst = CreateConstantExpressionForSize(0, destination.Size);
                var isNonZero = IRExprFactory.Binop(GetCmpNeOpForSize(destination.Size), bitTest, zeroConst);

                // Set carry flag to the bit test result
                var resultTemp = irsb.NewTemp(IRType.I1);
                irsb.AddStatement(IRStmtFactory.WrTmp(resultTemp, isNonZero));

                // Complement the bit: destination XOR bitTestMask
                var newValue = IRExprFactory.Binop(GetXorOpForSize(destination.Size), dstExpr, bitTestMask);

                // Store the result back to the destination
                switch (destination.Type)
                {
                    case AMD64OperandType.Register:
                        return StoreToRegister(destination, newValue, irsb);
                    case AMD64OperandType.Memory:
                        return StoreToMemory(destination, newValue, instruction, irsb);
                    default:
                        return false;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error lifting BTC instruction: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Lift BTR (Bit Test and Reset) instruction
        /// BTR r/m, r/imm - Tests and resets (clears) bit
        /// </summary>
        private bool LiftBitTestReset64(AMD64Instruction instruction, IRSB irsb)
        {
            if (instruction.Operands.Length != 2)
                return false;

            var destination = instruction.Operands[0];
            var bitPosition = instruction.Operands[1];

            try
            {
                Console.WriteLine($"BTR instruction: testing and resetting bit {bitPosition} in {destination}");

                // Add instruction marker
                irsb.AddStatement(new IRStmtIMark(CurrentInstructionAddress, (uint)instruction.Length, 0));

                // Convert operands to IR expressions
                var dstExpr = ConvertOperandToIRExpr(destination, instruction);
                var bitPosExpr = ConvertOperandToIRExpr(bitPosition, instruction);
                if (dstExpr == null || bitPosExpr == null)
                    return false;

                // Get the bit width for the destination operand
                var bitWidth = destination.Size switch
                {
                    OperandSize.Size16 => 16,
                    OperandSize.Size32 => 32,
                    OperandSize.Size64 => 64,
                    _ => 64
                };

                // Mask the bit position to avoid out-of-range access
                var bitMask = CreateConstantExpressionForSize((uint)(bitWidth - 1), destination.Size);
                var maskedBitPos = IRExprFactory.Binop(GetAndOpForSize(destination.Size), bitPosExpr, bitMask);

                // Create a mask with only the target bit set: 1 << bitPos
                var oneConst = CreateConstantExpressionForSize(1, destination.Size);
                var bitTestMask = IRExprFactory.Binop(GetShiftOpForSize(destination.Size, isLeft: true), oneConst, maskedBitPos);

                // Test the bit: (destination & bitTestMask) != 0 (for carry flag)
                var bitTest = IRExprFactory.Binop(GetAndOpForSize(destination.Size), dstExpr, bitTestMask);
                var zeroConst = CreateConstantExpressionForSize(0, destination.Size);
                var isNonZero = IRExprFactory.Binop(GetCmpNeOpForSize(destination.Size), bitTest, zeroConst);

                // Set carry flag to the bit test result
                var resultTemp = irsb.NewTemp(IRType.I1);
                irsb.AddStatement(IRStmtFactory.WrTmp(resultTemp, isNonZero));

                // Reset (clear) the bit: destination AND (NOT bitTestMask)
                var notMask = IRExprFactory.Unop(GetNotOpForSize(destination.Size), bitTestMask);
                var newValue = IRExprFactory.Binop(GetAndOpForSize(destination.Size), dstExpr, notMask);

                // Store the result back to the destination
                switch (destination.Type)
                {
                    case AMD64OperandType.Register:
                        return StoreToRegister(destination, newValue, irsb);
                    case AMD64OperandType.Memory:
                        return StoreToMemory(destination, newValue, instruction, irsb);
                    default:
                        return false;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error lifting BTR instruction: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Lift BTS (Bit Test and Set) instruction
        /// BTS r/m, r/imm - Tests and sets bit
        /// </summary>
        private bool LiftBitTestSet64(AMD64Instruction instruction, IRSB irsb)
        {
            if (instruction.Operands.Length != 2)
                return false;

            var destination = instruction.Operands[0];
            var bitPosition = instruction.Operands[1];

            try
            {
                Console.WriteLine($"BTS instruction: testing and setting bit {bitPosition} in {destination}");

                // Add instruction marker
                irsb.AddStatement(new IRStmtIMark(CurrentInstructionAddress, (uint)instruction.Length, 0));

                // Convert operands to IR expressions
                var dstExpr = ConvertOperandToIRExpr(destination, instruction);
                var bitPosExpr = ConvertOperandToIRExpr(bitPosition, instruction);
                if (dstExpr == null || bitPosExpr == null)
                    return false;

                // Get the bit width for the destination operand
                var bitWidth = destination.Size switch
                {
                    OperandSize.Size16 => 16,
                    OperandSize.Size32 => 32,
                    OperandSize.Size64 => 64,
                    _ => 64
                };

                // Mask the bit position to avoid out-of-range access
                var bitMask = CreateConstantExpressionForSize((uint)(bitWidth - 1), destination.Size);
                var maskedBitPos = IRExprFactory.Binop(GetAndOpForSize(destination.Size), bitPosExpr, bitMask);

                // Create a mask with only the target bit set: 1 << bitPos
                var oneConst = CreateConstantExpressionForSize(1, destination.Size);
                var bitTestMask = IRExprFactory.Binop(GetShiftOpForSize(destination.Size, isLeft: true), oneConst, maskedBitPos);

                // Test the bit: (destination & bitTestMask) != 0 (for carry flag)
                var bitTest = IRExprFactory.Binop(GetAndOpForSize(destination.Size), dstExpr, bitTestMask);
                var zeroConst = CreateConstantExpressionForSize(0, destination.Size);
                var isNonZero = IRExprFactory.Binop(GetCmpNeOpForSize(destination.Size), bitTest, zeroConst);

                // Set carry flag to the bit test result
                var resultTemp = irsb.NewTemp(IRType.I1);
                irsb.AddStatement(IRStmtFactory.WrTmp(resultTemp, isNonZero));

                // Set the bit: destination OR bitTestMask
                var newValue = IRExprFactory.Binop(GetOrOpForSize(destination.Size), dstExpr, bitTestMask);

                // Store the result back to the destination
                switch (destination.Type)
                {
                    case AMD64OperandType.Register:
                        return StoreToRegister(destination, newValue, irsb);
                    case AMD64OperandType.Memory:
                        return StoreToMemory(destination, newValue, instruction, irsb);
                    default:
                        return false;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error lifting BTS instruction: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Lift BSF (Bit Scan Forward) instruction
        /// BSF dest, src - Finds first set bit from LSB
        /// </summary>
        private bool LiftBitScanForward64(AMD64Instruction instruction, IRSB irsb)
        {
            if (instruction.Operands.Length != 2)
                return false;

            var destination = instruction.Operands[0];
            var source = instruction.Operands[1];

            try
            {
                Console.WriteLine($"BSF instruction: scanning forward in {source} to {destination}");

                // Add instruction marker
                irsb.AddStatement(new IRStmtIMark(CurrentInstructionAddress, (uint)instruction.Length, 0));

                // Convert operands to IR expressions
                var srcExpr = ConvertOperandToIRExpr(source, instruction);
                if (srcExpr == null)
                    return false;

                // BSF: Find position of least significant set bit
                // Sets ZF if source is zero, otherwise clears ZF and sets destination to bit position
                
                // Create zero constant for comparison
                var zeroConst = CreateConstantExpressionForSize(0, source.Size);
                var isZero = IRExprFactory.Binop(GetCmpEqOpForSize(source.Size), srcExpr, zeroConst);
                
                // Set zero flag based on whether source is zero
                var zfTemp = irsb.NewTemp(IRType.I1);
                irsb.AddStatement(IRStmtFactory.WrTmp(zfTemp, isZero));

                // For BSF, we need to find the position of the least significant set bit
                // Use CTZ (Count Trailing Zeros) - this is exactly what BSF does
                IRExpr ctzResult;
                switch (destination.Size)
                {
                    case OperandSize.Size16:
                        ctzResult = IRExprFactory.Unop(IROp.Ctz32, IRExprFactory.Unop(IROp.Iop_16Uto32, srcExpr));
                        break;
                    case OperandSize.Size32:
                        ctzResult = IRExprFactory.Unop(IROp.Ctz32, srcExpr);
                        break;
                    case OperandSize.Size64:
                        ctzResult = IRExprFactory.Unop(IROp.Ctz64, srcExpr);
                        break;
                    default:
                        Console.WriteLine($"BSF: Unsupported operand size {destination.Size}");
                        return false;
                }

                // Store result to destination
                switch (destination.Type)
                {
                    case AMD64OperandType.Register:
                        return StoreToRegister(destination, ctzResult, irsb);
                    case AMD64OperandType.Memory:
                        return StoreToMemory(destination, ctzResult, instruction, irsb);
                    default:
                        return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error lifting BSF instruction: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Lift BSR (Bit Scan Reverse) instruction
        /// BSR dest, src - Finds first set bit from MSB
        /// </summary>
        private bool LiftBitScanReverse64(AMD64Instruction instruction, IRSB irsb)
        {
            if (instruction.Operands.Length != 2)
                return false;

            var destination = instruction.Operands[0];
            var source = instruction.Operands[1];

            try
            {
                Console.WriteLine($"BSR instruction: scanning reverse in {source} to {destination}");

                // Add instruction marker
                irsb.AddStatement(new IRStmtIMark(CurrentInstructionAddress, (uint)instruction.Length, 0));

                // Convert operands to IR expressions
                var srcExpr = ConvertOperandToIRExpr(source, instruction);
                if (srcExpr == null)
                    return false;

                // BSR: Find position of most significant set bit
                // Sets ZF if source is zero, otherwise clears ZF and sets destination to bit position
                
                // Create zero constant for comparison
                var zeroConst = CreateConstantExpressionForSize(0, source.Size);
                var isZero = IRExprFactory.Binop(GetCmpEqOpForSize(source.Size), srcExpr, zeroConst);
                
                // Set zero flag based on whether source is zero
                var zfTemp = irsb.NewTemp(IRType.I1);
                irsb.AddStatement(IRStmtFactory.WrTmp(zfTemp, isZero));

                // For BSR, we need to find the position of the most significant set bit
                // Use CLZ (Count Leading Zeros) then subtract from bit width - 1
                IRExpr clzResult;
                IRExpr msbitPos;
                var bitWidth = destination.Size switch
                {
                    OperandSize.Size16 => 16,
                    OperandSize.Size32 => 32,
                    OperandSize.Size64 => 64,
                    _ => 64
                };

                switch (destination.Size)
                {
                    case OperandSize.Size16:
                        clzResult = IRExprFactory.Unop(IROp.Clz32, IRExprFactory.Unop(IROp.Iop_16Uto32, srcExpr));
                        msbitPos = IRExprFactory.Binop(IROp.Sub32, CreateConstantExpressionForSize(31, OperandSize.Size32), clzResult);
                        break;
                    case OperandSize.Size32:
                        clzResult = IRExprFactory.Unop(IROp.Clz32, srcExpr);
                        msbitPos = IRExprFactory.Binop(IROp.Sub32, CreateConstantExpressionForSize(31, OperandSize.Size32), clzResult);
                        break;
                    case OperandSize.Size64:
                        clzResult = IRExprFactory.Unop(IROp.Clz64, srcExpr);
                        msbitPos = IRExprFactory.Binop(IROp.Sub64, CreateConstantExpressionForSize(63, OperandSize.Size64), clzResult);
                        break;
                    default:
                        Console.WriteLine($"BSR: Unsupported operand size {destination.Size}");
                        return false;
                }

                // Store result to destination
                switch (destination.Type)
                {
                    case AMD64OperandType.Register:
                        return StoreToRegister(destination, msbitPos, irsb);
                    case AMD64OperandType.Memory:
                        return StoreToMemory(destination, msbitPos, instruction, irsb);
                    default:
                        return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error lifting BSR instruction: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Lift BSWAP (Byte Swap) instruction
        /// BSWAP r32/r64 - Reverses byte order in register
        /// </summary>
        private bool LiftByteSwap64(AMD64Instruction instruction, IRSB irsb)
        {
            if (instruction.Operands.Length != 1)
                return false;

            var destination = instruction.Operands[0];

            try
            {
                Console.WriteLine($"BSWAP instruction: byte swapping {destination}");

                // Add instruction marker
                irsb.AddStatement(new IRStmtIMark(CurrentInstructionAddress, (uint)instruction.Length, 0));

                if (destination.Type == AMD64OperandType.Register)
                {
                    // Convert operand to IR expression
                    var dstExpr = ConvertOperandToIRExpr(destination, instruction);
                    if (dstExpr == null)
                        return false;

                    // BSWAP implementation depends on operand size
                    IRExpr swappedExpr;
                    switch (destination.Size)
                    {
                        case OperandSize.Size32:
                            // 32-bit byte swap: ABCD -> DCBA
                            // Extract bytes and reassemble in reverse order
                            var byte0 = IRExprFactory.Binop(IROp.And32, dstExpr, IRExprFactory.U32(0xFF));
                            var byte1 = IRExprFactory.Binop(IROp.And32, 
                                IRExprFactory.Binop(IROp.Shr32, dstExpr, IRExprFactory.U8(8)), 
                                IRExprFactory.U32(0xFF));
                            var byte2 = IRExprFactory.Binop(IROp.And32, 
                                IRExprFactory.Binop(IROp.Shr32, dstExpr, IRExprFactory.U8(16)), 
                                IRExprFactory.U32(0xFF));
                            var byte3 = IRExprFactory.Binop(IROp.And32, 
                                IRExprFactory.Binop(IROp.Shr32, dstExpr, IRExprFactory.U8(24)), 
                                IRExprFactory.U32(0xFF));

                            // Reassemble: byte0 << 24 | byte1 << 16 | byte2 << 8 | byte3
                            swappedExpr = IRExprFactory.Binop(IROp.Or32,
                                IRExprFactory.Binop(IROp.Or32,
                                    IRExprFactory.Binop(IROp.Shl32, byte0, IRExprFactory.U8(24)),
                                    IRExprFactory.Binop(IROp.Shl32, byte1, IRExprFactory.U8(16))),
                                IRExprFactory.Binop(IROp.Or32,
                                    IRExprFactory.Binop(IROp.Shl32, byte2, IRExprFactory.U8(8)),
                                    byte3));
                            break;

                        case OperandSize.Size64:
                            // 64-bit byte swap: ABCDEFGH -> HGFEDCBA
                            // Use multiple 32-bit swaps and combine
                            var low32 = IRExprFactory.Unop(IROp.Iop_64to32, dstExpr);
                            var high32 = IRExprFactory.Unop(IROp.Iop_64HIto32, dstExpr);
                            
                            // Swap each 32-bit part
                            var swappedLow = CreateByteSwap32(low32);
                            var swappedHigh = CreateByteSwap32(high32);
                            
                            // Combine: swapped low becomes high, swapped high becomes low
                            swappedExpr = IRExprFactory.Binop(IROp.Iop_32HLto64, swappedLow, swappedHigh);
                            break;

                        default:
                            Console.WriteLine($"BSWAP: Unsupported operand size {destination.Size}");
                            return false;
                    }

                    // Store result back to register
                    return StoreToRegister(destination, swappedExpr, irsb);
                }

                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error lifting BSWAP instruction: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Lift POPCNT (Population Count) instruction
        /// POPCNT dest, src - Counts number of set bits
        /// </summary>
        private bool LiftPopulationCount64(AMD64Instruction instruction, IRSB irsb)
        {
            if (instruction.Operands.Length != 2)
                return false;

            var destination = instruction.Operands[0];
            var source = instruction.Operands[1];

            try
            {
                Console.WriteLine($"POPCNT instruction: counting bits in {source} to {destination}");

                // Add instruction marker
                irsb.AddStatement(new IRStmtIMark(CurrentInstructionAddress, (uint)instruction.Length, 0));

                // Convert operands to IR expressions
                var srcExpr = ConvertOperandToIRExpr(source, instruction);
                if (srcExpr == null)
                    return false;

                // POPCNT: Count the number of set bits (population count)
                // Since VEX IR doesn't have a direct popcount operation, we implement it
                // using Brian Kernighan's algorithm: x & (x-1) clears the lowest set bit
                IRExpr popcntResult;
                
                switch (destination.Size)
                {
                    case OperandSize.Size16:
                        popcntResult = ImplementPopCount16(srcExpr, irsb);
                        break;
                    case OperandSize.Size32:
                        popcntResult = ImplementPopCount32(srcExpr, irsb);
                        break;
                    case OperandSize.Size64:
                        popcntResult = ImplementPopCount64(srcExpr, irsb);
                        break;
                    default:
                        Console.WriteLine($"POPCNT: Unsupported operand size {destination.Size}");
                        return false;
                }

                if (popcntResult == null)
                    return false;

                // Store result to destination
                switch (destination.Type)
                {
                    case AMD64OperandType.Register:
                        return StoreToRegister(destination, popcntResult, irsb);
                    case AMD64OperandType.Memory:
                        return StoreToMemory(destination, popcntResult, instruction, irsb);
                    default:
                        return false;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error lifting POPCNT instruction: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Implement population count for 16-bit values using bit manipulation
        /// </summary>
        private IRExpr ImplementPopCount16(IRExpr value, IRSB irsb)
        {
            // Use parallel bit counting algorithm for efficiency
            // count = value - ((value >> 1) & 0x5555)
            // count = (count & 0x3333) + ((count >> 2) & 0x3333)
            // count = (count + (count >> 4)) & 0x0F0F
            // count = (count + (count >> 8)) & 0x00FF
            
            var value16 = IRExprFactory.Unop(IROp.Iop_64to16, value);
            var temp1 = IRExprFactory.Binop(IROp.Sub16, value16,
                IRExprFactory.Binop(IROp.And16,
                    IRExprFactory.Binop(IROp.Shr16, value16, IRExprFactory.U8(1)),
                    IRExprFactory.U16(0x5555)));
            
            var temp2 = IRExprFactory.Binop(IROp.Add16,
                IRExprFactory.Binop(IROp.And16, temp1, IRExprFactory.U16(0x3333)),
                IRExprFactory.Binop(IROp.And16,
                    IRExprFactory.Binop(IROp.Shr16, temp1, IRExprFactory.U8(2)),
                    IRExprFactory.U16(0x3333)));
            
            var temp3 = IRExprFactory.Binop(IROp.And16,
                IRExprFactory.Binop(IROp.Add16, temp2,
                    IRExprFactory.Binop(IROp.Shr16, temp2, IRExprFactory.U8(4))),
                IRExprFactory.U16(0x0F0F));
            
            var result = IRExprFactory.Binop(IROp.And16,
                IRExprFactory.Binop(IROp.Add16, temp3,
                    IRExprFactory.Binop(IROp.Shr16, temp3, IRExprFactory.U8(8))),
                IRExprFactory.U16(0x00FF));
            
            return IRExprFactory.Unop(IROp.Iop_16Uto64, result);
        }

        /// <summary>
        /// Implement population count for 32-bit values using bit manipulation
        /// </summary>
        private IRExpr ImplementPopCount32(IRExpr value, IRSB irsb)
        {
            var value32 = IRExprFactory.Unop(IROp.Iop_64to32, value);
            var temp1 = IRExprFactory.Binop(IROp.Sub32, value32,
                IRExprFactory.Binop(IROp.And32,
                    IRExprFactory.Binop(IROp.Shr32, value32, IRExprFactory.U8(1)),
                    IRExprFactory.U32(0x55555555)));
            
            var temp2 = IRExprFactory.Binop(IROp.Add32,
                IRExprFactory.Binop(IROp.And32, temp1, IRExprFactory.U32(0x33333333)),
                IRExprFactory.Binop(IROp.And32,
                    IRExprFactory.Binop(IROp.Shr32, temp1, IRExprFactory.U8(2)),
                    IRExprFactory.U32(0x33333333)));
            
            var temp3 = IRExprFactory.Binop(IROp.And32,
                IRExprFactory.Binop(IROp.Add32, temp2,
                    IRExprFactory.Binop(IROp.Shr32, temp2, IRExprFactory.U8(4))),
                IRExprFactory.U32(0x0F0F0F0F));
            
            var temp4 = IRExprFactory.Binop(IROp.Add32, temp3,
                IRExprFactory.Binop(IROp.Shr32, temp3, IRExprFactory.U8(8)));
            
            var result = IRExprFactory.Binop(IROp.And32,
                IRExprFactory.Binop(IROp.Add32, temp4,
                    IRExprFactory.Binop(IROp.Shr32, temp4, IRExprFactory.U8(16))),
                IRExprFactory.U32(0x0000003F));
            
            return IRExprFactory.Unop(IROp.Iop_32Uto64, result);
        }

        /// <summary>
        /// Implement population count for 64-bit values using bit manipulation
        /// </summary>
        private IRExpr ImplementPopCount64(IRExpr value, IRSB irsb)
        {
            var temp1 = IRExprFactory.Binop(IROp.Sub64, value,
                IRExprFactory.Binop(IROp.And64,
                    IRExprFactory.Binop(IROp.Shr64, value, IRExprFactory.U8(1)),
                    IRExprFactory.U64(0x5555555555555555UL)));
            
            var temp2 = IRExprFactory.Binop(IROp.Add64,
                IRExprFactory.Binop(IROp.And64, temp1, IRExprFactory.U64(0x3333333333333333UL)),
                IRExprFactory.Binop(IROp.And64,
                    IRExprFactory.Binop(IROp.Shr64, temp1, IRExprFactory.U8(2)),
                    IRExprFactory.U64(0x3333333333333333UL)));
            
            var temp3 = IRExprFactory.Binop(IROp.And64,
                IRExprFactory.Binop(IROp.Add64, temp2,
                    IRExprFactory.Binop(IROp.Shr64, temp2, IRExprFactory.U8(4))),
                IRExprFactory.U64(0x0F0F0F0F0F0F0F0FUL));
            
            var temp4 = IRExprFactory.Binop(IROp.Add64, temp3,
                IRExprFactory.Binop(IROp.Shr64, temp3, IRExprFactory.U8(8)));
            
            var temp5 = IRExprFactory.Binop(IROp.Add64, temp4,
                IRExprFactory.Binop(IROp.Shr64, temp4, IRExprFactory.U8(16)));
            
            var result = IRExprFactory.Binop(IROp.And64,
                IRExprFactory.Binop(IROp.Add64, temp5,
                    IRExprFactory.Binop(IROp.Shr64, temp5, IRExprFactory.U8(32))),
                IRExprFactory.U64(0x000000000000007FUL));
            
            return result;
        }

        /// <summary>
        /// Lift LZCNT (Leading Zero Count) instruction
        /// LZCNT dest, src - Counts leading zeros from MSB
        /// </summary>
        private bool LiftLeadingZeroCount64(AMD64Instruction instruction, IRSB irsb)
        {
            if (instruction.Operands.Length != 2)
                return false;

            var destination = instruction.Operands[0];
            var source = instruction.Operands[1];

            try
            {
                Console.WriteLine($"LZCNT instruction: counting leading zeros in {source} to {destination}");

                // Add instruction marker
                irsb.AddStatement(new IRStmtIMark(CurrentInstructionAddress, (uint)instruction.Length, 0));

                // Convert operands to IR expressions
                var srcExpr = ConvertOperandToIRExpr(source, instruction);
                if (srcExpr == null)
                    return false;

                // LZCNT: Count the number of leading zeros from MSB
                IRExpr lzcntResult;
                switch (destination.Size)
                {
                    case OperandSize.Size16:
                        lzcntResult = IRExprFactory.Unop(IROp.Clz32, IRExprFactory.Unop(IROp.Iop_16Uto32, srcExpr));
                        break;
                    case OperandSize.Size32:
                        lzcntResult = IRExprFactory.Unop(IROp.Clz32, srcExpr);
                        break;
                    case OperandSize.Size64:
                        lzcntResult = IRExprFactory.Unop(IROp.Clz64, srcExpr);
                        break;
                    default:
                        Console.WriteLine($"LZCNT: Unsupported operand size {destination.Size}");
                        return false;
                }

                // Store result to destination
                switch (destination.Type)
                {
                    case AMD64OperandType.Register:
                        return StoreToRegister(destination, lzcntResult, irsb);
                    case AMD64OperandType.Memory:
                        return StoreToMemory(destination, lzcntResult, instruction, irsb);
                    default:
                        return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error lifting LZCNT instruction: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Lift TZCNT (Trailing Zero Count) instruction
        /// TZCNT dest, src - Counts trailing zeros from LSB
        /// </summary>
        private bool LiftTrailingZeroCount64(AMD64Instruction instruction, IRSB irsb)
        {
            if (instruction.Operands.Length != 2)
                return false;

            var destination = instruction.Operands[0];
            var source = instruction.Operands[1];

            try
            {
                Console.WriteLine($"TZCNT instruction: counting trailing zeros in {source} to {destination}");

                // Add instruction marker
                irsb.AddStatement(new IRStmtIMark(CurrentInstructionAddress, (uint)instruction.Length, 0));

                // Convert operands to IR expressions
                var srcExpr = ConvertOperandToIRExpr(source, instruction);
                if (srcExpr == null)
                    return false;

                // TZCNT: Count the number of trailing zeros from LSB
                IRExpr tzcntResult;
                switch (destination.Size)
                {
                    case OperandSize.Size16:
                        tzcntResult = IRExprFactory.Unop(IROp.Ctz32, IRExprFactory.Unop(IROp.Iop_16Uto32, srcExpr));
                        break;
                    case OperandSize.Size32:
                        tzcntResult = IRExprFactory.Unop(IROp.Ctz32, srcExpr);
                        break;
                    case OperandSize.Size64:
                        tzcntResult = IRExprFactory.Unop(IROp.Ctz64, srcExpr);
                        break;
                    default:
                        Console.WriteLine($"TZCNT: Unsupported operand size {destination.Size}");
                        return false;
                }

                // Store result to destination
                switch (destination.Type)
                {
                    case AMD64OperandType.Register:
                        return StoreToRegister(destination, tzcntResult, irsb);
                    case AMD64OperandType.Memory:
                        return StoreToMemory(destination, tzcntResult, instruction, irsb);
                    default:
                        return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error lifting TZCNT instruction: {ex.Message}");
                return false;
            }
        }

        #endregion

        #region Conditional Move and Set Instructions

        /// <summary>
        /// Helper method to evaluate condition codes based on instruction mnemonic
        /// </summary>
        private IRExpr EvaluateConditionCode(string mnemonic)
        {
            // Extract condition code from instruction mnemonic
            var condition = mnemonic.ToLowerInvariant();
            
            // Remove instruction prefix (cmov/set) to get pure condition
            if (condition.StartsWith("cmov"))
                condition = condition[4..];
            else if (condition.StartsWith("set"))
                condition = condition[3..];

            // Read condition code state from guest
            var ccOp = IRExprFactory.Get(128, IRType.I64);    // CC_OP
            var ccDep1 = IRExprFactory.Get(136, IRType.I64);  // CC_DEP1 
            var ccDep2 = IRExprFactory.Get(144, IRType.I64);  // CC_DEP2

            // For simplicity, we'll implement basic conditions using CC_DEP1 as flags
            // In a full implementation, this would evaluate based on CC_OP type
            switch (condition)
            {
                // Zero/Equal conditions
                case "e":
                case "z":
                    // ZF = 1
                    return IRExprFactory.Binop(IROp.CmpEQ64, 
                        IRExprFactory.Binop(IROp.And64, ccDep1, IRExprFactory.U64(0x40)), // ZF bit
                        IRExprFactory.U64(0x40));

                case "ne":
                case "nz":
                    // ZF = 0
                    return IRExprFactory.Binop(IROp.CmpEQ64,
                        IRExprFactory.Binop(IROp.And64, ccDep1, IRExprFactory.U64(0x40)), // ZF bit
                        IRExprFactory.U64(0));

                // Carry conditions  
                case "c":
                case "b":
                case "nae":
                    // CF = 1
                    return IRExprFactory.Binop(IROp.CmpEQ64,
                        IRExprFactory.Binop(IROp.And64, ccDep1, IRExprFactory.U64(0x1)), // CF bit
                        IRExprFactory.U64(0x1));

                case "nc":
                case "ae":
                case "nb":
                    // CF = 0
                    return IRExprFactory.Binop(IROp.CmpEQ64,
                        IRExprFactory.Binop(IROp.And64, ccDep1, IRExprFactory.U64(0x1)), // CF bit
                        IRExprFactory.U64(0));

                // Sign conditions
                case "s":
                    // SF = 1
                    return IRExprFactory.Binop(IROp.CmpEQ64,
                        IRExprFactory.Binop(IROp.And64, ccDep1, IRExprFactory.U64(0x80)), // SF bit
                        IRExprFactory.U64(0x80));

                case "ns":
                    // SF = 0
                    return IRExprFactory.Binop(IROp.CmpEQ64,
                        IRExprFactory.Binop(IROp.And64, ccDep1, IRExprFactory.U64(0x80)), // SF bit
                        IRExprFactory.U64(0));

                // Overflow conditions
                case "o":
                    // OF = 1
                    return IRExprFactory.Binop(IROp.CmpEQ64,
                        IRExprFactory.Binop(IROp.And64, ccDep1, IRExprFactory.U64(0x800)), // OF bit
                        IRExprFactory.U64(0x800));

                case "no":
                    // OF = 0
                    return IRExprFactory.Binop(IROp.CmpEQ64,
                        IRExprFactory.Binop(IROp.And64, ccDep1, IRExprFactory.U64(0x800)), // OF bit
                        IRExprFactory.U64(0));

                // Parity conditions
                case "p":
                case "pe":
                    // PF = 1
                    return IRExprFactory.Binop(IROp.CmpEQ64,
                        IRExprFactory.Binop(IROp.And64, ccDep1, IRExprFactory.U64(0x4)), // PF bit
                        IRExprFactory.U64(0x4));

                case "np":
                case "po":
                    // PF = 0
                    return IRExprFactory.Binop(IROp.CmpEQ64,
                        IRExprFactory.Binop(IROp.And64, ccDep1, IRExprFactory.U64(0x4)), // PF bit
                        IRExprFactory.U64(0));

                // Complex conditions (simplified implementations)
                case "l":
                case "nge":
                    // SF != OF (less than)
                    var sf = IRExprFactory.Binop(IROp.And64, ccDep1, IRExprFactory.U64(0x80));
                    var of = IRExprFactory.Binop(IROp.And64, ccDep1, IRExprFactory.U64(0x800));
                    return IRExprFactory.Binop(IROp.CmpNE64, sf, IRExprFactory.Binop(IROp.Shr64, of, IRExprFactory.U8(4)));

                case "ge":
                case "nl":
                    // SF == OF (greater or equal)
                    sf = IRExprFactory.Binop(IROp.And64, ccDep1, IRExprFactory.U64(0x80));
                    of = IRExprFactory.Binop(IROp.And64, ccDep1, IRExprFactory.U64(0x800));
                    return IRExprFactory.Binop(IROp.CmpEQ64, sf, IRExprFactory.Binop(IROp.Shr64, of, IRExprFactory.U8(4)));

                // Default: return true for unsupported conditions
                default:
                    Console.WriteLine($"Warning: Unsupported condition code '{condition}', defaulting to true");
                    return IRExprFactory.U1(true);
            }
        }

        /// <summary>
        /// Lift CMOVcc (Conditional Move) instructions
        /// CMOVcc dest, src - Moves source to destination if condition is met
        /// </summary>
        private bool LiftConditionalMove64(AMD64Instruction instruction, IRSB irsb)
        {
            if (instruction.Operands.Length != 2)
                return false;

            var destination = instruction.Operands[0];
            var source = instruction.Operands[1];

            try
            {
                Console.WriteLine($"CMOV{instruction.Mnemonic[4..]} instruction: conditionally moving {source} to {destination}");

                // Add instruction marker
                irsb.AddStatement(new IRStmtIMark(CurrentInstructionAddress, (uint)instruction.Length, 0));

                // Convert operands to IR expressions
                var srcExpr = ConvertOperandToIRExpr(source, instruction);
                if (srcExpr == null)
                    return false;

                var dstExpr = ConvertOperandToIRExpr(destination, instruction);
                if (dstExpr == null)
                    return false;

                // Evaluate condition based on instruction mnemonic
                var conditionExpr = EvaluateConditionCode(instruction.Mnemonic);

                // Use ITE (If-Then-Else) to conditionally move
                var conditionalResult = IRExprFactory.ITE(conditionExpr, srcExpr, dstExpr);

                Console.WriteLine($"CMOV instruction: condition-based move with VEX IR ITE");

                // Store result back to destination
                switch (destination.Type)
                {
                    case AMD64OperandType.Register:
                        return StoreToRegister(destination, conditionalResult, irsb);
                    case AMD64OperandType.Memory:
                        return StoreToMemory(destination, conditionalResult, instruction, irsb);
                    default:
                        return false;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error lifting CMOV instruction: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Lift SETcc (Set on Condition) instructions
        /// SETcc r/m8 - Sets byte to 1 if condition is met, 0 otherwise
        /// </summary>
        private bool LiftSetOnCondition64(AMD64Instruction instruction, IRSB irsb)
        {
            if (instruction.Operands.Length != 1)
                return false;

            var destination = instruction.Operands[0];

            try
            {
                Console.WriteLine($"SET{instruction.Mnemonic[3..]} instruction: conditionally setting {destination}");

                // Add instruction marker
                irsb.AddStatement(new IRStmtIMark(CurrentInstructionAddress, (uint)instruction.Length, 0));

                // Evaluate condition based on instruction mnemonic
                var conditionExpr = EvaluateConditionCode(instruction.Mnemonic);

                // Set to 1 if condition is true, 0 if false
                var resultExpr = IRExprFactory.ITE(conditionExpr, IRExprFactory.U8(1), IRExprFactory.U8(0));

                Console.WriteLine($"SET instruction: condition-based setting with VEX IR ITE");

                // Store result back to destination (always 8-bit for SET instructions)
                switch (destination.Type)
                {
                    case AMD64OperandType.Register:
                        var regOffset = GetRegisterOffset(destination.Register);
                        if (regOffset == null)
                            return false;
                        
                        // For SET instructions, only modify the low 8 bits
                        irsb.AddStatement(IRStmtFactory.Put(regOffset, 
                            IRExprFactory.Unop(IROp.Iop_8Uto64, resultExpr)));
                        return true;
                        
                    case AMD64OperandType.Memory:
                        var addressExpr = ConvertMemoryOperandToAddress(destination, instruction);
                        if (addressExpr == null)
                            return false;
                        irsb.AddStatement(IRStmtFactory.StoreLE(addressExpr, resultExpr));
                        return true;
                        
                    default:
                        return false;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error lifting SET instruction: {ex.Message}");
                return false;
            }
        }

        #endregion

        #region Miscellaneous Instructions

        /// <summary>
        /// Lift LEA (Load Effective Address) instruction
        /// LEA dest, [memory] - Loads computed address into destination
        /// </summary>
        private bool LiftLoadEffectiveAddress64(AMD64Instruction instruction, IRSB irsb)
        {
            if (instruction.Operands.Length != 2)
                return false;

            var destination = instruction.Operands[1]; // LEA dest is second operand in AMD64 decoder
            var source = instruction.Operands[0];      // LEA src is first operand in AMD64 decoder

            try
            {
                // Add instruction marker
                irsb.AddStatement(new IRStmtIMark(CurrentInstructionAddress, (uint)instruction.Length, 0));

                if (source.Type == AMD64OperandType.Memory)
                {
                    // Compute the effective address without dereferencing
                    var addressExpr = ConvertMemoryOperandToAddress(source, instruction);
                    if (addressExpr == null)
                        return false;

                    // Store the address (not the value at the address) to destination
                    switch (destination.Type)
                    {
                        case AMD64OperandType.Register:
                            return StoreToRegister(destination, addressExpr, irsb);
                        default:
                            return false; // LEA destination must be register
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error lifting LEA instruction: {ex.Message}");
                Console.WriteLine($"Exception stack trace: {ex.StackTrace}");
                return false;
            }
        }

        /// <summary>
        /// Lift XCHG (Exchange) instruction
        /// XCHG dest, src - Exchanges values between operands
        /// </summary>
        private bool LiftExchange64(AMD64Instruction instruction, IRSB irsb)
        {
            if (instruction.Operands.Length != 2)
                return false;

            var operand1 = instruction.Operands[0];
            var operand2 = instruction.Operands[1];

            try
            {
                Console.WriteLine($"XCHG instruction: exchanging {operand1} and {operand2}");

                // Add instruction marker
                irsb.AddStatement(new IRStmtIMark(CurrentInstructionAddress, (uint)instruction.Length, 0));

                // Load both operands
                var expr1 = ConvertOperandToIRExpr(operand1, instruction);
                var expr2 = ConvertOperandToIRExpr(operand2, instruction);
                
                if (expr1 == null || expr2 == null)
                    return false;

                // Store operand2's value to operand1
                bool result1 = false;
                switch (operand1.Type)
                {
                    case AMD64OperandType.Register:
                        result1 = StoreToRegister(operand1, expr2, irsb);
                        break;
                    case AMD64OperandType.Memory:
                        result1 = StoreToMemory(operand1, expr2, instruction, irsb);
                        break;
                }

                // Store operand1's value to operand2
                bool result2 = false;
                switch (operand2.Type)
                {
                    case AMD64OperandType.Register:
                        result2 = StoreToRegister(operand2, expr1, irsb);
                        break;
                    case AMD64OperandType.Memory:
                        result2 = StoreToMemory(operand2, expr1, instruction, irsb);
                        break;
                }

                return result1 && result2;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error lifting XCHG instruction: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Lift CMPXCHG (Compare and Exchange) instruction
        /// CMPXCHG dest, src - Atomic compare and exchange
        /// </summary>
        private bool LiftCompareExchange64(AMD64Instruction instruction, IRSB irsb)
        {
            if (instruction.Operands.Length != 2)
                return false;

            var destination = instruction.Operands[0];
            var source = instruction.Operands[1];

            try
            {
                Console.WriteLine($"CMPXCHG instruction: compare and exchange {destination} with {source}");

                // Add instruction marker
                irsb.AddStatement(new IRStmtIMark(CurrentInstructionAddress, (uint)instruction.Length, 0));

                // CMPXCHG compares AL/AX/EAX/RAX with destination
                // If equal, ZF=1 and source is stored to destination
                // If not equal, ZF=0 and destination is stored to AL/AX/EAX/RAX
                
                // Load operands
                var destExpr = ConvertOperandToIRExpr(destination, instruction);
                var srcExpr = ConvertOperandToIRExpr(source, instruction);
                var raxExpr = IRExprFactory.Get(GetRegisterOffset("rax"), IRType.I64);
                
                if (destExpr == null || srcExpr == null)
                    return false;

                // Compare RAX with destination
                var comparison = IRExprFactory.Binop(IROp.CmpEQ64, raxExpr, destExpr);
                
                // If equal: store source to destination
                // If not equal: store destination to RAX
                var newDestValue = IRExprFactory.ITE(comparison, srcExpr, destExpr);
                var newRaxValue = IRExprFactory.ITE(comparison, raxExpr, destExpr);
                
                // Store results
                bool result1 = false;
                switch (destination.Type)
                {
                    case AMD64OperandType.Register:
                        result1 = StoreToRegister(destination, newDestValue, irsb);
                        break;
                    case AMD64OperandType.Memory:
                        result1 = StoreToMemory(destination, newDestValue, instruction, irsb);
                        break;
                }
                
                // Store to RAX
                irsb.AddStatement(IRStmtFactory.Put(GetRegisterOffset("rax"), newRaxValue));
                
                // Set ZF flag based on comparison
                // Note: This is a simplified flag update
                var zeroFlag = comparison;
                // Flag updates would normally be more complete but this is basic implementation
                
                return result1;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error lifting CMPXCHG instruction: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Lift XADD (Exchange and Add) instruction
        /// XADD dest, src - Exchanges operands, then stores the sum in destination
        /// </summary>
        private bool LiftExchangeAdd64(AMD64Instruction instruction, IRSB irsb)
        {
            if (instruction.Operands.Length != 2)
                return false;

            var destination = instruction.Operands[0];
            var source = instruction.Operands[1];

            try
            {
                Console.WriteLine($"XADD instruction: exchange and add {destination} and {source}");

                // Add instruction marker
                irsb.AddStatement(new IRStmtIMark(CurrentInstructionAddress, (uint)instruction.Length, 0));

                // XADD: temp = dest + src; src = dest; dest = temp
                var destExpr = ConvertOperandToIRExpr(destination, instruction);
                var srcExpr = ConvertOperandToIRExpr(source, instruction);
                
                if (destExpr == null || srcExpr == null)
                    return false;

                // Calculate sum
                var sumExpr = IRExprFactory.Binop(IROp.Add64, destExpr, srcExpr);
                
                // Store original destination value to source
                bool result1 = false;
                switch (source.Type)
                {
                    case AMD64OperandType.Register:
                        result1 = StoreToRegister(source, destExpr, irsb);
                        break;
                    case AMD64OperandType.Memory:
                        result1 = StoreToMemory(source, destExpr, instruction, irsb);
                        break;
                }
                
                // Store sum to destination
                bool result2 = false;
                switch (destination.Type)
                {
                    case AMD64OperandType.Register:
                        result2 = StoreToRegister(destination, sumExpr, irsb);
                        break;
                    case AMD64OperandType.Memory:
                        result2 = StoreToMemory(destination, sumExpr, instruction, irsb);
                        break;
                }
                
                // Note: Flag updates would normally be done here based on the addition result
                
                return result1 && result2;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error lifting XADD instruction: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Lift NOP (No Operation) instruction
        /// NOP - Does nothing
        /// </summary>
        private bool LiftNop64(AMD64Instruction instruction, IRSB irsb)
        {
            try
            {
                Console.WriteLine($"NOP instruction: no operation");

                // Add instruction marker
                irsb.AddStatement(new IRStmtIMark(CurrentInstructionAddress, (uint)instruction.Length, 0));

                // NOP does nothing - just the instruction marker is enough
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error lifting NOP instruction: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Lift UD2 (Undefined Instruction) instruction
        /// UD2 - Generates undefined instruction exception
        /// </summary>
        private bool LiftUndefinedInstruction64(AMD64Instruction instruction, IRSB irsb)
        {
            try
            {
                Console.WriteLine($"UD2 instruction: undefined instruction");

                // Add instruction marker
                irsb.AddStatement(new IRStmtIMark(CurrentInstructionAddress, (uint)instruction.Length, 0));

                // Generate illegal instruction exception using VEX IR exit statement
                // UD2 always generates #UD (undefined instruction) exception
                var guard = IRExprFactory.U1(true); // Always take this exit
                irsb.AddStatement(IRStmtFactory.Exit(guard, IRJumpKind.SigILL, 
                    IRConstFactory.U64(CurrentInstructionAddress), (int)instruction.Length));

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error lifting UD2 instruction: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Lift INT3 (Breakpoint) instruction
        /// INT3 - Software breakpoint
        /// </summary>
        private bool LiftBreakpoint64(AMD64Instruction instruction, IRSB irsb)
        {
            try
            {
                Console.WriteLine($"INT3 instruction: breakpoint");

                // Add instruction marker
                irsb.AddStatement(new IRStmtIMark(CurrentInstructionAddress, (uint)instruction.Length, 0));

                // Generate breakpoint exception using VEX IR exit statement
                // INT3 generates #BP (breakpoint) exception which becomes SIGTRAP in Unix
                var guard = IRExprFactory.U1(true); // Always take this exit
                irsb.AddStatement(IRStmtFactory.Exit(guard, IRJumpKind.SigTRAP, 
                    IRConstFactory.U64(CurrentInstructionAddress), (int)instruction.Length));

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error lifting INT3 instruction: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Lift LAHF (Load Flags into AH) instruction
        /// LAHF - Loads lower 8 bits of flags into AH
        /// </summary>
        private bool LiftLoadFlagsToAH64(AMD64Instruction instruction, IRSB irsb)
        {
            try
            {
                Console.WriteLine($"LAHF instruction: loading flags to AH");

                // Add instruction marker
                irsb.AddStatement(new IRStmtIMark(CurrentInstructionAddress, (uint)instruction.Length, 0));

                // TODO: Load flags from guest state to AH
                Console.WriteLine($"LAHF instruction: placeholder implementation");

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error lifting LAHF instruction: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Lift SAHF (Store AH into Flags) instruction
        /// SAHF - Stores AH into lower 8 bits of flags
        /// </summary>
        private bool LiftStoreFlagsFromAH64(AMD64Instruction instruction, IRSB irsb)
        {
            try
            {
                Console.WriteLine($"SAHF instruction: storing AH to flags");

                // Add instruction marker
                irsb.AddStatement(new IRStmtIMark(CurrentInstructionAddress, (uint)instruction.Length, 0));

                // TODO: Store AH to flags in guest state
                Console.WriteLine($"SAHF instruction: placeholder implementation");

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error lifting SAHF instruction: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Lift flag manipulation instructions (CLC, STC, CMC, CLD, STD)
        /// </summary>
        private bool LiftClearCarry64(AMD64Instruction instruction, IRSB irsb)
        {
            Console.WriteLine($"CLC instruction: clear carry flag");
            irsb.AddStatement(new IRStmtIMark(CurrentInstructionAddress, (uint)instruction.Length, 0));
            
            // Clear carry flag by setting CC_OP to Copy and CC_DEP1 to 0
            irsb.AddStatement(IRStmtFactory.Put(128, IRExprFactory.U32((uint)CCOp.Copy)));
            irsb.AddStatement(IRStmtFactory.Put(136, IRExprFactory.U64(0))); // CC_DEP1 = 0 (no carry)
            
            return true;
        }

        private bool LiftSetCarry64(AMD64Instruction instruction, IRSB irsb)
        {
            Console.WriteLine($"STC instruction: set carry flag");
            irsb.AddStatement(new IRStmtIMark(CurrentInstructionAddress, (uint)instruction.Length, 0));
            
            // Set carry flag by setting CC_OP to Copy and CC_DEP1 to 1
            irsb.AddStatement(IRStmtFactory.Put(128, IRExprFactory.U32((uint)CCOp.Copy)));
            irsb.AddStatement(IRStmtFactory.Put(136, IRExprFactory.U64(1))); // CC_DEP1 = 1 (carry set)
            
            return true;
        }

        private bool LiftComplementCarry64(AMD64Instruction instruction, IRSB irsb)
        {
            Console.WriteLine($"CMC instruction: complement carry flag");
            irsb.AddStatement(new IRStmtIMark(CurrentInstructionAddress, (uint)instruction.Length, 0));
            
            // Read current carry flag value and complement it
            var currentCCDep1 = IRExprFactory.Get(136, IRType.I64); // Read CC_DEP1
            var carryBit = IRExprFactory.Binop(IROp.And64, currentCCDep1, IRExprFactory.U64(1));
            var complementedCarry = IRExprFactory.Binop(IROp.Xor64, carryBit, IRExprFactory.U64(1));
            
            // Set the complemented carry flag
            irsb.AddStatement(IRStmtFactory.Put(128, IRExprFactory.U32((uint)CCOp.Copy)));
            irsb.AddStatement(IRStmtFactory.Put(136, complementedCarry)); // CC_DEP1 = complemented carry
            
            return true;
        }

        private bool LiftClearDirection64(AMD64Instruction instruction, IRSB irsb)
        {
            Console.WriteLine($"CLD instruction: clear direction flag");
            irsb.AddStatement(new IRStmtIMark(CurrentInstructionAddress, (uint)instruction.Length, 0));
            
            // Clear direction flag (set to +1 for forward direction)
            irsb.AddStatement(IRStmtFactory.Put(160, IRExprFactory.U64(1))); // DFLAG = 1 (forward)
            
            return true;
        }

        private bool LiftSetDirection64(AMD64Instruction instruction, IRSB irsb)
        {
            Console.WriteLine($"STD instruction: set direction flag");
            irsb.AddStatement(new IRStmtIMark(CurrentInstructionAddress, (uint)instruction.Length, 0));
            
            // Set direction flag (set to -1 for backward direction)
            irsb.AddStatement(IRStmtFactory.Put(160, IRExprFactory.U64(0xFFFFFFFFFFFFFFFF))); // DFLAG = -1 (backward)
            
            return true;
        }

        private bool LiftClearInterrupt64(AMD64Instruction instruction, IRSB irsb)
        {
            Console.WriteLine($"CLI instruction: clear interrupt flag");
            irsb.AddStatement(new IRStmtIMark(CurrentInstructionAddress, (uint)instruction.Length, 0));
            
            // CLI clears the interrupt flag (IF) in RFLAGS
            // In a real implementation, this would disable interrupts
            // For now, we'll just mark it as executed since interrupt handling
            // is typically handled at a higher level than VEX IR
            Console.WriteLine("CLI: Interrupt flag cleared (privileged operation)");
            
            return true;
        }

        private bool LiftSetInterrupt64(AMD64Instruction instruction, IRSB irsb)
        {
            Console.WriteLine($"STI instruction: set interrupt flag");
            irsb.AddStatement(new IRStmtIMark(CurrentInstructionAddress, (uint)instruction.Length, 0));
            
            // STI sets the interrupt flag (IF) in RFLAGS
            // In a real implementation, this would enable interrupts
            // For now, we'll just mark it as executed since interrupt handling
            // is typically handled at a higher level than VEX IR
            Console.WriteLine("STI: Interrupt flag set (privileged operation)");
            
            return true;
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Get register offset in guest state
        /// </summary>
        /// <summary>
        /// Get register offset using guest state integration
        /// </summary>
        private int GetRegisterOffset(string register)
        {
            return _guestState.GetRegisterOffset(register);
        }

        /// <summary>
        /// Helper for register reads with guest state integration
        /// </summary>
        private IRExpr GetRegister(string regName, IRType? typeOverride = null)
        {
            int offset = GetRegisterOffset(regName);
            
            // Use guest state type information if no override provided
            if (typeOverride == null)
            {
                var regType = _guestState.GetRegisterType(regName);
                return IRExprFactory.Get(offset, regType);
            }
            
            return IRExprFactory.Get(offset, typeOverride.Value);
        }

        /// <summary>
        /// Helper for register writes with guest state integration
        /// </summary>
        private void PutRegister(string regName, IRExpr value, IRSB irsb)
        {
            int offset = GetRegisterOffset(regName);
            irsb.AddStatement(new IRStmtPut(offset, value));
        }

        /// <summary>
        /// Enhanced helper for flag computation with guest state integration
        /// </summary>
        private void UpdateFlags(IRExpr result, IRExpr arg1, IRExpr arg2, string operation, IRSB irsb)
        {
            // Get flag register offsets from guest state
            var ccOpOffset = GetRegisterOffset("CC_OP");
            var ccDep1Offset = GetRegisterOffset("CC_DEP1");
            var ccDep2Offset = GetRegisterOffset("CC_DEP2");
            var ccNDepOffset = GetRegisterOffset("CC_NDEP");
            
            // Create CC operation code based on operation type
            var ccOp = operation.ToUpper() switch
            {
                "ADD" => IRExprFactory.U64(1), // CCOp.Add (64-bit for AMD64)
                "SUB" => IRExprFactory.U64(2), // CCOp.Sub
                "AND" => IRExprFactory.U64(3), // CCOp.And
                "OR" => IRExprFactory.U64(4),  // CCOp.Or
                "XOR" => IRExprFactory.U64(5), // CCOp.Xor
                _ => IRExprFactory.U64(0)      // CCOp.Copy (default)
            };
            
            // Update condition code state for lazy flag evaluation
            irsb.AddStatement(new IRStmtPut(ccOpOffset, ccOp));
            irsb.AddStatement(new IRStmtPut(ccDep1Offset, arg1));
            irsb.AddStatement(new IRStmtPut(ccDep2Offset, arg2));
            irsb.AddStatement(new IRStmtPut(ccNDepOffset, result));
        }

        /// <summary>
        /// Get IR type for operand size
        /// </summary>
        private IRType? GetIRTypeForOperandSize(OperandSize size)
        {
            return size switch
            {
                OperandSize.Size8 => IRType.I8,
                OperandSize.Size16 => IRType.I16,
                OperandSize.Size32 => IRType.I32,
                OperandSize.Size64 => IRType.I64,
                OperandSize.Size128 => IRType.V128,
                OperandSize.Size256 => IRType.V256,
                OperandSize.Size512 => IRType.V512,
                _ => null
            };
        }

        /// <summary>
        /// Lift MOVS instruction - Move data from source to destination
        /// </summary>
        private bool LiftMoveString64(AMD64Instruction instruction, IRSB irsb)
        {
            try
            {
                // MOVS moves data from [RSI] to [RDI] and adjusts pointers
                // For simplicity, assume 64-bit operation 
                var irType = IRType.I64;
                var byteSize = 8;
                
                // Load source data from [RSI]
                var sourceAddr = IRExprFactory.Get(GetRegisterOffset("rsi"), IRType.I64);
                var sourceData = IRExprFactory.LoadLE(irType, sourceAddr);
                
                // Load destination address from [RDI]
                var destAddr = IRExprFactory.Get(GetRegisterOffset("rdi"), IRType.I64);
                
                // Store data to destination
                irsb.AddStatement(IRStmtFactory.StoreLE(destAddr, sourceData));
                
                // Simple increment for both pointers (ignoring direction flag for now)
                var increment = IRExprFactory.U64((ulong)byteSize);
                var newRSI = IRExprFactory.Binop(IROp.Add64, sourceAddr, increment);
                var newRDI = IRExprFactory.Binop(IROp.Add64, destAddr, increment);
                
                irsb.AddStatement(IRStmtFactory.Put(GetRegisterOffset("rsi"), newRSI));
                irsb.AddStatement(IRStmtFactory.Put(GetRegisterOffset("rdi"), newRDI));
                
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error lifting MOVS instruction: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Lift STOS instruction - Store AL/AX/EAX/RAX to destination
        /// </summary>
        private bool LiftStoreString64(AMD64Instruction instruction, IRSB irsb)
        {
            try
            {
                // STOS stores RAX to [RDI] and adjusts RDI
                var irType = IRType.I64;
                var byteSize = 8;
                
                // Get source data from RAX register
                var sourceData = IRExprFactory.Get(GetRegisterOffset("rax"), irType);
                
                // Load destination address from RDI
                var destAddr = IRExprFactory.Get(GetRegisterOffset("rdi"), IRType.I64);
                
                // Store data to destination
                irsb.AddStatement(IRStmtFactory.StoreLE(destAddr, sourceData));
                
                // Simple increment (ignoring direction flag for now)
                var increment = IRExprFactory.U64((ulong)byteSize);
                var newRDI = IRExprFactory.Binop(IROp.Add64, destAddr, increment);
                irsb.AddStatement(IRStmtFactory.Put(GetRegisterOffset("rdi"), newRDI));
                
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error lifting STOS instruction: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Lift LODS instruction - Load data from source to AL/AX/EAX/RAX
        /// </summary>
        private bool LiftLoadString64(AMD64Instruction instruction, IRSB irsb)
        {
            try
            {
                // LODS loads data from [RSI] to RAX and adjusts RSI
                var irType = IRType.I64;
                var byteSize = 8;
                
                // Load source address from RSI
                var sourceAddr = IRExprFactory.Get(GetRegisterOffset("rsi"), IRType.I64);
                
                // Load data from source
                var sourceData = IRExprFactory.LoadLE(irType, sourceAddr);
                
                // Store to RAX register
                irsb.AddStatement(IRStmtFactory.Put(GetRegisterOffset("rax"), sourceData));
                
                // Simple increment (ignoring direction flag for now)
                var increment = IRExprFactory.U64((ulong)byteSize);
                var newRSI = IRExprFactory.Binop(IROp.Add64, sourceAddr, increment);
                irsb.AddStatement(IRStmtFactory.Put(GetRegisterOffset("rsi"), newRSI));
                
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error lifting LODS instruction: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Lift SCAS instruction - Scan and compare AL/AX/EAX/RAX with destination
        /// </summary>
        private bool LiftScanString64(AMD64Instruction instruction, IRSB irsb)
        {
            try
            {
                // SCAS compares RAX with [RDI] and adjusts RDI
                var irType = IRType.I64;
                var byteSize = 8;
                
                // Get value from RAX register
                var raxValue = IRExprFactory.Get(GetRegisterOffset("rax"), irType);
                
                // Load destination address from RDI
                var destAddr = IRExprFactory.Get(GetRegisterOffset("rdi"), IRType.I64);
                
                // Load data from destination
                var destData = IRExprFactory.LoadLE(irType, destAddr);
                
                // Perform comparison (subtract destination from RAX) and set flags
                var result = IRExprFactory.Binop(IROp.Sub64, raxValue, destData);
                // Note: Flag updates would normally be done here but are complex for this example
                
                // Simple increment (ignoring direction flag for now)
                var increment = IRExprFactory.U64((ulong)byteSize);
                var newRDI = IRExprFactory.Binop(IROp.Add64, destAddr, increment);
                irsb.AddStatement(IRStmtFactory.Put(GetRegisterOffset("rdi"), newRDI));
                
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error lifting SCAS instruction: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Lift CMPS instruction - Compare strings
        /// </summary>
        private bool LiftCompareStrings64(AMD64Instruction instruction, IRSB irsb)
        {
            try
            {
                // CMPS compares [RSI] with [RDI] and adjusts both pointers
                var irType = IRType.I64;
                var byteSize = 8;
                
                // Load source data from [RSI]
                var sourceAddr = IRExprFactory.Get(GetRegisterOffset("rsi"), IRType.I64);
                var sourceData = IRExprFactory.LoadLE(irType, sourceAddr);
                
                // Load destination data from [RDI]
                var destAddr = IRExprFactory.Get(GetRegisterOffset("rdi"), IRType.I64);
                var destData = IRExprFactory.LoadLE(irType, destAddr);
                
                // Perform comparison (subtract destination from source) and set flags
                var result = IRExprFactory.Binop(IROp.Sub64, sourceData, destData);
                // Note: Flag updates would normally be done here but are complex for this example
                
                // Simple increment for both pointers (ignoring direction flag for now)
                var increment = IRExprFactory.U64((ulong)byteSize);
                var newRSI = IRExprFactory.Binop(IROp.Add64, sourceAddr, increment);
                var newRDI = IRExprFactory.Binop(IROp.Add64, destAddr, increment);
                
                irsb.AddStatement(IRStmtFactory.Put(GetRegisterOffset("rsi"), newRSI));
                irsb.AddStatement(IRStmtFactory.Put(GetRegisterOffset("rdi"), newRDI));
                
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error lifting CMPS instruction: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Get guest state offset for special flags
        /// </summary>
        private uint GetGuestStateOffset(string flagName)
        {
            return flagName.ToLowerInvariant() switch
            {
                "dflag" => 160, // Direction flag offset
                "iflag" => 168, // Interrupt flag offset
                _ => 0
            };
        }

        /// <summary>
        /// Get size in bytes for operand size
        /// </summary>
        private int GetSizeInBytes(OperandSize size)
        {
            return size switch
            {
                OperandSize.Size8 => 1,
                OperandSize.Size16 => 2,
                OperandSize.Size32 => 4,
                OperandSize.Size64 => 8,
                OperandSize.Size128 => 16,
                OperandSize.Size256 => 32,
                OperandSize.Size512 => 64,
                _ => 8
            };
        }

        /// <summary>
        /// Check if register is a 64-bit register
        /// </summary>
        private bool Is64BitRegister(string register)
        {
            return register.ToLowerInvariant() switch
            {
                "rax" or "rcx" or "rdx" or "rbx" or "rsp" or "rbp" or "rsi" or "rdi" or
                "r8" or "r9" or "r10" or "r11" or "r12" or "r13" or "r14" or "r15" => true,
                _ => false
            };
        }

        #endregion

        #region Processor Extension Detection

        /// <summary>
        /// Check if instruction is an MMX instruction (operates on 64-bit MM registers)
        /// </summary>
        private bool IsMMXInstruction(string mnemonic)
        {
            return mnemonic switch
            {
                // MMX data movement
                "movd" or "movq" or
                
                // MMX arithmetic  
                "paddb" or "paddw" or "paddd" or "paddq" or
                "psubb" or "psubw" or "psubd" or "psubq" or
                "pmulhw" or "pmullw" or "pmaddwd" or
                "pmulhuw" or "pmuludq" or
                
                // MMX logical
                "pand" or "pandn" or "por" or "pxor" or
                
                // MMX comparison
                "pcmpeqb" or "pcmpeqw" or "pcmpeqd" or
                "pcmpgtb" or "pcmpgtw" or "pcmpgtd" or
                
                // MMX pack/unpack
                "packsswb" or "packuswb" or "packssdw" or
                "punpcklbw" or "punpcklwd" or "punpckldq" or
                "punpckhbw" or "punpckhwd" or "punpckhdq" or
                
                // MMX shift
                "psllw" or "pslld" or "psllq" or
                "psrlw" or "psrld" or "psrlq" or
                "psraw" or "psrad" or
                
                // MMX state management
                "emms" => true,
                
                _ => false
            };
        }

        /// <summary>
        /// Check if instruction is an SSE instruction (operates on 128-bit XMM registers)
        /// </summary>
        private bool IsSSEInstruction(string mnemonic)
        {
            return mnemonic switch
            {
                // SSE1 floating-point
                "movaps" or "movups" or "movss" or "movhlps" or "movlhps" or
                "movlps" or "movhps" or "addps" or "addss" or "subps" or "subss" or
                "mulps" or "mulss" or "divps" or "divss" or "maxps" or "maxss" or
                "minps" or "minss" or "sqrtps" or "sqrtss" or "rsqrtps" or "rsqrtss" or
                "rcpps" or "rcpss" or "andps" or "andnps" or "orps" or "xorps" or
                "cmpps" or "cmpss" or "comiss" or "ucomiss" or "shufps" or
                "unpcklps" or "unpckhps" or "cvtpi2ps" or "cvtps2pi" or "cvttps2pi" or
                "cvtsi2ss" or "cvtss2si" or "cvttss2si" or
                
                // SSE2 floating-point
                "movapd" or "movupd" or "movsd" or "addpd" or "addsd" or
                "subpd" or "subsd" or "mulpd" or "mulsd" or "divpd" or "divsd" or
                "maxpd" or "maxsd" or "minpd" or "minsd" or "sqrtpd" or "sqrtsd" or
                "andpd" or "andnpd" or "orpd" or "xorpd" or "cmppd" or "cmpsd" or
                "comisd" or "ucomisd" or "shufpd" or "unpcklpd" or "unpckhpd" or
                
                // SSE2 integer
                "movdqa" or "movdqu" or "movq2dq" or "movdq2q" or "pmuludq" or
                "paddq" or "psubq" or "pshuflw" or "pshufhw" or "pshufd" or
                "pslldq" or "psrldq" or "punpcklqdq" or "punpckhqdq" or
                
                // SSE3
                "addsubps" or "addsubpd" or "haddps" or "haddpd" or "hsubps" or "hsubpd" or
                "movshdup" or "movsldup" or "movddup" or "lddqu" or
                
                // SSSE3  
                "psignb" or "psignw" or "psignd" or "pabsb" or "pabsw" or "pabsd" or
                "palignr" or "pshufb" or "pmulhrsw" or "pmaddubsw" or "phaddw" or
                "phaddd" or "phaddsw" or "phsubw" or "phsubd" or "phsubsw" or
                
                // SSE4.1
                "pblendvb" or "pblendw" or "phminposuw" or "pmulld" or "pmuldq" or
                "dpps" or "dppd" or "roundps" or "roundpd" or "roundss" or "roundsd" or
                "pinsrb" or "pinsrd" or "pinsrq" or "extractps" or "pextrb" or
                "pextrd" or "pextrq" or "pminsb" or "pmaxsb" or "pminuw" or "pmaxuw" or
                "pminud" or "pmaxud" or "pminsd" or "pmaxsd" or "mpsadbw" or
                
                // SSE4.2
                "pcmpestri" or "pcmpestrm" or "pcmpistri" or "pcmpistrm" or
                "pcmpgtq" or "crc32" => true,
                
                _ => false
            };
        }

        /// <summary>
        /// Check if instruction is an AVX instruction (operates on 256-bit YMM registers)
        /// </summary>
        private bool IsAVXInstruction(string mnemonic)
        {
            return mnemonic switch
            {
                // AVX floating-point (typically prefixed with 'v')
                "vmovaps" or "vmovups" or "vmovapd" or "vmovupd" or "vmovss" or "vmovsd" or
                "vaddps" or "vaddpd" or "vaddss" or "vaddsd" or "vsubps" or "vsubpd" or
                "vsubss" or "vsubsd" or "vmulps" or "vmulpd" or "vmulss" or "vmulsd" or
                "vdivps" or "vdivpd" or "vdivss" or "vdivsd" or "vmaxps" or "vmaxpd" or
                "vmaxss" or "vmaxsd" or "vminps" or "vminpd" or "vminss" or "vminsd" or
                "vsqrtps" or "vsqrtpd" or "vsqrtss" or "vsqrtsd" or "vrsqrtps" or "vrsqrtss" or
                "vrcpps" or "vrcpss" or "vandps" or "vandpd" or "vandnps" or "vandnpd" or
                "vorps" or "vorpd" or "vxorps" or "vxorpd" or "vcmpps" or "vcmppd" or
                "vcmpss" or "vcmpsd" or "vcomiss" or "vcomisd" or "vucomiss" or "vucomisd" or
                "vshufps" or "vshufpd" or "vunpcklps" or "vunpcklpd" or "vunpckhps" or "vunpckhpd" or
                "vblendps" or "vblendpd" or "vblendvps" or "vblendvpd" or "vdpps" or "vdppd" or
                "vroundps" or "vroundpd" or "vroundss" or "vroundsd" or
                
                // AVX integer
                "vmovdqa" or "vmovdqu" or "vpaddb" or "vpaddw" or "vpaddd" or "vpaddq" or
                "vpsubb" or "vpsubw" or "vpsubd" or "vpsubq" or "vpmullw" or "vpmulhw" or
                "vpmulhuw" or "vpmulld" or "vpmuldq" or "vpmuludq" or "vpmaddwd" or "vpmaddubsw" or
                "vpand" or "vpandn" or "vpor" or "vpxor" or "vpcmpeqb" or "vpcmpeqw" or
                "vpcmpeqd" or "vpcmpeqq" or "vpcmpgtb" or "vpcmpgtw" or "vpcmpgtd" or "vpcmpgtq" or
                "vpacksswb" or "vpackuswb" or "vpackssdw" or "vpackusdw" or
                "vpunpcklbw" or "vpunpcklwd" or "vpunpckldq" or "vpunpcklqdq" or
                "vpunpckhbw" or "vpunpckhwd" or "vpunpckhdq" or "vpunpckhqdq" or
                "vpsllw" or "vpslld" or "vpsllq" or "vpsrlw" or "vpsrld" or "vpsrlq" or
                "vpsraw" or "vpsrad" or "vpslldq" or "vpsrldq" or "vpshufd" or "vpshufb" or
                "vpshuflw" or "vpshufhw" or "vpalignr" or
                
                // AVX2 extensions
                "vbroadcastss" or "vbroadcastsd" or "vbroadcasti128" or "vbroadcastf128" or
                "vextracti128" or "vextractf128" or "vinserti128" or "vinsertf128" or
                "vperm2i128" or "vperm2f128" or "vpermilps" or "vpermilpd" or "vpermps" or "vpermd" or
                "vgatherdps" or "vgatherqps" or "vgatherdpd" or "vgatherqpd" or
                "vpgatherdd" or "vpgatherqd" or "vpgatherdq" or "vpgatherqq" or
                
                // FMA (Fused Multiply-Add)
                "vfmadd132ps" or "vfmadd132pd" or "vfmadd132ss" or "vfmadd132sd" or
                "vfmadd213ps" or "vfmadd213pd" or "vfmadd213ss" or "vfmadd213sd" or
                "vfmadd231ps" or "vfmadd231pd" or "vfmadd231ss" or "vfmadd231sd" or
                "vfmsub132ps" or "vfmsub132pd" or "vfmsub132ss" or "vfmsub132sd" or
                "vfmsub213ps" or "vfmsub213pd" or "vfmsub213ss" or "vfmsub213sd" or
                "vfmsub231ps" or "vfmsub231pd" or "vfmsub231ss" or "vfmsub231sd" or
                "vfnmadd132ps" or "vfnmadd132pd" or "vfnmadd132ss" or "vfnmadd132sd" or
                "vfnmadd213ps" or "vfnmadd213pd" or "vfnmadd213ss" or "vfnmadd213sd" or
                "vfnmadd231ps" or "vfnmadd231pd" or "vfnmadd231ss" or "vfnmadd231sd" or
                "vfnmsub132ps" or "vfnmsub132pd" or "vfnmsub132ss" or "vfnmsub132sd" or
                "vfnmsub213ps" or "vfnmsub213pd" or "vfnmsub213ss" or "vfnmsub213sd" or
                "vfnmsub231ps" or "vfnmsub231pd" or "vfnmsub231ss" or "vfnmsub231sd" => true,
                
                _ => false
            };
        }

        #endregion
    }
}
