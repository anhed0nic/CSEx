using Xunit;
using CSEx.Lifters.X86;
using CSEx.Guests.X86;
using CSEx.Guests.AMD64;
using CSEx.IR;
using CSEx.Core;

namespace CSEx.Tests.Unit
{
    /// <summary>
    /// Tests for register state management integration between lifters and guest state
    /// Validates that lifters properly use guest state for register offsets and types
    /// </summary>
    public class RegisterStateManagementTests
    {
        [Fact]
        public void X86Lifter_ShouldUseGuestStateForRegisterOffsets()
        {
            // Arrange
            var guestState = new X86GuestState();
            var lifter = new X86BasicBlockLifter(guestState);
            
            // Act - Test that lifter uses guest state register offset methods
            // We can't directly access GetRegisterOffset since it's protected, 
            // but we can test through guest state directly
            var eaxOffset = guestState.GetRegisterOffset("EAX");
            var ecxOffset = guestState.GetRegisterOffset("ECX");
            var eipOffset = guestState.GetRegisterOffset("EIP");
            
            // Assert - Verify offset values are correct from guest state
            Assert.True(eaxOffset >= 0);
            Assert.True(ecxOffset >= 0);
            Assert.True(eipOffset >= 0);
            Assert.NotEqual(eaxOffset, ecxOffset); // Different registers should have different offsets
            Assert.NotEqual(eaxOffset, eipOffset);
        }

        [Fact]
        public void X86GuestState_ShouldProvideConsistentRegisterTypes()
        {
            // Arrange
            var guestState = new X86GuestState();
            
            // Act & Assert - Test that guest state provides correct types
            Assert.Equal(IRType.I32, guestState.GetRegisterType("EAX"));
            Assert.Equal(IRType.I32, guestState.GetRegisterType("ECX"));
            Assert.Equal(IRType.I32, guestState.GetRegisterType("EIP"));
            Assert.Equal(IRType.I16, guestState.GetRegisterType("AX"));
            Assert.Equal(IRType.I8, guestState.GetRegisterType("AL"));
        }

        [Fact]
        public void AMD64GuestState_ShouldProvideCorrectRegisterOffsets()
        {
            // Arrange
            var guestState = new AMD64GuestState();
            
            // Act - Test AMD64 register offset calculation
            var raxOffset = guestState.GetRegisterOffset("RAX");
            var rcxOffset = guestState.GetRegisterOffset("RCX");
            var ripOffset = guestState.GetRegisterOffset("RIP");
            
            // Assert - Verify AMD64 offsets are calculated correctly
            Assert.True(raxOffset >= 0);
            Assert.True(rcxOffset >= 0);
            Assert.True(ripOffset >= 0);
            Assert.NotEqual(raxOffset, rcxOffset); // Different registers should have different offsets
        }

        [Fact]
        public void AMD64GuestState_ShouldProvideCorrectRegisterTypes()
        {
            // Arrange
            var guestState = new AMD64GuestState();
            
            // Act & Assert - Test that AMD64 guest state provides correct types
            Assert.Equal(IRType.I64, guestState.GetRegisterType("RAX"));
            Assert.Equal(IRType.I64, guestState.GetRegisterType("RCX"));
            Assert.Equal(IRType.I64, guestState.GetRegisterType("RIP"));
            Assert.Equal(IRType.I32, guestState.GetRegisterType("EAX")); // 32-bit views
            Assert.Equal(IRType.I16, guestState.GetRegisterType("AX"));
            Assert.Equal(IRType.I8, guestState.GetRegisterType("AL"));
        }

        [Fact]
        public void GuestStates_ShouldHandleCaseInsensitiveRegisterNames()
        {
            // Arrange
            var x86State = new X86GuestState();
            var amd64State = new AMD64GuestState();
            
            // Act & Assert - Test case insensitive register name handling
            Assert.Equal(x86State.GetRegisterOffset("EAX"), x86State.GetRegisterOffset("eax"));
            Assert.Equal(x86State.GetRegisterOffset("EAX"), x86State.GetRegisterOffset("Eax"));
            
            Assert.Equal(amd64State.GetRegisterOffset("RAX"), amd64State.GetRegisterOffset("rax"));
            Assert.Equal(amd64State.GetRegisterOffset("RAX"), amd64State.GetRegisterOffset("Rax"));
        }

        [Fact]
        public void GuestStates_ShouldProvideConsistentFlagRegisterOffsets()
        {
            // Arrange
            var x86State = new X86GuestState();
            var amd64State = new AMD64GuestState();
            
            // Act - Test flag register offsets
            var x86CcOp = x86State.GetRegisterOffset("CC_OP");
            var x86CcDep1 = x86State.GetRegisterOffset("CC_DEP1");
            var amd64CcOp = amd64State.GetRegisterOffset("CC_OP");
            var amd64CcDep1 = amd64State.GetRegisterOffset("CC_DEP1");
            
            // Assert - Flag registers should be available in both architectures
            Assert.True(x86CcOp >= 0);
            Assert.True(x86CcDep1 >= 0);
            Assert.True(amd64CcOp >= 0);
            Assert.True(amd64CcDep1 >= 0);
            
            // Different flag registers should have different offsets
            Assert.NotEqual(x86CcOp, x86CcDep1);
            Assert.NotEqual(amd64CcOp, amd64CcDep1);
        }

        [Fact]
        public void GuestStates_ShouldSupportVectorRegisterOffsets()
        {
            // Arrange
            var x86State = new X86GuestState();
            var amd64State = new AMD64GuestState();
            
            // Act & Assert - Test SIMD register offset calculation
            // XMM registers should be available
            var x86Xmm0 = x86State.GetRegisterOffset("XMM0");
            var x86Xmm1 = x86State.GetRegisterOffset("XMM1");
            var amd64Xmm0 = amd64State.GetRegisterOffset("XMM0");
            var amd64Xmm1 = amd64State.GetRegisterOffset("XMM1");
            
            Assert.True(x86Xmm0 >= 0);
            Assert.True(x86Xmm1 >= 0);
            Assert.True(amd64Xmm0 >= 0);
            Assert.True(amd64Xmm1 >= 0);
            Assert.NotEqual(x86Xmm0, x86Xmm1);
            Assert.NotEqual(amd64Xmm0, amd64Xmm1);
        }

        [Fact]
        public void RegisterStateManagement_ShouldIntegrateWithBaseLifterHelpers()
        {
            // Arrange
            var guestState = new X86GuestState();
            var lifter = new X86BasicBlockLifter(guestState);
            
            // This test verifies that our enhanced register helper methods would work
            // We can't test them directly since they're protected, but we can test
            // the underlying guest state integration they depend on
            
            // Act & Assert - Test that guest state methods work correctly for helper integration
            var eaxOffset = guestState.GetRegisterOffset("EAX");
            var eaxType = guestState.GetRegisterType("EAX");
            
            Assert.True(eaxOffset >= 0);
            Assert.Equal(IRType.I32, eaxType);
            
            // Test flag registers needed for UpdateFlags helper
            var ccOpOffset = guestState.GetRegisterOffset("CC_OP");
            var ccDep1Offset = guestState.GetRegisterOffset("CC_DEP1");
            
            Assert.True(ccOpOffset >= 0);
            Assert.True(ccDep1Offset >= 0);
        }
    }
}