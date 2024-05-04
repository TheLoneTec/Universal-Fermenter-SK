#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;

namespace UniversalFermenterSK
{
    public class JobDriver_TakeProductOutOfUF : JobDriver
    {
        private const TargetIndex FermenterInd = TargetIndex.A;
        private const TargetIndex ProductToHaulInd = TargetIndex.B;
        private const TargetIndex StorageCellInd = TargetIndex.C;

        protected Building_UF Fermenter => (Building_UF)job.GetTarget(FermenterInd).Thing;

        protected Thing Product => job.GetTarget(ProductToHaulInd).Thing;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(Fermenter, job);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            // Verify fermenter validity
            this.FailOn(() => Fermenter.Empty || !(Fermenter.AnyFinished || Fermenter.AnyRuined));
            this.FailOnDestroyedNullOrForbidden(FermenterInd);

            // Reserve fermenter
            yield return Toils_Reserve.Reserve(FermenterInd);

            // Go to the fermenter
            yield return Toils_Goto.GotoThing(FermenterInd, PathEndMode.ClosestTouch);

            // Add delay for collecting product from fermenter, if it is ready
            yield return Toils_General.Wait(Fermenter.DefUF.takeDelay)
                .FailOnDestroyedNullOrForbidden(FermenterInd)
                .WithProgressBarToilDelay(FermenterInd);

            // Collect products
            yield return FinishRecipeAndStartStoringProduct(Fermenter);

            // Reserve the storage cell
            yield return Toils_Reserve.Reserve(StorageCellInd);

            // Carry the product to the storage cell, then place it down
            Toil carry = Toils_Haul.CarryHauledThingToCell(StorageCellInd);
            yield return carry;
            yield return Toils_Haul.PlaceHauledThingInCell(StorageCellInd, carry, true);
            yield break;
        }

        public void CalculateBonus(UF_Progress progress, ref List<Thing> products)
        {

            foreach (BonusOutput item in progress.Process.bonusOutputs)
            {
                // If Ruined, check for ruinedProducts
                if (progress.Ruined && item.isRuinedProduct)
                {
                    AddBonusStacks(progress, item, ref products);
                }
                else if (!progress.Ruined && !item.isRuinedProduct)
                {
                    AddBonusStacks(progress, item, ref products);
                }
            }
        }

        public void AddBonusStacks(UF_Progress progress,BonusOutput item, ref List<Thing> products)
        {
            Random randomGen = new Random();
            int stacks = 0;

            if (randomGen.Next(100) / 100 <= item.chance)
            {
                Thing thing = new Thing();
                if (item.thingDef != null && progress.dominantIngredient != null)
                    thing = ThingMaker.MakeThing(stuff: (!item.thingDef.MadeFromStuff) ? null : progress.dominantIngredient.def, def: item.thingDef);

                if (thing == null || thing.def == null)
                    return;

                if (thing.def.stackLimit > 1 && item.amount <= thing.def.stackLimit)
                {
                    // stack count more than 1 but less than 1 stack
                    thing.stackCount = item.amount;
                }
                else if (thing.def.stackLimit > 1)
                {
                    // mod the amount of stacks
                    stacks = Convert.ToInt32(Math.Floor((double)item.amount / thing.def.stackLimit));
                    thing.stackCount = thing.def.stackLimit;
                    for (int i = 0; i < stacks; i++)
                    {
                        products.Add(thing);
                    }
                    thing.stackCount = item.amount % thing.def.stackLimit;
                }
                else
                {
                    //if not stackable, add extra items to drop
                    if (item.amount > 1)
                    {
                        for (int i = 0; i < item.amount - 1; i++)
                        {
                            products.Add(thing);
                        }
                    }
                }

                products.Add(thing);
            }
        }

