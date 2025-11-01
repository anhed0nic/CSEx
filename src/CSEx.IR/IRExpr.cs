using System;
using CSEx.Core;

namespace CSEx.IR
{
    /// <summary>
    /// The different kinds of expressions (equivalent to VEX IRExprTag)
    /// </summary>
    public enum IRExprTag : uint
    {
        Binder = 0x1900,  // Used only in pattern matching within Vex
        Get,              // Read guest register at fixed offset
        GetI,             // Read guest register at non-fixed offset (circular indexing)
        RdTmp,            // Read temporary value
        Qop,              // Quaternary operation
        Triop,            // Ternary operation
        Binop,            // Binary operation
        Unop,             // Unary operation
        Load,             // Load from memory
        Const,            // Constant value
        ITE,              // If-then-else
        CCall,            // Call to pure helper function
        VECRET,           // Vector return (special case)
        GSPTR             // Guest state pointer (special case)
    }

    /// <summary>
    /// Base class for all IR expressions (equivalent to VEX IRExpr)
    /// Expressions represent operations without side effects.
    /// </summary>
    public abstract class IRExpr : IEquatable<IRExpr>
    {
        public abstract IRExprTag Tag { get; }

        public abstract bool Equals(IRExpr? other);
        public abstract override bool Equals(object? obj);
        public abstract override int GetHashCode();
        public abstract override string ToString();

        /// <summary>
        /// Deep copy the expression
        /// </summary>
        public abstract IRExpr DeepCopy();

        /// <summary>
        /// Pretty print the expression  
        /// </summary>
        public virtual string PrettyPrint() => ToString();

        /// <summary>
        /// Accept visitor for traversal
        /// </summary>
        public abstract T Accept<T>(IIRExprVisitor<T> visitor);
    }

    /// <summary>
    /// Visitor interface for IR expressions
    /// </summary>
    public interface IIRExprVisitor<T>
    {
        T VisitBinder(IRExprBinder expr);
        T VisitGet(IRExprGet expr);
        T VisitGetI(IRExprGetI expr);
        T VisitRdTmp(IRExprRdTmp expr);
        T VisitQop(IRExprQop expr);
        T VisitTriop(IRExprTriop expr);
        T VisitBinop(IRExprBinop expr);
        T VisitUnop(IRExprUnop expr);
        T VisitLoad(IRExprLoad expr);
        T VisitConst(IRExprConst expr);
        T VisitITE(IRExprITE expr);
        T VisitCCall(IRExprCCall expr);
        T VisitVECRET(IRExprVECRET expr);
        T VisitGSPTR(IRExprGSPTR expr);
    }

    /// <summary>
    /// Pattern matching binder (used only within Vex, should not be seen outside)
    /// </summary>
    public sealed class IRExprBinder : IRExpr
    {
        public int Binder { get; }
        public override IRExprTag Tag => IRExprTag.Binder;

        public IRExprBinder(int binder)
        {
            Binder = binder;
        }

        public override bool Equals(IRExpr? other) =>
            other is IRExprBinder binder && Binder == binder.Binder;

        public override bool Equals(object? obj) =>
            obj is IRExprBinder other && Equals(other);

        public override int GetHashCode() => Binder.GetHashCode();

        public override string ToString() => $"BINDER({Binder})";

        public override IRExpr DeepCopy() => new IRExprBinder(Binder);

        public override T Accept<T>(IIRExprVisitor<T> visitor) => visitor.VisitBinder(this);
    }

    /// <summary>
    /// Read a guest register at a fixed offset in the guest state
    /// ppIRExpr output: GET:<ty>(<offset>), eg. GET:I32(0)
    /// </summary>
    public sealed class IRExprGet : IRExpr
    {
        public int Offset { get; }      // Offset into the guest state
        public IRType Type { get; }     // Type of the value being read
        public override IRExprTag Tag => IRExprTag.Get;

        public IRExprGet(int offset, IRType type)
        {
            Offset = offset;
            Type = type;
        }

        public override bool Equals(IRExpr? other) =>
            other is IRExprGet get && Offset == get.Offset && Type == get.Type;

        public override bool Equals(object? obj) =>
            obj is IRExprGet other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(Offset, Type);

        public override string ToString() => $"GET:{Type.PrettyPrint()}({Offset})";

