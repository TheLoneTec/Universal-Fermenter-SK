using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

#nullable enable
namespace UniversalFermenterSK
{
    [StaticConstructorOnStartup]
    public static class UF_Utility
    {
        public static List<RecipeDef_UF> allUFProcesses = new();

        public static Dictionary<RecipeDef_UF, Command_Action> processGizmos = new();
        public static Dictionary<QualityCategory, Command_Action> qualityGizmos = new();

        public static Dictionary<RecipeDef_UF, Material> processMaterials = new();
        public static Dictionary<QualityCategory, Material> qualityMaterials = new();

        private static int gooseAngle = Rand.Range(0, 360);
        private static readonly HashSet<char> Vowels = new() { 'a', 'e', 'i', 'o', 'u' };

        public static char TempChar => Prefs.TemperatureMode switch
        {
            TemperatureDisplayMode.Celsius => 'C',
            TemperatureDisplayMode.Fahrenheit => 'F',
            TemperatureDisplayMode.Kelvin => 'K',
            _ => throw new System.ArgumentException()
        };

        static UF_Utility()
        {
            CheckForErrors();
            CacheAllProcesses();
            RecacheAll();
        }

        public static void CheckForErrors()
        {
            bool sendError = false;
            StringBuilder stringBuilder = new();
            stringBuilder.AppendLine("<-- Universal Fermenter Errors -->");

            foreach (ThingDef_UF thingDef in DefDatabase<ThingDef_UF>.AllDefs)
            {
                // Replace AllRecipes for ThingDef_UF
                foreach (var recipeDef in thingDef.AllRecipes)
                    if (!(recipeDef is RecipeDef_UF))
                    {
                        stringBuilder.AppendLine($"RecipeDef '{recipeDef.defName}' should have Class=\"UniversalFermenter.RecipeDef_UF\"" +
                                $"because this recipe uses by ThingDef_UF {thingDef.defName}.");
                        sendError = true;
                    }

                thingDef.AllRecipes.Clear();
                if (thingDef.recipes != null)
                    foreach (var recipeDef in thingDef.recipes)
                        if (recipeDef is RecipeDef_UF)
                            thingDef.AllRecipes.Add(recipeDef);

                foreach (var recipeDef in DefDatabase<RecipeDef_UF>.AllDefs)
                    if (recipeDef.recipeUsers != null && recipeDef.recipeUsers.Contains(thingDef))
                        thingDef.AllRecipes.Add(recipeDef);

                thingDef.Processes.AddRange(thingDef.AllRecipes.Select(r => (RecipeDef_UF)r));

                // Check RecipeDef_UF fields
                foreach (var recipeDef in thingDef.Processes)
                {
                    if (recipeDef.products.EnumerableNullOrEmpty() || recipeDef.ingredients.EnumerableNullOrEmpty())
                    {
                        stringBuilder.AppendLine($"RecipeDef_UF '{recipeDef.defName}' has not product or has not ingredients. These fields are required.");
                        thingDef.Processes.Remove(recipeDef);
                        thingDef.AllRecipes.Remove(recipeDef);
                        sendError = true;
                        continue;
                    }

                    if (recipeDef.efficiencyStat != null)
                    {
                        stringBuilder.AppendLine($"RecipeDef_UF '{recipeDef.defName}' has forbidden 'efficiencyStat' field. This fields will be cleared.");
                        recipeDef.efficiencyStat = null;
                        sendError = true;
                    }
                }
            }

            if (sendError)
                Log.Error(stringBuilder.ToString().TrimEndNewlines());
        }

        public static void RecacheAll() //Gets called in constructor and in writeSettings
        {
            RecacheProcessMaterials();
            RecacheQualityGizmos();
        }

        private static void CacheAllProcesses()
        {
            foreach (ThingDef_UF thingDef in DefDatabase<ThingDef_UF>.AllDefs)
                allUFProcesses.AddRange(thingDef.Processes);
        }

        public static void RecacheProcessMaterials()
        {
            processMaterials.Clear();
            foreach (var process in allUFProcesses)
            {
                Texture2D? icon = GetIcon(process.DisplayedProduct.thingDef, UF_Settings.singleItemIcon);
                Material mat = MaterialPool.MatFrom(icon);
                processMaterials.Add(process, mat);
            }

            qualityMaterials.Clear();
            foreach (QualityCategory quality in Enum.GetValues(typeof(QualityCategory)))
            {
                Texture2D icon = ContentFinder<Texture2D>.Get("UI/QualityIcons/" + quality);
                Material mat = MaterialPool.MatFrom(icon);
                qualityMaterials.Add(quality, mat);
            }
        }

        public static void RecacheQualityGizmos()
        {
            qualityGizmos.Clear();
            foreach (QualityCategory quality in Enum.GetValues(typeof(QualityCategory)))
            {
                Command_Quality command_Quality = new()
                {
                    defaultLabel = quality.GetLabel().CapitalizeFirst(),
                    defaultDesc = "UF_SetQualityDesc".Translate(),
                    //activateSound = SoundDefOf.Tick_Tiny,
                    icon = (Texture2D)qualityMaterials[quality].mainTexture,
                    qualityToTarget = quality
                };
                command_Quality.action = () =>
                {
                    FloatMenu floatMenu = new(command_Quality.RightClickFloatMenuOptions.ToList())
                    {
                        vanishIfMouseDistant = true
                    };
                    Find.WindowStack.Add(floatMenu);
                };
                qualityGizmos.Add(quality, command_Quality);
            }
        }

