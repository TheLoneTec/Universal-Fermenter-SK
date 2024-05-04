using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace UniversalFermenterSK
{
    public class WorkGiver_TakeProductOutOfUF : WorkGiver_Scanner
    {
        public override PathEndMode PathEndMode => PathEndMode.Touch;

        public override bool ShouldSkip(Pawn pawn, bool forced = false)
        {
            return !pawn.Map.GetComponent<MapComponent_UF>().thingsWithUFComp.Any();
        }

        public override IEnumerable<Thing> PotentialWorkThingsGlobal(Pawn pawn)
        {
            return pawn.Map.GetComponent<MapComponent_UF>().thingsWithUFComp;
        }

        public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            return t is Building_UF fermenter
                   && !t.IsBurning()
                   && !t.IsForbidden(pawn)
                   && (fermenter.AnyFinished || fermenter.AnyRuined)
                   && pawn.CanReserveAndReach(t, PathEndMode.Touch, pawn.NormalMaxDanger(), 1, -1, null, forced);
        }

        public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            return new Job(UF_DefOf.TakeProductOutOfUniversalFermenter, t);
        }
    }
}
