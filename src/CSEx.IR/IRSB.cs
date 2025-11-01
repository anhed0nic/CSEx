using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CSEx.Core;

namespace CSEx.IR
{
    /// <summary>
    /// IR Type Environment - manages types for IR temporaries
    /// Equivalent to VEX IRTypeEnv
    /// </summary>
    public sealed class IRTypeEnv : IEquatable<IRTypeEnv>
    {
        private readonly List<IRType> _types;

        /// <summary>
        /// Initialize empty type environment
        /// </summary>
        public IRTypeEnv()
        {
            _types = new List<IRType>();
        }

        /// <summary>
        /// Initialize with specific capacity
        /// </summary>
        public IRTypeEnv(int capacity)
        {
            _types = new List<IRType>(capacity);
        }

        /// <summary>
        /// Copy constructor
        /// </summary>
        private IRTypeEnv(IEnumerable<IRType> types)
        {
            _types = new List<IRType>(types);
        }

        /// <summary>
        /// Number of temporaries defined
        /// </summary>
        public int Count => _types.Count;

        /// <summary>
        /// Get type of a temporary
        /// </summary>
        public IRType GetType(IRTemp temp)
        {
            if (temp.Value >= _types.Count)
                throw new ArgumentOutOfRangeException(nameof(temp), $"Temporary t{temp.Value} not defined in type environment");
            
            return _types[(int)temp.Value];
        }

        /// <summary>
        /// Create a new temporary with the given type
        /// </summary>
        public IRTemp NewTemp(IRType type)
        {
            var temp = new IRTemp((uint)_types.Count);
            _types.Add(type);
            return temp;
        }

        /// <summary>
        /// Add a temporary with explicit type (for reconstruction)
        /// </summary>
        public void AddTemp(IRType type)
        {
            _types.Add(type);
        }

        /// <summary>
        /// Get all types in order
        /// </summary>
        public IReadOnlyList<IRType> Types => _types.AsReadOnly();

        /// <summary>
        /// Check if a temporary is defined
        /// </summary>
        public bool IsTempDefined(IRTemp temp) => temp.Value < _types.Count;

        /// <summary>
        /// Deep copy the type environment
        /// </summary>
        public IRTypeEnv DeepCopy() => new IRTypeEnv(_types);

        /// <summary>
        /// Pretty print the type environment
        /// </summary>
        public string PrettyPrint()
        {
            var sb = new StringBuilder();
            sb.AppendLine("Type Environment:");
            for (int i = 0; i < _types.Count; i++)
            {
                sb.AppendLine($"  t{i} : {_types[i].PrettyPrint()}");
            }
            return sb.ToString();
        }

        public bool Equals(IRTypeEnv? other) =>
            other != null && _types.SequenceEqual(other._types);

        public override bool Equals(object? obj) =>
            obj is IRTypeEnv other && Equals(other);

        public override int GetHashCode()
        {
            var hash = new HashCode();
            foreach (var type in _types)
                hash.Add(type);
            return hash.ToHashCode();
        }

        public override string ToString() => PrettyPrint();

        public static bool operator ==(IRTypeEnv? left, IRTypeEnv? right) => 
            left?.Equals(right) ?? right is null;

        public static bool operator !=(IRTypeEnv? left, IRTypeEnv? right) => 
            !(left == right);
    }

    /// <summary>
    /// IR Super Block - a single-entry, multiple-exit sequence of statements
    /// Equivalent to VEX IRSB
    /// </summary>
    public sealed class IRSB : IEquatable<IRSB>
    {
        private readonly List<IRStmt> _statements;

        /// <summary>
        /// Type environment for temporaries
        /// </summary>
        public IRTypeEnv TypeEnv { get; }

        /// <summary>
        /// Next destination if block executes to completion
        /// </summary>
        public IRExpr? Next { get; set; }

        /// <summary>
        /// Jump kind for final exit
        /// </summary>
        public IRJumpKind JumpKind { get; set; }

        /// <summary>
        /// Guest state offset for instruction pointer
        /// </summary>
        public int OffsIP { get; set; }

        /// <summary>
        /// Read-only view of statements
        /// </summary>
        public IReadOnlyList<IRStmt> Statements => _statements.AsReadOnly();

        /// <summary>
        /// Create empty IRSB
        /// </summary>
        public IRSB()
        {
            TypeEnv = new IRTypeEnv();
            _statements = new List<IRStmt>();
            Next = null;
            JumpKind = IRJumpKind.Boring;
            OffsIP = 0;
        }

        /// <summary>
        /// Create IRSB with specified type environment
        /// </summary>
        public IRSB(IRTypeEnv typeEnv)
        {
            TypeEnv = typeEnv ?? throw new ArgumentNullException(nameof(typeEnv));
            _statements = new List<IRStmt>();
            Next = null;
            JumpKind = IRJumpKind.Boring;
            OffsIP = 0;
        }

        /// <summary>
        /// Create IRSB with capacity hint
        /// </summary>
        public IRSB(int capacity)
        {
            TypeEnv = new IRTypeEnv();
            _statements = new List<IRStmt>(capacity);
            Next = null;
            JumpKind = IRJumpKind.Boring;
            OffsIP = 0;
        }

        /// <summary>
        /// Copy constructor for deep copy
        /// </summary>
        private IRSB(IRTypeEnv typeEnv, IEnumerable<IRStmt> statements, IRExpr? next, IRJumpKind jumpKind, int offsIP)
        {
            TypeEnv = typeEnv;
            _statements = new List<IRStmt>(statements);
            Next = next;
            JumpKind = jumpKind;
            OffsIP = offsIP;
        }

