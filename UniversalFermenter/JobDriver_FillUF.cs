using RimWorld;
using System.Collections.Generic;
using Verse;
using Verse.AI;

#nullable enable
namespace UniversalFermenterSK
{
    public class JobDriver_FillUF : JobDriver
    {
        private const TargetIndex FermenterInd = TargetIndex.A;
        private const TargetIndex IngredientInd = TargetIndex.B;

        protected Building_UF Fermenter => (Building_UF)job.GetTarget(TargetIndex.A).Thing;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            if (!pawn.Reserve(Fermenter, job, errorOnFailed: errorOnFailed))
                return false;

            pawn.ReserveAsManyAsPossible(job.GetTargetQueue(IngredientInd), job);
            return true;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            // Verify fermenter and ingredient validity
            this.FailOnDespawnedNullOrForbidden(FermenterInd);
            this.FailOnBurningImmobile(FermenterInd);
            this.FailOn(delegate ()
            {
                if (job.GetTarget(FermenterInd).Thing is IBillGiver billGiver)
                {
                    if (job.bill.DeletedOrDereferenced)
                        return true;

                    /*if (!billGiver.CurrentlyUsableForBills())
                        return true;*/
                }

                return false;
            });

            Toil gotoBillGiver = Toils_Goto.GotoThing(FermenterInd, PathEndMode.Touch);
            yield return Toils_Jump.JumpIf(gotoBillGiver, () => job.GetTargetQueue(IngredientInd).NullOrEmpty());

            // Transfer ingridients to fermenter
            Toil extract = Toils_JobTransforms.ExtractNextTargetFromQueue(IngredientInd);
            yield return extract;
            Toil getToHaulTarget = Toils_Goto.GotoThing(IngredientInd, PathEndMode.ClosestTouch)
                .FailOnDespawnedNullOrForbidden(IngredientInd)
                .FailOnSomeonePhysicallyInteracting(IngredientInd);

            yield return getToHaulTarget;
            yield return Toils_Haul.StartCarryThing(IngredientInd, true, false, true);
            yield return JobDriver_DoBill.JumpToCollectNextIntoHandsForBill(getToHaulTarget, IngredientInd);
            yield return gotoBillGiver;
            Toil findPlaceTarget = Toils_JobTransforms.SetTargetToIngredientPlaceCell(FermenterInd, IngredientInd, TargetIndex.C);
            yield return findPlaceTarget;
            yield return Toils_Haul.PlaceHauledThingInCell(TargetIndex.C, findPlaceTarget, storageMode: false);
            yield return Toils_Jump.JumpIfHaveTargetInQueue(IngredientInd, extract);

            yield return gotoBillGiver;

            // Add delay for adding ingredients to the fermenter
            yield return Toils_General.Wait(Fermenter.DefUF.fillDelay, FermenterInd)
                .FailOnDespawnedNullOrForbiddenPlacedThings(TargetIndex.B)
                .FailOnDestroyedNullOrForbidden(FermenterInd)
                .FailOnCannotTouch(FermenterInd, PathEndMode.Touch)
                .WithProgressBarToilDelay(FermenterInd);

            // Fill fermenter
            // The UniversalFermenter automatically destroys held ingredients
            var toil = new Toil()
            {
                //defaultCompleteMode = ToilCompleteMode.Instant
            };
            toil.initAction = () =>
            {
                Pawn actor = toil.actor;
                Job curJob = actor.jobs.curJob;

                List<Thing> ingredients = VanillaPrivate.CalculateIngredients(curJob, actor);
                Thing? dominantIngredient = VanillaPrivate.CalculateDominantIngredient(curJob, ingredients);

                // Despawn ingredients
                foreach (var thing in ingredients)
                {
                    actor.Map.designationManager.RemoveAllDesignationsOn(thing);
                    if (thing.Spawned)
                        thing.DeSpawn();
                }

                // Add UF_Progress
                Fermenter.AddProgress(ingredients, dominantIngredient, (RecipeDef_UF)curJob.bill.recipe);

                // Notify bill with RepeatCount
                if (((Bill_Production)curJob.bill).repeatMode == BillRepeatModeDefOf.RepeatCount)
                    curJob.bill.Notify_IterationCompleted(actor, ingredients);

                actor.jobs.EndCurrentJob(JobCondition.Succeeded);
            };

            yield return toil;
        }
    }
}
