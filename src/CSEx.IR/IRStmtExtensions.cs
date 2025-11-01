using System;
using CSEx.Core;

namespace CSEx.IR
{
    /// <summary>
    /// Write a guest register at a non-fixed offset
    /// ppIRStmt output: PUTI<descr>[<ix>,<bias>] = <data>, eg. PUTI(64:8xF64)[t5,0] = t1
    /// </summary>
    public sealed class IRStmtPutI : IRStmt
    {
        public IRPutI Details { get; }
        public override IRStmtTag Tag => IRStmtTag.PutI;

        public IRStmtPutI(IRPutI details)
        {
            Details = details ?? throw new ArgumentNullException(nameof(details));
        }

        public override bool Equals(IRStmt? other) =>
            other is IRStmtPutI putI && Details.Equals(putI.Details);

        public override bool Equals(object? obj) =>
            obj is IRStmtPutI other && Equals(other);

        public override int GetHashCode() => Details.GetHashCode();

        public override string ToString() => Details.ToString();

        public override IRStmt DeepCopy() => new IRStmtPutI(Details.DeepCopy());

        public override T Accept<T>(IIRStmtVisitor<T> visitor) => visitor.VisitPutI(this);
    }

    /// <summary>
    /// Guarded load from memory
    /// ppIRStmt output: t<tmp> = if (<guard>) <cvt>(LD<end>(<addr>)) else <alt>
    /// </summary>
    public sealed class IRStmtLoadG : IRStmt
    {
        public IRLoadG Details { get; }
        public override IRStmtTag Tag => IRStmtTag.LoadG;

        public IRStmtLoadG(IRLoadG details)
        {
            Details = details ?? throw new ArgumentNullException(nameof(details));
        }

        public override bool Equals(IRStmt? other) =>
            other is IRStmtLoadG loadG && Details.Equals(loadG.Details);

        public override bool Equals(object? obj) =>
            obj is IRStmtLoadG other && Equals(other);

        public override int GetHashCode() => Details.GetHashCode();

        public override string ToString() => Details.ToString();

        public override IRStmt DeepCopy() => new IRStmtLoadG(Details.DeepCopy());

        public override T Accept<T>(IIRStmtVisitor<T> visitor) => visitor.VisitLoadG(this);
    }

    /// <summary>
    /// Guarded store to memory
    /// ppIRStmt output: if (<guard>) ST<end>(<addr>) = <data>
    /// </summary>
    public sealed class IRStmtStoreG : IRStmt
    {
        public IRStoreG Details { get; }
        public override IRStmtTag Tag => IRStmtTag.StoreG;

        public IRStmtStoreG(IRStoreG details)
        {
            Details = details ?? throw new ArgumentNullException(nameof(details));
        }

        public override bool Equals(IRStmt? other) =>
            other is IRStmtStoreG storeG && Details.Equals(storeG.Details);

        public override bool Equals(object? obj) =>
            obj is IRStmtStoreG other && Equals(other);

        public override int GetHashCode() => Details.GetHashCode();

        public override string ToString() => Details.ToString();

        public override IRStmt DeepCopy() => new IRStmtStoreG(Details.DeepCopy());

        public override T Accept<T>(IIRStmtVisitor<T> visitor) => visitor.VisitStoreG(this);
    }

    /// <summary>
    /// Atomic compare-and-swap operation
    /// ppIRStmt output: t<tmp> = CAS<end>(<addr> :: <expected> -> <new>)
    /// </summary>
    public sealed class IRStmtCAS : IRStmt
    {
        public IRCAS Details { get; }
        public override IRStmtTag Tag => IRStmtTag.CAS;

        public IRStmtCAS(IRCAS details)
        {
            Details = details ?? throw new ArgumentNullException(nameof(details));
        }

        public override bool Equals(IRStmt? other) =>
            other is IRStmtCAS cas && Details.Equals(cas.Details);

        public override bool Equals(object? obj) =>
            obj is IRStmtCAS other && Equals(other);

        public override int GetHashCode() => Details.GetHashCode();

        public override string ToString() => Details.ToString();

        public override IRStmt DeepCopy() => new IRStmtCAS(Details.DeepCopy());

        public override T Accept<T>(IIRStmtVisitor<T> visitor) => visitor.VisitCAS(this);
    }

