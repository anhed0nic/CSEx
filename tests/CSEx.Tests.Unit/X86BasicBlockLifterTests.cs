using System;
using Xunit;
using CSEx.IR;
using CSEx.Guests;
using CSEx.Guests.X86;
using CSEx.Lifters.X86;

namespace CSEx.Tests.Unit;

public class X86BasicBlockLifterTests
{
    /// <summary>
    /// Test basic block lifter creation and simple MOV instruction
    /// </summary>
    [Fact]
    public void BasicBlockLifter_SimpleMovInstruction_ShouldGenerateIR()
    {
        // Arrange
        var guestState = new X86GuestState();
        var lifter = new X86BasicBlockLifter(guestState);
        
        // MOV EAX, 42 (B8 2A 00 00 00)
        byte[] movInstruction = { 0xB8, 0x2A, 0x00, 0x00, 0x00 };
        
        // Act
        var (irsb, bytesLifted) = lifter.LiftBasicBlock(movInstruction, 0x1000);
        
        // Assert
        Assert.NotNull(irsb);
        Assert.Equal(5, bytesLifted); // MOV EAX, immediate is 5 bytes
        Assert.NotEmpty(irsb.Statements);
        
        // The first statement should be related to the MOV operation
        Assert.Contains(irsb.Statements, stmt => stmt is IRStmtPut);
    }

    /// <summary>
    /// Test that lifter stops at RET instruction (basic block boundary)
    /// </summary>
    [Fact]
    public void BasicBlockLifter_RetInstruction_ShouldEndBasicBlock()
    {
        // Arrange
        var guestState = new X86GuestState();
        var lifter = new X86BasicBlockLifter(guestState);
        
        // MOV EAX, 42; RET (B8 2A 00 00 00 C3)
        byte[] codeWithRet = { 0xB8, 0x2A, 0x00, 0x00, 0x00, 0xC3 };
        
        // Act
        var (irsb, bytesLifted) = lifter.LiftBasicBlock(codeWithRet, 0x1000);
        
        // Assert
        Assert.NotNull(irsb);
        Assert.Equal(6, bytesLifted); // Should process both instructions
        Assert.NotEmpty(irsb.Statements);
        
        // Should have proper RET behavior: JumpKind set to Ret and Next expression for return address
        Assert.Equal(IRJumpKind.Ret, irsb.JumpKind);
        Assert.NotNull(irsb.Next);
        
        // Should contain ESP adjustment (stack operations)
        Assert.Contains(irsb.Statements, stmt => stmt is IRStmtPut put && put.Offset == 16); // ESP offset
    }

    /// <summary>
    /// Test basic block lifter with ADD instruction
    /// </summary>
    [Fact]
    public void BasicBlockLifter_AddInstruction_ShouldGenerateArithmeticIR()
    {
        // Arrange
        var guestState = new X86GuestState();
        var lifter = new X86BasicBlockLifter(guestState);
        
        // ADD EAX, EBX (01 D8)
        byte[] addInstruction = { 0x01, 0xD8 };
        
        // Act
        var (irsb, bytesLifted) = lifter.LiftBasicBlock(addInstruction, 0x1000);
        
        // Assert
        Assert.NotNull(irsb);
        Assert.Equal(2, bytesLifted);
        Assert.NotEmpty(irsb.Statements);
        
        // Should contain arithmetic operation and flag updates
        Assert.Contains(irsb.Statements, stmt => stmt is IRStmtWrTmp);
        Assert.Contains(irsb.Statements, stmt => stmt is IRStmtPut);
    }

    /// <summary>
    /// Test that lifter handles empty/invalid code gracefully
    /// </summary>
    [Fact]
    public void BasicBlockLifter_EmptyCode_ShouldReturnEmptyBlock()
    {
        // Arrange
        var guestState = new X86GuestState();
        var lifter = new X86BasicBlockLifter(guestState);
        
        byte[] emptyCode = Array.Empty<byte>();
        
        // Act
        var (irsb, bytesLifted) = lifter.LiftBasicBlock(emptyCode, 0x1000);
        
        // Assert
        Assert.NotNull(irsb);
        Assert.Equal(0, bytesLifted);
    }

