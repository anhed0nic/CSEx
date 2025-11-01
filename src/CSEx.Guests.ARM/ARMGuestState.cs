using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using CSEx.Core;
using CSEx.Guests;
using CSEx.IR;

namespace CSEx.Guests.ARM
{
    /// <summary>
    /// ARM 32-bit guest CPU state representation.
    /// Corresponds to VexGuestARMState in original VEX.
    /// </summary>
    public class ARMGuestState : GuestStateBase
    {
        // General purpose registers (32-bit)
        public UInt32 GuestR0 { get; set; }
        public UInt32 GuestR1 { get; set; }
        public UInt32 GuestR2 { get; set; }
        public UInt32 GuestR3 { get; set; }
        public UInt32 GuestR4 { get; set; }
        public UInt32 GuestR5 { get; set; }
        public UInt32 GuestR6 { get; set; }
        public UInt32 GuestR7 { get; set; }
        public UInt32 GuestR8 { get; set; }
        public UInt32 GuestR9 { get; set; }
        public UInt32 GuestR10 { get; set; }
        public UInt32 GuestR11 { get; set; }
        public UInt32 GuestR12 { get; set; }
        public UInt32 GuestR13 { get; set; }  // Stack pointer
        public UInt32 GuestR14 { get; set; }  // Link register
        public UInt32 GuestR15T { get; set; } // PC + Thumb bit (bit 0 = Thumb mode)
        
        // ARM-specific condition code calculation state
        public UInt32 GuestCCOp { get; set; }      // ARM CC operation type
        public UInt32 GuestCCDep1 { get; set; }    // First dependency
        public UInt32 GuestCCDep2 { get; set; }    // Second dependency  
        public UInt32 GuestCCNDep { get; set; }    // Non-dependency
        
        // ARM-specific flags
        public UInt32 GuestQFlag32 { get; set; }   // Sticky saturation flag (Q bit)
        public UInt32 GuestGEFlag0 { get; set; }   // Greater-than-or-Equal flag 0
        public UInt32 GuestGEFlag1 { get; set; }   // Greater-than-or-Equal flag 1
        public UInt32 GuestGEFlag2 { get; set; }   // Greater-than-or-Equal flag 2
        public UInt32 GuestGEFlag3 { get; set; }   // Greater-than-or-Equal flag 3
        
        // VFP (Vector Floating Point) state - D0-D31 registers (64-bit each)
        public UInt64[] GuestD { get; set; } = new UInt64[32];
        
        // VFP control register
        public UInt32 GuestFPSCR { get; set; }
        
        public override IRType GuestWordSize => IRType.I32;
        public override IRType GuestIPType => IRType.I32;
        
        /// <summary>
        /// ARM condition code operations
        /// </summary>
        public enum ARMCCOp : uint
        {
            Copy = 0,          // CC_DEP1 contains NZCV flags directly
            Add32,             // Addition
            Sub32,             // Subtraction
            Adc32,             // Add with carry
            Sbc32,             // Subtract with carry
            Logic32,           // Logical operation (AND, OR, XOR)
            Mov32,             // Move operation
            Cmp32,             // Compare operation
            Tst32,             // Test operation
            Invalid = 0xFFFFFFFF
        }
        
        /// <summary>
        /// Register name to offset mapping for ARM
        /// </summary>
        private static readonly Dictionary<string, (int offset, IRType type)> RegisterMap = 
            new Dictionary<string, (int, IRType)>
            {
                // 32-bit general purpose registers
                { "R0", (0, IRType.I32) },
                { "R1", (4, IRType.I32) },
                { "R2", (8, IRType.I32) },
                { "R3", (12, IRType.I32) },
                { "R4", (16, IRType.I32) },
                { "R5", (20, IRType.I32) },
                { "R6", (24, IRType.I32) },
                { "R7", (28, IRType.I32) },
                { "R8", (32, IRType.I32) },
                { "R9", (36, IRType.I32) },
                { "R10", (40, IRType.I32) },
                { "R11", (44, IRType.I32) },
                { "R12", (48, IRType.I32) },
                { "R13", (52, IRType.I32) },
                { "R14", (56, IRType.I32) },
                { "R15T", (60, IRType.I32) },
                
                // Stack pointer and link register aliases
                { "SP", (52, IRType.I32) },  // Same as R13
                { "LR", (56, IRType.I32) },  // Same as R14
                { "PC", (60, IRType.I32) },  // Same as R15T
                
                // Condition code state
                { "CC_OP", (64, IRType.I32) },
                { "CC_DEP1", (68, IRType.I32) },
                { "CC_DEP2", (72, IRType.I32) },
                { "CC_NDEP", (76, IRType.I32) },
                
                // ARM-specific flags
                { "QFLAG32", (80, IRType.I32) },
                { "GEFLAG0", (84, IRType.I32) },
                { "GEFLAG1", (88, IRType.I32) },
                { "GEFLAG2", (92, IRType.I32) },
                { "GEFLAG3", (96, IRType.I32) },
                
                // VFP control
                { "FPSCR", (200, IRType.I32) },
            };
        
