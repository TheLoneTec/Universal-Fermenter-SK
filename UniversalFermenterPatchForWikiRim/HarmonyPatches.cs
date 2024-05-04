using HarmonyLib;
using HelpTab;
using System.Collections.Generic;
using UniversalFermenterSK;
using Verse;

namespace UniversalFermenterPatchForWikiRim
{
    [StaticConstructorOnStartup]
    static class Patches
    {
        static Patches()
        {
            new Harmony("qwerty19106.uf_patch_for_wikirim").PatchAll();
        }
    }

    [HarmonyPatch(typeof(HelpBuilder), "HelpForRecipe")]
    public static class HelpBuilder_HelpForRecipePatch
    {
        public static void Postfix(RecipeDef recipeDef, ref HelpDef __result)
        {
            if (recipeDef is RecipeDef_UF def)
            {
                var stringDescs = new List<string>
                {
                    Resources.RecipeDef_UF__slotsRequired,
                    Resources.RecipeDef_UF__processDays,
                };
                if (def.usesTemperature)
                {
                    stringDescs.Add(Resources.RecipeDef_UF__temperatureSafe);
                    stringDescs.Add(Resources.RecipeDef_UF__temperatureIdeal);
                    stringDescs.Add(Resources.RecipeDef_UF__ruinedPerDegreePerHour);
                }

                var suffixes = new List<string>
                {
                    def.slotsRequired.ToString(),
                    def.processDays.ToString(),
                };
                if (def.usesTemperature)
                {
                    suffixes.Add(def.temperatureSafe.ToString());
                    suffixes.Add(def.temperatureIdeal.ToString());
                    suffixes.Add(def.ruinedPerDegreePerHour.ToString());
                }

                var item = new HelpDetailSection(Resources.UF_WikiRim_Section, stringDescs.ToArray(), null, suffixes.ToArray());
                __result.HelpDetailSections.Add(item);
            }
        }
    }

    [HarmonyPatch(typeof(HelpBuilder), "HelpForBuildable")]
    public static class HelpBuilder_HelpForBuildablePatch
    {
        public static void Postfix(BuildableDef buildableDef, ref HelpDef __result)
        {
            if (buildableDef is ThingDef_UF def)
            {
                var stringDescs = new List<string>
                {
                    Resources.ThingDef_UF__slotsCount,
                };

                var suffixes = new List<string>
                {
                    def.slotsCount.ToString(),
                };

                var item = new HelpDetailSection(Resources.UF_WikiRim_Section, stringDescs.ToArray(), null, suffixes.ToArray());
                __result.HelpDetailSections.Add(item);
            }
        }
    }
}
