using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using HarmonyLib;
using JetBrains.Annotations;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

#nullable enable
namespace UniversalFermenterSK
{
    [StaticConstructorOnStartup]
    public static class HarmonyPatches
    {
        static HarmonyPatches()
        {
            Harmony harmony = new("Syrchalis.Rimworld.UniversalFermenter");
            harmony.PatchAll(Assembly.GetExecutingAssembly());

            // Patch for WhatsMissing
            if (ModsConfig.IsActive("revolus.whatsmissing"))
            {
                Type WhatsMissingMod = AccessTools.TypeByName("Revolus.WhatsMissing.WhatsMissingMod");

                var method = AccessTools.Method(WhatsMissingMod, "Patch__Dialog_BillConfig__DoWindowContents__Mixin");
                var transpiler = AccessTools.Method(typeof(WhatsMissingMod_Patch_For_Mixin), "Transpiler");
                harmony.Patch(method, null, null, new HarmonyMethod(transpiler));
            }
        }
    }

    [HarmonyPatch(typeof(Building_FermentingBarrel), nameof(Building_FermentingBarrel.GetInspectString))]
    public static class OldBarrel_GetInspectStringPatch
    {
        public static bool Prefix(ref string __result)
        {
            __result = "UF_OldBarrelInspectString".Translate();
            return false;
        }
    }

    /// <summary>
    /// Dialog_BillConfig.DoWindowContents patch to adopt it to RecipeDef_UF. It disables selection of pawns and skill.
    /// 
    /// It wrapping code
    ///   Listing_Standard listing_Standard4 = listing_Standard.BeginSection((float)Dialog_BillConfig.WorkerSelectionSubdialogHeight);
    ///   ...
    ///   listing_Standard.EndSection(listing_Standard4);
    /// as
    ///   if (!(this.bill.recipe is RecipeDef_UF)) {
    ///     ...
    ///   }
    /// </summary>
    [HarmonyPatch(typeof(Dialog_BillConfig))] //changed in 1.4
    [HarmonyPatch("DoWindowContents")]
    public static class Dialog_BillConfig_DoWindowContents_Patch
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            var codes = new List<CodeInstruction>(instructions);

            FieldInfo? WorkerSelectionSubdialogHeightField = typeof(Dialog_BillConfig).GetField("WorkerSelectionSubdialogHeight", BindingFlags.NonPublic | BindingFlags.Static);
            if (WorkerSelectionSubdialogHeightField == null)
                throw new Exception($"Error on patching Dialog_BillConfig.DoWindowContents: can not get Dialog_BillConfig.WorkerSelectionSubdialogHeight field by reflection");

            MethodInfo? endMethod = typeof(Listing).GetMethod("End");
            if (endMethod == null)
                throw new Exception($"Error on patching Dialog_BillConfig.DoWindowContents: can not get Listing.End method by reflection");

            bool startFound = false;
            bool endFound = false;
            for (var i = 0; i < codes.Count - 1; i++)
            {
                if (!startFound &&
                    codes[i + 1].opcode == OpCodes.Ldsfld &&
                    codes[i + 1].operand is FieldInfo fieldInfo &&
                    fieldInfo == WorkerSelectionSubdialogHeightField)
                {
                    startFound = true;
                    var to_end = generator.DefineLabel();

                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return new CodeInstruction(OpCodes.Ldfld, typeof(Dialog_BillConfig).GetField("bill", BindingFlags.NonPublic | BindingFlags.Instance));
                    yield return new CodeInstruction(OpCodes.Ldfld, typeof(Bill).GetField("recipe"));
                    yield return new CodeInstruction(OpCodes.Isinst, typeof(RecipeDef_UF));

                    yield return new CodeInstruction(OpCodes.Brtrue_S, to_end);

                    // Find listing_Standard.End();
                    for (var j = i + 1; j < codes.Count - 1; j++)
                    {
                        if (codes[j].opcode == OpCodes.Ldloc_S &&
                            codes[j + 1].opcode == OpCodes.Callvirt &&
                            codes[j + 1].operand is MethodInfo methodInfo &&
                            methodInfo == endMethod)
                        {
                            codes[j].labels.Add(to_end);
                            endFound = true;
                            break;
                        }
                    }
                }

                yield return codes[i];
            }

