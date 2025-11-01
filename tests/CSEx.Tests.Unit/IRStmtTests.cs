using CSEx.IR;
using Xunit;
using System;

namespace CSEx.Tests.Unit;

public class IRStmtTests
{
    [Fact]
    public void NoOp_Creation_ShouldSucceed()
    {
        var noop = IRStmtFactory.NoOp();
        
        Assert.NotNull(noop);
        Assert.IsType<IRStmtNoOp>(noop);
    }

    [Fact]
    public void NoOp_Visitor_ShouldCallCorrectMethod()
    {
        var noop = IRStmtFactory.NoOp();
        var visitor = new TestStmtVisitor();
        
        noop.Accept(visitor);
        
        Assert.Equal("NoOp", visitor.LastVisited);
    }

    [Fact]
    public void IMark_Creation_ShouldSucceed()
    {
        var addr = 0x1000UL;
        var len = 4U;
        byte delta = 8;
        
        var imark = IRStmtFactory.IMark(addr, len, delta);
        
        Assert.NotNull(imark);
        Assert.IsType<IRStmtIMark>(imark);
        var imarkStmt = (IRStmtIMark)imark;
        Assert.Equal(addr, imarkStmt.Addr);
        Assert.Equal(len, imarkStmt.Len);
        Assert.Equal(delta, imarkStmt.Delta);
    }

    [Fact]
    public void AbiHint_Creation_ShouldSucceed()
    {
        var baseExpr = IRExprFactory.U64(0x1000);
        var len = 8;  // int instead of IRExpr
        var nia = IRExprFactory.U64(0x1008);
        
        var abiHint = IRStmtFactory.AbiHint(baseExpr, len, nia);
        
        Assert.NotNull(abiHint);
        Assert.IsType<IRStmtAbiHint>(abiHint);
        var abiHintStmt = (IRStmtAbiHint)abiHint;
        Assert.Equal(baseExpr, abiHintStmt.Base);
        Assert.Equal(len, abiHintStmt.Len);
        Assert.Equal(nia, abiHintStmt.Nia);
    }

    [Fact]
    public void Put_Creation_ShouldSucceed()
    {
        var offset = 16;
        var data = IRExprFactory.U64(42);
        
        var put = IRStmtFactory.Put(offset, data);
        
        Assert.NotNull(put);
        Assert.IsType<IRStmtPut>(put);
        var putStmt = (IRStmtPut)put;
        Assert.Equal(offset, putStmt.Offset);
        Assert.Equal(data, putStmt.Data);
    }

    [Fact]
    public void WrTmp_Creation_ShouldSucceed()
    {
        var tmp = new IRTemp(1);
        var data = IRExprFactory.U64(42);
        
        var wrTmp = IRStmtFactory.WrTmp(tmp, data);
        
        Assert.NotNull(wrTmp);
        Assert.IsType<IRStmtWrTmp>(wrTmp);
        var wrTmpStmt = (IRStmtWrTmp)wrTmp;
        Assert.Equal(tmp, wrTmpStmt.Tmp);
        Assert.Equal(data, wrTmpStmt.Data);
    }

    [Fact]
    public void Store_Creation_ShouldSucceed()
    {
        var addr = IRExprFactory.U64(0x1000);
        var data = IRExprFactory.U32(42);
        
        var store = IRStmtFactory.Store(IREndness.LE, addr, data);
        
        Assert.NotNull(store);
        Assert.IsType<IRStmtStore>(store);
        var storeStmt = (IRStmtStore)store;
        Assert.Equal(IREndness.LE, storeStmt.End);
        Assert.Equal(addr, storeStmt.Addr);
        Assert.Equal(data, storeStmt.Data);
    }

    [Fact]
    public void LoadLinked_Creation_ShouldSucceed()
    {
        var result = new IRTemp(1);
        var addr = IRExprFactory.U64(0x1000);
        
        var llsc = IRStmtFactory.LoadLinked(IREndness.LE, result, addr);
        
        Assert.NotNull(llsc);
        Assert.IsType<IRStmtLLSC>(llsc);
        var llscStmt = (IRStmtLLSC)llsc;
        Assert.Equal(IREndness.LE, llscStmt.End);
        Assert.Equal(result, llscStmt.Result);
        Assert.Equal(addr, llscStmt.Addr);
        Assert.Null(llscStmt.StoreData);
    }

