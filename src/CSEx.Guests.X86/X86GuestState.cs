using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using CSEx.Core;
using CSEx.Guests;
using CSEx.IR;

namespace CSEx.Guests.X86
{
    /// <summary>
    /// x86 32-bit guest CPU state representation.
    /// Corresponds to VexGuestX86State in original VEX.
    /// </summary>
    public class X86GuestState : GuestStateBase
    {
        // General purpose registers (32-bit)
        public UInt32 GuestEAX { get; set; }
        public UInt32 GuestECX { get; set; }
        public UInt32 GuestEDX { get; set; }
        public UInt32 GuestEBX { get; set; }
        public UInt32 GuestESP { get; set; }
        public UInt32 GuestEBP { get; set; }
        public UInt32 GuestESI { get; set; }
        public UInt32 GuestEDI { get; set; }
        
        // Condition code calculation state
        public UInt32 GuestCCOp { get; set; }      // CCOp operation type
        public UInt32 GuestCCDep1 { get; set; }    // First dependency
        public UInt32 GuestCCDep2 { get; set; }    // Second dependency  
        public UInt32 GuestCCNDep { get; set; }    // Non-dependency (for carry)
        
        // Individual flags stored separately for efficiency
        public UInt32 GuestDFlag { get; set; }     // Direction flag (-1 or +1)
        public UInt32 GuestIDFlag { get; set; }    // ID flag (bit 21 of EFLAGS)
        public UInt32 GuestACFlag { get; set; }    // Alignment check flag (bit 18)
        
        // Instruction pointer
        public UInt32 GuestEIP { get; set; }
        
        // FPU state
        public UInt64[] GuestFPReg { get; set; } = new UInt64[8];  // 8 x 64-bit FP registers
        public byte[] GuestFPTag { get; set; } = new byte[8];      // 8 x tag bytes
        public UInt32 GuestFPRound { get; set; }                   // FPU rounding mode
        public UInt32 GuestFC3210 { get; set; }                    // FPU condition codes
        public UInt32 GuestFTop { get; set; }                      // FPU stack top pointer
        
        // SSE state
        public UInt32 GuestSSERound { get; set; }                  // SSE rounding mode
        public V128[] GuestXMM { get; set; } = new V128[8];        // 8 x 128-bit SSE registers
        
        // AVX state (256-bit YMM registers encompass XMM registers)
        public V256[] GuestYMM { get; set; } = new V256[8];        // 8 x 256-bit AVX registers
        
        // AVX-512 state (512-bit ZMM registers encompass YMM registers)
        public V512[] GuestZMM { get; set; } = new V512[8];        // 8 x 512-bit AVX-512 registers (x86 has 8, x64 has 32)
        
        // AVX-512 mask registers
        public UInt64[] GuestK { get; set; } = new UInt64[8];       // 8 x 64-bit mask registers (k0-k7)
        
        // Segment registers (16-bit)
        public UInt16 GuestCS { get; set; }
        public UInt16 GuestDS { get; set; }
        public UInt16 GuestES { get; set; }
        public UInt16 GuestFS { get; set; }
        public UInt16 GuestGS { get; set; }
        public UInt16 GuestSS { get; set; }
        
        // LDT/GDT pointers (host addresses in original, we'll handle differently)
        public UInt64 GuestLDT { get; set; }
        public UInt64 GuestGDT { get; set; }
        
        // Darwin-specific syscall class
        public UInt32 GuestSCClass { get; set; }
        
        public override IRType GuestWordSize => IRType.I32;
        public override IRType GuestIPType => IRType.I32;
        
        /// <summary>
        /// Register name to offset mapping for x86
        /// </summary>
        private static readonly Dictionary<string, (int offset, IRType type)> RegisterMap = 
            new Dictionary<string, (int, IRType)>
            {
                // 32-bit general purpose registers  
                { "EAX", (0, IRType.I32) },
                { "ECX", (4, IRType.I32) },
                { "EDX", (8, IRType.I32) },
                { "EBX", (12, IRType.I32) },
                { "ESP", (16, IRType.I32) },
                { "EBP", (20, IRType.I32) },
                { "ESI", (24, IRType.I32) },
                { "EDI", (28, IRType.I32) },
                
                // 16-bit register views (same offsets as 32-bit)
                { "AX", (0, IRType.I16) },
                { "CX", (4, IRType.I16) },
                { "DX", (8, IRType.I16) },
                { "BX", (12, IRType.I16) },
                { "SP", (16, IRType.I16) },
                { "BP", (20, IRType.I16) },
                { "SI", (24, IRType.I16) },
                { "DI", (28, IRType.I16) },
                
                // 8-bit register views (low bytes)
                { "AL", (0, IRType.I8) },
                { "CL", (4, IRType.I8) },
                { "DL", (8, IRType.I8) },
                { "BL", (12, IRType.I8) },
                
                // 8-bit register views (high bytes)
                { "AH", (1, IRType.I8) },
                { "CH", (5, IRType.I8) },
                { "DH", (9, IRType.I8) },
                { "BH", (13, IRType.I8) },
                
                // Condition code state
                { "CC_OP", (32, IRType.I32) },
                { "CC_DEP1", (36, IRType.I32) },
                { "CC_DEP2", (40, IRType.I32) },
                { "CC_NDEP", (44, IRType.I32) },
                
                // Individual flags
                { "DFLAG", (48, IRType.I32) },
                { "IDFLAG", (52, IRType.I32) },
                { "ACFLAG", (56, IRType.I32) },
                
                // Instruction pointer
                { "EIP", (60, IRType.I32) },
                
                // FPU registers  
                { "FPROUND", (128, IRType.I32) },
                { "FC3210", (132, IRType.I32) },
                { "FTOP", (136, IRType.I32) },
                
                // SSE rounding
                { "SSEROUND", (140, IRType.I32) },
                
                // Segment registers
                { "CS", (150, IRType.I16) },
                { "DS", (152, IRType.I16) },
                { "ES", (154, IRType.I16) },
                { "FS", (156, IRType.I16) },
                { "GS", (158, IRType.I16) },
                { "SS", (160, IRType.I16) },
            };
        
