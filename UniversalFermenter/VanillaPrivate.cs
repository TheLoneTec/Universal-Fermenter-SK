using System;
using System.Collections.Generic;
using System.Reflection;
using RimWorld;
using SK;
using UnityEngine;
using Verse;
using Verse.AI;

#nullable enable
namespace UniversalFermenterSK
{
    [StaticConstructorOnStartup]
    public static class VanillaPrivate {

        /// Toils_Recipe.CalculateIngredients method
        public static readonly Func<Job, Pawn, List<Thing>> CalculateIngredients;

        /// Toils_Recipe.CalculateDominantIngredient method
        public static readonly Func<Job, List<Thing>, Thing?> CalculateDominantIngredient;

        /// Toils_Recipe.ConsumeIngredients method
        public static readonly Action<List<Thing>, RecipeDef, Map> ConsumeIngredients;

        /// WorkGiver_DoBill.TryFindBestBillIngredients method
        public static readonly Func<Bill, Pawn, Thing, List<ThingCount>, List<IngredientCount>, bool> TryFindBestBillIngredients;

        /// CompPowerLowIdleDraw.TogglePower method
        static readonly MethodInfo TogglePowerMethod;
        public static void TogglePower(CompPowerLowIdleDraw comp) {
            TogglePowerMethod.Invoke(comp, null);
        }

        /// CompFoodPoisonable.poisonPct field
        static readonly FieldInfo poisonPctField;

        private static readonly FieldInfo missingIngredients;

        public static void setPoisonPct(this CompFoodPoisonable comp, float value) {
            poisonPctField.SetValue(comp, value);
        }

        public static List<IngredientCount> getMissingIngredients()
        {

            Type type = missingIngredients.GetType();
            object instance = Activator.CreateInstance(type);
            object theActualValue = missingIngredients.GetValue(instance);
            List<IngredientCount> Variables = (List<IngredientCount>)theActualValue;
            return Variables;
        }

        public static readonly Texture2D TexButton_Paste = ContentFinder<Texture2D>.Get("UI/Buttons/Paste", true);

        static VanillaPrivate()
        {
            MethodInfo? method = typeof(Toils_Recipe).GetMethod("CalculateIngredients", BindingFlags.Static | BindingFlags.NonPublic);
            if (method == null)
                throw new UFException("Can not get Toils_Recipe.CalculateIngredients method by reflection");
            CalculateIngredients = (Func<Job, Pawn, List<Thing>>)Delegate.CreateDelegate(typeof(Func<Job, Pawn, List<Thing>>), method);

            method = typeof(Toils_Recipe).GetMethod("CalculateDominantIngredient", BindingFlags.Static | BindingFlags.NonPublic);
            if (method == null)
                throw new UFException("Can not get Toils_Recipe.CalculateIngredients method by reflection");
            CalculateDominantIngredient = (Func<Job, List<Thing>, Thing>)Delegate.CreateDelegate(typeof(Func<Job, List<Thing>, Thing>), method);

            method = typeof(Toils_Recipe).GetMethod("ConsumeIngredients", BindingFlags.Static | BindingFlags.NonPublic);
            if (method == null)
                throw new UFException("Can not get JobDriver_DoBill.JumpToCollectNextIntoHandsForBill method by reflection");
            ConsumeIngredients = (Action<List<Thing>, RecipeDef, Map>)Delegate.CreateDelegate(typeof(Action<List<Thing>, RecipeDef, Map>), method);

            method = typeof(WorkGiver_DoBill).GetMethod("TryFindBestBillIngredients", BindingFlags.Static | BindingFlags.NonPublic);
            if (method == null)
                throw new UFException("Can not get WorkGiver_DoBill.TryFindBestBillIngredients method by reflection");
            TryFindBestBillIngredients = (Func<Bill, Pawn, Thing, List<ThingCount>, List<IngredientCount>, bool>)Delegate.CreateDelegate(typeof(Func<Bill, Pawn, Thing, List<ThingCount>, List<IngredientCount>, bool >), method);

            method = typeof(CompPowerLowIdleDraw).GetMethod("TogglePower", BindingFlags.Instance | BindingFlags.NonPublic);
            if (method == null)
                throw new UFException("Can not get CompPowerLowIdleDraw.TogglePower method by reflection");
            TogglePowerMethod = method;

            var field = typeof(CompFoodPoisonable).GetField("poisonPct", BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null)
                throw new UFException("Can not get CompFoodPoisonable.poisonPct field by reflection");
            poisonPctField = field;
            var field2 = typeof(WorkGiver_DoBill).GetField("missingIngredients", BindingFlags.Static | BindingFlags.NonPublic);
            if (field2 == null)
            {
                throw new UFException("Can not get WorkGiver_DoBill.missingIngredients field by reflection", null);
            }
            missingIngredients = field2;
        }
    }
}