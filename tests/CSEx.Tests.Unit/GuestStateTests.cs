using Xunit;
using CSEx.Guests;
using CSEx.Guests.X86;
using CSEx.Guests.AMD64;
using CSEx.Guests.ARM;
using CSEx.Core;
using CSEx.IR;
using System;

namespace CSEx.Tests.Unit
{
    public class GuestStateTests
    {
        [Fact]
        public void X86GuestState_Constructor_InitializesCorrectly()
        {
            var state = new X86GuestState();
            
            Assert.Equal(IRType.I32, state.GuestWordSize);
            Assert.Equal(IRType.I32, state.GuestIPType);
            Assert.Equal(0u, state.GuestEAX);
            Assert.Equal(0u, state.GuestEIP);
        }
        
        [Fact]
        public void X86GuestState_Initialize_SetsDefaults()
        {
            var state = new X86GuestState();
            state.GuestEAX = 0x12345678;
            
            state.Initialize();
            
            Assert.Equal(0u, state.GuestEAX);
            Assert.Equal(0u, state.GuestECX);
            Assert.Equal(0u, state.GuestEIP);
            Assert.Equal(1u, state.GuestDFlag); // Forward direction
            Assert.Equal((uint)CCOp.Copy, state.GuestCCOp);
        }
        
        [Fact]
        public void X86GuestState_GetRegisterOffset_ReturnsCorrectOffsets()
        {
            var state = new X86GuestState();
            
            var eaxOffset = state.GetRegisterOffset("EAX");
            var ecxOffset = state.GetRegisterOffset("ECX");
            var eipOffset = state.GetRegisterOffset("EIP");
            
            Assert.True(eaxOffset >= 0);
            Assert.True(ecxOffset > eaxOffset);
            Assert.True(eipOffset != eaxOffset);
            
            // Test case insensitivity
            Assert.Equal(eaxOffset, state.GetRegisterOffset("eax"));
        }
        
        [Fact]
        public void X86GuestState_GetRegisterType_ReturnsCorrectTypes()
        {
            var state = new X86GuestState();
            
            Assert.Equal(IRType.I32, state.GetRegisterType("EAX"));
            Assert.Equal(IRType.I32, state.GetRegisterType("EIP"));
            Assert.Equal(IRType.I16, state.GetRegisterType("CS"));
            Assert.Equal(IRType.I64, state.GetRegisterType("FPREG0"));
            Assert.Equal(IRType.I8, state.GetRegisterType("FPTAG0"));
            Assert.Equal(IRType.V128, state.GetRegisterType("XMM0"));
        }
        
        [Fact]
        public void X86GuestState_DeepCopy_CreatesIndependentCopy()
        {
            var original = new X86GuestState();
            original.Initialize();
            original.GuestEAX = 0x12345678;
            original.GuestEIP = 0x08000000;
            original.GuestFPReg[0] = 0x123456789ABCDEF0;
            
            var copy = (X86GuestState)original.DeepCopy();
            
            Assert.Equal(original.GuestEAX, copy.GuestEAX);
            Assert.Equal(original.GuestEIP, copy.GuestEIP);
            Assert.Equal(original.GuestFPReg[0], copy.GuestFPReg[0]);
            
            // Verify independence
            copy.GuestEAX = 0x87654321;
            Assert.NotEqual(original.GuestEAX, copy.GuestEAX);
        }
        
        [Fact]
        public void AMD64GuestState_Constructor_InitializesCorrectly()
        {
            var state = new AMD64GuestState();
            
            Assert.Equal(IRType.I64, state.GuestWordSize);
            Assert.Equal(IRType.I64, state.GuestIPType);
            Assert.Equal(0ul, state.GuestRAX);
            Assert.Equal(0ul, state.GuestRIP);
        }
        
        [Fact]
        public void AMD64GuestState_GetRegisterOffset_ReturnsCorrectOffsets()
        {
            var state = new AMD64GuestState();
            
            var raxOffset = state.GetRegisterOffset("RAX");
            var rcxOffset = state.GetRegisterOffset("RCX");
            var r8Offset = state.GetRegisterOffset("R8");
            var ripOffset = state.GetRegisterOffset("RIP");
            
            Assert.True(raxOffset >= 0);
            Assert.True(rcxOffset > raxOffset);
            Assert.True(r8Offset > rcxOffset);
            Assert.True(ripOffset != raxOffset);
        }
        