    /// <summary>
    /// Load-Linked or Store-Conditional operation
    /// </summary>
    public sealed class IRStmtLLSC : IRStmt
    {
        public IREndness End { get; }       // Endianness
        public IRTemp Result { get; }       // Result temporary
        public IRExpr Addr { get; }         // Address
        public IRExpr? StoreData { get; }   // NULL => LL, non-NULL => SC
        public override IRStmtTag Tag => IRStmtTag.LLSC;

        public IRStmtLLSC(IREndness end, IRTemp result, IRExpr addr, IRExpr? storeData)
        {
            End = end;
            Result = result;
            Addr = addr ?? throw new ArgumentNullException(nameof(addr));
            StoreData = storeData;
        }

        public bool IsLoadLinked => StoreData == null;
        public bool IsStoreConditional => StoreData != null;

        public override bool Equals(IRStmt? other) =>
            other is IRStmtLLSC llsc && End == llsc.End && Result.Equals(llsc.Result) && 
            Addr.Equals(llsc.Addr) && 
            ((StoreData == null && llsc.StoreData == null) || (StoreData?.Equals(llsc.StoreData) == true));

        public override bool Equals(object? obj) =>
            obj is IRStmtLLSC other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(End, Result, Addr, StoreData);

        public override string ToString()
        {
            var endStr = End == IREndness.LE ? "le" : "be";
            if (IsLoadLinked)
            {
                return $"{Result} = LD{endStr}-Linked({Addr.PrettyPrint()})";
            }
            else
            {
                return $"{Result} = ( ST{endStr}-Cond({Addr.PrettyPrint()}) = {StoreData!.PrettyPrint()} )";
            }
        }

        public override IRStmt DeepCopy() => new IRStmtLLSC(End, Result, Addr.DeepCopy(), StoreData?.DeepCopy());

        public override T Accept<T>(IIRStmtVisitor<T> visitor) => visitor.VisitLLSC(this);
    }

    /// <summary>
    /// Call a function with side effects (dirty call)
    /// ppIRStmt output: t<tmp> = DIRTY <guard> <effects> ::: <callee>(<args>)
    /// </summary>
    public sealed class IRStmtDirty : IRStmt
    {
        public IRDirty Details { get; }
        public override IRStmtTag Tag => IRStmtTag.Dirty;

        public IRStmtDirty(IRDirty details)
        {
            Details = details ?? throw new ArgumentNullException(nameof(details));
        }

        public override bool Equals(IRStmt? other) =>
            other is IRStmtDirty dirty && Details.Equals(dirty.Details);

        public override bool Equals(object? obj) =>
            obj is IRStmtDirty other && Equals(other);

        public override int GetHashCode() => Details.GetHashCode();

        public override string ToString() => Details.ToString();

        public override IRStmt DeepCopy() => new IRStmtDirty(Details.DeepCopy());

        public override T Accept<T>(IIRStmtVisitor<T> visitor) => visitor.VisitDirty(this);
    }

    /// <summary>
    /// Memory bus event (fence, lock acquisition/release)
    /// ppIRStmt output: MBusEvent-Fence, MBusEvent-BusLock, MBusEvent-BusUnlock
    /// </summary>
    public sealed class IRStmtMBE : IRStmt
    {
        public IRMBusEvent Event { get; }
        public override IRStmtTag Tag => IRStmtTag.MBE;

        public IRStmtMBE(IRMBusEvent eventType)
        {
            Event = eventType;
        }

        public override bool Equals(IRStmt? other) =>
            other is IRStmtMBE mbe && Event == mbe.Event;

        public override bool Equals(object? obj) =>
            obj is IRStmtMBE other && Equals(other);

        public override int GetHashCode() => Event.GetHashCode();

        public override string ToString() =>
            Event switch
            {
                IRMBusEvent.Fence => "MBusEvent-Fence",
                IRMBusEvent.CancelReservation => "MBusEvent-CancelReservation",
                _ => $"MBusEvent-{Event}"
            };

        public override IRStmt DeepCopy() => new IRStmtMBE(Event);

        public override T Accept<T>(IIRStmtVisitor<T> visitor) => visitor.VisitMBE(this);
    }