        public override IRExpr DeepCopy() => new IRExprGet(Offset, Type);

        public override T Accept<T>(IIRExprVisitor<T> visitor) => visitor.VisitGet(this);
    }

    /// <summary>
    /// Guest register array descriptor for circular indexing
    /// </summary>
    public sealed class IRRegArray : IEquatable<IRRegArray>
    {
        public int Base { get; }        // Guest state offset of start of indexed area
        public IRType ElemType { get; } // Type of each element in the indexed area
        public int NumElems { get; }    // Number of elements in the indexed area

        public IRRegArray(int baseOffset, IRType elemType, int numElems)
        {
            Base = baseOffset;
            ElemType = elemType;
            NumElems = numElems;
        }

        public bool Equals(IRRegArray? other) =>
            other != null && Base == other.Base && ElemType == other.ElemType && NumElems == other.NumElems;

        public override bool Equals(object? obj) =>
            obj is IRRegArray other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(Base, ElemType, NumElems);

        public override string ToString() => $"({Base}:{NumElems}x{ElemType.PrettyPrint()})";

        public IRRegArray DeepCopy() => new(Base, ElemType, NumElems);
    }

    /// <summary>
    /// Read a guest register at a non-fixed offset (circular indexing)
    /// ppIRExpr output: GETI<descr>[<ix>,<bias>], eg. GETI(128:8xI8)[t1,0]
    /// </summary>
    public sealed class IRExprGetI : IRExpr
    {
        public IRRegArray Descr { get; }  // Part of guest state treated as circular
        public IRExpr Ix { get; }         // Variable part of index into array
        public int Bias { get; }          // Constant offset part of index
        public override IRExprTag Tag => IRExprTag.GetI;

        public IRExprGetI(IRRegArray descr, IRExpr ix, int bias)
        {
            Descr = descr;
            Ix = ix;
            Bias = bias;
        }

        public override bool Equals(IRExpr? other) =>
            other is IRExprGetI getI && Descr.Equals(getI.Descr) && 
            Ix.Equals(getI.Ix) && Bias == getI.Bias;

        public override bool Equals(object? obj) =>
            obj is IRExprGetI other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(Descr, Ix, Bias);

        public override string ToString() => $"GETI{Descr}[{Ix.PrettyPrint()},{Bias}]";

        public override IRExpr DeepCopy() => new IRExprGetI(Descr.DeepCopy(), Ix.DeepCopy(), Bias);

        public override T Accept<T>(IIRExprVisitor<T> visitor) => visitor.VisitGetI(this);
    }

    /// <summary>
    /// The value held by a temporary
    /// ppIRExpr output: t<tmp>, eg. t1
    /// </summary>
    public sealed class IRExprRdTmp : IRExpr
    {
        public IRTemp Tmp { get; }       // The temporary number
        public override IRExprTag Tag => IRExprTag.RdTmp;

        public IRExprRdTmp(IRTemp tmp)
        {
            Tmp = tmp;
        }

        public override bool Equals(IRExpr? other) =>
            other is IRExprRdTmp rdTmp && Tmp.Equals(rdTmp.Tmp);

        public override bool Equals(object? obj) =>
            obj is IRExprRdTmp other && Equals(other);

        public override int GetHashCode() => Tmp.GetHashCode();

        public override string ToString() => Tmp.ToString();

        public override IRExpr DeepCopy() => new IRExprRdTmp(Tmp);

        public override T Accept<T>(IIRExprVisitor<T> visitor) => visitor.VisitRdTmp(this);
    }

    /// <summary>
    /// A quaternary operation
    /// ppIRExpr output: <op>(<arg1>, <arg2>, <arg3>, <arg4>), eg. MAddF64r32(t1, t2, t3, t4)
    /// </summary>
    public sealed class IRExprQop : IRExpr
    {
        public IROp Op { get; }         // Operation code
        public IRExpr Arg1 { get; }     // Operand 1
        public IRExpr Arg2 { get; }     // Operand 2  
        public IRExpr Arg3 { get; }     // Operand 3
        public IRExpr Arg4 { get; }     // Operand 4
        public override IRExprTag Tag => IRExprTag.Qop;

        public IRExprQop(IROp op, IRExpr arg1, IRExpr arg2, IRExpr arg3, IRExpr arg4)
        {
            Op = op;
            Arg1 = arg1;
            Arg2 = arg2;
            Arg3 = arg3;
            Arg4 = arg4;
        }

