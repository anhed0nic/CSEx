using System;
using Xunit;
using CSEx.IR;
using CSEx.Guests;
using CSEx.Guests.X86;
using CSEx.Guests.AMD64;
using CSEx.Lifters.X86;
using CSEx.Lifters.AMD64;

namespace CSEx.Tests.Unit;

public class IRSBIntegrationTests
{
    /// <summary>
    /// Test that X86 lifter IRSB state management works correctly for composition pattern
    /// </summary>
    [Fact]
    public void X86Lifter_IRSBIntegration_ShouldMaintainStateCorrectly()
    {
        // Arrange
        var guestState = new X86GuestState();
        var lifter = new X86BasicBlockLifter(guestState);
        
        // Use a simple instruction that we know works
        byte[] movInstruction = { 0xB8, 0x2A, 0x00, 0x00, 0x00 }; // MOV EAX, 42
        
        // Act
        var (irsb, bytesLifted) = lifter.LiftBasicBlock(movInstruction, 0x1000);
        
        // Assert - Verify IRSB state is properly managed
        Assert.NotNull(irsb);
        Assert.Equal(5, bytesLifted);
        Assert.NotEmpty(irsb.Statements);
        
        // Verify the IRSB has expected structure
        Assert.NotNull(irsb.TypeEnv);
        Assert.Equal(IRJumpKind.Boring, irsb.JumpKind);
        Assert.NotNull(irsb.Next);
        
        // Should contain instruction marker and register update
        Assert.Contains(irsb.Statements, stmt => stmt is IRStmtIMark);
        Assert.Contains(irsb.Statements, stmt => stmt is IRStmtPut);
    }

    /// <summary>
    /// Test that AMD64 lifter IRSB state management works correctly for inheritance pattern
    /// </summary>
    [Fact]
    public void AMD64Lifter_IRSBIntegration_ShouldMaintainStateCorrectly()
    {
        // Arrange
        var guestState = new AMD64GuestState();
        var lifter = new AMD64BasicBlockLifter(guestState);
        
        // Use a simple instruction that we know works
        byte[] movInstruction = { 0x48, 0xB8, 0x2A, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 }; // MOV RAX, 42
        
        // Act
        var (irsb, bytesLifted) = lifter.LiftBasicBlock(movInstruction, 0x1000);
        
        // Assert - Verify IRSB state is properly managed
        Assert.NotNull(irsb);
        Assert.Equal(10, bytesLifted);
        Assert.NotEmpty(irsb.Statements);
        
        // Verify the IRSB has expected structure
        Assert.NotNull(irsb.TypeEnv);
        Assert.Equal(IRJumpKind.Boring, irsb.JumpKind);
        Assert.NotNull(irsb.Next);
        
        // Should contain instruction marker and register update
        Assert.Contains(irsb.Statements, stmt => stmt is IRStmtIMark);
        Assert.Contains(irsb.Statements, stmt => stmt is IRStmtPut);
    }

    /// <summary>
    /// Test that multiple instructions in a basic block maintain IRSB state correctly
    /// </summary>
    [Fact]
    public void BasicBlockLifter_MultipleInstructions_ShouldMaintainConsistentIRSBState()
    {
        // Arrange
        var guestState = new X86GuestState();
        var lifter = new X86BasicBlockLifter(guestState);
        
        // Multiple MOV instructions that we know work
        byte[] multiMovCode = { 
            0xB8, 0x01, 0x00, 0x00, 0x00, // MOV EAX, 1
            0xBB, 0x02, 0x00, 0x00, 0x00, // MOV EBX, 2
            0xB9, 0x03, 0x00, 0x00, 0x00  // MOV ECX, 3
        };
        
        // Act
        var (irsb, bytesLifted) = lifter.LiftBasicBlock(multiMovCode, 0x1000);
        
        // Assert
        Assert.NotNull(irsb);
        Assert.Equal(15, bytesLifted); // 5 bytes per MOV instruction
        Assert.NotEmpty(irsb.Statements);
        
        // Should have one IMark statement per instruction
        var imarkCount = 0;
        foreach (var stmt in irsb.Statements)
        {
            if (stmt is IRStmtIMark) imarkCount++;
        }
        Assert.Equal(3, imarkCount); // One IMark per MOV instruction
        
        // IRSB should have consistent state
        Assert.NotNull(irsb.TypeEnv);
        Assert.NotNull(irsb.Next);
    }

    /// <summary>
    /// Test that IRSB integration works for both architectures with the same interface
    /// </summary>
    [Fact]
    public void BothLifters_SameInterface_ShouldProduceValidIRSB()
    {
        // Arrange - Test that both architectures produce valid IRSBs
        var x86GuestState = new X86GuestState();
        var amd64GuestState = new AMD64GuestState();
        var x86Lifter = new X86BasicBlockLifter(x86GuestState);
        var amd64Lifter = new AMD64BasicBlockLifter(amd64GuestState);
        
        // Use MOV instructions that work on both architectures
        byte[] x86MovInstruction = { 0xB8, 0x2A, 0x00, 0x00, 0x00 }; // MOV EAX, 42
        byte[] amd64MovInstruction = { 0x48, 0xB8, 0x2A, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 }; // MOV RAX, 42
        
        // Act
        var (x86Irsb, x86Bytes) = x86Lifter.LiftBasicBlock(x86MovInstruction, 0x1000);
        var (amd64Irsb, amd64Bytes) = amd64Lifter.LiftBasicBlock(amd64MovInstruction, 0x1000);
        
        // Assert - Both should produce valid IRSBs with same structure
        Assert.NotNull(x86Irsb);
        Assert.NotNull(amd64Irsb);
        Assert.Equal(5, x86Bytes);
        Assert.Equal(10, amd64Bytes);
        
        // Both should have similar IRSB structure
        Assert.Equal(x86Irsb.JumpKind, amd64Irsb.JumpKind);
        Assert.NotNull(x86Irsb.Next);
        Assert.NotNull(amd64Irsb.Next);
        Assert.NotEmpty(x86Irsb.Statements);
        Assert.NotEmpty(amd64Irsb.Statements);
    }

    /// <summary>
    /// Test IRSB state persistence across processor extension calls (composition pattern)
    /// </summary>
    [Fact]
    public void X86Lifter_ProcessorExtensionAccess_ShouldMaintainIRSBState()
    {
        // Arrange
        var guestState = new X86GuestState();
        var lifter = new X86BasicBlockLifter(guestState);
        
        // Use a MOV instruction that we know works
        byte[] instruction = { 0xB8, 0x2A, 0x00, 0x00, 0x00 }; // MOV EAX, 42
        
        // Act
        var (irsb, bytesLifted) = lifter.LiftBasicBlock(instruction, 0x1000);
        
        // Assert - IRSB should be properly constructed and accessible
        Assert.NotNull(irsb);
        Assert.Equal(5, bytesLifted);
        
        // Verify IRSB state integrity
        Assert.NotNull(irsb.TypeEnv);
        Assert.True(irsb.Statements.Count >= 1); // At least the IMark statement
        
        // Check that the IRSB can be validated (indicates proper state management)
        var exception = Record.Exception(() => IRSBSanityCheck.SanityCheck(irsb, "Test"));
        Assert.Null(exception);
    }
}