    /// <summary>
    /// Conditional exit from the middle of an IRSB
    /// ppIRStmt output: if (<guard>) goto {<jk>} <dst>, eg. if (t69) goto {Boring} 0x4000AAA:I32
    /// </summary>
    public sealed class IRStmtExit : IRStmt
    {
        public IRExpr Guard { get; }        // Conditional expression
        public IRConst Dst { get; }         // Jump target (constant only)
        public IRJumpKind Jk { get; }       // Jump kind
        public int OffsIP { get; }          // Guest state offset for IP
        public override IRStmtTag Tag => IRStmtTag.Exit;

        public IRStmtExit(IRExpr guard, IRJumpKind jk, IRConst dst, int offsIP)
        {
            Guard = guard ?? throw new ArgumentNullException(nameof(guard));
            Dst = dst ?? throw new ArgumentNullException(nameof(dst));
            Jk = jk;
            OffsIP = offsIP;
        }

        public override bool Equals(IRStmt? other) =>
            other is IRStmtExit exit && Guard.Equals(exit.Guard) && Dst.Equals(exit.Dst) && 
            Jk == exit.Jk && OffsIP == exit.OffsIP;

        public override bool Equals(object? obj) =>
            obj is IRStmtExit other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(Guard, Dst, Jk, OffsIP);

        public override string ToString() => $"if ({Guard.PrettyPrint()}) goto {{{Jk}}} {Dst}";

        public override IRStmt DeepCopy() => new IRStmtExit(Guard.DeepCopy(), Jk, Dst.DeepCopy(), OffsIP);

        public override T Accept<T>(IIRStmtVisitor<T> visitor) => visitor.VisitExit(this);
    }

    /// <summary>
    /// Factory methods for creating IR statements
    /// </summary>
    public static class IRStmtFactory
    {
        // Basic statements
        public static IRStmtNoOp NoOp() => new();
        public static IRStmtIMark IMark(ulong addr, uint len, byte delta = 0) => new(addr, len, delta);
        public static IRStmtAbiHint AbiHint(IRExpr baseExpr, int len, IRExpr nia) => new(baseExpr, len, nia);

        // Register operations
        public static IRStmtPut Put(int offset, IRExpr data) => new(offset, data);
        public static IRStmtPutI PutI(IRRegArray descr, IRExpr ix, int bias, IRExpr data) => 
            new(new IRPutI(descr, ix, bias, data));

        // Temporary assignment
        public static IRStmtWrTmp WrTmp(IRTemp tmp, IRExpr data) => new(tmp, data);

        // Memory operations
        public static IRStmtStore Store(IREndness end, IRExpr addr, IRExpr data) => new(end, addr, data);
        public static IRStmtStore StoreLE(IRExpr addr, IRExpr data) => Store(IREndness.LE, addr, data);
        public static IRStmtStore StoreBE(IRExpr addr, IRExpr data) => Store(IREndness.BE, addr, data);

        // Guarded operations
        public static IRStmtLoadG LoadG(IREndness end, IRLoadGOp cvt, IRTemp dst, IRExpr addr, IRExpr alt, IRExpr guard) =>
            new(new IRLoadG(end, cvt, dst, addr, alt, guard));

        public static IRStmtStoreG StoreG(IREndness end, IRExpr addr, IRExpr data, IRExpr guard) =>
            new(new IRStoreG(end, addr, data, guard));

        // Atomic operations
        public static IRStmtCAS CAS(IRTemp oldHi, IRTemp oldLo, IREndness end, IRExpr addr,
                                    IRExpr? expdHi, IRExpr expdLo, IRExpr? dataHi, IRExpr dataLo) =>
            new(new IRCAS(oldHi, oldLo, end, addr, expdHi, expdLo, dataHi, dataLo));

        public static IRStmtLLSC LoadLinked(IREndness end, IRTemp result, IRExpr addr) =>
            new(end, result, addr, null);

        public static IRStmtLLSC StoreConditional(IREndness end, IRTemp result, IRExpr addr, IRExpr storeData) =>
            new(end, result, addr, storeData);

        // Side effects
        public static IRStmtDirty Dirty(IRCallTarget ccall, IRExpr? guard, IRExpr[] args, IRTemp tmp, IRType mfx) =>
            new(new IRDirty(ccall, guard, args, tmp, mfx));

        // Control flow
        public static IRStmtMBE MBE(IRMBusEvent eventType) => new(eventType);
        public static IRStmtExit Exit(IRExpr guard, IRJumpKind jk, IRConst dst, int offsIP) => new(guard, jk, dst, offsIP);
    }
}