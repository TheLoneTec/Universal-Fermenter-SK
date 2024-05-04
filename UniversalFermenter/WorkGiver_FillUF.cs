using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Reflection;
using RimWorld;
using SK;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.Noise;

#nullable enable
namespace UniversalFermenterSK
{
    public class WorkGiver_FillUF : WorkGiver_Scanner
    {
        public override PathEndMode PathEndMode => PathEndMode.Touch;

        public override bool Prioritized => true;

        public override bool ShouldSkip(Pawn pawn, bool forced = false)
        {
            return !pawn.Map.GetComponent<MapComponent_UF>().thingsWithUFComp.Any();
        }

        public override IEnumerable<Thing> PotentialWorkThingsGlobal(Pawn pawn)
        {
            return pawn.Map.GetComponent<MapComponent_UF>().thingsWithUFComp;
        }

        public override float GetPriority(Pawn pawn, TargetInfo t)
        {
            if (t.Thing is not Building_UF fermenter)
                return 0;

            if (fermenter.LeftSlots == 0)
                return 0;

            return 1f / fermenter.LeftSlots;
        }

        Bill? GetBillOnThing(Pawn pawn, Thing t, out List<ThingCount> ingredients, bool setFailReason, bool forced = false)
        {
            ingredients = new();
            //Log.Message("new passed");
            if (t is not Building_UF fermenter || fermenter.LeftSlots == 0)
                return null;
            //Log.Message("fermenter passed");
            if (!fermenter.Powered)
                return null;
            //Log.Message("Powered passed");
            if (pawn.Map.designationManager.DesignationOn(fermenter, DesignationDefOf.Deconstruct) != null
            || fermenter.IsForbidden(pawn)
            || !pawn.CanReserveAndReach(fermenter, PathEndMode.Touch, pawn.NormalMaxDanger(), 1, -1, null, forced)
                || fermenter.IsBurning())
                return null;
            //Log.Message("DesignationOn passed");
            List<Bill> bills = fermenter.BillStack.Bills.Where(bill => bill.ShouldDoNow()).ToList();
            if (bills.Count == 0)
                return null;
            //Log.Message("bills found");
            float temp = fermenter.AmbientTemperature;
            foreach (var bill in bills)
            {
                //Log.Message("bills entered");
                SkillRequirement? skillRequirement = bill.recipe.FirstSkillRequirementPawnDoesntSatisfy(pawn);
                if (skillRequirement != null)
                {
                    if (setFailReason)
                        JobFailReason.Is("UnderRequiredSkill".Translate(skillRequirement.minLevel), bill.Label);
                    continue;
                }
                //Log.Message("UnderRequiredSkill passed");
                var process = (RecipeDef_UF)bill.recipe;
                if (fermenter.LeftSlots < process.slotsRequired)
                {
                    if (setFailReason)
                        JobFailReason.Is("UF_NoSlotsForProcess".Translate(fermenter.LeftSlots, process!.slotsRequired));
                    continue;
                }
                //Log.Message("UF_NoSlotsForProcess passed");
                if (process.usesTemperature && (temp < process.temperatureSafe.min || temp > process.temperatureSafe.max))
                {
                    if (setFailReason)
                        JobFailReason.Is("BadTemperature".Translate().ToLower());
                    continue;
                }
                //Log.Message("BadTemperature passed");
                ingredients.Clear();
                //custom code
                float radiusSq = bill.ingredientSearchRadius * bill.ingredientSearchRadius;
                //Log.Message("radiusSq passed");
                Region rootReg = t.Position.GetRegion(pawn.Map);
                //Log.Message("GetRegion passed");
                if (rootReg == null)
                    return null;
                //Log.Message("rootReg passed");
                TraverseParms traverseParams = TraverseParms.For(pawn);
                //Log.Message("traverseParams passed");
                //RegionEntryPredicate entryCondition = (RegionEntryPredicate)null;
                //Log.Message("entryCondition entered");
                RegionEntryPredicate entryCondition = (double)Math.Abs(999f - bill.ingredientSearchRadius) < 1.0 ? (RegionEntryPredicate)((from, r) => r.Allows(traverseParams, false)) : (RegionEntryPredicate)((from, r) =>
                {
                    if (!r.Allows(traverseParams, false))
                        return false;
                    CellRect extentsClose = r.extentsClose;
                    int num1 = Math.Abs(t.Position.x - Math.Max(extentsClose.minX, Math.Min(t.Position.x, extentsClose.maxX)));
                    if ((double)num1 > (double)bill.ingredientSearchRadius)
                        return false;
                    int num2 = Math.Abs(t.Position.z - Math.Max(extentsClose.minZ, Math.Min(t.Position.z, extentsClose.maxZ)));
                    return (double)num2 <= (double)bill.ingredientSearchRadius && (double)(num1 * num1 + num2 * num2) <= (double)radiusSq;
                });
                //Log.Message("entryCondition passed");
                int adjacentRegionsAvailable = rootReg.Neighbors.Count<Region>((Func<Region, bool>)(region => entryCondition(rootReg, region)));
                //Log.Message("adjacentRegionsAvailable");
                bool ingredientsAvailable = false;
                List<ThingCount> ingredientsFound = new List<ThingCount>();
                List<ThingCount> outputIngredients = ingredients;
                //List<IngredientCount> missingIngredients = VanillaPrivate.getMissingIngredients();
                Predicate<Thing> baseValidator = (Predicate<Thing>)(t => t.Spawned && IsUsableIngredient(t,bill) && (double)(t.Position - fermenter.Position).LengthHorizontalSquared < (double)radiusSq && !t.IsForbidden(pawn) && pawn.CanReserve((LocalTargetInfo)t));
                //Log.Message("baseValidator");
                int count = bill.recipe.ingredients.Count;
                RegionProcessor regionProcessor = (RegionProcessor)(r =>
                {
                    List<Thing> thingList = r.ListerThings.ThingsMatching(ThingRequest.ForGroup(ThingRequestGroup.HaulableEver));
                    if(!bill.recipe.ingredients.NullOrEmpty())
                    {
                        //foreach (var item1 in bill.recipe.ingredients)
                        //{
                        //    Log.Message("ingredients 1: " + item1.filter.AnyAllowedDef.defName);
                        //}
                        for (int index = 0; index < thingList.Count; ++index)
                        {
                            //bool skip = false;
                            Thing thing = thingList[index];
                            if (!outputIngredients.Where(i => i.Thing.ThingID == thing.ThingID).EnumerableNullOrEmpty() || thing.IsForbidden(pawn) ||( thing.GetRoom() != null && thing.GetRoom().IsPrisonCell))
                                continue;
                            //Log.Message("Checking: " + thing.def.defName + ". ThingID: " + thing.ThingID + ". Unique Load ID: " + thing.GetUniqueLoadID());
                            foreach (var recipeItem in bill.recipe.ingredients)
                            {
                                //Log.Message(bill.recipe.ingredients.IndexOf(recipeItem) + " thing: " + recipeItem.filter.AnyAllowedDef.defName);
                                //Log.Message("checking for: " + item.GetBaseCount());
                                float amountNeeded = recipeItem.CountRequiredOfFor(thing.def, bill.recipe, bill);
                                //Log.Message("Reached here, ingredient count needed is: " + amountNeeded);
                                foreach (var item2 in outputIngredients)
                                {
                                    if (item2.Thing.def.defName == thing.def.defName || recipeItem.filter.Allows(item2.Thing.def))
                                        amountNeeded -= item2.Count;
                                }

                                // Here change Allows to a where, where it can check the defname instead, as its not finding it
                                bool allowed = !recipeItem.filter.AllowedThingDefs.Where(d => d.defName == thing.def.defName).EnumerableNullOrEmpty();
                                //Log.Message("Reached here 2, amount needed: " + amountNeeded + ". Allowed: " + allowed);
                                //if (!allowed)
                                //{
                                //Log.Message("Skipping Not Allowed Ingredient: " + thing.def.defName + " Count: " + thing.stackCount);
                                //allowed = false;
                                //}
                                //if (allowed)
                                    //Log.Message("Found: " + thing.def.defName + ". Count: " + amountNeeded);
                                if (allowed && amountNeeded <= 0)
                                {
                                    allowed = false;

                                    //Log.Message("Ingredient Amount Found for: " + thing.def.defName);
                                }

                                if (!allowed)
                                {
                                    //Log.Message("Skipped Duplicate: " + thing.def.defName);
                                    continue;
                                }
                                if (allowed)
                                {
                                    amountNeeded = recipeItem.CountRequiredOfFor(thing.def, bill.recipe, bill);
                                    foreach (var foundIngredient in ingredientsFound.Where(t => t.Thing.def.defName == thing.def.defName || (recipeItem.filter.DisplayRootCategory.catDef.defName == "Corpses" && t.Thing.def.IsCorpse && thing.def.IsCorpse)))
                                    {
                                        amountNeeded -= foundIngredient.Count;
                                    }
                                    //Log.Message("amount Needed: " + amountNeeded);
                                    if (amountNeeded > 0)
                                    {
                                        if (allowed && ReachabilityWithinRegion.ThingFromRegionListerReachable(thing, r, PathEndMode.ClosestTouch, pawn) && baseValidator(thing) && !(thing.def.IsMedicine))
                                        {
                                            //Log.Message("Found: " + thing.def.defName + ": " + thing.stackCount + ". Same Region?: " + (r == pawn.GetRegion() ? "True" : "False"));
                                            if (!bill.recipe.ingredients.First().filter.AllowedThingDefs.Where(d => d.defName == thing.def.defName).EnumerableNullOrEmpty())
                                            {
                                                ingredientsFound.Insert(0, new ThingCount(thing, Mathf.Min(amountNeeded, thing.stackCount).RoundToAsInt(1)));
                                            }
                                            else
                                            {
                                                ingredientsFound.Add(new ThingCount(thing, Mathf.Min(amountNeeded, thing.stackCount).RoundToAsInt(1)));
                                            }
                                        }
                                    }
                                    if (thing.stackCount >= amountNeeded && amountNeeded > 0)
                                    {
                                        amountNeeded = recipeItem.CountRequiredOfFor(thing.def, bill.recipe, bill);
                                        //ingredientsFound.SortByDescending(t => t.Count);
                                        //List<ThingCount> toRemove = new List<ThingCount>();
                                        foreach (var toAdd in ingredientsFound.Where(tc => tc.Thing.def.defName == thing.def.defName))
                                        {
                                            if (!bill.recipe.ingredients.First().filter.AllowedThingDefs.Where(d => d.defName == thing.def.defName).EnumerableNullOrEmpty())
                                            {
                                                //Log.Message("Adding: " + toAdd.Thing.def.defName + " : " + toAdd.Count + " to the front" + ". ThingID: " + (toAdd.Thing.ThingID != null ? toAdd.Thing.ThingID : "Null"));
                                                outputIngredients.Insert(0, toAdd);
                                                amountNeeded -= toAdd.Count;
                                                //toRemove.Add(toAdd);
                                            }
                                            else
                                            {
                                                //Log.Message("Adding: " + toAdd.Thing.def.defName + " : " + toAdd.Count + ". ThingID: " + (toAdd.Thing.ThingID != null ? toAdd.Thing.ThingID : "Null"));
                                                outputIngredients.Add(toAdd);
                                                amountNeeded -= toAdd.Count;
                                                //toRemove.Add(toAdd);
                                            }
                                            if (amountNeeded < 1)
                                                count--;
                                        }/*
                                        if (!toRemove.NullOrEmpty())
                                        {
                                            foreach (var item in toRemove)
                                            {
                                                ingredientsFound.Remove(item);
                                            }
                                        }*/
                                    }
                                }
                            }

                            //foreach (var item in outputIngredients)
                            //{
                            //    Log.Message("Item: " + item.Thing.def.defName);
                            //}
                            //Log.Message("ingredientsFound " + outputIngredients.Count + " / " + bill.recipe.ingredients.Count);
                            
                            //List<ThingCount> excess = new List<ThingCount>();
                            if (count == 0)
                            {
                                ingredientsAvailable = true;
                                //Log.Message("output count: " + outputIngredients.Count);
                                foreach (var item in bill.recipe.ingredients)
                                {
                                    float amountNeeded = item.CountRequiredOfFor(thing.def, bill.recipe, bill);
                                    float outputAmount = 0;
                                    foreach (var item2 in outputIngredients)
                                    {
                                        //if (DebugSettings.godMode)
                                            //Log.Message("outputIngredients: " + item2.Thing.def.defName + ". Loc: " + item2.Thing.Position + ". Count: " + item2.Count);
                                        if (!item.filter.AllowedThingDefs.Where(d => d.defName == item2.Thing.def.defName).EnumerableNullOrEmpty())
                                            outputAmount += item2.Count;
                                        //if (outputAmount > amountNeeded)
                                        //{
                                        //    ThingCount tmp = item2.WithCount(Convert.ToInt32(item2.Count - (outputAmount - amountNeeded)));
                                        //    excess.Add(tmp);
                                        //}
                                    }
                                    if (outputAmount < amountNeeded)
                                    {
                                        ingredientsAvailable = false;
                                        break;
                                    }

                                    //if (!ingredientsAvailable)
                                    //    break;

                                }
                                //foreach (var item in excess)
                                //{
                                //    outputIngredients.RemoveAt(outputIngredients.FindIndex(i => i.Thing.def == item));
                                //}
                                return true;
                            }
                        }
                    }

                    return false;
                });
                //Log.Message("BreadthFirstTraverse before");
                RegionTraverser.BreadthFirstTraverse(rootReg, entryCondition, regionProcessor, 99999);
                //Log.Message("BreadthFirstTraverse after");
                //custom code end
                //foreach (var item in outputIngredients)
                //{
                //    Log.Message("output Ingredient: " + item.Thing.def.defName + " Count: " + item.Count);
                //}
                //foreach (var item in missingIngredients)
                //{
                //Log.Message("missing ingredient: " + item.filter.AnyAllowedDef.defName);
                //}
                /*
                if (!VanillaPrivate.TryFindBestBillIngredients(bill, pawn, fermenter, ingredients, missingIngredients))
                {
                    if (setFailReason)
                        JobFailReason.Is("UF_NoIngredient".Translate());
                    continue;
                }
                */
                //Log.Message("Ingredients Available: " + ingredientsAvailable);
                if (ingredientsAvailable)
                    return bill;
                else
                    if (setFailReason)
                {
                    JobFailReason.Is("UF_NoIngredient".Translate());
                    continue;
                }
            }

            return null;
        }

