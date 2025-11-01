using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using CSEx.Core;
using CSEx.Guests;
using CSEx.IR;

namespace CSEx.Guests.AMD64
{
    /// <summary>
    /// AMD64 (x86-64) guest CPU state representation.
    /// Corresponds to VexGuestAMD64State in original VEX.
    /// </summary>
    public class AMD64GuestState : GuestStateBase
    {
        // General purpose registers (64-bit)
        public UInt64 GuestRAX { get; set; }
        public UInt64 GuestRCX { get; set; }
        public UInt64 GuestRDX { get; set; }
        public UInt64 GuestRBX { get; set; }
        public UInt64 GuestRSP { get; set; }
        public UInt64 GuestRBP { get; set; }
        public UInt64 GuestRSI { get; set; }
        public UInt64 GuestRDI { get; set; }
        public UInt64 GuestR8 { get; set; }
        public UInt64 GuestR9 { get; set; }
        public UInt64 GuestR10 { get; set; }
        public UInt64 GuestR11 { get; set; }
        public UInt64 GuestR12 { get; set; }
        public UInt64 GuestR13 { get; set; }
        public UInt64 GuestR14 { get; set; }
        public UInt64 GuestR15 { get; set; }
        
        // Condition code calculation state (64-bit)
        public UInt64 GuestCCOp { get; set; }      // CCOp operation type
        public UInt64 GuestCCDep1 { get; set; }    // First dependency
        public UInt64 GuestCCDep2 { get; set; }    // Second dependency  
        public UInt64 GuestCCNDep { get; set; }    // Non-dependency (for carry)
        
        // Individual flags stored separately for efficiency
        public UInt64 GuestDFlag { get; set; }     // Direction flag (-1 or +1)
        public UInt64 GuestIDFlag { get; set; }    // ID flag (bit 21 of RFLAGS)
        public UInt64 GuestACFlag { get; set; }    // Alignment check flag (bit 18)
        
        // Instruction pointer
        public UInt64 GuestRIP { get; set; }
        
        // Filesystem constant for TLS support
        public UInt64 GuestFSConst { get; set; }
        
        // SSE/AVX state - YMM registers (256-bit, includes XMM as lower 128 bits)
        public UInt64 GuestSSERound { get; set; }                   // SSE rounding mode
        public V256[] GuestYMM { get; set; } = new V256[17];        // 16 real + 1 temp YMM register
        
        // Control registers
        public UInt64[] GuestCR { get; set; } = new UInt64[16];     // CR0-CR15
        
        // FPU state (similar to x86 but with 64-bit addressing)
        public UInt32 GuestFTop { get; set; }                       // FPU stack top (still 32-bit)
        public UInt32 Pad1 { get; set; }                           // Padding for alignment
        public UInt64[] GuestFPReg { get; set; } = new UInt64[8];  // 8 x 64-bit FP registers
        public byte[] GuestFPTag { get; set; } = new byte[8];      // 8 x tag bytes  
        public UInt64 GuestFPRound { get; set; }                   // FPU rounding mode
        public UInt64 GuestFC3210 { get; set; }                   // FPU condition codes
        
        public override IRType GuestWordSize => IRType.I64;
        public override IRType GuestIPType => IRType.I64;
        
