using Xunit;
using CSEx.Lifters.X86;

namespace CSEx.Tests.Unit
{
    /// <summary>
    /// Tests for the x86 instruction decoder infrastructure
    /// </summary>
    public class X86DecoderTests
    {
        [Fact]
        public void X86InstructionDecoder_DecodeInstruction_CanDecodeSimpleMovRegImm()
        {
            // MOV EAX, 0x12345678 = B8 78 56 34 12
            var bytes = new byte[] { 0xB8, 0x78, 0x56, 0x34, 0x12 };
            var decoder = new X86InstructionDecoder(bytes);
            
            var instruction = decoder.DecodeInstruction();
            
            Assert.NotNull(instruction);
            Assert.Equal("mov", instruction.Mnemonic);
            Assert.Equal(0xB8, instruction.Opcode);
            Assert.Equal(5, instruction.Length);
            Assert.Equal(2, instruction.Operands.Count);
            
            // First operand should be EAX register
            Assert.Equal(X86Decoder.OperandType.Register, instruction.Operands[0].Type);
            Assert.Equal(X86Decoder.X86Register.EAX, instruction.Operands[0].Register);
            
            // Second operand should be immediate 0x12345678
            Assert.Equal(X86Decoder.OperandType.Immediate, instruction.Operands[1].Type);
            Assert.Equal(0x12345678u, instruction.Operands[1].Immediate);
        }
        
        [Fact]
        public void X86InstructionDecoder_DecodeInstruction_CanDecodeSimpleMovReg8Imm()
        {
            // MOV AL, 0x42 = B0 42
            var bytes = new byte[] { 0xB0, 0x42 };
            var decoder = new X86InstructionDecoder(bytes);
            
            var instruction = decoder.DecodeInstruction();
            
            Assert.NotNull(instruction);
            Assert.Equal("mov", instruction.Mnemonic);
            Assert.Equal(0xB0, instruction.Opcode);
            Assert.Equal(2, instruction.Length);
            Assert.Equal(2, instruction.Operands.Count);
            
            // First operand should be AL register
            Assert.Equal(X86Decoder.OperandType.Register, instruction.Operands[0].Type);
            Assert.Equal(X86Decoder.X86Register.AL, instruction.Operands[0].Register);
            
            // Second operand should be immediate 0x42
            Assert.Equal(X86Decoder.OperandType.Immediate, instruction.Operands[1].Type);
            Assert.Equal(0x42u, instruction.Operands[1].Immediate);
        }
        
        [Fact]
        public void X86InstructionDecoder_DecodeInstruction_CanDecodeAddRegReg()
        {
            // ADD EAX, EBX = 01 D8 (ModR/M: 11 011 000)
            var bytes = new byte[] { 0x01, 0xD8 };
            var decoder = new X86InstructionDecoder(bytes);
            
            var instruction = decoder.DecodeInstruction();
            
            Assert.NotNull(instruction);
            Assert.Equal("add", instruction.Mnemonic);
            Assert.Equal(0x01, instruction.Opcode);
            Assert.Equal(2, instruction.Length);
            Assert.Equal(2, instruction.Operands.Count);
            
            // First operand should be EAX register (destination)
            Assert.Equal(X86Decoder.OperandType.Register, instruction.Operands[0].Type);
            Assert.Equal(X86Decoder.X86Register.EAX, instruction.Operands[0].Register);
            
            // Second operand should be EBX register (source)
            Assert.Equal(X86Decoder.OperandType.Register, instruction.Operands[1].Type);
            Assert.Equal(X86Decoder.X86Register.EBX, instruction.Operands[1].Register);
        }
        
        [Fact]
        public void X86Decoder_GetRegister_ReturnsCorrectRegisters()
        {
            // Test 32-bit registers
            Assert.Equal(X86Decoder.X86Register.EAX, X86Decoder.GetRegister(0, 4));
            Assert.Equal(X86Decoder.X86Register.ECX, X86Decoder.GetRegister(1, 4));
            Assert.Equal(X86Decoder.X86Register.EDX, X86Decoder.GetRegister(2, 4));
            Assert.Equal(X86Decoder.X86Register.EBX, X86Decoder.GetRegister(3, 4));
            
            // Test 8-bit registers
            Assert.Equal(X86Decoder.X86Register.AL, X86Decoder.GetRegister(0, 1));
            Assert.Equal(X86Decoder.X86Register.CL, X86Decoder.GetRegister(1, 1));
            Assert.Equal(X86Decoder.X86Register.DL, X86Decoder.GetRegister(2, 1));
            Assert.Equal(X86Decoder.X86Register.BL, X86Decoder.GetRegister(3, 1));
        }
        
