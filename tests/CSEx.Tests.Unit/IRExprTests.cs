using System;
using Xunit;
using CSEx.IR;
using CSEx.Core;

namespace CSEx.Tests.Unit
{
    public class IRExprTests
    {
        [Fact]
        public void IRExprConst_U32_CreatesCorrectConstant()
        {
            var expr = IRExprFactory.U32(42);
            
            Assert.Equal(IRExprTag.Const, expr.Tag);
            Assert.IsType<IRConstU32>(expr.Con);
            Assert.Equal(42u, ((IRConstU32)expr.Con).Value);
        }

        [Fact]
        public void IRExprRdTmp_CreatesCorrectTempRead()
        {
            var tmp = new IRTemp(123);
            var expr = IRExprFactory.RdTmp(tmp);
            
            Assert.Equal(IRExprTag.RdTmp, expr.Tag);
            Assert.Equal(tmp, expr.Tmp);
            Assert.Equal("t123", expr.ToString());
        }

        [Fact]
        public void IRExprGet_CreatesCorrectRegisterRead()
        {
            var expr = IRExprFactory.Get(16, IRType.I32);
            
            Assert.Equal(IRExprTag.Get, expr.Tag);
            Assert.Equal(16, expr.Offset);
            Assert.Equal(IRType.I32, expr.Type);
            Assert.Equal("GET:I32(16)", expr.ToString());
        }

        [Fact]
        public void IRExprLoad_CreatesCorrectMemoryLoad()
        {
            var addr = IRExprFactory.RdTmp(new IRTemp(1));
            var expr = IRExprFactory.LoadLE(IRType.I64, addr);
            
            Assert.Equal(IRExprTag.Load, expr.Tag);
            Assert.Equal(IREndness.LE, expr.End);
            Assert.Equal(IRType.I64, expr.Type);
            Assert.Equal(addr, expr.Addr);
            Assert.Equal("LDle:I64(t1)", expr.ToString());
        }

        [Fact]
        public void IRExprBinop_CreatesCorrectBinaryOperation()
        {
            var arg1 = IRExprFactory.RdTmp(new IRTemp(1));
            var arg2 = IRExprFactory.U32(42);
            var expr = IRExprFactory.Binop(IROp.Add32, arg1, arg2);
            
            Assert.Equal(IRExprTag.Binop, expr.Tag);
            Assert.Equal(IROp.Add32, expr.Op);
            Assert.Equal(arg1, expr.Arg1);
            Assert.Equal(arg2, expr.Arg2);
            Assert.Equal("Add32(t1, 0x0000002A:I32)", expr.ToString());
        }

        [Fact]
        public void IRExprUnop_CreatesCorrectUnaryOperation()
        {
            var arg = IRExprFactory.RdTmp(new IRTemp(1));
            var expr = IRExprFactory.Unop(IROp.Not32, arg);
            
            Assert.Equal(IRExprTag.Unop, expr.Tag);
            Assert.Equal(IROp.Not32, expr.Op);
            Assert.Equal(arg, expr.Arg);
            Assert.Equal("Not32(t1)", expr.ToString());
        }

        [Fact]
        public void IRExprITE_CreatesCorrectConditional()
        {
            var cond = IRExprFactory.RdTmp(new IRTemp(1));
            var ifTrue = IRExprFactory.U32(1);
            var ifFalse = IRExprFactory.U32(0);
            var expr = IRExprFactory.ITE(cond, ifTrue, ifFalse);
            
            Assert.Equal(IRExprTag.ITE, expr.Tag);
            Assert.Equal(cond, expr.Cond);
            Assert.Equal(ifTrue, expr.IfTrue);
            Assert.Equal(ifFalse, expr.IfFalse);
            Assert.Equal("ITE(t1,0x00000001:I32,0x00000000:I32)", expr.ToString());
        }

