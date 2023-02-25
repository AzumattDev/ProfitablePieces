﻿using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

namespace ProfitablePieces;

[HarmonyPatch(typeof(Piece), nameof(Piece.DropResources))]
public static class PieceDropResourcesTranspiler
{
    private static MethodInfo _methodPieceIsPlacedByPlayer =
        AccessTools.Method(typeof(Piece), nameof(Piece.IsPlacedByPlayer));

    private static FieldInfo _fieldRequirementMRecover =
        AccessTools.Field(typeof(Piece.Requirement), nameof(Piece.Requirement.m_recover));

    /// <summary>
    /// This transpiler does two things:
    ///
    /// * First, it patches IsPlacedByPlayer to always return true inside Piece.DropResources, ensuring the resources that drop are
    ///   never less than the resources it cost to build the piece in the first place.
    /// * Second, some pieces are marked m_recover = false (e.g. never drop). We can patch out this check to ensure that all pieces
    ///   always drop even if they have been marked by the Valheim devs to never drop.
    /// </summary>
    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        if (ProfitablePiecesPlugin.ModEnabled.Value == ProfitablePiecesPlugin.Toggle.Off) return instructions;

        if(ProfitablePiecesPlugin.AdminOnly.Value == ProfitablePiecesPlugin.Toggle.On && !ProfitablePiecesPlugin.ConfigSyncVar.IsAdmin)
            return instructions;
        
        List<CodeInstruction> il = instructions.ToList();

        if (ProfitablePiecesPlugin.AlwaysDropResources.Value == ProfitablePiecesPlugin.Toggle.On)
        {
            // Patch out the call to Piece::IsPlacedByPlayer().
            // We want to always return true from this call site rather than whatever the original function does.
            // We can't hook the original function because the JIT inlines it.
            for (int i = 0; i < il.Count; ++i)
            {
                if (il[i].Calls(_methodPieceIsPlacedByPlayer))
                {
                    il[i] = new CodeInstruction(OpCodes.Ldc_I4_1, null); // replace with a true return value
                    il.RemoveAt(i - 1); // remove prev ldarg.0
                }
            }
        }

        if (ProfitablePiecesPlugin.AlwaysDropExcludedResources.Value == ProfitablePiecesPlugin.Toggle.Off)
        {
            // Patch out the m_recover check.
            for (int i = 0; i < il.Count; ++i)
            {
                if (il[i].LoadsField(_fieldRequirementMRecover))
                {
                    il.RemoveRange(i - 1, 3); // ldloc.3, ldfld, brfalse
                }
            }
        }

        return il.AsEnumerable();
    }
}