        [Fact]
        public void X86Decoder_DecodeModRM_DecodesCorrectly()
        {
            // ModR/M byte 11 011 000 (mod=3, reg=3, rm=0)
            var (mod, reg, rm) = X86Decoder.DecodeModRM(0xD8);
            
            Assert.Equal(3, mod);
            Assert.Equal(3, reg);
            Assert.Equal(0, rm);
        }
        
        [Fact]
        public void X86InstructionDecoder_DecodeInstruction_RejectsInvalidOpcode()
        {
            // Invalid single-byte opcode
            var bytes = new byte[] { 0xFF }; 
            var decoder = new X86InstructionDecoder(bytes);
            
            var instruction = decoder.DecodeInstruction();
            
            Assert.Null(instruction);
        }

        [Fact]
        public void X86InstructionDecoder_DecodeInstruction_CanDecodeEVEXInstruction()
        {
            // VMOVDQU32 ZMM0, ZMM1 (valid EVEX encoding)
            // EVEX: 62 F1 7C 48 6F C1
            // 62 = EVEX prefix
            // F1 = P0: R=1, X=1, B=1, R'=1, mm=01 (0F map), bits[1:0]=01 (we need 0F map)
            // 7C = P1: W=0, vvvv=1111, U=1, pp=00 (no prefix) 
            // 48 = P2: z=0, L'L=10 (512-bit), b=0, V'=1, aaa=000 (no mask)
            // 6F = MOVDQU opcode
            // C1 = ModR/M: mod=11, reg=000, r/m=001 (register to register)
            var bytes = new byte[] { 0x62, 0xF1, 0x7C, 0x48, 0x6F, 0xC1 };
            var decoder = new X86InstructionDecoder(bytes);
            
            var instruction = decoder.DecodeInstruction();
            
            Assert.NotNull(instruction);
            Assert.True(instruction.HasEVEXPrefix);
            Assert.NotNull(instruction.EVEX);
            
            // Check EVEX prefix fields
            Assert.Equal(0x01, instruction.EVEX.MapSelect); // 0F map
            Assert.False(instruction.EVEX.W); // 32-bit operation
            Assert.Equal(2, instruction.EVEX.LL); // 512-bit vector length
            Assert.False(instruction.EVEX.HasMasking()); // No mask register
            Assert.Equal(512, instruction.EVEX.GetVectorLength());
            
            // Check instruction details
            Assert.Equal(0x6F, instruction.Opcode);
            Assert.Equal(6, instruction.Length);
            Assert.Equal("vmovdqu32", instruction.Mnemonic);
            
            // Check operands (destination ZMM0, source ZMM1)
            Assert.Equal(3, instruction.Operands.Count); // dest, vvvv, src
            Assert.Equal(X86Decoder.OperandType.ZMMRegister, instruction.Operands[0].Type);
            Assert.Equal(0, instruction.Operands[0].ZMMRegister);
            Assert.Equal(X86Decoder.OperandType.ZMMRegister, instruction.Operands[2].Type);
            Assert.Equal(1, instruction.Operands[2].ZMMRegister);
        }

        [Fact]
        public void X86InstructionDecoder_DecodeInstruction_CanDecodeEVEXWithMasking()
        {
            // VPADDD ZMM0{k1}, ZMM1, ZMM2 (with mask register k1)
            // EVEX: 62 F1 75 49 FE C2
            // 62 = EVEX prefix
            // F1 = P0: R=1, X=1, B=1, R'=1, mm=01 (0F map)
            // 75 = P1: W=0, vvvv=1110 (ZMM1), U=1, pp=01 (66 prefix)
            // 49 = P2: z=0, L'L=10 (512-bit), b=0, V'=1, aaa=001 (mask k1)
            // FE = PADDD opcode  
            // C2 = ModR/M: mod=11, reg=000, r/m=010 (ZMM0, ZMM2)
            var bytes = new byte[] { 0x62, 0xF1, 0x75, 0x49, 0xFE, 0xC2 };
            var decoder = new X86InstructionDecoder(bytes);
            
            var instruction = decoder.DecodeInstruction();
            
            Assert.NotNull(instruction);
            Assert.True(instruction.HasEVEXPrefix);
            Assert.NotNull(instruction.EVEX);
            
            // Check masking support
            Assert.True(instruction.EVEX.HasMasking());
            Assert.Equal(1, instruction.EVEX.GetMaskRegister()); // k1
            
            // Check instruction details
            Assert.Equal(0xFE, instruction.Opcode);
            Assert.Equal("vpaddd", instruction.Mnemonic);
            Assert.Equal(512, instruction.EVEX.GetVectorLength());
            
            // Verify it's recognized as SSE instruction type for SIMD operations
            Assert.Equal(X86Decoder.InstructionType.SSE, instruction.InstructionType);
        }
    }
}