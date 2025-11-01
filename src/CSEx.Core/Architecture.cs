namespace CSEx.Core
{
    /// <summary>
    /// VEX supported architectures (equivalent to VexArch)
    /// </summary>
    public enum VexArch : uint
    {
        Invalid = 0x400,
        X86,
        AMD64, 
        ARM,
        ARM64,
        PPC32,
        PPC64,
        S390X,
        MIPS32,
        MIPS64,
        TILEGX,
        RISCV64
    }

    /// <summary>
    /// Endianness specification (equivalent to VexEndness)
    /// </summary>
    public enum VexEndness : uint
    {
        Invalid = 0x600,    // unknown endianness
        LE,                 // little endian
        BE                  // big endian
    }

    /// <summary>
    /// Architecture information and capabilities
    /// </summary>
    public struct VexArchInfo
    {
        public VexEndness Endness;
        public uint HwCaps;

        public VexArchInfo(VexEndness endness, uint hwCaps = 0)
        {
            Endness = endness;
            HwCaps = hwCaps;
        }
    }

    /// <summary>
    /// Hardware capabilities for x86 architecture
    /// </summary>
    public static class VexHwCapsX86
    {
        public const uint MMXEXT = 1 << 1;  // A subset of SSE1 on early AMD
        public const uint SSE1 = 1 << 2;    // SSE1 support (Pentium III)
        public const uint SSE2 = 1 << 3;    // SSE2 support (Pentium 4)
        public const uint SSE3 = 1 << 4;    // SSE3 support (>= Prescott)
        public const uint LZCNT = 1 << 5;   // SSE4a LZCNT insn
    }

    /// <summary>
    /// Hardware capabilities for AMD64 architecture
    /// </summary>
    public static class VexHwCapsAMD64
    {
        public const uint SSE3 = 1 << 5;     // SSE3 support
        public const uint CX16 = 1 << 6;     // cmpxchg16b support
        public const uint LZCNT = 1 << 7;    // SSE4a LZCNT insn
        public const uint AVX = 1 << 8;      // AVX instructions
        public const uint RDTSCP = 1 << 9;   // RDTSCP instruction
    }

    /// <summary>
    /// Hardware capabilities for ARM architecture
    /// </summary>
    public static class VexHwCapsARM
    {
        public const uint VFP = 1 << 0;      // VFP support
        public const uint VFP2 = 1 << 1;     // VFP2 support
        public const uint VFP3 = 1 << 2;     // VFP3 support
        public const uint NEON = 1 << 3;     // NEON support
    }

    /// <summary>
    /// Cache information (equivalent to VexCache)
    /// </summary>
    public enum VexCacheKind : uint
    {
        DataCache = 0x500,
        InsnCache,
        UnifiedCache
    }

    public struct VexCache
    {
        public VexCacheKind Kind;
        public uint Level;        // level this cache is at, e.g. 1 for L1 cache
        public uint SizeB;        // size of this cache in bytes
        public uint LineSizeB;    // cache line size in bytes
        public uint Assoc;        // set associativity
        public bool IsTraceCache; // False, except for certain Pentium 4 models

        public VexCache(VexCacheKind kind, uint level, uint sizeB, uint lineSizeB, uint assoc, bool isTraceCache = false)
        {
            Kind = kind;
            Level = level;
            SizeB = sizeB;
            LineSizeB = lineSizeB;
            Assoc = assoc;
            IsTraceCache = isTraceCache;
        }
    }

    /// <summary>
    /// Cache system information
    /// </summary>
    public struct VexCacheInfo
    {
        public uint NumLevels;    // Number of cache levels
        public uint NumCaches;    // Total number of caches
        public VexCache[] Caches; // Array of cache descriptions
        public bool HasL2;        // Has L2 cache?
        public bool HasL3;        // Has L3 cache?

        public VexCacheInfo(VexCache[] caches)
        {
            Caches = caches;
            NumCaches = (uint)caches.Length;
            NumLevels = 0;
            HasL2 = false;
            HasL3 = false;

            foreach (var cache in caches)
            {
                if (cache.Level > NumLevels)
                    NumLevels = cache.Level;
                if (cache.Level == 2)
                    HasL2 = true;
                if (cache.Level == 3)
                    HasL3 = true;
            }
        }
    }

    /// <summary>
    /// Extension methods for architecture utilities
    /// </summary>
    public static class VexArchExtensions
    {
        /// <summary>
        /// Get human-readable name for architecture
        /// </summary>
        public static string GetName(this VexArch arch) => arch switch
        {
            VexArch.Invalid => "INVALID",
            VexArch.X86 => "X86",
            VexArch.AMD64 => "AMD64",
            VexArch.ARM => "ARM",
            VexArch.ARM64 => "ARM64",
            VexArch.PPC32 => "PPC32",
            VexArch.PPC64 => "PPC64",
            VexArch.S390X => "S390X",
            VexArch.MIPS32 => "MIPS32",
            VexArch.MIPS64 => "MIPS64",
            VexArch.TILEGX => "TILEGX",
            VexArch.RISCV64 => "RISCV64",
            _ => "VexArch???"
        };

        /// <summary>
        /// Get human-readable name for endianness
        /// </summary>
        public static string GetName(this VexEndness endness) => endness switch
        {
            VexEndness.Invalid => "INVALID",
            VexEndness.LE => "LittleEndian",
            VexEndness.BE => "BigEndian",
            _ => "VexEndness???"
        };

        /// <summary>
        /// Check if architecture is 64-bit
        /// </summary>
        public static bool Is64Bit(this VexArch arch) => arch switch
        {
            VexArch.AMD64 or VexArch.ARM64 or VexArch.PPC64 or 
            VexArch.MIPS64 or VexArch.S390X or VexArch.TILEGX or 
            VexArch.RISCV64 => true,
            _ => false
        };

        /// <summary>
        /// Get word size in bytes for architecture
        /// </summary>
        public static int GetWordSize(this VexArch arch) => arch.Is64Bit() ? 8 : 4;

        /// <summary>
        /// Get pointer size in bytes for architecture
        /// </summary>
        public static int GetPointerSize(this VexArch arch) => arch.GetWordSize();
    }
}