        [Fact]
        public void AMD64GuestState_GetRegisterType_ReturnsCorrectTypes()
        {
            var state = new AMD64GuestState();
            
            Assert.Equal(IRType.I64, state.GetRegisterType("RAX"));
            Assert.Equal(IRType.I64, state.GetRegisterType("R15"));
            Assert.Equal(IRType.I64, state.GetRegisterType("RIP"));
            Assert.Equal(IRType.I64, state.GetRegisterType("CR0"));
            Assert.Equal(IRType.V256, state.GetRegisterType("YMM0"));
            Assert.Equal(IRType.V128, state.GetRegisterType("XMM15"));
        }
        
        [Fact]
        public void ARMGuestState_Constructor_InitializesCorrectly()
        {
            var state = new ARMGuestState();
            
            Assert.Equal(IRType.I32, state.GuestWordSize);
            Assert.Equal(IRType.I32, state.GuestIPType);
            Assert.Equal(0u, state.GuestR0);
            Assert.Equal(0u, state.GuestR15T);
        }
        
        [Fact]
        public void ARMGuestState_PCHelpers_WorkCorrectly()
        {
            var state = new ARMGuestState();
            
            // Test ARM mode
            state.SetPC(0x08000000, false);
            Assert.Equal(0x08000000u, state.GetPC());
            Assert.False(state.IsThumbMode());
            Assert.Equal(0x08000000u, state.GuestR15T);
            
            // Test Thumb mode
            state.SetPC(0x08000100, true);
            Assert.Equal(0x08000100u, state.GetPC());
            Assert.True(state.IsThumbMode());
            Assert.Equal(0x08000101u, state.GuestR15T);
        }
        
        [Fact]
        public void ARMGuestState_GetRegisterOffset_ReturnsCorrectOffsets()
        {
            var state = new ARMGuestState();
            
            var r0Offset = state.GetRegisterOffset("R0");
            var r1Offset = state.GetRegisterOffset("R1");
            var spOffset = state.GetRegisterOffset("SP");
            var r13Offset = state.GetRegisterOffset("R13");
            
            Assert.True(r0Offset >= 0);
            Assert.True(r1Offset > r0Offset);
            Assert.Equal(spOffset, r13Offset); // SP should alias R13
        }
        
        [Fact]
        public void ARMGuestState_VFPRegisters_AccessCorrectly()
        {
            var state = new ARMGuestState();
            
            // Test D register access
            var d0Offset = state.GetRegisterOffset("D0");
            var d1Offset = state.GetRegisterOffset("D1");
            Assert.Equal(IRType.I64, state.GetRegisterType("D0"));
            Assert.Equal(d0Offset + 8, d1Offset);
            
            // Test S register access (should map to halves of D registers)
            var s0Offset = state.GetRegisterOffset("S0");
            var s1Offset = state.GetRegisterOffset("S1");
            var s2Offset = state.GetRegisterOffset("S2");
            
            Assert.Equal(IRType.F32, state.GetRegisterType("S0"));
            Assert.Equal(d0Offset, s0Offset);      // S0 = lower half of D0
            Assert.Equal(d0Offset + 4, s1Offset);  // S1 = upper half of D0
            Assert.Equal(d1Offset, s2Offset);      // S2 = lower half of D1
        }
        
        [Fact]
        public void GuestState_RequiresPreciseMemoryExceptions_WorksCorrectly()
        {
            var x86State = new X86GuestState();
            var amd64State = new AMD64GuestState();
            var armState = new ARMGuestState();
            
            // Test that stack pointer regions require precise exceptions
            var x86EspOffset = x86State.GetRegisterOffset("ESP");
            var amd64RspOffset = amd64State.GetRegisterOffset("RSP");
            var armSpOffset = armState.GetRegisterOffset("SP");
            
            Assert.True(x86State.RequiresPreciseMemoryExceptions(x86EspOffset, x86EspOffset + 3));
            Assert.True(amd64State.RequiresPreciseMemoryExceptions(amd64RspOffset, amd64RspOffset + 7));
            Assert.True(armState.RequiresPreciseMemoryExceptions(armSpOffset, armSpOffset + 3));
        }
        
        [Fact]
        public void GuestState_RegisterNameValidation_ThrowsForInvalidNames()
        {
            var state = new X86GuestState();
            
            Assert.Throws<ArgumentException>(() => state.GetRegisterOffset("INVALID"));
            Assert.Throws<ArgumentException>(() => state.GetRegisterType("NONEXISTENT"));
            Assert.Throws<ArgumentException>(() => state.GetRegisterOffset("FPREG99")); // Out of range
        }
    }
}