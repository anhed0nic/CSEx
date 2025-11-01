using CSEx.IR;
using Xunit;
using System;

namespace CSEx.Tests.Unit;

public class IRStmtSupportingTypesTests
{
    [Fact]
    public void IRJumpKind_AllValuesAreDistinct()
    {
        var values = Enum.GetValues<IRJumpKind>();
        Assert.True(values.Length > 0);
        
        // Check that each value is unique
        for (int i = 0; i < values.Length; i++)
        {
            for (int j = i + 1; j < values.Length; j++)
            {
                Assert.NotEqual(values[i], values[j]);
            }
        }
    }

    [Fact]
    public void IRMBusEvent_AllValuesAreDistinct()
    {
        var values = Enum.GetValues<IRMBusEvent>();
        Assert.True(values.Length > 0);
        
        // Check that each value is unique
        for (int i = 0; i < values.Length; i++)
        {
            for (int j = i + 1; j < values.Length; j++)
            {
                Assert.NotEqual(values[i], values[j]);
            }
        }
    }

    [Fact]
    public void IRLoadGOp_AllValuesAreDistinct()
    {
        var values = Enum.GetValues<IRLoadGOp>();
        Assert.True(values.Length > 0);
        
        // Check that each value is unique
        for (int i = 0; i < values.Length; i++)
        {
            for (int j = i + 1; j < values.Length; j++)
            {
                Assert.NotEqual(values[i], values[j]);
            }
        }
    }

    [Fact]
    public void IRRegArray_Creation_ShouldSucceed()
    {
        var baseOffset = 16;
        var elemType = IRType.I32;
        var numElems = 8;
        
        var regArray = new IRRegArray(baseOffset, elemType, numElems);
        
        Assert.Equal(baseOffset, regArray.Base);
        Assert.Equal(elemType, regArray.ElemType);
        Assert.Equal(numElems, regArray.NumElems);
    }

    [Fact]
    public void IRRegArray_Equality_ShouldWorkCorrectly()
    {
        var regArray1 = new IRRegArray(16, IRType.I32, 8);
        var regArray2 = new IRRegArray(16, IRType.I32, 8);
        var regArray3 = new IRRegArray(16, IRType.I64, 8);
        
        Assert.Equal(regArray1, regArray2);
        Assert.NotEqual(regArray1, regArray3);
        Assert.True(regArray1.Equals(regArray2));
        Assert.False(regArray1.Equals(regArray3));
    }

    [Fact]
    public void IRRegArray_DeepCopy_ShouldCreateDistinctInstance()
    {
        var original = new IRRegArray(16, IRType.I32, 8);
        var copy = original.DeepCopy();
        
        Assert.NotSame(original, copy);
        Assert.Equal(original, copy);
    }

    [Fact]
    public void IRCallTarget_Creation_ShouldSucceed()
    {
        var regParms = 2u;
        var name = "test_func";
        var addr = new IntPtr(0x1000);
        
        var target = new IRCallTarget(regParms, name, addr);
        
        Assert.Equal(regParms, target.RegParms);
        Assert.Equal(name, target.Name);
        Assert.Equal(addr, target.Addr);
    }

    [Fact]
    public void IRCallTarget_Equality_ShouldWorkCorrectly()
    {
        var target1 = new IRCallTarget(2u, "test_func", new IntPtr(0x1000));
        var target2 = new IRCallTarget(2u, "test_func", new IntPtr(0x1000));
        var target3 = new IRCallTarget(3u, "test_func", new IntPtr(0x1000));
        
        Assert.Equal(target1, target2);
        Assert.NotEqual(target1, target3);
        Assert.True(target1.Equals(target2));
        Assert.False(target1.Equals(target3));
    }

    [Fact]
    public void IRCallTarget_DeepCopy_ShouldCreateDistinctInstance()
    {
        var original = new IRCallTarget(2u, "test_func", new IntPtr(0x1000));
        var copy = original.DeepCopy();
        
        Assert.NotSame(original, copy);
        Assert.Equal(original, copy);
    }
}