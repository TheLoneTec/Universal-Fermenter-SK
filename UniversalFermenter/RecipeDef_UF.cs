using RimWorld;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Verse;

#nullable enable
namespace UniversalFermenterSK
{
    public class RecipeDef_UF : RecipeDef
    {
        /// <summary>Color to apply to the texture.</summary>
        public Color color = new(1.0f, 1.0f, 1.0f);

        /// <summary>Whether the process has a color-coded overlay.</summary>
        public bool colorCoded = false;

        /// <summary>Custom label to give finished products.</summary>
        public string customLabel = "";

        /// <summary>When there are items in the fermenter for this process, this suffix will be added to the fermenter's graphic, to load a "filled" graphic.</summary>
        public string? graphicSuffix = null;

        /// <summary>If the fermenter is destroyed, does the in-process product drop on the ground or is it destroyed?</summary>
        public bool incompleteProductsCanBeRetrieved = true;

        /// <summary>Count of slots, which will be filled by 1 this process.</summary>
        public int slotsRequired = 1;

        /// <summary>If first ingredient is Pawn with meatDef then products will be scaled on scaleToMeatAmountBase</summary>
        public bool scaleToMeatAmount = false;

        /// <summary>Base scale value for scaleToMeatAmount option</summary>
        public int scaleToMeatAmountBase = 80;

        /// <summary>The total number of days for the fermentation process to complete, assuming perfect conditions.</summary>
        public float processDays = 6f;

        /// <summary>The number of days for each quality level of product, from awful to legendary.</summary>
        public QualityDays qualityDays = new(1, 0, 0, 0, 0, 0, 0);

        /// <summary>The speed multipliers at the minimum and maximum possible rain amounts.</summary>
        public FloatRange rainFactor = new(1f, 1f);

        /// <summary>If outside the safe temperature range, the product gets ruined this percentage, per degree non-ideal, per hour.</summary>
        public float ruinedPerDegreePerHour = 2.5f;

        /// <summary>If fermenter is powered off, the product gets ruined this percentage per hour.</summary>
        public float ruinedIfNoPowerOrNoFuelOrFlickedOffPerHour = 0f;

        /// <summary>The speed multipliers at the minimum and maximum possible snow amounts.</summary>
        public FloatRange snowFactor = new(1f, 1f);

        /// <summary>The speed of the fermentatino process above the maximum safe temperature.</summary>
        public float speedAboveSafe = 1f;

        /// <summary>The speed of the fermentation process below the minimum safe temperature.</summary>
        public float speedBelowSafe = 0.1f;

        /// <summary>The speed multipliers at the minimum and maximum possible sun amounts.</summary>
        public FloatRange sunFactor = new(1f, 1f);

        /// <summary>The ideal range of temperatures for this process. Outside the ideal range, the process will slow down.</summary>
        public FloatRange temperatureIdeal = new(7f, 32f);

        /// <summary>The safe range of temperatures for this process. Outside the safe range, the product will start to spoil/degrade.</summary>
        public FloatRange temperatureSafe = new(-1f, 32f);

        /// <summary>Whether the speed of the fermentation process is affected by the temperature. The process can still be ruined by bad temperatures.</summary>
        public bool usesTemperature = true;

        /// <summary>The speed multipliers at the minimum and maximum possible wind speeds.</summary>
        public FloatRange windFactor = new(1f, 1f);

        /// <summary>The product that is created at the end of the fermentation process.</summary>
        public ThingDefCountClass DisplayedProduct => products[0];

        /// <summary>Whether the fermentation process results in different quality levels of product depending on how long it has fermented.</summary>
        public bool UsesQuality => DisplayedProduct.thingDef.HasComp(typeof(CompQuality));

        //All variables past this point are new.

        /// <summary>How much being unpowered effects the process 0-1f. (default 0)</summary>
        public float unPoweredFactor = 0.0f;

        /// <summary>How much being unfueled effects the process 0-1f.(default 0)</summary>
        public float unFueledFactor = 0.0f;

        /// <summary>How much being powered effects the process 0-1f.(default 1) (Not Yet Implemented)</summary>
        public float powerUseFactor = 1f;

        /// <summary>How much being fueled effects the process 0-1f.(default 1) (Not Yet Implemented)</summary>
        public float fuelUseFactor = 1f;

        /// <summary>The chance that the building is destroyed after use.</summary>
        public float destroyChance = 0.0f;

        /// <summary>Thingdefs that have a certain chance to also be produced.</summary>
        public List<BonusOutput> bonusOutputs = new List<BonusOutput>();

        /// <summary>Using SK FuelComp Internal temperature.</summary>
        public bool useInternalTemeprature = false;

        /// <summary>Thingdefs that have a certain chance to also be produced. (Not Yet Implemented)</summary>
        public bool partialIngredientsRuinedOnPercent = false;

        public float RuinedPerDegreePerHour => 
            Prefs.TemperatureMode == TemperatureDisplayMode.Celsius || Prefs.TemperatureMode == TemperatureDisplayMode.Kelvin ? 
            ruinedPerDegreePerHour : ruinedPerDegreePerHour * 1.8f; // 1 degree C = 1.8 degree F

        

    public string Dialog_BillConfigExtString()
        {
            var str = new StringBuilder();
            //str.AppendLine(Resources.UF_WikiRim_Section);
            str.AppendLine();

            str.Append(Resources.RecipeDef_UF__slotsRequired);
            str.Append(" : ");
            str.AppendLine(slotsRequired.ToString());

            str.Append(Resources.RecipeDef_UF__processDays);
            str.Append(" : ");
            str.AppendLine(processDays.ToString());
                
            if (usesTemperature)
            {
                str.Append(Resources.RecipeDef_UF__temperatureSafe);
                str.Append(" : ");
                str.AppendLine(temperatureSafe.ToStringTemperature());

                str.Append(Resources.RecipeDef_UF__temperatureIdeal);
                str.Append(" : ");
                str.AppendLine(temperatureIdeal.ToStringTemperature());

                str.Append(Resources.RecipeDef_UF__ruinedPerDegreePerHour);
                str.Append(" : ");
                str.Append(RuinedPerDegreePerHour.ToString() + "% ");
                str.AppendLine(Resources.RecipeDef_UF__ruinedPerDegreePerHourUnit);
            }

            return str.ToString();
        }
    }

    [StaticConstructorOnStartup]
    public static class Resources
    {
        public static readonly string UF_WikiRim_Section = "UF_WikiRim_Section".Translate();

        public static readonly string RecipeDef_UF__slotsRequired = "RecipeDef_UF__slotsRequired".Translate();
        public static readonly string RecipeDef_UF__processDays = "RecipeDef_UF__processDays".Translate();
        public static readonly string RecipeDef_UF__temperatureSafe = "RecipeDef_UF__temperatureSafe".Translate();
        public static readonly string RecipeDef_UF__temperatureIdeal = "RecipeDef_UF__temperatureIdeal".Translate();
        public static readonly string RecipeDef_UF__ruinedPerDegreePerHour = "RecipeDef_UF__ruinedPerDegreePerHour".Translate();
        public static readonly string RecipeDef_UF__ruinedPerDegreePerHourUnit = "RecipeDef_UF__ruinedPerDegreePerHourUnit".Translate();
        public static readonly string ThingDef_UF__slotsCount = "ThingDef_UF__slotsCount".Translate();
    }
}
