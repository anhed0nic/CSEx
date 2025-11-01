using System;
using System.Linq;

namespace CSEx.IR
{
    /// <summary>
    /// Type analysis utilities for IR expressions and statements
    /// Equivalent to VEX type checking functions
    /// </summary>
    public static class IRTypeAnalysis
    {
        /// <summary>
        /// Get the type of an IR constant
        /// </summary>
        public static IRType TypeOfConst(IRConst cons)
        {
            if (cons == null)
                throw new ArgumentNullException(nameof(cons));

            return cons.Tag switch
            {
                IRConstTag.U1 => IRType.I1,
                IRConstTag.U8 => IRType.I8,
                IRConstTag.U16 => IRType.I16,
                IRConstTag.U32 => IRType.I32,
                IRConstTag.U64 => IRType.I64,
                IRConstTag.F32 => IRType.F32,
                IRConstTag.F32i => IRType.F32,
                IRConstTag.F64 => IRType.F64,
                IRConstTag.F64i => IRType.F64,
                IRConstTag.V128 => IRType.V128,
                IRConstTag.V256 => IRType.V256,
                IRConstTag.V512 => IRType.V512,
                _ => throw new ArgumentException($"Unknown constant tag: {cons.Tag}")
            };
        }

        /// <summary>
        /// Get the type of a temporary from the type environment
        /// </summary>
        public static IRType TypeOfTemp(IRTypeEnv typeEnv, IRTemp temp)
        {
            if (typeEnv == null)
                throw new ArgumentNullException(nameof(typeEnv));

            return typeEnv.GetType(temp);
        }

        /// <summary>
        /// Get the type of an IR expression
        /// </summary>
        public static IRType TypeOfExpr(IRTypeEnv typeEnv, IRExpr expr)
        {
            if (typeEnv == null)
                throw new ArgumentNullException(nameof(typeEnv));
            if (expr == null)
                throw new ArgumentNullException(nameof(expr));

            return expr.Accept(new TypeCheckVisitor(typeEnv));
        }

        /// <summary>
        /// Get result type for Load-G operation
        /// </summary>
        public static (IRType resultType, IRType argType) TypeOfLoadGOp(IRLoadGOp op)
        {
            return op switch
            {
                IRLoadGOp.ILGop_INVALID => throw new ArgumentException("Invalid LoadG operation"),
                IRLoadGOp.ILGop_IdentV128 => (IRType.V128, IRType.V128),
                IRLoadGOp.ILGop_Ident64 => (IRType.I64, IRType.I64),
                IRLoadGOp.ILGop_Ident32 => (IRType.I32, IRType.I32),
                IRLoadGOp.ILGop_16Uto32 => (IRType.I32, IRType.I16),
                IRLoadGOp.ILGop_16Sto32 => (IRType.I32, IRType.I16),
                IRLoadGOp.ILGop_8Uto32 => (IRType.I32, IRType.I8),
                IRLoadGOp.ILGop_8Sto32 => (IRType.I32, IRType.I8),
                _ => throw new ArgumentException($"Unknown LoadG operation: {op}")
            };
        }

        /// <summary>
        /// Check if a type value is plausible (for sanity checking)
        /// </summary>
        public static bool IsPlausibleType(IRType type)
        {
            return type switch
            {
                IRType.Invalid => false,
                IRType.I1 or IRType.I8 or IRType.I16 or IRType.I32 or IRType.I64 or IRType.I128 => true,
                IRType.F16 or IRType.F32 or IRType.F64 or IRType.F128 => true,
                IRType.D32 or IRType.D64 or IRType.D128 => true,
                IRType.V128 or IRType.V256 or IRType.V512 => true,
                _ => false
            };
        }

