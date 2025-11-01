using System;
using CSEx.Core;

namespace CSEx.IR
{
    /// <summary>
    /// A binary operation
    /// ppIRExpr output: <op>(<arg1>, <arg2>), eg. Add32(t1, t2)
    /// </summary>
    public sealed class IRExprBinop : IRExpr
    {
        public IROp Op { get; }         // Operation code
        public IRExpr Arg1 { get; }     // Left operand
        public IRExpr Arg2 { get; }     // Right operand
        public override IRExprTag Tag => IRExprTag.Binop;

        public IRExprBinop(IROp op, IRExpr arg1, IRExpr arg2)
        {
            Op = op;
            Arg1 = arg1;
            Arg2 = arg2;
        }

        public override bool Equals(IRExpr? other) =>
            other is IRExprBinop binop && Op == binop.Op && 
            Arg1.Equals(binop.Arg1) && Arg2.Equals(binop.Arg2);

        public override bool Equals(object? obj) =>
            obj is IRExprBinop other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(Op, Arg1, Arg2);

        public override string ToString() => 
            $"{Op.PrettyPrint()}({Arg1.PrettyPrint()}, {Arg2.PrettyPrint()})";

        public override IRExpr DeepCopy() => 
            new IRExprBinop(Op, Arg1.DeepCopy(), Arg2.DeepCopy());

        public override T Accept<T>(IIRExprVisitor<T> visitor) => visitor.VisitBinop(this);
    }

    /// <summary>
    /// A unary operation
    /// ppIRExpr output: <op>(<arg>), eg. Not32(t1)
    /// </summary>
    public sealed class IRExprUnop : IRExpr
    {
        public IROp Op { get; }         // Operation code
        public IRExpr Arg { get; }      // Operand
        public override IRExprTag Tag => IRExprTag.Unop;

        public IRExprUnop(IROp op, IRExpr arg)
        {
            Op = op;
            Arg = arg;
        }

        public override bool Equals(IRExpr? other) =>
            other is IRExprUnop unop && Op == unop.Op && Arg.Equals(unop.Arg);

        public override bool Equals(object? obj) =>
            obj is IRExprUnop other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(Op, Arg);

        public override string ToString() => $"{Op.PrettyPrint()}({Arg.PrettyPrint()})";

        public override IRExpr DeepCopy() => new IRExprUnop(Op, Arg.DeepCopy());

        public override T Accept<T>(IIRExprVisitor<T> visitor) => visitor.VisitUnop(this);
    }

    /// <summary>
    /// Memory endianness for Load/Store operations
    /// </summary>
    public enum IREndness : uint
    {
        LE = 0x1210,  // Little endian
        BE = 0x1211   // Big endian
    }

    /// <summary>
    /// Read a value from memory
    /// ppIRExpr output: LD<end>:<ty>(<addr>), eg. LDle:I32(t1)
    /// </summary>
    public sealed class IRExprLoad : IRExpr
    {
        public IREndness End { get; }   // Endianness of the load
        public IRType Type { get; }     // Type of value being loaded
        public IRExpr Addr { get; }     // Address to load from
        public override IRExprTag Tag => IRExprTag.Load;

        public IRExprLoad(IREndness end, IRType type, IRExpr addr)
        {
            End = end;
            Type = type;
            Addr = addr;
        }

        public override bool Equals(IRExpr? other) =>
            other is IRExprLoad load && End == load.End && Type == load.Type && Addr.Equals(load.Addr);

        public override bool Equals(object? obj) =>
            obj is IRExprLoad other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(End, Type, Addr);

        public override string ToString()
        {
            var endStr = End == IREndness.LE ? "le" : "be";
            return $"LD{endStr}:{Type.PrettyPrint()}({Addr.PrettyPrint()})";
        }

        public override IRExpr DeepCopy() => new IRExprLoad(End, Type, Addr.DeepCopy());

        public override T Accept<T>(IIRExprVisitor<T> visitor) => visitor.VisitLoad(this);
    }

    /// <summary>
    /// A constant value
    /// ppIRExpr output: <con>, eg. 0x4:I32
    /// </summary>
    public sealed class IRExprConst : IRExpr
    {
        public IRConst Con { get; }     // The constant value
        public override IRExprTag Tag => IRExprTag.Const;