        /// <summary>
        /// Register name to offset mapping for AMD64
        /// </summary>
        private static readonly Dictionary<string, (int offset, IRType type)> RegisterMap = 
            new Dictionary<string, (int, IRType)>
            {
                // 64-bit general purpose registers
                { "RAX", (0, IRType.I64) },
                { "RCX", (8, IRType.I64) },
                { "RDX", (16, IRType.I64) },
                { "RBX", (24, IRType.I64) },
                { "RSP", (32, IRType.I64) },
                { "RBP", (40, IRType.I64) },
                { "RSI", (48, IRType.I64) },
                { "RDI", (56, IRType.I64) },
                { "R8", (64, IRType.I64) },
                { "R9", (72, IRType.I64) },
                { "R10", (80, IRType.I64) },
                { "R11", (88, IRType.I64) },
                { "R12", (96, IRType.I64) },
                { "R13", (104, IRType.I64) },
                { "R14", (112, IRType.I64) },
                { "R15", (120, IRType.I64) },
                
                // 32-bit register views (lower 32 bits)
                { "EAX", (0, IRType.I32) },
                { "ECX", (8, IRType.I32) },
                { "EDX", (16, IRType.I32) },
                { "EBX", (24, IRType.I32) },
                { "ESP", (32, IRType.I32) },
                { "EBP", (40, IRType.I32) },
                { "ESI", (48, IRType.I32) },
                { "EDI", (56, IRType.I32) },
                { "R8D", (64, IRType.I32) },
                { "R9D", (72, IRType.I32) },
                { "R10D", (80, IRType.I32) },
                { "R11D", (88, IRType.I32) },
                { "R12D", (96, IRType.I32) },
                { "R13D", (104, IRType.I32) },
                { "R14D", (112, IRType.I32) },
                { "R15D", (120, IRType.I32) },
                
                // 16-bit register views
                { "AX", (0, IRType.I16) },
                { "CX", (8, IRType.I16) },
                { "DX", (16, IRType.I16) },
                { "BX", (24, IRType.I16) },
                { "SP", (32, IRType.I16) },
                { "BP", (40, IRType.I16) },
                { "SI", (48, IRType.I16) },
                { "DI", (56, IRType.I16) },
                { "R8W", (64, IRType.I16) },
                { "R9W", (72, IRType.I16) },
                { "R10W", (80, IRType.I16) },
                { "R11W", (88, IRType.I16) },
                { "R12W", (96, IRType.I16) },
                { "R13W", (104, IRType.I16) },
                { "R14W", (112, IRType.I16) },
                { "R15W", (120, IRType.I16) },
                
                // 8-bit register views (low bytes)
                { "AL", (0, IRType.I8) },
                { "CL", (8, IRType.I8) },
                { "DL", (16, IRType.I8) },
                { "BL", (24, IRType.I8) },
                { "SPL", (32, IRType.I8) },
                { "BPL", (40, IRType.I8) },
                { "SIL", (48, IRType.I8) },
                { "DIL", (56, IRType.I8) },
                { "R8B", (64, IRType.I8) },
                { "R9B", (72, IRType.I8) },
                { "R10B", (80, IRType.I8) },
                { "R11B", (88, IRType.I8) },
                { "R12B", (96, IRType.I8) },
                { "R13B", (104, IRType.I8) },
                { "R14B", (112, IRType.I8) },
                { "R15B", (120, IRType.I8) },
                
                // 8-bit register views (high bytes - only for legacy registers)
                { "AH", (1, IRType.I8) },
                { "CH", (9, IRType.I8) },
                { "DH", (17, IRType.I8) },
                { "BH", (25, IRType.I8) },
                
                // Condition code state
                { "CC_OP", (128, IRType.I64) },
                { "CC_DEP1", (136, IRType.I64) },
                { "CC_DEP2", (144, IRType.I64) },
                { "CC_NDEP", (152, IRType.I64) },
                
                // Individual flags
                { "DFLAG", (160, IRType.I64) },
                { "IDFLAG", (168, IRType.I64) },
                { "ACFLAG", (176, IRType.I64) },
                
                // Instruction pointer
                { "RIP", (184, IRType.I64) },
                
                // FS constant for TLS
                { "FS_CONST", (192, IRType.I64) },
                
                // FPU state
                { "FTOP", (200, IRType.I64) },
                { "FPROUND", (208, IRType.I64) },
                { "FC3210", (216, IRType.I64) },
                
                // SSE rounding
                { "SSEROUND", (224, IRType.I64) },
            };
        
        protected override void InitializeArchSpecific()
        {
            // Initialize general purpose registers
            GuestRAX = GuestRCX = GuestRDX = GuestRBX = 0;
            GuestRSP = GuestRBP = GuestRSI = GuestRDI = 0;
            GuestR8 = GuestR9 = GuestR10 = GuestR11 = 0;
            GuestR12 = GuestR13 = GuestR14 = GuestR15 = 0;
            
            // Initialize condition code state
            GuestCCOp = (uint)CCOp.Copy;  // Default to copy mode
            GuestCCDep1 = GuestCCDep2 = GuestCCNDep = 0;
            
            // Initialize flags
            GuestDFlag = 1;      // Forward direction (positive)
            GuestIDFlag = 0;     // ID flag clear
            GuestACFlag = 0;     // Alignment check disabled
            
            // Initialize instruction pointer
            GuestRIP = 0;
            
            // Initialize TLS support
            GuestFSConst = 0;
            
            // Initialize FPU state
            GuestFTop = 0;      // Stack top at register 0
            Pad1 = 0;
            for (int i = 0; i < 8; i++)
            {
                GuestFPReg[i] = 0;
                GuestFPTag[i] = 0;  // Empty
            }
            GuestFPRound = 0;  // Round to nearest (default)
            GuestFC3210 = 0;   // Clear condition codes
            
            // Initialize SSE/AVX state
            GuestSSERound = 0;  // Round to nearest
            for (int i = 0; i < 17; i++)  // 16 real + 1 temp
            {
                GuestYMM[i] = new V256(); // Zero
            }
            
            // Initialize control registers
            for (int i = 0; i < 16; i++)
            {
                GuestCR[i] = 0;
            }
        }
        