        /// <summary>
        /// Add a statement to the IRSB
        /// </summary>
        public void AddStatement(IRStmt stmt)
        {
            if (stmt == null)
                throw new ArgumentNullException(nameof(stmt));
            _statements.Add(stmt);
        }

        /// <summary>
        /// Add multiple statements
        /// </summary>
        public void AddStatements(IEnumerable<IRStmt> statements)
        {
            if (statements == null)
                throw new ArgumentNullException(nameof(statements));
            
            foreach (var stmt in statements)
            {
                if (stmt == null)
                    throw new ArgumentException("Statement cannot be null", nameof(statements));
                _statements.Add(stmt);
            }
        }

        /// <summary>
        /// Insert statement at specific index
        /// </summary>
        public void InsertStatement(int index, IRStmt stmt)
        {
            if (stmt == null)
                throw new ArgumentNullException(nameof(stmt));
            _statements.Insert(index, stmt);
        }

        /// <summary>
        /// Remove statement at index
        /// </summary>
        public void RemoveStatementAt(int index)
        {
            _statements.RemoveAt(index);
        }

        /// <summary>
        /// Replace statement at index
        /// </summary>
        public void ReplaceStatement(int index, IRStmt stmt)
        {
            if (stmt == null)
                throw new ArgumentNullException(nameof(stmt));
            _statements[index] = stmt;
        }

        /// <summary>
        /// Clear all statements
        /// </summary>
        public void ClearStatements()
        {
            _statements.Clear();
        }

        /// <summary>
        /// Number of statements
        /// </summary>
        public int StatementCount => _statements.Count;

        /// <summary>
        /// Create a new temporary in the type environment
        /// </summary>
        public IRTemp NewTemp(IRType type) => TypeEnv.NewTemp(type);

        /// <summary>
        /// Get type of a temporary
        /// </summary>
        public IRType TypeOfTemp(IRTemp temp) => TypeEnv.GetType(temp);

        /// <summary>
        /// Deep copy the IRSB
        /// </summary>
        public IRSB DeepCopy()
        {
            var typeEnvCopy = TypeEnv.DeepCopy();
            var statementsCopy = _statements.Select(s => s.DeepCopy());
            var nextCopy = Next?.DeepCopy();
            
            return new IRSB(typeEnvCopy, statementsCopy, nextCopy, JumpKind, OffsIP);
        }

        /// <summary>
        /// Deep copy IRSB except statements (for optimization passes)
        /// </summary>
        public IRSB DeepCopyExceptStatements()
        {
            var typeEnvCopy = TypeEnv.DeepCopy();
            var nextCopy = Next?.DeepCopy();
            
            return new IRSB(typeEnvCopy, Enumerable.Empty<IRStmt>(), nextCopy, JumpKind, OffsIP);
        }

        /// <summary>
        /// Pretty print the IRSB
        /// </summary>
        public string PrettyPrint()
        {
            var sb = new StringBuilder();
            
            // Print type environment
            sb.AppendLine("------ Type Environment ------");
            foreach (var line in TypeEnv.PrettyPrint().Split('\n', StringSplitOptions.RemoveEmptyEntries).Skip(1))
            {
                sb.AppendLine(line);
            }
            
            // Print statements
            sb.AppendLine();
            sb.AppendLine("------ Statements ------");
            for (int i = 0; i < _statements.Count; i++)
            {
                sb.AppendLine($"{i:00}: {_statements[i].PrettyPrint()}");
            }
            
            // Print exit information
            sb.AppendLine();
            sb.AppendLine("------ Exit ------");
            if (Next != null)
            {
                sb.AppendLine($"Next: {Next.PrettyPrint()}");
                sb.AppendLine($"Jump Kind: {JumpKind}");
                sb.AppendLine($"IP Offset: {OffsIP}");
            }
            else
            {
                sb.AppendLine("No exit information");
            }
            
            return sb.ToString();
        }

        public bool Equals(IRSB? other)
        {
            if (other == null) return false;
            
            return TypeEnv.Equals(other.TypeEnv) &&
                   _statements.SequenceEqual(other._statements) &&
                   ((Next == null && other.Next == null) || (Next?.Equals(other.Next) == true)) &&
                   JumpKind == other.JumpKind &&
                   OffsIP == other.OffsIP;
        }

        public override bool Equals(object? obj) =>
            obj is IRSB other && Equals(other);

        public override int GetHashCode()
        {
            var hash = new HashCode();
            hash.Add(TypeEnv);
            foreach (var stmt in _statements)
                hash.Add(stmt);
            hash.Add(Next);
            hash.Add(JumpKind);
            hash.Add(OffsIP);
            return hash.ToHashCode();
        }

        public override string ToString() => PrettyPrint();

        public static bool operator ==(IRSB? left, IRSB? right) => 
            left?.Equals(right) ?? right is null;

        public static bool operator !=(IRSB? left, IRSB? right) => 
            !(left == right);
    }

    /// <summary>
    /// Factory class for creating IRSBs
    /// </summary>
    public static class IRSBFactory
    {
        /// <summary>
        /// Create an empty IRSB
        /// </summary>
        public static IRSB Empty() => new IRSB();

        /// <summary>
        /// Create an empty IRSB with capacity hint
        /// </summary>
        public static IRSB Empty(int capacity) => new IRSB(capacity);

        /// <summary>
        /// Create an IRSB with a pre-existing type environment
        /// </summary>
        public static IRSB WithTypeEnv(IRTypeEnv typeEnv) => new IRSB(typeEnv);
    }
}