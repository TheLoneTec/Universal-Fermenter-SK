using UnityEngine;
using WikiProject;
using RimWorld;
using Verse;
using UniversalFermenterSK;
using System;
using System.Collections.Generic;

namespace UniversalFermenterPatchForGlitterpedia;

public class WikiExtraInfo_UniversalFermenter : WIkiExtraInfo
{
    public override IEnumerable<WikiItemParam> GetExtraParms(IWikiItem wikiItem)
    {
        if (wikiItem.GetDef() is not RecipeDef_UF recipe) throw new InvalidOperationException($"Def {wikiItem.GetDef()} is not a UniversalFermenter def");
        yield return new WikiItemParam(UniversalFermenterSK.Resources.RecipeDef_UF__slotsRequired, recipe.slotsRequired.ToString());
        yield return new WikiItemParam(UniversalFermenterSK.Resources.RecipeDef_UF__processDays, recipe.processDays.ToString());
        if (recipe.usesTemperature)
        {
            yield return new WikiItemParam(UniversalFermenterSK.Resources.RecipeDef_UF__temperatureSafe, recipe.temperatureSafe.ToStringTemperature());
            yield return new WikiItemParam(UniversalFermenterSK.Resources.RecipeDef_UF__temperatureIdeal, recipe.temperatureIdeal.ToStringTemperature());
            yield return new WikiItemParam(UniversalFermenterSK.Resources.RecipeDef_UF__ruinedPerDegreePerHour, recipe.ruinedPerDegreePerHour.ToString() + "%");
        }
    }

    public override void ExtraDraw(Rect rect, IWikiItem wikiItem, ref float height)
    {
    }
}