    [Fact]
    public void MBE_Creation_ShouldSucceed()
    {
        var mbe = IRStmtFactory.MBE(IRMBusEvent.Fence);
        
        Assert.NotNull(mbe);
        Assert.IsType<IRStmtMBE>(mbe);
        var mbeStmt = (IRStmtMBE)mbe;
        Assert.Equal(IRMBusEvent.Fence, mbeStmt.Event);
    }

    [Fact]
    public void Exit_Creation_ShouldSucceed()
    {
        var guard = IRExprFactory.U1(true);
        var dst = IRConstFactory.U64(0x1000);  // IRConst instead of IRExpr
        
        var exit = IRStmtFactory.Exit(guard, IRJumpKind.Boring, dst, 16);
        
        Assert.NotNull(exit);
        Assert.IsType<IRStmtExit>(exit);
        var exitStmt = (IRStmtExit)exit;
        Assert.Equal(guard, exitStmt.Guard);
        Assert.Equal(IRJumpKind.Boring, exitStmt.Jk);
        Assert.Equal(dst, exitStmt.Dst);
        Assert.Equal(16, exitStmt.OffsIP);
    }

    [Fact]
    public void IRStmt_DeepCopy_ShouldCreateDistinctInstances()
    {
        var original = IRStmtFactory.WrTmp(new IRTemp(1), IRExprFactory.U32(42));
        var copy = original.DeepCopy();
        
        Assert.NotSame(original, copy);
        Assert.Equal(original, copy);
        Assert.True(original.Equals(copy));
    }

    [Fact]
    public void IRStmt_Equality_ShouldWorkCorrectly()
    {
        var stmt1 = IRStmtFactory.WrTmp(new IRTemp(1), IRExprFactory.U32(42));
        var stmt2 = IRStmtFactory.WrTmp(new IRTemp(1), IRExprFactory.U32(42));
        var stmt3 = IRStmtFactory.WrTmp(new IRTemp(2), IRExprFactory.U32(42));
        
        Assert.Equal(stmt1, stmt2);
        Assert.NotEqual(stmt1, stmt3);
        Assert.True(stmt1.Equals(stmt2));
        Assert.False(stmt1.Equals(stmt3));
    }

    [Fact]
    public void IRStmt_ToString_ShouldReturnValidRepresentation()
    {
        var stmt = IRStmtFactory.WrTmp(new IRTemp(1), IRExprFactory.U32(42));
        var str = stmt.ToString();
        
        Assert.NotNull(str);
        Assert.NotEmpty(str);
        Assert.Contains("t1", str);  // Check for temp name instead of "WrTmp"
    }

    [Fact]
    public void IRStmt_HashCode_ShouldBeConsistent()
    {
        var stmt1 = IRStmtFactory.WrTmp(new IRTemp(1), IRExprFactory.U32(42));
        var stmt2 = IRStmtFactory.WrTmp(new IRTemp(1), IRExprFactory.U32(42));
        
        Assert.Equal(stmt1.GetHashCode(), stmt2.GetHashCode());
    }

    // Helper visitor class for testing
    private class TestStmtVisitor : IIRStmtVisitor<string>
    {
        public string LastVisited { get; private set; } = "";

        public string VisitNoOp(IRStmtNoOp stmt) => LastVisited = "NoOp";
        public string VisitIMark(IRStmtIMark stmt) => LastVisited = "IMark";
        public string VisitAbiHint(IRStmtAbiHint stmt) => LastVisited = "AbiHint";
        public string VisitPut(IRStmtPut stmt) => LastVisited = "Put";
        public string VisitPutI(IRStmtPutI stmt) => LastVisited = "PutI";
        public string VisitWrTmp(IRStmtWrTmp stmt) => LastVisited = "WrTmp";
        public string VisitStore(IRStmtStore stmt) => LastVisited = "Store";
        public string VisitLoadG(IRStmtLoadG stmt) => LastVisited = "LoadG";
        public string VisitStoreG(IRStmtStoreG stmt) => LastVisited = "StoreG";
        public string VisitCAS(IRStmtCAS stmt) => LastVisited = "CAS";
        public string VisitLLSC(IRStmtLLSC stmt) => LastVisited = "LLSC";
        public string VisitDirty(IRStmtDirty stmt) => LastVisited = "Dirty";
        public string VisitMBE(IRStmtMBE stmt) => LastVisited = "MBE";
        public string VisitExit(IRStmtExit stmt) => LastVisited = "Exit";
    }
}