        public IRExprConst(IRConst con)
        {
            Con = con;
        }

        public override bool Equals(IRExpr? other) =>
            other is IRExprConst constExpr && Con.Equals(constExpr.Con);

        public override bool Equals(object? obj) =>
            obj is IRExprConst other && Equals(other);

        public override int GetHashCode() => Con.GetHashCode();

        public override string ToString() => Con.ToString();

        public override IRExpr DeepCopy() => new IRExprConst(Con.DeepCopy());

        public override T Accept<T>(IIRExprVisitor<T> visitor) => visitor.VisitConst(this);
    }

    /// <summary>
    /// A conditional expression. Evaluate 'cond', and if non-zero return 'iftrue', else return 'iffalse'.
    /// ppIRExpr output: ITE(<cond>,<iftrue>,<iffalse>), eg. ITE(t6,Add32(t7,1),t8)
    /// </summary>
    public sealed class IRExprITE : IRExpr
    {
        public IRExpr Cond { get; }     // Condition expression
        public IRExpr IfTrue { get; }   // Expression if condition is non-zero
        public IRExpr IfFalse { get; }  // Expression if condition is zero
        public override IRExprTag Tag => IRExprTag.ITE;

        public IRExprITE(IRExpr cond, IRExpr ifTrue, IRExpr ifFalse)
        {
            Cond = cond;
            IfTrue = ifTrue;
            IfFalse = ifFalse;
        }

        public override bool Equals(IRExpr? other) =>
            other is IRExprITE ite && Cond.Equals(ite.Cond) && 
            IfTrue.Equals(ite.IfTrue) && IfFalse.Equals(ite.IfFalse);

        public override bool Equals(object? obj) =>
            obj is IRExprITE other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(Cond, IfTrue, IfFalse);

        public override string ToString() => 
            $"ITE({Cond.PrettyPrint()},{IfTrue.PrettyPrint()},{IfFalse.PrettyPrint()})";

        public override IRExpr DeepCopy() => 
            new IRExprITE(Cond.DeepCopy(), IfTrue.DeepCopy(), IfFalse.DeepCopy());

        public override T Accept<T>(IIRExprVisitor<T> visitor) => visitor.VisitITE(this);
    }

    /// <summary>
    /// Call to a pure (no side effects, idempotent) helper function.
    /// ppIRExpr output: <callee>(<args>):I<retty>, eg. foo(t1,t2):I32
    /// </summary>
    public sealed class IRExprCCall : IRExpr
    {
        public IRCallTarget Callee { get; }  // Function to call
        public IRType RetTy { get; }         // Return type
        public IRExpr[] Args { get; }        // Arguments
        public override IRExprTag Tag => IRExprTag.CCall;

        public IRExprCCall(IRCallTarget callee, IRType retTy, params IRExpr[] args)
        {
            Callee = callee;
            RetTy = retTy;
            Args = args ?? Array.Empty<IRExpr>();
        }

        public override bool Equals(IRExpr? other)
        {
            if (other is not IRExprCCall ccall || !Callee.Equals(ccall.Callee) || RetTy != ccall.RetTy || Args.Length != ccall.Args.Length)
                return false;
            
            for (int i = 0; i < Args.Length; i++)
            {
                if (!Args[i].Equals(ccall.Args[i]))
                    return false;
            }
            return true;
        }

        public override bool Equals(object? obj) =>
            obj is IRExprCCall other && Equals(other);

        public override int GetHashCode()
        {
            var hash = HashCode.Combine(Callee, RetTy, Args.Length);
            foreach (var arg in Args)
            {
                hash = HashCode.Combine(hash, arg);
            }
            return hash;
        }

        public override string ToString()
        {
            var argsStr = string.Join(",", Array.ConvertAll(Args, arg => arg.PrettyPrint()));
            return $"{Callee.PrettyPrint()}({argsStr}):{RetTy.PrettyPrint()}";
        }

        public override IRExpr DeepCopy()
        {
            var newArgs = new IRExpr[Args.Length];
            for (int i = 0; i < Args.Length; i++)
            {
                newArgs[i] = Args[i].DeepCopy();
            }
            return new IRExprCCall(Callee.DeepCopy(), RetTy, newArgs);
        }