        protected override void InitializeArchSpecific()
        {
            // Initialize general purpose registers
            GuestEAX = GuestECX = GuestEDX = GuestEBX = 0;
            GuestESP = GuestEBP = GuestESI = GuestEDI = 0;
            
            // Initialize condition code state
            GuestCCOp = (uint)CCOp.Copy;  // Default to copy mode
            GuestCCDep1 = GuestCCDep2 = GuestCCNDep = 0;
            
            // Initialize flags
            GuestDFlag = 1;      // Forward direction (positive)
            GuestIDFlag = 0;     // ID flag clear
            GuestACFlag = 0;     // Alignment check disabled
            
            // Initialize instruction pointer
            GuestEIP = 0;
            
            // Initialize FPU state
            for (int i = 0; i < 8; i++)
            {
                GuestFPReg[i] = 0;
                GuestFPTag[i] = 0;  // Empty
            }
            GuestFPRound = 0;  // Round to nearest (default)
            GuestFC3210 = 0;   // Clear condition codes
            GuestFTop = 0;     // Stack top at register 0
            
            // Initialize SSE state
            GuestSSERound = 0;  // Round to nearest
            for (int i = 0; i < 8; i++)
            {
                GuestXMM[i] = new V128(); // Zero
            }
            
            // Initialize AVX state (YMM registers encompass XMM)
            for (int i = 0; i < 8; i++)
            {
                GuestYMM[i] = new V256(); // Zero
            }
            
            // Initialize segment registers
            GuestCS = GuestDS = GuestES = 0;
            GuestFS = GuestGS = GuestSS = 0;
            
            // Initialize descriptor table pointers
            GuestLDT = GuestGDT = 0;
            
            // Initialize syscall class
            GuestSCClass = 0;
        }
        
        public override IGuestState DeepCopy()
        {
            var copy = new X86GuestState();
            
            // Copy base class fields
            copy.HostEvCFailAddr = HostEvCFailAddr;
            copy.HostEvCCounter = HostEvCCounter;
            copy.GuestEmNote = GuestEmNote;
            copy.GuestCMStart = GuestCMStart;
            copy.GuestCMLen = GuestCMLen;
            copy.GuestNRAddr = GuestNRAddr;
            copy.GuestIPAtSyscall = GuestIPAtSyscall;
            
            // Copy x86-specific fields
            copy.GuestEAX = GuestEAX;
            copy.GuestECX = GuestECX;
            copy.GuestEDX = GuestEDX;
            copy.GuestEBX = GuestEBX;
            copy.GuestESP = GuestESP;
            copy.GuestEBP = GuestEBP;
            copy.GuestESI = GuestESI;
            copy.GuestEDI = GuestEDI;
            
            copy.GuestCCOp = GuestCCOp;
            copy.GuestCCDep1 = GuestCCDep1;
            copy.GuestCCDep2 = GuestCCDep2;
            copy.GuestCCNDep = GuestCCNDep;
            
            copy.GuestDFlag = GuestDFlag;
            copy.GuestIDFlag = GuestIDFlag;
            copy.GuestACFlag = GuestACFlag;
            copy.GuestEIP = GuestEIP;
            
            // Deep copy arrays
            Array.Copy(GuestFPReg, copy.GuestFPReg, 8);
            Array.Copy(GuestFPTag, copy.GuestFPTag, 8);
            copy.GuestFPRound = GuestFPRound;
            copy.GuestFC3210 = GuestFC3210;
            copy.GuestFTop = GuestFTop;
            
            copy.GuestSSERound = GuestSSERound;
            for (int i = 0; i < 8; i++)
            {
                copy.GuestXMM[i] = GuestXMM[i]; // V128 should be value type
            }
            
            // Copy AVX state
            for (int i = 0; i < 8; i++)
            {
                copy.GuestYMM[i] = GuestYMM[i]; // V256 should be value type
            }
            
            copy.GuestCS = GuestCS;
            copy.GuestDS = GuestDS;
            copy.GuestES = GuestES;
            copy.GuestFS = GuestFS;
            copy.GuestGS = GuestGS;
            copy.GuestSS = GuestSS;
            
            copy.GuestLDT = GuestLDT;
            copy.GuestGDT = GuestGDT;
            copy.GuestSCClass = GuestSCClass;
            
            return copy;
        }
        