        [Fact]
        public void IRExprTriop_CreatesCorrectTernaryOperation()
        {
            var arg1 = IRExprFactory.U32(1);
            var arg2 = IRExprFactory.F64(2.0);
            var arg3 = IRExprFactory.F64(3.0);
            var expr = IRExprFactory.Triop(IROp.AddF64, arg1, arg2, arg3);
            
            Assert.Equal(IRExprTag.Triop, expr.Tag);
            Assert.Equal(IROp.AddF64, expr.Op);
            Assert.Equal(arg1, expr.Arg1);
            Assert.Equal(arg2, expr.Arg2);
            Assert.Equal(arg3, expr.Arg3);
        }

        [Fact]
        public void IRExprCCall_CreatesCorrectFunctionCall()
        {
            var target = IRExprFactory.CallTarget(2, "helper_function", IntPtr.Zero);
            var arg1 = IRExprFactory.RdTmp(new IRTemp(1));
            var arg2 = IRExprFactory.U32(42);
            var expr = IRExprFactory.CCall(target, IRType.I32, arg1, arg2);
            
            Assert.Equal(IRExprTag.CCall, expr.Tag);
            Assert.Equal(target, expr.Callee);
            Assert.Equal(IRType.I32, expr.RetTy);
            Assert.Equal(2, expr.Args.Length);
            Assert.Equal(arg1, expr.Args[0]);
            Assert.Equal(arg2, expr.Args[1]);
        }

        [Fact]
        public void IRRegArray_CreatesCorrectDescriptor()
        {
            var descr = IRExprFactory.RegArray(128, IRType.I8, 16);
            
            Assert.Equal(128, descr.Base);
            Assert.Equal(IRType.I8, descr.ElemType);
            Assert.Equal(16, descr.NumElems);
            Assert.Equal("(128:16xI8)", descr.ToString());
        }

        [Fact]
        public void IRExprGetI_CreatesCorrectIndexedRegisterRead()
        {
            var descr = IRExprFactory.RegArray(128, IRType.I8, 16);
            var ix = IRExprFactory.RdTmp(new IRTemp(1));
            var expr = IRExprFactory.GetI(descr, ix, 0);
            
            Assert.Equal(IRExprTag.GetI, expr.Tag);
            Assert.Equal(descr, expr.Descr);
            Assert.Equal(ix, expr.Ix);
            Assert.Equal(0, expr.Bias);
            Assert.Equal("GETI(128:16xI8)[t1,0]", expr.ToString());
        }

        [Fact]
        public void IRExpr_DeepCopy_CreatesIndependentCopy()
        {
            var original = IRExprFactory.Binop(IROp.Add32, 
                IRExprFactory.RdTmp(new IRTemp(1)), 
                IRExprFactory.U32(42));
            
            var copy = original.DeepCopy();
            
            Assert.NotSame(original, copy);
            Assert.Equal(original, copy);
            Assert.NotSame(original.Arg1, ((IRExprBinop)copy).Arg1);
            Assert.NotSame(original.Arg2, ((IRExprBinop)copy).Arg2);
        }

        [Fact]
        public void IRExpr_Equality_WorksCorrectly()
        {
            var expr1 = IRExprFactory.Binop(IROp.Add32, 
                IRExprFactory.RdTmp(new IRTemp(1)), 
                IRExprFactory.U32(42));
            
            var expr2 = IRExprFactory.Binop(IROp.Add32, 
                IRExprFactory.RdTmp(new IRTemp(1)), 
                IRExprFactory.U32(42));
            
            var expr3 = IRExprFactory.Binop(IROp.Sub32, 
                IRExprFactory.RdTmp(new IRTemp(1)), 
                IRExprFactory.U32(42));
            
            Assert.Equal(expr1, expr2);
            Assert.NotEqual(expr1, expr3);
            Assert.True(expr1.Equals(expr2));
            Assert.False(expr1.Equals(expr3));
        }
    }
}