        /// <summary>
        /// Check if an IR statement is "flat" (no nested expressions beyond simple forms)
        /// </summary>
        public static bool IsFlatStmt(IRStmt stmt)
        {
            if (stmt == null)
                throw new ArgumentNullException(nameof(stmt));

            // For now, implement a basic check - in full VEX this would be more complex
            // This is primarily used for sanity checking that IR is in a canonical form
            return stmt.Tag switch
            {
                IRStmtTag.NoOp => true,
                IRStmtTag.IMark => true,
                IRStmtTag.AbiHint => IsFlatExpr(stmt.Accept(new FlatCheckVisitor())),
                IRStmtTag.Put => IsFlatExpr(stmt.Accept(new FlatCheckVisitor())),
                IRStmtTag.PutI => true, // More complex checking would be needed
                IRStmtTag.WrTmp => IsFlatExpr(stmt.Accept(new FlatCheckVisitor())),
                IRStmtTag.Store => true, // More complex checking would be needed
                IRStmtTag.LoadG => true,
                IRStmtTag.StoreG => true,
                IRStmtTag.CAS => true,
                IRStmtTag.LLSC => true,
                IRStmtTag.Dirty => true,
                IRStmtTag.MBE => true,
                IRStmtTag.Exit => true,
                _ => false
            };
        }

        /// <summary>
        /// Check if an expression is "flat" (atomic)
        /// </summary>
        private static bool IsFlatExpr(IRExpr? expr)
        {
            if (expr == null) return true;

            return expr.Tag switch
            {
                IRExprTag.RdTmp => true,
                IRExprTag.Const => true,
                IRExprTag.Get => true,
                _ => false // More complex expressions are not "flat"
            };
        }

        // Helper methods for operation result types
        private static IRType TypeOfQop(IROp op)
        {
            // This would need to be implemented based on the specific Qop operations
            // For now, return a placeholder
            throw new NotImplementedException("Qop type analysis not yet implemented");
        }

        private static IRType TypeOfTriop(IROp op)
        {
            // This would need to be implemented based on the specific Triop operations
            // For now, return a placeholder
            throw new NotImplementedException("Triop type analysis not yet implemented");
        }

        private static IRType TypeOfBinop(IROp op)
        {
            // This would need to be implemented based on the specific binary operations
            // For now, return a placeholder
            throw new NotImplementedException("Binop type analysis not yet implemented");
        }

        private static IRType TypeOfUnop(IROp op)
        {
            // This would need to be implemented based on the specific unary operations
            // For now, return a placeholder
            throw new NotImplementedException("Unop type analysis not yet implemented");
        }

        /// <summary>
        /// Private visitor for flat checking
        /// </summary>
        private class FlatCheckVisitor : IIRStmtVisitor<IRExpr?>
        {
            public IRExpr? VisitNoOp(IRStmtNoOp stmt) => null;
            public IRExpr? VisitIMark(IRStmtIMark stmt) => null;
            public IRExpr? VisitAbiHint(IRStmtAbiHint stmt) => stmt.Base; // Check the base expression
            public IRExpr? VisitPut(IRStmtPut stmt) => stmt.Data; // Check the data expression
            public IRExpr? VisitPutI(IRStmtPutI stmt) => stmt.Details.Data; // Check the data expression via Details
            public IRExpr? VisitWrTmp(IRStmtWrTmp stmt) => stmt.Data; // Check the data expression
            public IRExpr? VisitStore(IRStmtStore stmt) => stmt.Data; // Check the data expression
            public IRExpr? VisitLoadG(IRStmtLoadG stmt) => null; // LoadG is always considered flat
            public IRExpr? VisitStoreG(IRStmtStoreG stmt) => null; // StoreG is always considered flat
            public IRExpr? VisitCAS(IRStmtCAS stmt) => null; // CAS is always considered flat
            public IRExpr? VisitLLSC(IRStmtLLSC stmt) => null; // LLSC is always considered flat
            public IRExpr? VisitDirty(IRStmtDirty stmt) => null; // Dirty is always considered flat
            public IRExpr? VisitMBE(IRStmtMBE stmt) => null; // MBE is always considered flat
            public IRExpr? VisitExit(IRStmtExit stmt) => stmt.Guard; // Check the guard expression
        }

