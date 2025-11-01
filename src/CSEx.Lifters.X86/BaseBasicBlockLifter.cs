using CSEx.IR;
using CSEx.Core;
using CSEx.Guests;
using System;

namespace CSEx.Lifters.X86
{
    /// <summary>
    /// Base class for x86-family basic block lifters.
    /// Provides common infrastructure for x86, AMD64, and other x86-family architectures.
    /// Based on VEX guest_*_toIR.c pattern for code reuse.
    /// </summary>
    /// <typeparam name="TGuestState">Guest state type (X86GuestState, AMD64GuestState, etc.)</typeparam>
    public abstract class BaseBasicBlockLifter<TGuestState> where TGuestState : GuestStateBase
    {
        protected readonly TGuestState _guestState;
        protected byte[] _code;
        protected ulong _baseAddress;
        protected int _position;
        protected IRSB _irsb;
        protected IRTypeEnv _typeEnv;
        
        /// <summary>
        /// Current instruction start address for debugging/error reporting
        /// </summary>
        public ulong CurrentInstructionAddress => _baseAddress + (ulong)_position;
        
        /// <summary>
        /// Maximum instructions to lift in a single basic block
        /// </summary>
        public int MaxInstructions { get; set; } = 50;
        
        /// <summary>
        /// Maximum bytes to process in a single basic block
        /// </summary>
        public int MaxBytes { get; set; } = 500;

        protected BaseBasicBlockLifter(TGuestState guestState)
        {
            _guestState = guestState ?? throw new ArgumentNullException(nameof(guestState));
            _code = Array.Empty<byte>();
            _baseAddress = 0;
            _position = 0;
            _irsb = new IRSB();
            _typeEnv = new IRTypeEnv();
        }

        /// <summary>
        /// Architecture-specific word size (32-bit for x86, 64-bit for AMD64)
        /// </summary>
        protected abstract IRType ArchWordSize { get; }
        
        /// <summary>
        /// Architecture-specific instruction pointer type
        /// </summary>
        protected abstract IRType ArchIPType { get; }

        /// <summary>
        /// Get architecture-specific register name for operand conversion
        /// Override for AMD64 to handle extended registers (R8-R15)
        /// </summary>
        /// <param name="regName">Register name</param>
        /// <returns>Architecture-specific register name</returns>
        protected virtual string GetArchRegisterName(string regName)
        {
            return regName; // Default implementation - override for extended registers
        }

        /// <summary>
        /// Architecture-specific operand size calculation
        /// Override for AMD64 to handle 64-bit default operand sizes
        /// </summary>
        /// <param name="operandSize">Base operand size</param>
        /// <returns>Architecture-specific operand size</returns>
        protected virtual int GetArchOperandSize(int operandSize)
        {
            return operandSize; // Default implementation
        }

        /// <summary>
        /// Check if instruction terminates a basic block
        /// Can be overridden for architecture-specific control flow
        /// </summary>
        protected virtual bool IsBasicBlockTerminator(X86Decoder.X86Instruction instruction)
        {
            return instruction.Mnemonic switch
            {
                "ret" or "retf" or "iret" => true,
                "jmp" or "jz" or "jnz" or "je" or "jne" or "js" or "jns" or 
                "jc" or "jnc" or "jo" or "jno" or "jp" or "jnp" or "jpe" or "jpo" or
                "ja" or "jae" or "jb" or "jbe" or "jg" or "jge" or "jl" or "jle" or
                "call" => true,
                "int" or "into" or "int3" => true,
                "hlt" => true,
                _ => false
            };
        }

