using System;
using CSEx.Core;

namespace CSEx.IR
{
    /// <summary>
    /// The different kinds of statements (equivalent to VEX IRStmtTag)
    /// Statements represent operations with side effects.
    /// </summary>
    public enum IRStmtTag : uint
    {
        NoOp = 0x1E00,      // No operation (can be optimized away)
        IMark,              // Instruction marker (META)
        AbiHint,            // ABI hint (META)
        Put,                // Write guest register at fixed offset
        PutI,               // Write guest register at non-fixed offset
        WrTmp,              // Write temporary value
        Store,              // Store to memory
        LoadG,              // Guarded load from memory
        StoreG,             // Guarded store to memory
        CAS,                // Compare-and-swap atomic operation
        LLSC,               // Load-linked/Store-conditional
        Dirty,              // Call function with side effects
        MBE,                // Memory bus event (fence, etc.)
        Exit                // Conditional exit from basic block
    }

    /// <summary>
    /// Base class for all IR statements (equivalent to VEX IRStmt)
    /// Statements represent operations with side effects.
    /// </summary>
    public abstract class IRStmt : IEquatable<IRStmt>
    {
        public abstract IRStmtTag Tag { get; }

        public abstract bool Equals(IRStmt? other);
        public abstract override bool Equals(object? obj);
        public abstract override int GetHashCode();
        public abstract override string ToString();

        /// <summary>
        /// Deep copy the statement
        /// </summary>
        public abstract IRStmt DeepCopy();

        /// <summary>
        /// Pretty print the statement  
        /// </summary>
        public virtual string PrettyPrint() => ToString();

        /// <summary>
        /// Accept visitor for traversal
        /// </summary>
        public abstract T Accept<T>(IIRStmtVisitor<T> visitor);
    }

    /// <summary>
    /// Visitor interface for IR statements
    /// </summary>
    public interface IIRStmtVisitor<T>
    {
        T VisitNoOp(IRStmtNoOp stmt);
        T VisitIMark(IRStmtIMark stmt);
        T VisitAbiHint(IRStmtAbiHint stmt);
        T VisitPut(IRStmtPut stmt);
        T VisitPutI(IRStmtPutI stmt);
        T VisitWrTmp(IRStmtWrTmp stmt);
        T VisitStore(IRStmtStore stmt);
        T VisitLoadG(IRStmtLoadG stmt);
        T VisitStoreG(IRStmtStoreG stmt);
        T VisitCAS(IRStmtCAS stmt);
        T VisitLLSC(IRStmtLLSC stmt);
        T VisitDirty(IRStmtDirty stmt);
        T VisitMBE(IRStmtMBE stmt);
        T VisitExit(IRStmtExit stmt);
    }

    /// <summary>
    /// A no-op statement (usually resulting from IR optimization)
    /// ppIRStmt output: IR-NoOp
    /// </summary>
    public sealed class IRStmtNoOp : IRStmt
    {
        public override IRStmtTag Tag => IRStmtTag.NoOp;

        public IRStmtNoOp()
        {
        }

        public override bool Equals(IRStmt? other) =>
            other is IRStmtNoOp;

        public override bool Equals(object? obj) =>
            obj is IRStmtNoOp;

        public override int GetHashCode() => Tag.GetHashCode();

        public override string ToString() => "IR-NoOp";

        public override IRStmt DeepCopy() => new IRStmtNoOp();

        public override T Accept<T>(IIRStmtVisitor<T> visitor) => visitor.VisitNoOp(this);
    }

    /// <summary>
    /// Instruction marker - marks the start of statements representing a single machine instruction
    /// ppIRStmt output: ------ IMark(<addr>, <len>, <delta>) ------, eg. ------ IMark(0x4000792, 5, 0) ------
    /// </summary>
    public sealed class IRStmtIMark : IRStmt
    {
        public ulong Addr { get; }      // Instruction address
        public uint Len { get; }        // Instruction length
        public byte Delta { get; }      // addr = program counter as encoded in guest state - delta
        public override IRStmtTag Tag => IRStmtTag.IMark;

        public IRStmtIMark(ulong addr, uint len, byte delta)
        {
            Addr = addr;
            Len = len;
            Delta = delta;
        }

        public override bool Equals(IRStmt? other) =>
            other is IRStmtIMark imark && Addr == imark.Addr && Len == imark.Len && Delta == imark.Delta;

        public override bool Equals(object? obj) =>
            obj is IRStmtIMark other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(Addr, Len, Delta);

        public override string ToString() => $"------ IMark(0x{Addr:X}, {Len}, {Delta}) ------";

        public override IRStmt DeepCopy() => new IRStmtIMark(Addr, Len, Delta);

        public override T Accept<T>(IIRStmtVisitor<T> visitor) => visitor.VisitIMark(this);
    }

    /// <summary>
    /// ABI hint - indicates that a chunk of address space has become undefined
    /// ppIRStmt output: ====== AbiHint(<base>, <len>, <nia>) ======, eg. ====== AbiHint(t1, 16, t2) ======
    /// </summary>
    public sealed class IRStmtAbiHint : IRStmt
    {
        public IRExpr Base { get; }     // Start of undefined chunk
        public int Len { get; }         // Length of undefined chunk
        public IRExpr Nia { get; }      // Address of next (guest) instruction
        public override IRStmtTag Tag => IRStmtTag.AbiHint;