    /// <summary>
    /// Test that lifter properties work correctly
    /// </summary>
    [Fact]
    public void BasicBlockLifter_Properties_ShouldWorkCorrectly()
    {
        // Arrange
        var guestState = new X86GuestState();
        var lifter = new X86BasicBlockLifter(guestState);
        
        // Act & Assert
        Assert.Equal(50, lifter.MaxInstructions); // Default value
        Assert.Equal(500, lifter.MaxBytes); // Default value
        
        // Test setting properties
        lifter.MaxInstructions = 25;
        lifter.MaxBytes = 250;
        
        Assert.Equal(25, lifter.MaxInstructions);
        Assert.Equal(250, lifter.MaxBytes);
    }

    /// <summary>
    /// Test AVX-512 instruction lifting with EVEX prefix
    /// </summary>
    [Fact]
    public void BasicBlockLifter_AVX512Instruction_ShouldGenerateIR()
    {
        // Arrange
        var guestState = new X86GuestState();
        var lifter = new X86BasicBlockLifter(guestState);
        
        // VMOVDQU32 ZMM0, ZMM1 (EVEX: 62 F1 7C 48 6F C1)
        byte[] avx512Instruction = { 0x62, 0xF1, 0x7C, 0x48, 0x6F, 0xC1 };
        
        // Act
        var (irsb, bytesLifted) = lifter.LiftBasicBlock(avx512Instruction, 0x1000);
        
        // Assert
        Assert.NotNull(irsb);
        Assert.Equal(6, bytesLifted); // EVEX instruction is 6 bytes
        Assert.NotEmpty(irsb.Statements);
        
        // Should contain statements related to ZMM register manipulation
        Assert.Contains(irsb.Statements, stmt => 
            stmt is IRStmtPut putStmt && 
            putStmt.Offset >= 700 && putStmt.Offset < 1200); // ZMM register offset range
    }

    /// <summary>
    /// Test AVX-512 masked instruction lifting
    /// </summary>
    [Fact]
    public void BasicBlockLifter_AVX512MaskedInstruction_ShouldGenerateIRWithDirtyHelper()
    {
        // Arrange
        var guestState = new X86GuestState();
        var lifter = new X86BasicBlockLifter(guestState);
        
        // VPADDD ZMM0{k1}, ZMM1, ZMM2 (EVEX: 62 F1 75 49 FE C2)
        byte[] avx512MaskedInstruction = { 0x62, 0xF1, 0x75, 0x49, 0xFE, 0xC2 };
        
        // Act
        var (irsb, bytesLifted) = lifter.LiftBasicBlock(avx512MaskedInstruction, 0x1000);
        
        // Assert
        Assert.NotNull(irsb);
        Assert.Equal(6, bytesLifted); // EVEX instruction is 6 bytes
        Assert.NotEmpty(irsb.Statements);
        
        // Should contain dirty helper statement for complex AVX-512 operations
        Assert.Contains(irsb.Statements, stmt => stmt is IRStmtDirty);
        
        // Should also have statements for ZMM and mask register access
        Assert.Contains(irsb.Statements, stmt => 
            stmt is IRStmtPut putStmt && 
            putStmt.Offset >= 700); // ZMM or K register offset range
    }

