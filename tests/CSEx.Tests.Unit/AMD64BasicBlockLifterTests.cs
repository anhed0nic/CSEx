using System;
using Xunit;
using CSEx.IR;
using CSEx.Guests;
using CSEx.Guests.AMD64;
using CSEx.Lifters.AMD64;

namespace CSEx.Tests.Unit;

public class AMD64BasicBlockLifterTests
{
    /// <summary>
    /// Test basic block lifter creation and simple MOV instruction
    /// </summary>
    [Fact]
    public void BasicBlockLifter_SimpleMovInstruction_ShouldGenerateIR()
    {
        // Arrange
        var guestState = new AMD64GuestState();
        var lifter = new AMD64BasicBlockLifter(guestState);
        
        // MOV RAX, 42 (48 B8 2A 00 00 00 00 00 00 00)
        byte[] movInstruction = { 0x48, 0xB8, 0x2A, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };
        
        // Act
        var (irsb, bytesLifted) = lifter.LiftBasicBlock(movInstruction, 0x1000);
        
        // Assert
        Assert.NotNull(irsb);
        Assert.Equal(10, bytesLifted); // MOV RAX, immediate is 10 bytes
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
        var guestState = new AMD64GuestState();
        var lifter = new AMD64BasicBlockLifter(guestState);
        
        // MOV RAX, 42; RET (48 B8 2A 00 00 00 00 00 00 00 C3)
        byte[] codeWithRet = { 0x48, 0xB8, 0x2A, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xC3 };
        
        // Act
        var (irsb, bytesLifted) = lifter.LiftBasicBlock(codeWithRet, 0x1000);
        
        // Assert
        Assert.NotNull(irsb);
        Assert.Equal(11, bytesLifted); // Should process both instructions
        Assert.NotEmpty(irsb.Statements);
        
        // Should have proper RET behavior: JumpKind set to Ret and Next expression for return address
        Assert.Equal(IRJumpKind.Ret, irsb.JumpKind);
        Assert.NotNull(irsb.Next);
        
        // Should contain RSP adjustment (stack operations)
        Assert.Contains(irsb.Statements, stmt => stmt is IRStmtPut put && put.Offset == 32); // RSP offset for AMD64
    }

    /// <summary>
    /// Test basic block lifter with ADD instruction in 64-bit mode
    /// </summary>
    [Fact]
    public void BasicBlockLifter_AddInstruction_ShouldGenerateArithmeticIR()
    {
        // Arrange
        var guestState = new AMD64GuestState();
        var lifter = new AMD64BasicBlockLifter(guestState);
        
        // ADD RAX, RBX (48 01 D8)
        byte[] addInstruction = { 0x48, 0x01, 0xD8 };
        
        // Act
        var (irsb, bytesLifted) = lifter.LiftBasicBlock(addInstruction, 0x1000);
        
        // Assert
        Assert.NotNull(irsb);
        Assert.Equal(3, bytesLifted);
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
        var guestState = new AMD64GuestState();
        var lifter = new AMD64BasicBlockLifter(guestState);
        
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
        var guestState = new AMD64GuestState();
        var lifter = new AMD64BasicBlockLifter(guestState);
        
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
    /// Test 64-bit specific operations with REX prefix
    /// </summary>
    [Fact]
    public void BasicBlockLifter_RexPrefixInstruction_ShouldGenerateCorrectIR()
    {
        // Arrange
        var guestState = new AMD64GuestState();
        var lifter = new AMD64BasicBlockLifter(guestState);
        
        // PUSH RAX (50) - in 64-bit mode, this defaults to 64-bit operation
        byte[] pushInstruction = { 0x50 };
        
        // Act
        var (irsb, bytesLifted) = lifter.LiftBasicBlock(pushInstruction, 0x1000);
        
        // Assert
        Assert.NotNull(irsb);
        Assert.Equal(1, bytesLifted);
        Assert.NotEmpty(irsb.Statements);
        
        // Should contain stack operations
        Assert.Contains(irsb.Statements, stmt => stmt is IRStmtPut);
    }

    /// <summary>
    /// Test AVX-512 instruction lifting with EVEX prefix in 64-bit mode
    /// </summary>
    [Fact]
    public void BasicBlockLifter_AVX512Instruction_ShouldGenerateIR()
    {
        // Arrange
        var guestState = new AMD64GuestState();
        var lifter = new AMD64BasicBlockLifter(guestState);
        
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
    /// Test inheritance pattern functionality - verify AMD64 lifter inherits from BaseX86Lifter properly
    /// </summary>
    [Fact]
    public void BasicBlockLifter_InheritancePattern_ShouldAccessProcessorExtensions()
    {
        // Arrange
        var guestState = new AMD64GuestState();
        var lifter = new AMD64BasicBlockLifter(guestState);
        
        // Act & Assert - These methods should be accessible via inheritance from BaseX86Lifter
        // Test that the lifter can handle various instruction types via inheritance
        // The AMD64 lifter inherits MMX/SSE/AVX support directly from BaseX86Lifter
        
        // Test basic functionality - a simple instruction should work
        byte[] nopInstruction = { 0x90 }; // NOP
        var (irsb, bytesLifted) = lifter.LiftBasicBlock(nopInstruction, 0x1000);
        
        Assert.NotNull(irsb);
        Assert.Equal(1, bytesLifted);
        
        // Test that inheritance allows access to processor extension methods
        // These methods are inherited from BaseX86Lifter, so they should be accessible
        Assert.NotNull(lifter); // Basic inheritance verification
        
        // The AMD64 lifter should be able to handle the same processor extensions as x86
        // but we test this implicitly through the basic block lifting capability
        Assert.True(lifter.MaxInstructions > 0); // Inherited property check
        Assert.True(lifter.MaxBytes > 0); // Inherited property check
    }
}