        public override IGuestState DeepCopy()
        {
            var copy = new AMD64GuestState();
            
            // Copy base class fields
            copy.HostEvCFailAddr = HostEvCFailAddr;
            copy.HostEvCCounter = HostEvCCounter;
            copy.GuestEmNote = GuestEmNote;
            copy.GuestCMStart = GuestCMStart;
            copy.GuestCMLen = GuestCMLen;
            copy.GuestNRAddr = GuestNRAddr;
            copy.GuestIPAtSyscall = GuestIPAtSyscall;
            
            // Copy AMD64-specific fields
            copy.GuestRAX = GuestRAX;
            copy.GuestRCX = GuestRCX;
            copy.GuestRDX = GuestRDX;
            copy.GuestRBX = GuestRBX;
            copy.GuestRSP = GuestRSP;
            copy.GuestRBP = GuestRBP;
            copy.GuestRSI = GuestRSI;
            copy.GuestRDI = GuestRDI;
            copy.GuestR8 = GuestR8;
            copy.GuestR9 = GuestR9;
            copy.GuestR10 = GuestR10;
            copy.GuestR11 = GuestR11;
            copy.GuestR12 = GuestR12;
            copy.GuestR13 = GuestR13;
            copy.GuestR14 = GuestR14;
            copy.GuestR15 = GuestR15;
            
            copy.GuestCCOp = GuestCCOp;
            copy.GuestCCDep1 = GuestCCDep1;
            copy.GuestCCDep2 = GuestCCDep2;
            copy.GuestCCNDep = GuestCCNDep;
            
            copy.GuestDFlag = GuestDFlag;
            copy.GuestIDFlag = GuestIDFlag;
            copy.GuestACFlag = GuestACFlag;
            copy.GuestRIP = GuestRIP;
            copy.GuestFSConst = GuestFSConst;
            
            // Deep copy arrays
            copy.GuestFTop = GuestFTop;
            copy.Pad1 = Pad1;
            Array.Copy(GuestFPReg, copy.GuestFPReg, 8);
            Array.Copy(GuestFPTag, copy.GuestFPTag, 8);
            copy.GuestFPRound = GuestFPRound;
            copy.GuestFC3210 = GuestFC3210;
            
            copy.GuestSSERound = GuestSSERound;
            for (int i = 0; i < 17; i++)
            {
                copy.GuestYMM[i] = GuestYMM[i]; // V256 should be value type
            }
            
            Array.Copy(GuestCR, copy.GuestCR, 16);
            
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
                    const int baseOffset = 400; // FP registers start around here
                    return baseOffset + (regNum * sizeof(UInt64));
                }
            }
            
            // Handle FP tag registers
            if (upperName.StartsWith("FPTAG"))
            {
                if (int.TryParse(upperName.Substring(5), out int regNum) && regNum >= 0 && regNum < 8)
                {
                    const int baseOffset = 464; // FP tags start around here
                    return baseOffset + regNum;
                }
            }
            
            // Handle YMM registers (includes XMM as lower 128 bits)
            if (upperName.StartsWith("YMM"))
            {
                if (int.TryParse(upperName.Substring(3), out int regNum) && regNum >= 0 && regNum < 17)
                {
                    const int baseOffset = 500; // YMM registers start around here
                    return baseOffset + (regNum * 32); // 32 bytes per YMM register
                }
            }
            
            // Handle XMM registers (map to lower 128 bits of YMM)
            if (upperName.StartsWith("XMM"))
            {
                if (int.TryParse(upperName.Substring(3), out int regNum) && regNum >= 0 && regNum < 16)
                {
                    const int baseOffset = 500; // Same as YMM 
                    return baseOffset + (regNum * 32); // Same offset as YMM
                }
            }
            
            // Handle control registers
            if (upperName.StartsWith("CR"))
            {
                if (int.TryParse(upperName.Substring(2), out int regNum) && regNum >= 0 && regNum < 16)
                {
                    var baseOffset = Marshal.OffsetOf<AMD64GuestState>(nameof(GuestCR)).ToInt32();
                    return baseOffset + (regNum * sizeof(UInt64));
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
            
            // Handle YMM registers
            if (upperName.StartsWith("YMM"))
            {
                return IRType.V256; // YMM registers are 256-bit vectors
            }
            
            // Handle XMM registers (accessed as 128-bit subset of YMM)
            if (upperName.StartsWith("XMM"))
            {
                return IRType.V128; // XMM registers are 128-bit vectors
            }
            
            // Handle control registers
            if (upperName.StartsWith("CR"))
            {
                return IRType.I64;  // Control registers are 64-bit
            }
            
            throw new ArgumentException($"Unknown register name: {registerName}");
        }
        
        public override bool RequiresPreciseMemoryExceptions(int minOffset, int maxOffset)
        {
            // For AMD64, require precise exceptions for RSP, RBP, and RIP
            var rspOffset = GetRegisterOffset("RSP");
            var rbpOffset = GetRegisterOffset("RBP");
            var ripOffset = GetRegisterOffset("RIP");
            
            // Check if range overlaps with critical registers
            return OverlapsRange(minOffset, maxOffset, rspOffset, rspOffset + 7) ||
                   OverlapsRange(minOffset, maxOffset, rbpOffset, rbpOffset + 7) ||
                   OverlapsRange(minOffset, maxOffset, ripOffset, ripOffset + 7);
        }
        
        private static bool OverlapsRange(int min1, int max1, int min2, int max2)
        {
            return min1 <= max2 && max1 >= min2;
        }
    }
}