        /// <summary>
        /// Common infrastructure for lifting basic blocks
        /// Architecture-specific implementations override LiftInstruction
        /// </summary>
        public virtual (IRSB irsb, int bytesLifted) LiftBasicBlock(byte[] code, ulong baseAddress, int maxInstructions = 50)
        {
            _code = code ?? throw new ArgumentNullException(nameof(code));
            _baseAddress = baseAddress;
            _position = 0;
            
            // Create new IRSB for this basic block
            _typeEnv = new IRTypeEnv();
            _irsb = new IRSB(_typeEnv);
            
            // Set up basic block metadata
            _irsb.JumpKind = IRJumpKind.Boring;
            
            var decoder = CreateInstructionDecoder(_code);
            int instructionCount = 0;
            int startPosition = _position;
            
            try
            {
                while (_position < _code.Length && 
                       instructionCount < maxInstructions && 
                       (_position - startPosition) < MaxBytes)
                {
                    // Track current instruction address
                    ulong instrAddr = _baseAddress + (ulong)_position;
                    
                    // Decode the next instruction
                    var instruction = decoder.DecodeInstruction();
                    if (instruction == null)
                    {
                        // Failed to decode - end basic block
                        break;
                    }
                    
                    // Advance position by instruction length
                    _position += instruction.Length;
                    instructionCount++;
                    
                    // Lift instruction to IR
                    bool liftSuccess = LiftInstruction(instruction, instrAddr);
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
                if (_irsb.Next == null)
                {
                    _irsb.Next = IRExprFactory.U64((ulong)(_baseAddress + (ulong)_position));
                }
                
                // Validate the IRSB
                try
                {
                    IRSBSanityCheck.SanityCheck(_irsb, GetType().Name);
                }
                catch (Exception ex)
                {
                    // Log validation error but continue
                    Console.WriteLine($"IRSB validation warning: {ex.Message}");
                }
                
                return (_irsb, _position - startPosition);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to lift basic block at address 0x{baseAddress:X}: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Create architecture-specific instruction decoder
        /// Override for AMD64 to handle REX prefixes and extended encoding
        /// </summary>
        protected virtual X86InstructionDecoder CreateInstructionDecoder(byte[] code)
        {
            return new X86InstructionDecoder(code);
        }

        /// <summary>
        /// Architecture-specific instruction lifting
        /// Must be implemented by concrete classes
        /// </summary>
        protected abstract bool LiftInstruction(X86Decoder.X86Instruction instruction, ulong address);

        /// <summary>
        /// Common helper for creating IMark statements
        /// </summary>
        protected void AddIMark(ulong address, uint length)
        {
            _irsb.AddStatement(new IRStmtIMark(address, length, 0));
        }

        /// <summary>
        /// Common helper for temporary variable creation
        /// </summary>
        protected IRTemp NewTemp(IRType type)
        {
            return _typeEnv.NewTemp(type);
        }

        /// <summary>
        /// Architecture-specific register offset calculation
        /// Override for AMD64 to handle 64-bit register layout
        /// </summary>
        protected virtual int GetRegisterOffset(string regName)
        {
            // Default implementation - should be overridden by concrete classes
            throw new NotImplementedException($"Register offset lookup not implemented for {regName}");
        }

        /// <summary>
        /// Common helper for register reads with guest state integration
        /// </summary>
        protected IRExpr GetRegister(string regName, IRType? typeOverride = null)
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
        /// Common helper for register writes with guest state integration
        /// </summary>
        protected void PutRegister(string regName, IRExpr value)
        {
            int offset = GetRegisterOffset(regName);
            _irsb.AddStatement(new IRStmtPut(offset, value));
        }

        /// <summary>
        /// Enhanced helper for flag computation with guest state integration
        /// </summary>
        protected virtual void UpdateFlags(IRExpr result, IRExpr arg1, IRExpr arg2, string operation)
        {
            // Get flag register offsets from guest state
            var ccOpOffset = GetRegisterOffset("CC_OP");
            var ccDep1Offset = GetRegisterOffset("CC_DEP1"); 
            var ccDep2Offset = GetRegisterOffset("CC_DEP2");
            var ccNDepOffset = GetRegisterOffset("CC_NDEP");
            
            // Create CC operation code based on operation type
            var ccOp = operation.ToUpper() switch
            {
                "ADD" => IRExprFactory.U32(1), // CCOp.Add
                "SUB" => IRExprFactory.U32(2), // CCOp.Sub
                "AND" => IRExprFactory.U32(3), // CCOp.And
                "OR" => IRExprFactory.U32(4),  // CCOp.Or
                "XOR" => IRExprFactory.U32(5), // CCOp.Xor
                _ => IRExprFactory.U32(0)      // CCOp.Copy (default)
            };
            
            // Update condition code state for lazy flag evaluation
            _irsb.AddStatement(new IRStmtPut(ccOpOffset, ccOp));
            _irsb.AddStatement(new IRStmtPut(ccDep1Offset, arg1));
            _irsb.AddStatement(new IRStmtPut(ccDep2Offset, arg2));
            _irsb.AddStatement(new IRStmtPut(ccNDepOffset, result));
        }
    }
}