        public override T Accept<T>(IIRExprVisitor<T> visitor) => visitor.VisitCCall(this);
    }

    /// <summary>
    /// Call target for CCall expressions
    /// </summary>
    public sealed class IRCallTarget : IEquatable<IRCallTarget>
    {
        public uint RegParms { get; }    // Number of register parameters (0, 1, 2, or 3)
        public string Name { get; }      // Function name
        public IntPtr Addr { get; }      // Function address

        public IRCallTarget(uint regParms, string name, IntPtr addr)
        {
            if (regParms > 3)
                throw new ArgumentOutOfRangeException(nameof(regParms), "regParms must be 0, 1, 2, or 3");
            
            RegParms = regParms;
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Addr = addr;
        }

        public bool Equals(IRCallTarget? other) =>
            other != null && RegParms == other.RegParms && Name == other.Name && Addr == other.Addr;

        public override bool Equals(object? obj) =>
            obj is IRCallTarget other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(RegParms, Name, Addr);

        public override string ToString() => Name;

        public string PrettyPrint() => Name;

        public IRCallTarget DeepCopy() => new(RegParms, Name, Addr);
    }

    /// <summary>
    /// Factory methods for creating IR expressions
    /// </summary>
    public static class IRExprFactory
    {
        // Constant creation helpers
        public static IRExprConst U1(bool value) => new(IRConstFactory.U1(value));
        public static IRExprConst U8(byte value) => new(IRConstFactory.U8(value));
        public static IRExprConst U16(ushort value) => new(IRConstFactory.U16(value));
        public static IRExprConst U32(uint value) => new(IRConstFactory.U32(value));
        public static IRExprConst U64(ulong value) => new(IRConstFactory.U64(value));
        public static IRExprConst F32(float value) => new(IRConstFactory.F32(value));
        public static IRExprConst F32i(uint value) => new(IRConstFactory.F32i(value));
        public static IRExprConst F64(double value) => new(IRConstFactory.F64(value));
        public static IRExprConst F64i(ulong value) => new(IRConstFactory.F64i(value));
        public static IRExprConst V128(ushort value) => new(IRConstFactory.V128(value));
        public static IRExprConst V256(uint value) => new(IRConstFactory.V256(value));

        // Temporary read
        public static IRExprRdTmp RdTmp(IRTemp tmp) => new(tmp);

        // Register operations
        public static IRExprGet Get(int offset, IRType type) => new(offset, type);
        public static IRExprGetI GetI(IRRegArray descr, IRExpr ix, int bias) => new(descr, ix, bias);

        // Memory operations
        public static IRExprLoad Load(IREndness end, IRType type, IRExpr addr) => new(end, type, addr);
        public static IRExprLoad LoadLE(IRType type, IRExpr addr) => Load(IREndness.LE, type, addr);
        public static IRExprLoad LoadBE(IRType type, IRExpr addr) => Load(IREndness.BE, type, addr);

        // Operations
        public static IRExprUnop Unop(IROp op, IRExpr arg) => new(op, arg);
        public static IRExprBinop Binop(IROp op, IRExpr arg1, IRExpr arg2) => new(op, arg1, arg2);
        public static IRExprTriop Triop(IROp op, IRExpr arg1, IRExpr arg2, IRExpr arg3) => new(op, arg1, arg2, arg3);
        public static IRExprQop Qop(IROp op, IRExpr arg1, IRExpr arg2, IRExpr arg3, IRExpr arg4) => new(op, arg1, arg2, arg3, arg4);

        // Control flow
        public static IRExprITE ITE(IRExpr cond, IRExpr ifTrue, IRExpr ifFalse) => new(cond, ifTrue, ifFalse);

        // Function calls
        public static IRExprCCall CCall(IRCallTarget callee, IRType retTy, params IRExpr[] args) => new(callee, retTy, args);
        public static IRCallTarget CallTarget(uint regParms, string name, IntPtr addr) => new(regParms, name, addr);

        // Register array descriptor
        public static IRRegArray RegArray(int baseOffset, IRType elemType, int numElems) => new(baseOffset, elemType, numElems);
    }
}