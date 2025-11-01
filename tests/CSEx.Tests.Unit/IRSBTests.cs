using Xunit;
using CSEx.IR;
using CSEx.Core;
using System;

namespace CSEx.Tests.Unit
{
    public class IRSBTests
    {
        [Fact]
        public void IRTypeEnv_Constructor_InitializesEmpty()
        {
            var typeEnv = new IRTypeEnv();
            
            Assert.NotNull(typeEnv);
            Assert.Equal(0, typeEnv.Count);
        }

        [Fact]
        public void IRTypeEnv_NewTemp_AddsNewTemporary()
        {
            var typeEnv = new IRTypeEnv();
            var newTemp = typeEnv.NewTemp(IRType.I32);
            
            Assert.True(newTemp.IsValid);
            Assert.Equal(1, typeEnv.Count);
            Assert.Equal(IRType.I32, typeEnv.GetType(newTemp));
        }

        [Fact]
        public void IRTypeEnv_NewTemp_AllocatesSequentialTemps()
        {
            var typeEnv = new IRTypeEnv();
            var temp1 = typeEnv.NewTemp(IRType.I32);
            var temp2 = typeEnv.NewTemp(IRType.I64);
            var temp3 = typeEnv.NewTemp(IRType.F32);
            
            Assert.Equal(3, typeEnv.Count);
            Assert.Equal(IRType.I32, typeEnv.GetType(temp1));
            Assert.Equal(IRType.I64, typeEnv.GetType(temp2));
            Assert.Equal(IRType.F32, typeEnv.GetType(temp3));
            
            // Verify sequential allocation
            Assert.Equal(temp1.Value + 1, temp2.Value);
            Assert.Equal(temp2.Value + 1, temp3.Value);
        }

        [Fact]
        public void IRTypeEnv_GetType_ThrowsForInvalidTemp()
        {
            var typeEnv = new IRTypeEnv();
            var invalidTemp = new IRTemp(999);
            
            Assert.Throws<ArgumentException>(() => typeEnv.GetType(invalidTemp));
        }

        [Fact]
        public void IRTypeEnv_DeepCopy_CreatesIndependentCopy()
        {
            var original = new IRTypeEnv();
            var temp1 = original.NewTemp(IRType.I32);
            var temp2 = original.NewTemp(IRType.I64);
            
            var copy = original.DeepCopy();
            
            // Verify copy has same content
            Assert.Equal(original.Count, copy.Count);
            Assert.Equal(original.GetType(temp1), copy.GetType(temp1));
            Assert.Equal(original.GetType(temp2), copy.GetType(temp2));
            
            // Verify independence - changes to original don't affect copy
            var temp3 = original.NewTemp(IRType.F32);
            Assert.Equal(3, original.Count);
            Assert.Equal(2, copy.Count);
            Assert.Throws<ArgumentException>(() => copy.GetType(temp3));
        }

        [Fact]
        public void IRSB_Constructor_InitializesEmpty()
        {
            var irsb = new IRSB();
            
            Assert.NotNull(irsb);
            Assert.NotNull(irsb.TypeEnv);
            Assert.Empty(irsb.Statements);
            Assert.Null(irsb.Next);
            Assert.Equal(IRJumpKind.Boring, irsb.JumpKind);
            Assert.Equal(0, irsb.OffsIP);
        }

        [Fact]
        public void IRSB_Constructor_WithTypeEnv_InitializesCorrectly()
        {
            var typeEnv = new IRTypeEnv();
            var temp = typeEnv.NewTemp(IRType.I32);
            
            var irsb = new IRSB(typeEnv);
            
            Assert.Same(typeEnv, irsb.TypeEnv);
            Assert.Empty(irsb.Statements);
            Assert.Null(irsb.Next);
            Assert.Equal(IRJumpKind.Boring, irsb.JumpKind);
            Assert.Equal(0, irsb.OffsIP);
        }

        [Fact]
        public void IRSB_AddStatement_AppendsToStatements()
        {
            var irsb = new IRSB();
            var typeEnv = irsb.TypeEnv;
            
            var temp1 = typeEnv.NewTemp(IRType.I32);
            var temp2 = typeEnv.NewTemp(IRType.I64);
            var stmt1 = IRStmtFactory.WrTmp(temp1, IRExprFactory.U32(42));
            var stmt2 = IRStmtFactory.WrTmp(temp2, IRExprFactory.U64(100));
            
            irsb.AddStatement(stmt1);
            Assert.Single(irsb.Statements);
            Assert.Same(stmt1, irsb.Statements[0]);
            
            irsb.AddStatement(stmt2);
            Assert.Equal(2, irsb.Statements.Count);
            Assert.Same(stmt1, irsb.Statements[0]);
            Assert.Same(stmt2, irsb.Statements[1]);
        }

        [Fact]
        public void IRSB_SetNext_UpdatesNextAndJumpKind()
        {
            var irsb = new IRSB();
            
            var nextExpr = IRExprFactory.U64(0x2000);
            irsb.Next = nextExpr;
            irsb.JumpKind = IRJumpKind.Call;
            
            Assert.Same(nextExpr, irsb.Next);
            Assert.Equal(IRJumpKind.Call, irsb.JumpKind);
        }