        public IRStmtAbiHint(IRExpr baseExpr, int len, IRExpr nia)
        {
            Base = baseExpr ?? throw new ArgumentNullException(nameof(baseExpr));
            Len = len;
            Nia = nia ?? throw new ArgumentNullException(nameof(nia));
        }

        public override bool Equals(IRStmt? other) =>
            other is IRStmtAbiHint hint && Base.Equals(hint.Base) && Len == hint.Len && Nia.Equals(hint.Nia);

        public override bool Equals(object? obj) =>
            obj is IRStmtAbiHint other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(Base, Len, Nia);

        public override string ToString() => $"====== AbiHint({Base.PrettyPrint()}, {Len}, {Nia.PrettyPrint()}) ======";

        public override IRStmt DeepCopy() => new IRStmtAbiHint(Base.DeepCopy(), Len, Nia.DeepCopy());

        public override T Accept<T>(IIRStmtVisitor<T> visitor) => visitor.VisitAbiHint(this);
    }

    /// <summary>
    /// Write a guest register at a fixed offset in the guest state
    /// ppIRStmt output: PUT(<offset>) = <data>, eg. PUT(60) = t1
    /// </summary>
    public sealed class IRStmtPut : IRStmt
    {
        public int Offset { get; }      // Offset into the guest state
        public IRExpr Data { get; }     // The value to write
        public override IRStmtTag Tag => IRStmtTag.Put;

        public IRStmtPut(int offset, IRExpr data)
        {
            Offset = offset;
            Data = data ?? throw new ArgumentNullException(nameof(data));
        }

        public override bool Equals(IRStmt? other) =>
            other is IRStmtPut put && Offset == put.Offset && Data.Equals(put.Data);

        public override bool Equals(object? obj) =>
            obj is IRStmtPut other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(Offset, Data);

        public override string ToString() => $"PUT({Offset}) = {Data.PrettyPrint()}";

        public override IRStmt DeepCopy() => new IRStmtPut(Offset, Data.DeepCopy());

        public override T Accept<T>(IIRStmtVisitor<T> visitor) => visitor.VisitPut(this);
    }

    /// <summary>
    /// Write temporary value assignment - each tmp is only assigned to once (SSA form)
    /// ppIRStmt output: t<tmp> = <data>, eg. t1 = 3
    /// </summary>
    public sealed class IRStmtWrTmp : IRStmt
    {
        public IRTemp Tmp { get; }      // Temporary (LHS of assignment)
        public IRExpr Data { get; }     // Expression (RHS of assignment)
        public override IRStmtTag Tag => IRStmtTag.WrTmp;

        public IRStmtWrTmp(IRTemp tmp, IRExpr data)
        {
            Tmp = tmp;
            Data = data ?? throw new ArgumentNullException(nameof(data));
        }

        public override bool Equals(IRStmt? other) =>
            other is IRStmtWrTmp wrTmp && Tmp.Equals(wrTmp.Tmp) && Data.Equals(wrTmp.Data);

        public override bool Equals(object? obj) =>
            obj is IRStmtWrTmp other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(Tmp, Data);

        public override string ToString() => $"{Tmp} = {Data.PrettyPrint()}";

        public override IRStmt DeepCopy() => new IRStmtWrTmp(Tmp, Data.DeepCopy());

        public override T Accept<T>(IIRStmtVisitor<T> visitor) => visitor.VisitWrTmp(this);
    }

    /// <summary>
    /// Write a value to memory (normal store, not store-conditional)
    /// ppIRStmt output: ST<end>(<addr>) = <data>, eg. STle(t1) = t2
    /// </summary>
    public sealed class IRStmtStore : IRStmt
    {
        public IREndness End { get; }   // Endianness of the store
        public IRExpr Addr { get; }     // Store address
        public IRExpr Data { get; }     // Value to write
        public override IRStmtTag Tag => IRStmtTag.Store;

        public IRStmtStore(IREndness end, IRExpr addr, IRExpr data)
        {
            End = end;
            Addr = addr ?? throw new ArgumentNullException(nameof(addr));
            Data = data ?? throw new ArgumentNullException(nameof(data));
        }

        public override bool Equals(IRStmt? other) =>
            other is IRStmtStore store && End == store.End && Addr.Equals(store.Addr) && Data.Equals(store.Data);

        public override bool Equals(object? obj) =>
            obj is IRStmtStore other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(End, Addr, Data);

        public override string ToString()
        {
            var endStr = End == IREndness.LE ? "le" : "be";
            return $"ST{endStr}({Addr.PrettyPrint()}) = {Data.PrettyPrint()}";
        }

        public override IRStmt DeepCopy() => new IRStmtStore(End, Addr.DeepCopy(), Data.DeepCopy());

        public override T Accept<T>(IIRStmtVisitor<T> visitor) => visitor.VisitStore(this);
    }
}