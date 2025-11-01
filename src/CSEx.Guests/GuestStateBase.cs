using System;
using CSEx.Core;
using CSEx.IR;

namespace CSEx.Guests
{
    /// <summary>
    /// Base interface for all guest CPU state representations.
    /// Provides common functionality needed across all architectures.
    /// </summary>
    public interface IGuestState
    {
        /// <summary>
        /// The guest word size (32 or 64 bits)
        /// </summary>
        IRType GuestWordSize { get; }
        
        /// <summary>
        /// The instruction pointer type for this architecture
        /// </summary>
        IRType GuestIPType { get; }
        
        /// <summary>
        /// Initialize the guest state to default values
        /// </summary>
        void Initialize();
        
        /// <summary>
        /// Create a deep copy of the guest state
        /// </summary>
        IGuestState DeepCopy();
        
        /// <summary>
        /// Get the offset of a guest register within the state structure
        /// </summary>
        /// <param name="registerName">The name of the register</param>
        /// <returns>Byte offset within the state structure</returns>
        int GetRegisterOffset(string registerName);
        
        /// <summary>
        /// Get the type of a guest register
        /// </summary>
        /// <param name="registerName">The name of the register</param>
        /// <returns>The IR type of the register</returns>
        IRType GetRegisterType(string registerName);
        
        /// <summary>
        /// Check if the architecture requires precise memory exceptions for a given offset range
        /// </summary>
        /// <param name="minOffset">Minimum byte offset in state</param>
        /// <param name="maxOffset">Maximum byte offset in state</param>
        /// <returns>True if precise exceptions are required</returns>
        bool RequiresPreciseMemoryExceptions(int minOffset, int maxOffset);
    }
    
    /// <summary>
    /// Base class for all guest CPU states providing common functionality
    /// </summary>
    public abstract class GuestStateBase : IGuestState
    {
        /// <summary>
        /// Event check fail address - used for debugging/monitoring
        /// </summary>
        public UInt64 HostEvCFailAddr { get; set; }
        
        /// <summary>
        /// Event check counter - used for debugging/monitoring
        /// </summary>
        public UInt32 HostEvCCounter { get; set; }
        
        /// <summary>
        /// Emulation notes - used to communicate special conditions
        /// </summary>
        public UInt32 GuestEmNote { get; set; }
        
        /// <summary>
        /// Cache management start address
        /// </summary>
        public UInt32 GuestCMStart { get; set; }
        
        /// <summary>
        /// Cache management length
        /// </summary>
        public UInt32 GuestCMLen { get; set; }
        
        /// <summary>
        /// No-redirection address - used for address translation debugging
        /// </summary>
        public UInt32 GuestNRAddr { get; set; }
        
        /// <summary>
        /// Instruction pointer at last syscall - used for syscall restart
        /// </summary>
        public UInt64 GuestIPAtSyscall { get; set; }
        
        /// <summary>
        /// The guest word size (32 or 64 bits)
        /// </summary>
        public abstract IRType GuestWordSize { get; }
        
        /// <summary>
        /// The instruction pointer type for this architecture
        /// </summary>
        public abstract IRType GuestIPType { get; }
        
        /// <summary>
        /// Initialize the guest state to default values
        /// </summary>
        public virtual void Initialize()
        {
            HostEvCFailAddr = 0;
            HostEvCCounter = 0;
            GuestEmNote = 0; // EmNote_NONE equivalent
            GuestCMStart = 0;
            GuestCMLen = 0;
            GuestNRAddr = 0;
            GuestIPAtSyscall = 0;
            
            InitializeArchSpecific();
        }
        
        /// <summary>
        /// Initialize architecture-specific registers
        /// </summary>
        protected abstract void InitializeArchSpecific();
        
        /// <summary>
        /// Create a deep copy of the guest state
        /// </summary>
        public abstract IGuestState DeepCopy();
        
        /// <summary>
        /// Get the offset of a guest register within the state structure
        /// </summary>
        /// <param name="registerName">The name of the register</param>
        /// <returns>Byte offset within the state structure</returns>
        public abstract int GetRegisterOffset(string registerName);
        
        /// <summary>
        /// Get the type of a guest register
        /// </summary>
        /// <param name="registerName">The name of the register</param>
        /// <returns>The IR type of the register</returns>
        public abstract IRType GetRegisterType(string registerName);
        
        /// <summary>
        /// Check if the architecture requires precise memory exceptions for a given offset range
        /// </summary>
        /// <param name="minOffset">Minimum byte offset in state</param>
        /// <param name="maxOffset">Maximum byte offset in state</param>
        /// <returns>True if precise exceptions are required</returns>
        public virtual bool RequiresPreciseMemoryExceptions(int minOffset, int maxOffset)
        {
            // By default, be conservative and require precise exceptions
            return true;
        }
    }
    
    /// <summary>
    /// Condition code operation types - used for lazy flag evaluation
    /// Common across x86/AMD64 architectures  
    /// </summary>
    public enum CCOp : uint
    {
        Copy = 0,  // CC_DEP1 contains the flags
        // Add operations
        AddB, AddW, AddL, AddQ,
        // Subtract operations  
        SubB, SubW, SubL, SubQ,
        // And operations
        AndB, AndW, AndL, AndQ,
        // Or operations
        OrB, OrW, OrL, OrQ,
        // Xor operations
        XorB, XorW, XorL, XorQ,
        // Shift left operations
        ShlB, ShlW, ShlL, ShlQ,
        // Shift right operations
        ShrB, ShrW, ShrL, ShrQ,
        // Shift arithmetic right operations
        SarB, SarW, SarL, SarQ,
        // More complex operations...
        Invalid = 0xFFFFFFFF
    }
}