        [Fact]
        public void IRSB_DeepCopy_CreatesIndependentCopy()
        {
            var irsb = new IRSB();
            var typeEnv = irsb.TypeEnv;
            var temp = typeEnv.NewTemp(IRType.I32);
            var stmt = IRStmtFactory.WrTmp(temp, IRExprFactory.U32(42));
            var next = IRExprFactory.U64(0x1000);
            
            irsb.AddStatement(stmt);
            irsb.Next = next;
            irsb.JumpKind = IRJumpKind.Boring;
            irsb.OffsIP = 0x800;
            
            var copy = irsb.DeepCopy();
            
            // Verify structural equality but object independence
            Assert.NotSame(irsb, copy);
            Assert.NotSame(irsb.TypeEnv, copy.TypeEnv);
            Assert.NotSame(irsb.Statements, copy.Statements);
            Assert.NotSame(irsb.Next, copy.Next);
            
            // Verify content equality
            Assert.Equal(irsb.TypeEnv.Count, copy.TypeEnv.Count);
            Assert.Equal(irsb.Statements.Count, copy.Statements.Count);
            Assert.Equal(irsb.JumpKind, copy.JumpKind);
            Assert.Equal(irsb.OffsIP, copy.OffsIP);
            
            // Verify statement deep copy
            Assert.NotSame(irsb.Statements[0], copy.Statements[0]);
            Assert.True(irsb.Statements[0].Equals(copy.Statements[0]));
        }

        [Fact]
        public void IRSB_PrettyPrint_GeneratesReadableOutput()
        {
            var irsb = new IRSB();
            var typeEnv = irsb.TypeEnv;
            var temp = typeEnv.NewTemp(IRType.I32);
            var stmt = IRStmtFactory.WrTmp(temp, IRExprFactory.U32(42));
            var next = IRExprFactory.U64(0x1000);
            
            irsb.AddStatement(stmt);
            irsb.Next = next;
            irsb.JumpKind = IRJumpKind.Boring;
            irsb.OffsIP = 0x800;
            
            var output = irsb.PrettyPrint();
            
            Assert.Contains("IRSB", output);
            Assert.Contains("0x800", output);
            Assert.Contains(temp.ToString(), output);
            Assert.Contains("0x2a:I32", output); // 42 in hex
            Assert.Contains("0x1000:I64", output);
            Assert.Contains("Boring", output);
        }

        [Fact]
        public void IRTypeAnalysis_TypeOfExpr_HandlesBasicExpressions()
        {
            var typeEnv = new IRTypeEnv();
            var temp = typeEnv.NewTemp(IRType.I64);
            
            // Test constant
            var constExpr = IRExprFactory.U32(42);
            Assert.Equal(IRType.I32, IRTypeAnalysis.TypeOfExpr(typeEnv, constExpr));
            
            // Test temporary read
            var tempExpr = IRExprFactory.RdTmp(temp);
            Assert.Equal(IRType.I64, IRTypeAnalysis.TypeOfExpr(typeEnv, tempExpr));
            
            // Test guest register get
            var getExpr = IRExprFactory.Get(0, IRType.I32);
            Assert.Equal(IRType.I32, IRTypeAnalysis.TypeOfExpr(typeEnv, getExpr));
        }

        [Fact]
        public void IRTypeAnalysis_TypeOfExpr_HandlesMemoryLoad()
        {
            var typeEnv = new IRTypeEnv();
            var addrTemp = typeEnv.NewTemp(IRType.I64);
            var addrExpr = IRExprFactory.RdTmp(addrTemp);
            
            var loadExpr = IRExprFactory.Load(IREndness.LE, IRType.I32, addrExpr);
            Assert.Equal(IRType.I32, IRTypeAnalysis.TypeOfExpr(typeEnv, loadExpr));
        }

        [Fact]
        public void IRTypeAnalysis_IsFlatStmt_ValidatesStatementsCorrectly()
        {
            var typeEnv = new IRTypeEnv();
            var temp32 = typeEnv.NewTemp(IRType.I32);
            var temp64 = typeEnv.NewTemp(IRType.I64);
            
            // Valid statement: temp32 := const(42:I32)
            var validStmt = IRStmtFactory.WrTmp(temp32, IRExprFactory.U32(42));
            Assert.True(IRTypeAnalysis.IsFlatStmt(validStmt));
            
            // Valid statement: temp64 := temp32 (with implicit widening via unary op)
            var widenExpr = IRExprFactory.Unop(IROp.Iop_32Uto64, IRExprFactory.RdTmp(temp32));
            var widenStmt = IRStmtFactory.WrTmp(temp64, widenExpr);
            Assert.True(IRTypeAnalysis.IsFlatStmt(widenStmt));
        }

        [Fact]
        public void IRSBSanityCheck_SanityCheck_ValidatesBasicStructure()
        {
            var irsb = new IRSB();
            var typeEnv = irsb.TypeEnv;
            var temp = typeEnv.NewTemp(IRType.I32);
            var stmt = IRStmtFactory.WrTmp(temp, IRExprFactory.U32(42));
            var next = IRExprFactory.U64(0x1000);
            
            irsb.AddStatement(stmt);
            irsb.Next = next;
            irsb.JumpKind = IRJumpKind.Boring;
            irsb.OffsIP = 0x800;
            
            // Should not throw for valid IRSB
            IRSBSanityCheck.SanityCheck(irsb);
        }

        [Fact]
        public void IRSBSanityCheck_SanityCheck_RejectsInvalidNext()
        {
            var irsb = new IRSB();
            var typeEnv = irsb.TypeEnv;
            var temp = typeEnv.NewTemp(IRType.I32);
            var stmt = IRStmtFactory.WrTmp(temp, IRExprFactory.U32(42));
            
            irsb.AddStatement(stmt);
            // Leave Next as null which should be invalid
            irsb.JumpKind = IRJumpKind.Boring;
            
            Assert.Throws<Exception>(() => IRSBSanityCheck.SanityCheck(irsb));
        }
    }
}