        /// <summary>
        /// Private visitor for type checking expressions
        /// </summary>
        private class TypeCheckVisitor : IIRExprVisitor<IRType>
        {
            private readonly IRTypeEnv _typeEnv;

            public TypeCheckVisitor(IRTypeEnv typeEnv)
            {
                _typeEnv = typeEnv;
            }

            public IRType VisitBinder(IRExprBinder expr) =>
                throw new InvalidOperationException("Binder expressions should not appear in typed IR");

            public IRType VisitGet(IRExprGet expr) => expr.Type;

            public IRType VisitGetI(IRExprGetI expr) => expr.Descr.ElemType;

            public IRType VisitRdTmp(IRExprRdTmp expr) => TypeOfTemp(_typeEnv, expr.Tmp);

            public IRType VisitQop(IRExprQop expr) => TypeOfQop(expr.Op);

            public IRType VisitTriop(IRExprTriop expr) => TypeOfTriop(expr.Op);

            public IRType VisitBinop(IRExprBinop expr) => TypeOfBinop(expr.Op);

            public IRType VisitUnop(IRExprUnop expr) => TypeOfUnop(expr.Op);

            public IRType VisitLoad(IRExprLoad expr) => expr.Type;

            public IRType VisitConst(IRExprConst expr) => TypeOfConst(expr.Con);

            public IRType VisitITE(IRExprITE expr) => TypeOfExpr(_typeEnv, expr.IfTrue); // ITE result type = then/else type

            public IRType VisitCCall(IRExprCCall expr) => expr.RetTy;

            public IRType VisitVECRET(IRExprVECRET expr) =>
                throw new InvalidOperationException("VECRET expressions have no defined type");

            public IRType VisitGSPTR(IRExprGSPTR expr) => IRType.I64; // Guest state pointer is always 64-bit
        }
    }

    /// <summary>
    /// Sanity checking utilities for IRSB validation
    /// </summary>
    public static class IRSBSanityCheck
    {
        /// <summary>
        /// Perform sanity checks on an IRSB
        /// </summary>
        public static void SanityCheck(IRSB bb, string caller, bool requireFlatness, IRType guestWordSize)
        {
            if (bb == null)
                throw new ArgumentNullException(nameof(bb));

            // Check type environment consistency
            for (int i = 0; i < bb.TypeEnv.Count; i++)
            {
                var temp = new IRTemp((uint)i);
                var type = bb.TypeEnv.GetType(temp);
                
                if (!IRTypeAnalysis.IsPlausibleType(type))
                    throw new InvalidOperationException($"{caller}: Invalid type {type} for temporary t{i}");
            }

            // Check each statement
            for (int i = 0; i < bb.StatementCount; i++)
            {
                var stmt = bb.Statements[i];
                
                if (stmt == null)
                    throw new InvalidOperationException($"{caller}: Statement {i} is null");

                if (requireFlatness && !IRTypeAnalysis.IsFlatStmt(stmt))
                    throw new InvalidOperationException($"{caller}: Statement {i} is not flat: {stmt}");

                // Additional statement-specific checks could be added here
            }

            // Check exit information
            if (bb.Next != null)
            {
                try
                {
                    var nextType = IRTypeAnalysis.TypeOfExpr(bb.TypeEnv, bb.Next);
                    if (nextType != guestWordSize)
                    {
                        throw new InvalidOperationException(
                            $"{caller}: Next expression has type {nextType}, expected {guestWordSize}");
                    }
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException($"{caller}: Error analyzing next expression: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Basic sanity check with common defaults
        /// </summary>
        public static void SanityCheck(IRSB bb, string caller = "SanityCheck")
        {
            SanityCheck(bb, caller, requireFlatness: false, guestWordSize: IRType.I64);
        }
    }
}