        protected override void InitializeArchSpecific()
        {
            // Initialize general purpose registers
            GuestR0 = GuestR1 = GuestR2 = GuestR3 = 0;
            GuestR4 = GuestR5 = GuestR6 = GuestR7 = 0;
            GuestR8 = GuestR9 = GuestR10 = GuestR11 = 0;
            GuestR12 = GuestR13 = GuestR14 = 0;
            GuestR15T = 0;  // ARM mode (T bit = 0)
            
            // Initialize condition code state
            GuestCCOp = (uint)ARMCCOp.Copy;  // Default to copy mode
            GuestCCDep1 = GuestCCDep2 = GuestCCNDep = 0;
            
            // Initialize ARM-specific flags
            GuestQFlag32 = 0;    // No saturation
            GuestGEFlag0 = GuestGEFlag1 = 0;
            GuestGEFlag2 = GuestGEFlag3 = 0;
            
            // Initialize VFP state
            for (int i = 0; i < 32; i++)
            {
                GuestD[i] = 0;
            }
            GuestFPSCR = 0;  // Default VFP control state
        }
        
        public override IGuestState DeepCopy()
        {
            var copy = new ARMGuestState();
            
            // Copy base class fields
            copy.HostEvCFailAddr = HostEvCFailAddr;
            copy.HostEvCCounter = HostEvCCounter;
            copy.GuestEmNote = GuestEmNote;
            copy.GuestCMStart = GuestCMStart;
            copy.GuestCMLen = GuestCMLen;
            copy.GuestNRAddr = GuestNRAddr;
            copy.GuestIPAtSyscall = GuestIPAtSyscall;
            
            // Copy ARM-specific fields
            copy.GuestR0 = GuestR0;
            copy.GuestR1 = GuestR1;
            copy.GuestR2 = GuestR2;
            copy.GuestR3 = GuestR3;
            copy.GuestR4 = GuestR4;
            copy.GuestR5 = GuestR5;
            copy.GuestR6 = GuestR6;
            copy.GuestR7 = GuestR7;
            copy.GuestR8 = GuestR8;
            copy.GuestR9 = GuestR9;
            copy.GuestR10 = GuestR10;
            copy.GuestR11 = GuestR11;
            copy.GuestR12 = GuestR12;
            copy.GuestR13 = GuestR13;
            copy.GuestR14 = GuestR14;
            copy.GuestR15T = GuestR15T;
            
            copy.GuestCCOp = GuestCCOp;
            copy.GuestCCDep1 = GuestCCDep1;
            copy.GuestCCDep2 = GuestCCDep2;
            copy.GuestCCNDep = GuestCCNDep;
            
            copy.GuestQFlag32 = GuestQFlag32;
            copy.GuestGEFlag0 = GuestGEFlag0;
            copy.GuestGEFlag1 = GuestGEFlag1;
            copy.GuestGEFlag2 = GuestGEFlag2;
            copy.GuestGEFlag3 = GuestGEFlag3;
            
            // Deep copy VFP registers
            Array.Copy(GuestD, copy.GuestD, 32);
            copy.GuestFPSCR = GuestFPSCR;
            
            return copy;
        }
        
        public override int GetRegisterOffset(string registerName)
        {
            var upperName = registerName.ToUpperInvariant();
            
            if (RegisterMap.TryGetValue(upperName, out var info))
            {
                return info.offset;
            }
            
            // Handle D registers (VFP double-precision)
            if (upperName.StartsWith("D"))
            {
                if (int.TryParse(upperName.Substring(1), out int regNum) && regNum >= 0 && regNum < 32)
                {
                    const int baseOffset = 100; // D registers start around here
                    return baseOffset + (regNum * sizeof(UInt64));
                }
            }
            
            // Handle S registers (VFP single-precision, map to halves of D registers)
            if (upperName.StartsWith("S"))
            {
                if (int.TryParse(upperName.Substring(1), out int regNum) && regNum >= 0 && regNum < 64)
                {
                    const int baseOffset = 100; // Same as D registers
                    var dRegNum = regNum / 2;
                    var isHighHalf = (regNum % 2) == 1;
                    return baseOffset + (dRegNum * sizeof(UInt64)) + (isHighHalf ? 4 : 0);
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
            
            // Handle D registers (64-bit)
            if (upperName.StartsWith("D"))
            {
                return IRType.I64;
            }
            
            // Handle S registers (32-bit)
            if (upperName.StartsWith("S"))
            {
                return IRType.F32;
            }
            
            throw new ArgumentException($"Unknown register name: {registerName}");
        }
        
        public override bool RequiresPreciseMemoryExceptions(int minOffset, int maxOffset)
        {
            // For ARM, require precise exceptions for SP (R13), LR (R14), and PC (R15T)
            var spOffset = GetRegisterOffset("SP");
            var lrOffset = GetRegisterOffset("LR");
            var pcOffset = GetRegisterOffset("PC");
            
            // Check if range overlaps with critical registers
            return OverlapsRange(minOffset, maxOffset, spOffset, spOffset + 3) ||
                   OverlapsRange(minOffset, maxOffset, lrOffset, lrOffset + 3) ||
                   OverlapsRange(minOffset, maxOffset, pcOffset, pcOffset + 3);
        }
        
        private static bool OverlapsRange(int min1, int max1, int min2, int max2)
        {
            return min1 <= max2 && max1 >= min2;
        }
        
        /// <summary>
        /// Helper to extract the PC value (without Thumb bit)
        /// </summary>
        public UInt32 GetPC()
        {
            return GuestR15T & 0xFFFFFFFE; // Clear Thumb bit
        }
        
        /// <summary>
        /// Helper to check if processor is in Thumb mode
        /// </summary>
        public bool IsThumbMode()
        {
            return (GuestR15T & 1) != 0;
        }
        
        /// <summary>
        /// Helper to set PC and Thumb mode
        /// </summary>
        public void SetPC(UInt32 address, bool thumbMode)
        {
            GuestR15T = address | (thumbMode ? 1u : 0u);
        }
    }
}