            if (!startFound || !endFound)
                throw new Exception($"Error on patching Dialog_BillConfig.DoWindowContents: startFound{startFound} endFound{endFound}");

            yield return codes[codes.Count - 1];
        }
    }

    /// <summary>
    /// Dialog_BillConfig.DoWindowContents patch to adopt it to RecipeDef_UF. It add RecipeDef_UF fields to info string.
    /// 
    /// It add code
    ///   if (this.bill.recipe is RecipeDef_UF recipe)
    ///       stringBuilder.AppendLine(recipe.Dialog_BillConfigExtString());
    /// before
    ///   string text4 = stringBuilder.ToString();
    /// </summary>
    [HarmonyPatch(typeof(Dialog_BillConfig))]
    [HarmonyPatch("DoWindowContents")]
    public static class Dialog_BillConfig_DoWindowContents_Patch2
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            var codes = new List<CodeInstruction>(instructions);

            bool startFound = false;
            yield return codes[0];
            yield return codes[1];
            yield return codes[2];
            yield return codes[3];
            for (var i = 4; i < codes.Count; i++)
            {
                if (!startFound &&
                    codes[i - 4].opcode == OpCodes.Callvirt &&
                    codes[i - 3].opcode == OpCodes.Pop &&
                    codes[i - 2].opcode == OpCodes.Ldc_I4_1 &&
                    codes[i - 1].opcode == OpCodes.Call &&
                    codes[i].opcode == OpCodes.Ldloc_S
                    )
                {
                    startFound = true;

                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(Dialog_BillConfig), "bill"));
                    yield return codes[i].Clone(); // ldloc.s   stringBuilder
                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(Dialog_BillConfig_DoWindowContents_Patch2), nameof(Dialog_BillConfig_DoWindowContents_Patch2.Mixin)));
                }

                yield return codes[i];
            }

            if (!startFound)
                throw new Exception($"Error on patching Dialog_BillConfig.DoWindowContents2: can not found required ILCode.");
        }

        static void Mixin(Bill_Production bill, StringBuilder stringBuilder)
        {
            if (bill.recipe is RecipeDef_UF recipe)
                stringBuilder.AppendLine(recipe.Dialog_BillConfigExtString());
        }
    }

    /// <summary>
    /// WhatsMissingMod.Patch__Dialog_BillConfig__DoWindowContents__Mixin patch to adopt it to RecipeDef_UF. It add RecipeDef_UF fields to info string.
    /// 
    /// It add code
    ///   if (bill.recipe is RecipeDef_UF recipe)
    ///       listing.Label(recipe.Dialog_BillConfigExtString() + "\n");
    /// before
    ///   listing.Label("Requires (see tooltips, ingredients can be clicked):", -1f, null);
    /// </summary>
    public static class WhatsMissingMod_Patch_For_Mixin
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            var codes = new List<CodeInstruction>(instructions);

            bool startFound = false;
            for (var i = 0; i < codes.Count - 1; i++)
            {
                if (!startFound &&
                    codes[i].opcode == OpCodes.Ldarg_1 &&
                    codes[i + 1].opcode == OpCodes.Ldstr
                    )
                {
                    startFound = true;

                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return new CodeInstruction(OpCodes.Ldarg_1);
                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(WhatsMissingMod_Patch_For_Mixin), nameof(WhatsMissingMod_Patch_For_Mixin.Mixin2)));
                }

                yield return codes[i];
            }

            yield return codes[codes.Count - 1];

            if (!startFound)
                throw new Exception($"Error on patching WhatsMissingMod.Patch__Dialog_BillConfig__DoWindowContents__Mixin: can not found required ILCode.");
        }

        static void Mixin2(Bill_Production bill, Listing_Standard listing)
        {
            if (bill.recipe is RecipeDef_UF recipe)
                listing.Label(recipe.Dialog_BillConfigExtString() + "\n");
        }
    }

    // This patch set progress.CurrentQuality at GenRecipe.PostProcessProduct and remove unnecessary notification.
    [HarmonyPatch(typeof(GenRecipe), "PostProcessProduct")]
    public static class GenRecipe_PostProcessProductPatch
    {
        public static bool Prefix(ref Thing product, ref Pawn worker, out CompQuality? __state)
        {
            __state = null;

            if (worker.jobs.curDriver is JobDriver_TakeProductOutOfUF)
            {
                CompQuality? compQuality = product.TryGetComp<CompQuality>();
                if (compQuality != null)
                {
                    // Remove CompQuality to prevent call QualityUtility.SendCraftNotification by vanilla
                    ((ThingWithComps)product).AllComps.Remove(compQuality);
                    __state = compQuality;
                }
            }

            return true;
        }

        public static void Postfix(ref Thing product, ref Pawn worker, CompQuality? __state)
        {
            if (__state != null)
            {
                //Log.Message("__state is: " + __state.ToString());
                Job curJob = worker.jobs.curJob;
                //Log.Message("curJob is: " + curJob.def.defName);
                //Log.Message("jobGiver is: " + curJob.jobGiver.parent.ToString());
                //Log.Message("targetA is: " + curJob.targetA.ToString());
                //Log.Message("targetB is: " + curJob.targetB.ToString());
                //Log.Message("targetC is: " + curJob.targetC.ToString());
                Building_UF? fermenter = null;
                if (curJob.targetA.Thing is Building_UF)
                {
                    fermenter = curJob.targetA.Thing as Building_UF;
                }

                //var fermenter = (Building_UF)curJob.bill.billStack.billGiver;
                //Log.Message("fermenter is: " + fermenter == null ? "Null" : fermenter.def.defName);
                if (fermenter != null)
                {
                    UF_Progress? progress = fermenter.progresses.First(x => x.Finished || x.Ruined);
                    //Log.Message("Got here");
                    //Log.Message("progress is: " + progress == null ? "Null" : progress.Process.defName);

                    // Restore CompQuality
                    ((ThingWithComps)product).AllComps.Add(__state);

                    // Set quality
                    __state.SetQuality(progress.CurrentQuality, ArtGenerationContext.Colony);
                }

            }
        }
    }

    /// <summary>
    /// Toils_Haul.PlaceHauledThingInCell patch to use UF_DefOf.FillUniversalFermenter
    /// 
    /// It modify code
    ///   if (curJob.def == JobDefOf.DoBill || curJob.def == JobDefOf.RefuelAtomic || curJob.def == JobDefOf.RearmTurretAtomic)
    /// to
    ///   if (curJob.def == JobDefOf.DoBill || curJob.def == UF_DefOf.FillUniversalFermenter || curJob.def == JobDefOf.RefuelAtomic || curJob.def == JobDefOf.RearmTurretAtomic)
    /// </summary>
    [HarmonyPatch]
    public static class Toils_Haul_PlaceHauledThingInCell_Patch
    {
        [UsedImplicitly]
        public static MethodBase TargetMethod()
        {
            var type = AccessTools.Inner(typeof(Toils_Haul), "<>c__DisplayClass6_0");
            return AccessTools.Method(type, "<PlaceHauledThingInCell>b__0");
        }

        [HarmonyTranspiler]
        [UsedImplicitly]
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var codes = new List<CodeInstruction>(instructions);

            FieldInfo? DoBillField = typeof(JobDefOf).GetField("DoBill", BindingFlags.Public | BindingFlags.Static);
            if (DoBillField == null)
                throw new Exception($"Error on patching Toils_Haul.PlaceHauledThingInCell: can not get JobDefOf.DoBill field by reflection");

            FieldInfo? FillUniversalFermenterField = typeof(UF_DefOf).GetField("FillUniversalFermenter", BindingFlags.Public | BindingFlags.Static);
            if (FillUniversalFermenterField == null)
                throw new Exception($"Error on patching Toils_Haul.PlaceHauledThingInCell: can not get UF_DefOf.FillUniversalFermenter field by reflection");

            bool patched = false;
            for (var i = 0; i < codes.Count; i++)
            {
                if (!patched &&
                    codes[i].opcode == OpCodes.Ldsfld &&
                    codes[i].operand is FieldInfo fieldInfo &&
                    fieldInfo == DoBillField)
                {
                    patched = true;
                    yield return codes[i];                                                          // ldsfld JobDefOf::DoBill
                    yield return codes[i + 1];                                                      // beq.s     IL_00E3
                    i++;

                    yield return codes[i - 4];                                                      // ldloc_0
                    yield return codes[i - 3];                                                      // ldfld Toils_Haul::curJob
                    yield return codes[i - 2];                                                      // ldfld Job::def
                    yield return new CodeInstruction(OpCodes.Ldsfld, FillUniversalFermenterField);  // ldsfld UF_DefOf.FillUniversalFermenter
                    yield return codes[i];                                                          // beq.s     IL_00E3
                }
                else
                    yield return codes[i];
            }

            if (!patched)
                throw new Exception($"Error on patching Toils_Haul.PlaceHauledThingInCell: can not found required ILCode.");
        }
    }

    // This patch return billGiver.Position in WorkGiver_DoBill.GetBillGiverRootCell if billGiver is Building_UF.
    // It disables interaction cell for WorkGiver_FillUF.
    [HarmonyPatch(typeof(WorkGiver_DoBill), "GetBillGiverRootCell")]
    public static class WorkGiver_DoBill_GetBillGiverRootCellPatch
    {
        public static bool Prefix(ref Thing billGiver, ref IntVec3 __result)
        {
            if (billGiver is Building_UF)
            {
                __result = billGiver.Position;
                return false;
            }

            return true;
        }
    }

    // This patch return billGiver.Position in WorkGiver_DoBill.GetBillGiverRootCell if billGiver is Building_UF.
    // It disables interaction cell for WorkGiver_FillUF.
    /*
[HarmonyPatch(typeof(WorkGiver_DoBill), "TryFindBestIngredientsHelper")]
public static class WorkGiver_DoBill_TryFindBestIngredientsHelperPatch
{
        public static void Prefix(bool __result, WorkGiver_DoBill __instance, Predicate<Thing> thingValidator,
            Predicate<List<Thing>> foundAllIngredientsAndChoose,
            List<IngredientCount> ingredients,
            Pawn pawn,
            Thing billGiver,
            List<ThingCount> chosen,
            float searchRadius)
        {
            //Log.Message("FoundAll: " + __result);
            foreach (var item in ingredients)
            {
                if (item.filter.Summary == "ingredients")
                {
                    foreach (var item2 in item.filter.AllowedThingDefs)
                    {
                        Log.Message("ingredient: " + item2.defName);
                    }
                }
                else
                {
                    Log.Message("ingredient: " + item.filter.Summary);
                }
            }
            //Log.Message("pawn: " + pawn.Name);
            //Log.Message("billGiver: " + billGiver.def.defName);
            foreach (var item in chosen)
            {
                Log.Message("chosen: " + item.Thing.def.defName);
            }
            if (chosen.EnumerableNullOrEmpty())
                Log.Message("Missing Ingredients Empty");
            //Log.Message("searchRadius: " + searchRadius);
            return;
        }

        public static void Postfix(bool __result, WorkGiver_DoBill __instance, Predicate<Thing> thingValidator,
        Predicate<List<Thing>> foundAllIngredientsAndChoose,
        List<IngredientCount> ingredients,
        Pawn pawn,
        Thing billGiver,
        List<ThingCount> chosen,
        float searchRadius)
    {
  
                    Log.Message("FoundAll: " + __result);
        foreach (var item in ingredients)
        {
            if (item.filter.Summary == "ingredients")
            {
                foreach (var item2 in item.filter.AllowedThingDefs)
                {
                    Log.Message("ingredient 2: " + item2.defName);
                }
            }
            else
            {
                Log.Message("ingredient 2: " + item.filter.Summary);
            }
        }
            
            Log.Message("pawn: " + pawn.Name);
            Log.Message("billGiver: " + billGiver.def.defName);
            foreach (var item in chosen)
            {
                Log.Message("chosen 2: " + item.Thing.def.defName);
            }
            if (chosen.EnumerableNullOrEmpty())
                Log.Message("Missing Ingredients 2 Empty");
            //Log.Message("searchRadius: " + searchRadius);
            return;
    }
}*/
    /*
    [HarmonyPatch(typeof(ReservationManager), "CanReserve")]
    public static class Pawn_CanReservePatch
    {
        public static bool Postfix(bool __result, Pawn claimant,
            LocalTargetInfo target,
            int maxPawns = 1,
            int stackCount = -1,
            ReservationLayerDef layer = null,
            bool ignoreOtherReservations = false)
        {
            Log.Message("result: " + __result);
            Log.Message("thing: " + target.Thing.def.defName);
            Log.Message("stackCount: " + stackCount);
            Log.Message("ignoreOtherReservations: " + ignoreOtherReservations);

            return false;
        }
    }
    */
    // This patch return billGiver.Position in WorkGiver_DoBill.GetBillGiverRootCell if billGiver is Building_UF.
    // It disables interaction cell for WorkGiver_FillUF.
    /*
    [HarmonyPatch(typeof(Bill), "IsFixedOrAllowedIngredient")]
    [HarmonyPatch(new Type[] { typeof(Thing) })]
    public static class Bill_IsFixedOrAllowedIngredientPatch
    {
        public static bool Postfix(bool __result, Bill __instance, Thing thing)
        {
            Log.Message("result: " + __result);
            Log.Message("thing: " + thing.def.defName);

            return false;
        }
    }
    */
    /*
    [HarmonyPatch(typeof(ReachabilityWithinRegion), "ThingFromRegionListerReachable")]
    public static class ReachabilityWithinRegion_ThingFromRegionListerReachablePatch
    {
        public static bool Postfix(bool __result, Thing thing,
            Region region,
            PathEndMode peMode,
            Pawn traveler)
        {
            Log.Message("result: " + __result);
            Log.Message("thing: " + thing.def.defName);

            return true;
        }
    }*/

    // This patch make to fermenter considers own contents in BillRepeatModeDefOf.TargetCount mode of bill.
    [HarmonyPatch(typeof(RecipeWorkerCounter), nameof(RecipeWorkerCounter.CountProducts))]
public static class RecipeWorkerCounter_CountProductsPatch
{
    public static void Postfix(Bill_Production bill, ref int __result)
    {
        if (bill.billStack.billGiver is Building_UF buildingUF && bill.repeatMode == BillRepeatModeDefOf.TargetCount)
        {
            // Add current Building_UF contents to num, if needed
            foreach (var progress in buildingUF.progresses)
                if (progress.Process == bill.recipe)
                    __result += progress.Process.DisplayedProduct.count;
        }
    }
}

// This patch disable WorkTableWorkSpeedFactor stat for ThingDef_UF.
[HarmonyPatch(typeof(StatWorker), nameof(StatWorker.ShouldShowFor))]
public static class StatWorker_ShouldShowForPatch
{
    public static bool Prefix(StatRequest req, ref bool __result, StatDef ___stat)
    {
        if (req.Def is ThingDef_UF && ___stat == StatDefOf.WorkTableWorkSpeedFactor)
        {
            __result = false;
            return false;
        }

        return true;
    }
}
}