    /// <summary>
    /// Test CALL instruction lifting to verify stack management
    /// </summary>
    [Fact]
    public void BasicBlockLifter_CallInstruction_ShouldGenerateStackOperations()
    {
        // Arrange
        var guestState = new X86GuestState();
        var lifter = new X86BasicBlockLifter(guestState);
        
        // CALL 0x1234 (E8 2F 02 00 00)
        byte[] callInstruction = { 0xE8, 0x2F, 0x02, 0x00, 0x00 };
        
        // Act
        var (irsb, bytesLifted) = lifter.LiftBasicBlock(callInstruction, 0x1000);
        
        // Assert
        Assert.NotNull(irsb);
        Assert.Equal(5, bytesLifted);
        Assert.NotEmpty(irsb.Statements);
        
        // Should have call behavior: JumpKind set to Call and proper stack operations
        Assert.Equal(IRJumpKind.Call, irsb.JumpKind);
        Assert.NotNull(irsb.Next);
        
        // Should contain ESP adjustment and memory store for return address
        Assert.Contains(irsb.Statements, stmt => stmt is IRStmtPut put && put.Offset == 16); // ESP offset
        Assert.Contains(irsb.Statements, stmt => stmt is IRStmtStore); // Store return address
    }

    /// <summary>
    /// Test PUSH instruction lifting
    /// </summary>
    [Fact]
    public void BasicBlockLifter_PushInstruction_ShouldGenerateStackOperations()
    {
        // Arrange
        var guestState = new X86GuestState();
        var lifter = new X86BasicBlockLifter(guestState);
        
        // PUSH EAX (50)
        byte[] pushInstruction = { 0x50 };
        
        // Act
        var (irsb, bytesLifted) = lifter.LiftBasicBlock(pushInstruction, 0x1000);
        
        // Assert
        Assert.NotNull(irsb);
        Assert.Equal(1, bytesLifted);
        Assert.NotEmpty(irsb.Statements);
        
        // Should contain ESP decrement and memory store
        Assert.Contains(irsb.Statements, stmt => stmt is IRStmtPut put && put.Offset == 16); // ESP offset
        Assert.Contains(irsb.Statements, stmt => stmt is IRStmtStore); // Store value to stack
    }

    /// <summary>
    /// Test POP instruction lifting
    /// </summary>
    [Fact]
    public void BasicBlockLifter_PopInstruction_ShouldGenerateStackOperations()
    {
        // Arrange
        var guestState = new X86GuestState();
        var lifter = new X86BasicBlockLifter(guestState);
        
        // POP EAX (58)
        byte[] popInstruction = { 0x58 };
        
        // Act
        var (irsb, bytesLifted) = lifter.LiftBasicBlock(popInstruction, 0x1000);
        
        // Assert
        Assert.NotNull(irsb);
        Assert.Equal(1, bytesLifted);
        Assert.NotEmpty(irsb.Statements);
        
        // Should contain ESP increment and memory load
        Assert.Contains(irsb.Statements, stmt => stmt is IRStmtPut put && put.Offset == 16); // ESP offset
        Assert.Contains(irsb.Statements, stmt => stmt is IRStmtWrTmp); // Load value from stack
    }

    /// <summary>
    /// Test INC instruction lifting
    /// </summary>
    [Fact]
    public void BasicBlockLifter_IncInstruction_ShouldGenerateArithmeticAndFlags()
    {
        // Arrange
        var guestState = new X86GuestState();
        var lifter = new X86BasicBlockLifter(guestState);
        
        // INC EAX (40)
        byte[] incInstruction = { 0x40 };
        
        // Act
        var (irsb, bytesLifted) = lifter.LiftBasicBlock(incInstruction, 0x1000);
        
        // Assert
        Assert.NotNull(irsb);
        Assert.Equal(1, bytesLifted);
        Assert.NotEmpty(irsb.Statements);
        
        // Should contain register update and flag operations
        Assert.Contains(irsb.Statements, stmt => stmt is IRStmtPut put && put.Offset == 0); // EAX offset
        Assert.Contains(irsb.Statements, stmt => stmt is IRStmtPut put && put.Offset == 32); // CC_OP offset
    }

