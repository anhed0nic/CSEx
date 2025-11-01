using System;
using CSEx.Core;

namespace CSEx.IR
{
    /// <summary>
    /// Jump kinds for Exit statements (equivalent to VEX IRJumpKind)
    /// </summary>
    public enum IRJumpKind : uint
    {
        Boring = 0x1A00,        // Nothing interesting
        Call,                   // Guest is doing a call
        Ret,                    // Guest is doing a return
        ClientReq,              // Jump to client request handler
        Yield,                  // Jump to yield handler
        EmWarn,                 // Jump to emulation warning handler
        EmFail,                 // Jump to emulation failure handler
        NoDecode,               // Jump to no-decode handler
        MapFail,                // Jump to map-fail handler
        InvalICache,            // Invalidate icache for range [CMSTART, +CMLEN)
        FlushDCache,            // Flush dcache for range [CMSTART, +CMLEN)
        NoRedir,                // Jump to un-redirected guest addr
        SigILL,                 // Current instruction synths SIGILL
        SigTRAP,                // Current instruction synths SIGTRAP
        SigSEGV,                // Current instruction synths SIGSEGV
        SigBUS,                 // Current instruction synths SIGBUS
        SigFPE,                 // Current instruction synths generic SIGFPE
        SigFPE_IntDiv,          // Current instruction synths SIGFPE - IntDiv
        SigFPE_IntOvf,          // Current instruction synths SIGFPE - IntOvf
        Privileged,             // Current instruction should fail depending on privilege level
        // Guest-dependent syscall kinds
        Sys_syscall,            // amd64/x86 'syscall', ppc 'sc', arm 'svc #0'
        Sys_int,                // amd64/x86 'int *'
        Sys_int32,              // amd64/x86 'int $0x20'
        Sys_int128,             // amd64/x86 'int $0x80'
        Sys_int129,             // amd64/x86 'int $0x81'
        Sys_int130,             // amd64/x86 'int $0x82'
        Sys_int145,             // amd64/x86 'int $0x91'
        Sys_int210,             // amd64/x86 'int $0xD2'
        Sys_sysenter            // x86 'sysenter'
    }

    /// <summary>
    /// Memory bus events (equivalent to VEX IRMBusEvent)
    /// </summary>
    public enum IRMBusEvent : uint
    {
        Fence = 0x1C00,         // Memory fence
        CancelReservation       // Cancel LL/SC reservation (ARM specific)
    }

    /// <summary>
    /// Write a guest register at a non-fixed offset (equivalent to VEX IRPutI)
    /// ppIRStmt output: PUTI<descr>[<ix>,<bias>] = <data>, eg. PUTI(64:8xF64)[t5,0] = t1
    /// </summary>
    public sealed class IRPutI : IEquatable<IRPutI>
    {
        public IRRegArray Descr { get; }    // Part of guest state treated as circular
        public IRExpr Ix { get; }           // Variable part of index into array
        public int Bias { get; }            // Constant offset part of index
        public IRExpr Data { get; }         // Value to write

        public IRPutI(IRRegArray descr, IRExpr ix, int bias, IRExpr data)
        {
            Descr = descr ?? throw new ArgumentNullException(nameof(descr));
            Ix = ix ?? throw new ArgumentNullException(nameof(ix));
            Bias = bias;
            Data = data ?? throw new ArgumentNullException(nameof(data));
        }

        public bool Equals(IRPutI? other) =>
            other != null && Descr.Equals(other.Descr) && Ix.Equals(other.Ix) && 
            Bias == other.Bias && Data.Equals(other.Data);

        public override bool Equals(object? obj) =>
            obj is IRPutI other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(Descr, Ix, Bias, Data);

        public override string ToString() => $"PUTI{Descr}[{Ix.PrettyPrint()},{Bias}] = {Data.PrettyPrint()}";

        public IRPutI DeepCopy() => new(Descr.DeepCopy(), Ix.DeepCopy(), Bias, Data.DeepCopy());
    }

    /// <summary>
    /// Compare-and-swap atomic operation (equivalent to VEX IRCAS)
    /// </summary>
    public sealed class IRCAS : IEquatable<IRCAS>
    {
        public IRTemp OldHi { get; }        // Old value of *addr is written here (high part)
        public IRTemp OldLo { get; }        // Old value of *addr is written here (low part)
        public IREndness End { get; }       // Endianness of the data in memory
        public IRExpr Addr { get; }         // Store address
        public IRExpr? ExpdHi { get; }      // Expected old value at *addr (high part)
        public IRExpr ExpdLo { get; }       // Expected old value at *addr (low part)
        public IRExpr? DataHi { get; }      // New value for *addr (high part)
        public IRExpr DataLo { get; }       // New value for *addr (low part)