        public override bool Equals(IRExpr? other) =>
            other is IRExprQop qop && Op == qop.Op && 
            Arg1.Equals(qop.Arg1) && Arg2.Equals(qop.Arg2) && 
            Arg3.Equals(qop.Arg3) && Arg4.Equals(qop.Arg4);

        public override bool Equals(object? obj) =>
            obj is IRExprQop other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(Op, Arg1, Arg2, Arg3, Arg4);

        public override string ToString() => 
            $"{Op.PrettyPrint()}({Arg1.PrettyPrint()}, {Arg2.PrettyPrint()}, {Arg3.PrettyPrint()}, {Arg4.PrettyPrint()})";

        public override IRExpr DeepCopy() => 
            new IRExprQop(Op, Arg1.DeepCopy(), Arg2.DeepCopy(), Arg3.DeepCopy(), Arg4.DeepCopy());

        public override T Accept<T>(IIRExprVisitor<T> visitor) => visitor.VisitQop(this);
    }

    /// <summary>
    /// A ternary operation
    /// ppIRExpr output: <op>(<arg1>, <arg2>, <arg3>), eg. MulF64(1, 2.0, 3.0)
    /// </summary>
    public sealed class IRExprTriop : IRExpr
    {
        public IROp Op { get; }         // Operation code
        public IRExpr Arg1 { get; }     // Operand 1
        public IRExpr Arg2 { get; }     // Operand 2
        public IRExpr Arg3 { get; }     // Operand 3
        public override IRExprTag Tag => IRExprTag.Triop;

        public IRExprTriop(IROp op, IRExpr arg1, IRExpr arg2, IRExpr arg3)
        {
            Op = op;
            Arg1 = arg1;
            Arg2 = arg2;
            Arg3 = arg3;
        }

        public override bool Equals(IRExpr? other) =>
            other is IRExprTriop triop && Op == triop.Op && 
            Arg1.Equals(triop.Arg1) && Arg2.Equals(triop.Arg2) && Arg3.Equals(triop.Arg3);

        public override bool Equals(object? obj) =>
            obj is IRExprTriop other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(Op, Arg1, Arg2, Arg3);

        public override string ToString() => 
            $"{Op.PrettyPrint()}({Arg1.PrettyPrint()}, {Arg2.PrettyPrint()}, {Arg3.PrettyPrint()})";

        public override IRExpr DeepCopy() => 
            new IRExprTriop(Op, Arg1.DeepCopy(), Arg2.DeepCopy(), Arg3.DeepCopy());

        public override T Accept<T>(IIRExprVisitor<T> visitor) => visitor.VisitTriop(this);
    }

    /// <summary>
    /// Vector return placeholder - used in helper call arg lists to indicate 
    /// where the returned vector value should be placed.
    /// ppIRExpr output: VECRET
    /// </summary>
    public sealed class IRExprVECRET : IRExpr
    {
        public override IRExprTag Tag => IRExprTag.VECRET;

        public override bool Equals(IRExpr? other) => other is IRExprVECRET;

        public override bool Equals(object? obj) => obj is IRExprVECRET;

        public override int GetHashCode() => typeof(IRExprVECRET).GetHashCode();

        public override string ToString() => "VECRET";

        public override IRExpr DeepCopy() => new IRExprVECRET();

        public override T Accept<T>(IIRExprVisitor<T> visitor) => visitor.VisitVECRET(this);
    }

    /// <summary>
    /// Guest state pointer - used in helper call arg lists to pass a pointer
    /// to the guest state to the helper function.
    /// ppIRExpr output: GSPTR
    /// </summary>
    public sealed class IRExprGSPTR : IRExpr
    {
        public override IRExprTag Tag => IRExprTag.GSPTR;

        public override bool Equals(IRExpr? other) => other is IRExprGSPTR;

        public override bool Equals(object? obj) => obj is IRExprGSPTR;

        public override int GetHashCode() => typeof(IRExprGSPTR).GetHashCode();

        public override string ToString() => "GSPTR";

        public override IRExpr DeepCopy() => new IRExprGSPTR();

        public override T Accept<T>(IIRExprVisitor<T> visitor) => visitor.VisitGSPTR(this);
    }
}