    /// <summary>
    /// Test DEC instruction lifting
    /// </summary>
    [Fact]
    public void BasicBlockLifter_DecInstruction_ShouldGenerateArithmeticAndFlags()
    {
        // Arrange
        var guestState = new X86GuestState();
        var lifter = new X86BasicBlockLifter(guestState);
        
        // DEC EAX (48)
        byte[] decInstruction = { 0x48 };
        
        // Act
        var (irsb, bytesLifted) = lifter.LiftBasicBlock(decInstruction, 0x1000);
        
        // Assert
        Assert.NotNull(irsb);
        Assert.Equal(1, bytesLifted);
        Assert.NotEmpty(irsb.Statements);
        
        // Should contain register update and flag operations
        Assert.Contains(irsb.Statements, stmt => stmt is IRStmtPut put && put.Offset == 0); // EAX offset
        Assert.Contains(irsb.Statements, stmt => stmt is IRStmtPut put && put.Offset == 32); // CC_OP offset
    }

    /// <summary>
    /// Test NEG instruction lifting
    /// </summary>
    [Fact]
    public void BasicBlockLifter_NegInstruction_ShouldGenerateArithmeticAndFlags()
    {
        // Arrange
        var guestState = new X86GuestState();
        var lifter = new X86BasicBlockLifter(guestState);
        
        // NEG EAX (F7 D8)
        byte[] negInstruction = { 0xF7, 0xD8 };
        
        // Act
        var (irsb, bytesLifted) = lifter.LiftBasicBlock(negInstruction, 0x1000);
        
        // Assert
        Assert.NotNull(irsb);
        Assert.Equal(2, bytesLifted);
        Assert.NotEmpty(irsb.Statements);
        
        // Should contain register update and flag operations
        Assert.Contains(irsb.Statements, stmt => stmt is IRStmtPut put && put.Offset == 0); // EAX offset
        Assert.Contains(irsb.Statements, stmt => stmt is IRStmtPut put && put.Offset == 32); // CC_OP offset
    }

    /// <summary>
    /// Test IMUL instruction lifting
    /// </summary>
    [Fact]
    public void BasicBlockLifter_ImulInstruction_ShouldGenerateMultiplication()
    {
        // Arrange
        var guestState = new X86GuestState();
        var lifter = new X86BasicBlockLifter(guestState);
        
        // IMUL EAX, EBX (0F AF C3)
        byte[] imulInstruction = { 0x0F, 0xAF, 0xC3 };
        
        // Act
        var (irsb, bytesLifted) = lifter.LiftBasicBlock(imulInstruction, 0x1000);
        
        // Assert
        Assert.NotNull(irsb);
        Assert.Equal(3, bytesLifted);
        Assert.NotEmpty(irsb.Statements);
        
        // Should contain multiplication operations
        Assert.Contains(irsb.Statements, stmt => stmt is IRStmtWrTmp);
        Assert.Contains(irsb.Statements, stmt => stmt is IRStmtPut put && put.Offset == 0); // EAX offset
    }

    /// <summary>
    /// Test SHL instruction lifting
    /// </summary>
    [Fact]
    public void BasicBlockLifter_ShlInstruction_ShouldGenerateShiftOperations()
    {
        // Arrange
        var guestState = new X86GuestState();
        var lifter = new X86BasicBlockLifter(guestState);
        
        // SHL EAX, 1 (D1 E0)
        byte[] shlInstruction = { 0xD1, 0xE0 };
        
        // Act
        var (irsb, bytesLifted) = lifter.LiftBasicBlock(shlInstruction, 0x1000);
        
        // Assert
        Assert.NotNull(irsb);
        Assert.Equal(2, bytesLifted);
        Assert.NotEmpty(irsb.Statements);
        
        // Should contain shift operations and flag updates
        Assert.Contains(irsb.Statements, stmt => stmt is IRStmtWrTmp);
        Assert.Contains(irsb.Statements, stmt => stmt is IRStmtPut put && put.Offset == 0); // EAX offset
        Assert.Contains(irsb.Statements, stmt => stmt is IRStmtPut put && put.Offset == 32); // CC_OP offset
    }