        public static Command_Action DebugGizmo()
        {
            Command_Action gizmo = new()
            {
                defaultLabel = "Debug: Options",
                defaultDesc = "Opens a float menu with debug options.",
                icon = ContentFinder<Texture2D>.Get("UI/DebugGoose"),
                iconAngle = gooseAngle,
                iconDrawScale = 1.25f,
                action = () =>
                {
                    FloatMenu floatMenu = new(DebugOptions()) { vanishIfMouseDistant = true };
                    Find.WindowStack.Add(floatMenu);
                }
            };
            return gizmo;
        }

        public static List<FloatMenuOption> DebugOptions()
        {
            List<FloatMenuOption> floatMenuOptions = new();
            IEnumerable<Building_UF> things = Find.Selector.SelectedObjects.OfType<Building_UF>();

            if (things.Any(c => !c.Empty && !c.AnyFinished))
            {
                floatMenuOptions.Add(new FloatMenuOption("Finish process", () => FinishProcess(things)));
                floatMenuOptions.Add(new FloatMenuOption("Progress one day", () => ProgressOneDay(things)));
                floatMenuOptions.Add(new FloatMenuOption("Progress half quadrum", () => ProgressHalfQuadrum(things)));
            }

            floatMenuOptions.Add(new FloatMenuOption("Log speed factors", LogSpeedFactors));

            return floatMenuOptions;
        }

        internal static void FinishProcess(IEnumerable<Building_UF> things)
        {
            foreach (var fermenter in things)
            {
                foreach (var progress in fermenter.progresses)
                {
                    progress.progressTicks = progress.Process.UsesQuality
                        ? Mathf.RoundToInt(progress.DaysToReachTargetQuality * GenDate.TicksPerDay)
                        : Mathf.RoundToInt(progress.Process.processDays * GenDate.TicksPerDay);
                }
                fermenter.CachesInvalid();
            }

            gooseAngle = Rand.Range(0, 360);
            UF_DefOf.UF_Honk.PlayOneShotOnCamera();
        }

        internal static void ProgressOneDay(IEnumerable<Building_UF> things)
        {
            foreach (var fermenter in things)
            {
                foreach (var progress in fermenter.progresses)
                {
                    progress.progressTicks += GenDate.TicksPerDay;
                }
                fermenter.CachesInvalid();
            }

            gooseAngle = Rand.Range(0, 360);
            UF_DefOf.UF_Honk.PlayOneShotOnCamera();
        }

        internal static void ProgressHalfQuadrum(IEnumerable<Building_UF> things)
        {
            foreach (var fermenter in things)
            {
                foreach (var progress in fermenter.progresses)
                {
                    progress.progressTicks += GenDate.TicksPerQuadrum / 2;
                }
                fermenter.CachesInvalid();
            }

            gooseAngle = Rand.Range(0, 360);
            UF_DefOf.UF_Honk.PlayOneShotOnCamera();
        }

        internal static void LogSpeedFactors()
        {
            foreach (Building_UF thing in Find.Selector.SelectedObjects.OfType<Building_UF>())
            {
                foreach (var progress in thing.progresses)
                {
                    Log.Message(thing + ": " +
                                "sun: " + progress.CurrentSunFactor.ToStringPercent() +
                                "| rain: " + progress.CurrentRainFactor.ToStringPercent() +
                                "| snow: " + progress.CurrentSnowFactor.ToStringPercent() +
                                "| wind: " + progress.CurrentWindFactor.ToStringPercent() +
                                "| roofed: " + thing.RoofCoverage.Value.ToStringPercent());
                }
            }

            gooseAngle = Rand.Range(0, 360);
            UF_DefOf.UF_Honk.PlayOneShotOnCamera();
        }

        public static string IngredientFilterSummary(ThingFilter thingFilter)
        {
            return thingFilter.Summary;
        }

        public static string VowelTrim(string str, int limit)
        {
            int vowelsToRemove = str.Length - limit;
            for (int i = str.Length - 1; i > 0; i--)
            {
                if (vowelsToRemove <= 0)
                    break;

                if (!IsVowel(str[i]) || str[i - 1] == ' ')
                    continue;

                str = str.Remove(i, 1);
                vowelsToRemove--;
            }

            if (str.Length > limit)
            {
                str = str.Remove(limit - 2) + "..";
            }

            return str;
        }

        public static bool IsVowel(char c)
        {
            return Vowels.Contains(c);
        }

        // Try to get a texture of a thingDef; If not found, use LaunchReport icon
        public static Texture2D? GetIcon(ThingDef? thingDef, bool singleStack = true)
        {
            if (thingDef == null)
                return null;

            Texture2D? icon = ContentFinder<Texture2D>.Get(thingDef.graphicData.texPath, false);
            if (icon != null)
                return icon;

            // Use the first texture in the folder
            icon = singleStack ? ContentFinder<Texture2D>.GetAllInFolder(thingDef.graphicData.texPath).FirstOrDefault() : ContentFinder<Texture2D>.GetAllInFolder(thingDef.graphicData.texPath).LastOrDefault();
            if (icon != null)
                return icon;

            icon = ContentFinder<Texture2D>.Get("UI/Commands/LaunchReport");
            Log.Warning("Universal Fermenter:: No texture at " + thingDef.graphicData.texPath + ".");
            return icon;
        }

        public static string ToStringTemperature(this FloatRange range)
        {
            return range.min.ToStringTemperature("F0") + "~" + range.max.ToStringTemperature("F0");
        }
    }
}