        private static bool IsUsableIngredient(Thing t, Bill bill)
        {
            if (!bill.IsFixedOrAllowedIngredient(t))
                return false;
            foreach (IngredientCount ingredient in bill.recipe.ingredients)
            {
                if (ingredient.filter.Allows(t))
                {
                    //Log.Message("usable ingredient: " + t.def.defName);
                    return true;
                }
            }
            return false;
        }

        public override bool HasJobOnThing(Pawn pawn, Thing fermenter, bool forced = false)
        {
            Bill? bill = GetBillOnThing(pawn, fermenter, out _, true, forced);
            return bill != null;
        }

        public override Job? JobOnThing(Pawn pawn, Thing fermenter, bool forced = false)
        {
            Bill? bill = GetBillOnThing(pawn, fermenter, out List<ThingCount> ingredients, false, forced);

            //if (!(fermenter is IBillGiver giver) || !this.ThingIsUsableBillGiver(fermenter) || !giver.BillStack.AnyShouldDoNow || !giver.UsableForBillsAfterFueling() || !pawn.CanReserve((LocalTargetInfo)fermenter, ignoreOtherReservations: forced) || fermenter.IsBurning() || fermenter.IsForbidden(pawn) || fermenter.def.hasInteractionCell && !pawn.CanReserveSittableOrSpot(fermenter.InteractionCell, forced))
            //    return null;

            if (bill == null)
                return null;
            var job = new Job(UF_DefOf.FillUniversalFermenter, fermenter, ingredients[0].Thing)
            {               
                bill = bill,
                targetQueueB = new List<LocalTargetInfo>(ingredients.Count),
                countQueue = new List<int>(ingredients.Count),
                haulMode = HaulMode.ToCellNonStorage
            };
            //Log.Message("job made");
            for (int i = 0; i < ingredients.Count; i++)
            {
                //Log.Message("ingredient: " + ingredients[i].Count + " " + ingredients[i].Thing.def.defName + ". Position: " + ingredients[i].Thing.Position);
                job.targetQueueB.Add(ingredients[i].Thing);
                job.countQueue.Add(ingredients[i].Count);
            }
            //Log.Message("ingredients queued");
            return job;
        }

        public bool ThingIsUsableBillGiver(Thing thing)
        {
            Pawn? pawn1 = thing as Pawn;
            Corpse? corpse = thing as Corpse;
            Pawn? pawn2 = null;
            if (corpse != null)
                pawn2 = corpse.InnerPawn;
            return this.def.fixedBillGiverDefs != null && this.def.fixedBillGiverDefs.Contains(thing.def) || pawn1 != null && (this.def.billGiversAllHumanlikes && pawn1.RaceProps.Humanlike || this.def.billGiversAllMechanoids && pawn1.RaceProps.IsMechanoid || this.def.billGiversAllAnimals && pawn1.RaceProps.Animal) || corpse != null && pawn2 != null && (this.def.billGiversAllHumanlikesCorpses && pawn2.RaceProps.Humanlike || this.def.billGiversAllMechanoidsCorpses && pawn2.RaceProps.IsMechanoid || this.def.billGiversAllAnimalsCorpses && pawn2.RaceProps.Animal);
        }
    }
}
