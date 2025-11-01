using Xunit;
using CSEx.Core;
using CSEx.IR;

namespace CSEx.Tests.Unit
{
    public class BasicTypesTests
    {
        [Fact]
        public void IRTypes_SizeOf_ReturnsCorrectSizes()
        {
            Assert.Equal(1, IRType.I8.SizeOf());
            Assert.Equal(2, IRType.I16.SizeOf());
            Assert.Equal(4, IRType.I32.SizeOf());
            Assert.Equal(8, IRType.I64.SizeOf());
            Assert.Equal(16, IRType.I128.SizeOf());
            Assert.Equal(4, IRType.F32.SizeOf());
            Assert.Equal(8, IRType.F64.SizeOf());
            Assert.Equal(16, IRType.V128.SizeOf());
            Assert.Equal(32, IRType.V256.SizeOf());
        }

        [Fact]
        public void IRConst_Creation_WorksCorrectly()
        {
            var c1 = IRConstFactory.U32(0x12345678);
            var c2 = IRConstFactory.F32(3.14f);
            
            Assert.Equal(IRConstTag.U32, c1.Tag);
            Assert.Equal(IRConstTag.F32, c2.Tag);
            
            Assert.IsType<IRConstU32>(c1);
            Assert.IsType<IRConstF32>(c2);
        }

        [Fact]
        public void VexArch_Extensions_WorkCorrectly()
        {
            Assert.True(VexArch.AMD64.Is64Bit());
            Assert.False(VexArch.X86.Is64Bit());
            Assert.Equal(8, VexArch.AMD64.GetWordSize());
            Assert.Equal(4, VexArch.X86.GetWordSize());
        }
    }
}