        public IRCAS(IRTemp oldHi, IRTemp oldLo, IREndness end, IRExpr addr,
                     IRExpr? expdHi, IRExpr expdLo, IRExpr? dataHi, IRExpr dataLo)
        {
            OldHi = oldHi;
            OldLo = oldLo;
            End = end;
            Addr = addr ?? throw new ArgumentNullException(nameof(addr));
            ExpdHi = expdHi;
            ExpdLo = expdLo ?? throw new ArgumentNullException(nameof(expdLo));
            DataHi = dataHi;
            DataLo = dataLo ?? throw new ArgumentNullException(nameof(dataLo));
        }

        public bool IsDoubleElement => ExpdHi != null && DataHi != null;

        public bool Equals(IRCAS? other)
        {
            if (other == null) return false;
            return OldHi.Equals(other.OldHi) && OldLo.Equals(other.OldLo) && End == other.End &&
                   Addr.Equals(other.Addr) && 
                   ((ExpdHi == null && other.ExpdHi == null) || (ExpdHi?.Equals(other.ExpdHi) == true)) &&
                   ExpdLo.Equals(other.ExpdLo) &&
                   ((DataHi == null && other.DataHi == null) || (DataHi?.Equals(other.DataHi) == true)) &&
                   DataLo.Equals(other.DataLo);
        }

        public override bool Equals(object? obj) =>
            obj is IRCAS other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(OldHi, OldLo, End, Addr, ExpdHi, ExpdLo, DataHi, DataLo);

        public override string ToString()
        {
            var endStr = End == IREndness.LE ? "le" : "be";
            if (IsDoubleElement)
            {
                return $"{OldHi}:{OldLo} = CAS{endStr}({Addr.PrettyPrint()} :: {ExpdHi!.PrettyPrint()}:{ExpdLo.PrettyPrint()} -> {DataHi!.PrettyPrint()}:{DataLo.PrettyPrint()})";
            }
            else
            {
                return $"{OldLo} = CAS{endStr}({Addr.PrettyPrint()} :: {ExpdLo.PrettyPrint()} -> {DataLo.PrettyPrint()})";
            }
        }

        public IRCAS DeepCopy() => new(OldHi, OldLo, End, Addr.DeepCopy(), 
                                       ExpdHi?.DeepCopy(), ExpdLo.DeepCopy(), 
                                       DataHi?.DeepCopy(), DataLo.DeepCopy());
    }

    /// <summary>
    /// Load conversion operations for guarded loads
    /// </summary>
    public enum IRLoadGOp : uint
    {
        ILGop_INVALID = 0x1D00,
        ILGop_IdentV128,        // 128-bit identity
        ILGop_Ident64,          // 64-bit identity
        ILGop_Ident32,          // 32-bit identity
        ILGop_16Uto32,          // 16-bit unsigned to 32-bit
        ILGop_16Sto32,          // 16-bit signed to 32-bit
        ILGop_8Uto32,           // 8-bit unsigned to 32-bit
        ILGop_8Sto32            // 8-bit signed to 32-bit
    }

    /// <summary>
    /// Guarded load details (equivalent to VEX IRLoadG)
    /// </summary>
    public sealed class IRLoadG : IEquatable<IRLoadG>
    {
        public IREndness End { get; }       // Endianness of the load
        public IRLoadGOp Cvt { get; }       // Conversion operation
        public IRTemp Dst { get; }          // Destination temporary
        public IRExpr Addr { get; }         // Load address
        public IRExpr Alt { get; }          // Alternative value if guard is false
        public IRExpr Guard { get; }        // Guard condition

        public IRLoadG(IREndness end, IRLoadGOp cvt, IRTemp dst, IRExpr addr, IRExpr alt, IRExpr guard)
        {
            End = end;
            Cvt = cvt;
            Dst = dst;
            Addr = addr ?? throw new ArgumentNullException(nameof(addr));
            Alt = alt ?? throw new ArgumentNullException(nameof(alt));
            Guard = guard ?? throw new ArgumentNullException(nameof(guard));
        }

        public bool Equals(IRLoadG? other) =>
            other != null && End == other.End && Cvt == other.Cvt && Dst.Equals(other.Dst) &&
            Addr.Equals(other.Addr) && Alt.Equals(other.Alt) && Guard.Equals(other.Guard);