        public Toil FinishRecipeAndStartStoringProduct(Building_UF fermenter)
        {
            var toil = new Toil()
            {
                defaultCompleteMode = ToilCompleteMode.Instant
            };
            toil.initAction = () =>
            {
                Pawn actor = toil.actor;
                Job curJob = actor.jobs.curJob;

                UF_Progress? progress = fermenter.progresses.First(x => x.Finished || x.Ruined);
                var products = fermenter.TakeOutProduct(progress, actor);

                //Check bonus products
                CalculateBonus(progress, ref products);

                // Remove a ruined product
                if (products.Count == 0)
                {
                    EndJobWith(JobCondition.Succeeded);
                    return;
                }

                // Calc store mode
                // Note! Bill used for JobDriver_FillUF can already be deleted or changed at this moment.
                // Thus we should find suitable Bill in current BillStack.
                Bill_Production? curBill = null;
                foreach (var bill in fermenter.BillStack)
                    if (bill.recipe == progress.Process)
                        if (bill.ShouldDoNow())
                        {
                            curBill = (Bill_Production)bill;
                            break;
                        }
                if (curBill == null)
                    foreach (var bill in fermenter.BillStack)
                        if (bill.recipe == progress.Process)
                        {
                            curBill = (Bill_Production)bill;
                            break;
                        }

                BillStoreModeDef storeMode = curBill?.GetStoreMode() ?? BillStoreModeDefOf.DropOnFloor;
                BillRepeatModeDef repeatMode = curBill?.repeatMode ?? BillRepeatModeDefOf.RepeatCount;

                // Notify bill with not RepeatCount
                /*

                if (curBill != null)
                    curBill.Notify_IterationCompleted(actor, progress.ingredients);*/

                // Consume ingredients
                VanillaPrivate.ConsumeIngredients(progress.ingredients, progress.Process, actor.Map);

                // Notify quests
                if (products.Any())
                    Find.QuestManager.Notify_ThingsProduced(actor, products);

                // Notify map
                if (repeatMode == BillRepeatModeDefOf.TargetCount)
                    actor.Map.resourceCounter.UpdateResourceCounts();

                // Drop all products
                if (storeMode == BillStoreModeDefOf.DropOnFloor)
                {
                    foreach (var product in products)
                        if (!GenPlace.TryPlaceThing(product, actor.Position, actor.Map, ThingPlaceMode.Near))
                            Log.Error($"{actor} could not drop recipe product {product} near {actor.Position}");

                    actor.jobs.EndCurrentJob(JobCondition.Succeeded);
                }
                else
                {
                    // Drop all products exclude first
                    if (products.Count > 1)
                        foreach (var product in products.Skip(1))
                            if (!GenPlace.TryPlaceThing(product, actor.Position, actor.Map, ThingPlaceMode.Near))
                                Log.Error($"{actor} could not drop recipe product {product} near {actor.Position}");

                    // Find storage for first product
                    IntVec3 cell = IntVec3.Invalid;
                    if (storeMode == BillStoreModeDefOf.BestStockpile)
                        StoreUtility.TryFindBestBetterStoreCellFor(products[0], actor, actor.Map, StoragePriority.Unstored, actor.Faction, out cell);
                    else if (storeMode == BillStoreModeDefOf.SpecificStockpile)
                        StoreUtility.TryFindBestBetterStoreCellForIn(products[0], actor, actor.Map, StoragePriority.Unstored, actor.Faction, curBill!.GetStoreZone().slotGroup, out cell);
                    else
                        Log.ErrorOnce("Unknown store mode", 9158246);

                    if (cell.IsValid)
                    {
                        actor.carryTracker.TryStartCarry(products[0]);
                        curJob.SetTarget(StorageCellInd, cell);
                        curJob.SetTarget(ProductToHaulInd, products[0]);
                        curJob.count = products[0].stackCount;
                    }
                    else
                    {
                        if (!GenPlace.TryPlaceThing(products[0], actor.Position, actor.Map, ThingPlaceMode.Near))
                            Log.Error($"Bill does could not drop product {products[0]} near {actor.Position}");
                        actor.jobs.EndCurrentJob(JobCondition.Succeeded);
                    }
                }
                if (Rand.Chance(progress.Process.destroyChance))
                {
                    //if (PF_Settings.replaceDestroyedProcessors)
                    //GenConstruct.PlaceBlueprintForBuild((BuildableDef)this.parent.def, this.parent.Position, this.parent.Map, this.parent.Rotation, Faction.OfPlayer, (ThingDef)null);
                    fermenter.Destroy(DestroyMode.KillFinalize);
                }
            };

            return toil;
        }
    }
}