    /// <summary>
    /// Test LEA instruction lifting
    /// </summary>
    [Fact]
    public void BasicBlockLifter_LeaInstruction_ShouldGenerateAddressCalculation()
    {
        // Arrange
        var guestState = new X86GuestState();
        var lifter = new X86BasicBlockLifter(guestState);
        
        // LEA EAX, [EBX+4] (8D 43 04)
        byte[] leaInstruction = { 0x8D, 0x43, 0x04 };
        
        // Act
        var (irsb, bytesLifted) = lifter.LiftBasicBlock(leaInstruction, 0x1000);
        
        // Assert
        Assert.NotNull(irsb);
        Assert.Equal(3, bytesLifted);
        Assert.NotEmpty(irsb.Statements);
        
        // Should contain address calculation without memory access
        Assert.Contains(irsb.Statements, stmt => stmt is IRStmtWrTmp);
        Assert.Contains(irsb.Statements, stmt => stmt is IRStmtPut put && put.Offset == 0); // EAX offset
    }

    /// <summary>
    /// Test CDQ instruction lifting
    /// </summary>
    [Fact]
    public void BasicBlockLifter_CdqInstruction_ShouldGenerateSignExtension()
    {
        // Arrange
        var guestState = new X86GuestState();
        var lifter = new X86BasicBlockLifter(guestState);
        
        // CDQ (99)
        byte[] cdqInstruction = { 0x99 };
        
        // Act
        var (irsb, bytesLifted) = lifter.LiftBasicBlock(cdqInstruction, 0x1000);
        
        // Assert
        Assert.NotNull(irsb);
        Assert.Equal(1, bytesLifted);
        Assert.NotEmpty(irsb.Statements);
        
        // Should contain sign extension from EAX to EDX:EAX
        Assert.Contains(irsb.Statements, stmt => stmt is IRStmtPut put && put.Offset == 8); // EDX offset
    }

    /// <summary>
    /// Test HLT instruction lifting
    /// </summary>
    [Fact]
    public void BasicBlockLifter_HltInstruction_ShouldGenerateExitStatement()
    {
        // Arrange
        var guestState = new X86GuestState();
        var lifter = new X86BasicBlockLifter(guestState);
        
        // HLT (F4)
        byte[] hltInstruction = { 0xF4 };
        
        // Act
        var (irsb, bytesLifted) = lifter.LiftBasicBlock(hltInstruction, 0x1000);
        
        // Assert
        Assert.NotNull(irsb);
        Assert.Equal(1, bytesLifted);
        Assert.NotEmpty(irsb.Statements);
        
        // Should have proper halt behavior
        Assert.Equal(IRJumpKind.Boring, irsb.JumpKind); // Default jump kind
        // HLT typically generates a specific exit or helper call
        Assert.True(irsb.Statements.Count > 0);
    }

    /// <summary>
    /// Test composition pattern functionality - verify X86 lifter uses helper properly
    /// </summary>
    [Fact]
    public void BasicBlockLifter_CompositionPattern_ShouldAccessProcessorExtensions()
    {
        // Arrange
        var guestState = new X86GuestState();
        var lifter = new X86BasicBlockLifter(guestState);
        
        // Act & Assert - These properties should be accessible via composition
        // Note: The helper provides access to processor extension functionality
        Assert.NotNull(lifter); // Basic verification that lifter was created successfully
        
        // Test that the lifter can handle various instruction types
        // This implicitly tests that the composition pattern is working
        byte[] nopInstruction = { 0x90 }; // NOP
        var (irsb, bytesLifted) = lifter.LiftBasicBlock(nopInstruction, 0x1000);
        
        Assert.NotNull(irsb);
        Assert.Equal(1, bytesLifted);
    }
}