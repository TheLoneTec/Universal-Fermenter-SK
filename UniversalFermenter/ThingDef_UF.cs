using RimWorld;
using SK;
using System.Collections.Generic;
using UnityEngine;
using Verse;

#nullable enable
namespace UniversalFermenterSK
{
    public class ThingDef_UF: ThingDef_AnimatedWorktable
    {
		/// <summary>Offset for the fermentation progress bar overlay.</summary>
		public Vector2 barOffset = new(0f, 0.25f);

		/// <summary>Scale for the fermentation process bar overlay.</summary>
		public Vector2 barScale = new(1f, 1f);

		/// <summary>The slots count of fermenter.</summary>
		public int slotsCount = 1;

		/// <summary>Scale for the current product overlay.</summary>
		public Vector2 productIconSize = new(1f, 1f);

		/// <summary>Show the current product as an overlay on the fermenter?</summary>
		public bool showProductIcon = true;

		// Note! It is cache and will be filled in UF_Utility by [StaticConstructorOnStartup]
		public readonly List<RecipeDef_UF> Processes = new();

		/// <summary>Delay at filling ingredients to fermenter.</summary>
		public int fillDelay = 200;

		/// <summary>Delay at taking products from fermenter.</summary>
		public int takeDelay = 200;

		/// <summary>Chance to destroy itself on finished product. (takes into account workbench efficiency and quality(Not yet Implemented))</summary>
		public float buildingDestroyChance = 0f;

		/// <summary>How well the internal temperature is insulated inside the building.</summary>
		public float insulation = 1f;

		public override IEnumerable<StatDrawEntry> SpecialDisplayStats(StatRequest req)
		{
			// Draw base stats, exclude WorkTableWorkSpeedFactor
			foreach (StatDrawEntry statDrawEntry in base.SpecialDisplayStats(req)) 
				yield return statDrawEntry;

            // UF stats
            yield return new StatDrawEntry(UF_StatCategoryDefOf.UniversalFermenterStats, "UF_SlotsCountLabel".Translate(), slotsCount.ToString(), "UF_SlotsCountDesc".Translate(), 1);

            yield break;
        }
    }

	[DefOf]
	public static class UF_StatCategoryDefOf
	{
#pragma warning disable CS8618 // Поле, не допускающее значения NULL, должно содержать значение, отличное от NULL, при выходе из конструктора. Возможно, стоит объявить поле как допускающее значения NULL.
        public static StatCategoryDef UniversalFermenterStats;
#pragma warning restore CS8618 // Поле, не допускающее значения NULL, должно содержать значение, отличное от NULL, при выходе из конструктора. Возможно, стоит объявить поле как допускающее значения NULL.
    }
}