        public override int GetRegisterOffset(string registerName)
        {
            var upperName = registerName.ToUpperInvariant();
            
            if (RegisterMap.TryGetValue(upperName, out var info))
            {
                return info.offset;
            }
            
            // Handle FP registers dynamically
            if (upperName.StartsWith("FPREG"))
            {
                if (int.TryParse(upperName.Substring(5), out int regNum) && regNum >= 0 && regNum < 8)
                {
                    // Approximate offset - we'll calculate this properly when needed
                    const int baseOffset = 200; // FP registers start around here
                    return baseOffset + (regNum * sizeof(UInt64));
                }
            }
            
            // Handle FP tag registers
            if (upperName.StartsWith("FPTAG"))
            {
                if (int.TryParse(upperName.Substring(5), out int regNum) && regNum >= 0 && regNum < 8)
                {
                    const int baseOffset = 264; // FP tags start around here
                    return baseOffset + regNum;
                }
            }
            
            // Handle XMM registers
            if (upperName.StartsWith("XMM"))
            {
                if (int.TryParse(upperName.Substring(3), out int regNum) && regNum >= 0 && regNum < 8)
                {
                    const int baseOffset = 300; // XMM registers start around here
                    return baseOffset + (regNum * 16); // 16 bytes per XMM register
                }
            }
            
            // Handle YMM registers (AVX)
            if (upperName.StartsWith("YMM"))
            {
                if (int.TryParse(upperName.Substring(3), out int regNum) && regNum >= 0 && regNum < 8)
                {
                    const int baseOffset = 400; // YMM registers start after XMM
                    return baseOffset + (regNum * 32); // 32 bytes per YMM register
                }
            }
            
            // Handle ZMM registers (AVX-512)
            if (upperName.StartsWith("ZMM"))
            {
                if (int.TryParse(upperName.Substring(3), out int regNum) && regNum >= 0 && regNum < 8)
                {
                    const int baseOffset = 700; // ZMM registers start after YMM
                    return baseOffset + (regNum * 64); // 64 bytes per ZMM register
                }
            }
            
            // Handle mask registers (AVX-512)
            if (upperName.StartsWith("K") && upperName.Length >= 2 && char.IsDigit(upperName[1]))
            {
                if (int.TryParse(upperName.Substring(1), out int regNum) && regNum >= 0 && regNum < 8)
                {
                    const int baseOffset = 1200; // Mask registers start after ZMM
                    return baseOffset + (regNum * 8); // 8 bytes per mask register
                }
            }
            
            throw new ArgumentException($"Unknown register name: {registerName}");
        }
        
        public override IRType GetRegisterType(string registerName)
        {
            var upperName = registerName.ToUpperInvariant();
            
            if (RegisterMap.TryGetValue(upperName, out var info))
            {
                return info.type;
            }
            
            // Handle FP registers
            if (upperName.StartsWith("FPREG"))
            {
                return IRType.I64;  // FP registers are 64-bit
            }
            
            // Handle FP tag registers  
            if (upperName.StartsWith("FPTAG"))
            {
                return IRType.I8;   // Tag registers are 8-bit
            }
            
            // Handle XMM registers
            if (upperName.StartsWith("XMM"))
            {
                return IRType.V128; // XMM registers are 128-bit vectors
            }
            
            // Handle YMM registers (AVX)
            if (upperName.StartsWith("YMM"))
            {
                return IRType.V256; // YMM registers are 256-bit vectors
            }
            
            // Handle ZMM registers (AVX-512)
            if (upperName.StartsWith("ZMM"))
            {
                return IRType.V512; // ZMM registers are 512-bit vectors
            }
            
            // Handle mask registers (AVX-512)
            if (upperName.StartsWith("K") && upperName.Length >= 2 && char.IsDigit(upperName[1]))
            {
                return IRType.I64;  // Mask registers are 64-bit
            }
            
            throw new ArgumentException($"Unknown register name: {registerName}");
        }
        
        public override bool RequiresPreciseMemoryExceptions(int minOffset, int maxOffset)
        {
            // For x86, require precise exceptions for ESP, EBP, and EIP
            var espOffset = GetRegisterOffset("ESP");
            var ebpOffset = GetRegisterOffset("EBP");
            var eipOffset = GetRegisterOffset("EIP");
            
            // Check if range overlaps with critical registers
            return OverlapsRange(minOffset, maxOffset, espOffset, espOffset + 3) ||
                   OverlapsRange(minOffset, maxOffset, ebpOffset, ebpOffset + 3) ||
                   OverlapsRange(minOffset, maxOffset, eipOffset, eipOffset + 3);
        }
        
        private static bool OverlapsRange(int min1, int max1, int min2, int max2)
        {
            return min1 <= max2 && max1 >= min2;
        }
    }
}