        public override bool Equals(object? obj) =>
            obj is IRLoadG other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(End, Cvt, Dst, Addr, Alt, Guard);

        public override string ToString()
        {
            var endStr = End == IREndness.LE ? "le" : "be";
            return $"{Dst} = if ({Guard.PrettyPrint()}) {Cvt}(LD{endStr}({Addr.PrettyPrint()})) else {Alt.PrettyPrint()}";
        }

        public IRLoadG DeepCopy() => new(End, Cvt, Dst, Addr.DeepCopy(), Alt.DeepCopy(), Guard.DeepCopy());
    }

    /// <summary>
    /// Guarded store details (equivalent to VEX IRStoreG)
    /// </summary>
    public sealed class IRStoreG : IEquatable<IRStoreG>
    {
        public IREndness End { get; }       // Endianness of the store
        public IRExpr Addr { get; }         // Store address
        public IRExpr Data { get; }         // Value to write
        public IRExpr Guard { get; }        // Guard condition

        public IRStoreG(IREndness end, IRExpr addr, IRExpr data, IRExpr guard)
        {
            End = end;
            Addr = addr ?? throw new ArgumentNullException(nameof(addr));
            Data = data ?? throw new ArgumentNullException(nameof(data));
            Guard = guard ?? throw new ArgumentNullException(nameof(guard));
        }

        public bool Equals(IRStoreG? other) =>
            other != null && End == other.End && Addr.Equals(other.Addr) && 
            Data.Equals(other.Data) && Guard.Equals(other.Guard);

        public override bool Equals(object? obj) =>
            obj is IRStoreG other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(End, Addr, Data, Guard);

        public override string ToString()
        {
            var endStr = End == IREndness.LE ? "le" : "be";
            return $"if ({Guard.PrettyPrint()}) ST{endStr}({Addr.PrettyPrint()}) = {Data.PrettyPrint()}";
        }

        public IRStoreG DeepCopy() => new(End, Addr.DeepCopy(), Data.DeepCopy(), Guard.DeepCopy());
    }

    /// <summary>
    /// Dirty call details (equivalent to VEX IRDirty)
    /// Represents a call to a function with side effects
    /// </summary>
    public sealed class IRDirty : IEquatable<IRDirty>
    {
        public IRCallTarget CCall { get; }      // Function to call
        public IRExpr? Guard { get; }           // Conditional execution guard
        public IRExpr[] Args { get; }           // Function arguments
        public IRTemp Tmp { get; }              // Result temporary (IRTemp.INVALID if no result)
        public IRType MFx { get; }              // Memory effects type
        
        public IRDirty(IRCallTarget ccall, IRExpr? guard, IRExpr[] args, IRTemp tmp, IRType mfx)
        {
            CCall = ccall ?? throw new ArgumentNullException(nameof(ccall));
            Guard = guard;
            Args = args ?? Array.Empty<IRExpr>();
            Tmp = tmp;
            MFx = mfx;
        }

        public bool Equals(IRDirty? other)
        {
            if (other == null || !CCall.Equals(other.CCall) || Tmp != other.Tmp || MFx != other.MFx ||
                Args.Length != other.Args.Length)
                return false;

            if ((Guard == null) != (other.Guard == null) || (Guard != null && !Guard.Equals(other.Guard)))
                return false;

            for (int i = 0; i < Args.Length; i++)
            {
                if (!Args[i].Equals(other.Args[i]))
                    return false;
            }
            return true;
        }

        public override bool Equals(object? obj) =>
            obj is IRDirty other && Equals(other);

        public override int GetHashCode()
        {
            var hash = HashCode.Combine(CCall, Guard, Tmp, MFx, Args.Length);
            foreach (var arg in Args)
            {
                hash = HashCode.Combine(hash, arg);
            }
            return hash;
        }

        public override string ToString()
        {
            var result = Tmp.IsValid ? $"{Tmp} = " : "";
            var guard = Guard != null ? $"{Guard.PrettyPrint()} " : "";
            var args = string.Join(",", Array.ConvertAll(Args, arg => arg.PrettyPrint()));
            return $"{result}DIRTY {guard}::: {CCall.Name}({args})";
        }

        public IRDirty DeepCopy()
        {
            var newArgs = new IRExpr[Args.Length];
            for (int i = 0; i < Args.Length; i++)
            {
                newArgs[i] = Args[i].DeepCopy();
            }
            return new IRDirty(CCall.DeepCopy(), Guard?.DeepCopy(), newArgs, Tmp, MFx);
        }
    }
}