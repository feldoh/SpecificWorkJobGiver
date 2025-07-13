using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;

namespace SpecificWorkJobGiver
{
	public class JobGiver_SpecificWork : ThinkNode_JobGiver
	{
		private float overridePriority = -1f;
		private WorkGiverDef workGiverDef = null;
		private bool factionOnly = true;
		private bool ignoreOtherReservations = false;
		private Dictionary<string, int> minNextJobTicks = new();

		public override ThinkNode DeepCopy(bool resolve = true)
		{
			JobGiver_SpecificWork jobGiverSpecificWork = (JobGiver_SpecificWork)base.DeepCopy(resolve);
			jobGiverSpecificWork.overridePriority = overridePriority;
			jobGiverSpecificWork.workGiverDef = workGiverDef;
			jobGiverSpecificWork.factionOnly = factionOnly;
			jobGiverSpecificWork.ignoreOtherReservations = ignoreOtherReservations;
			return jobGiverSpecificWork;
		}
		
		protected override Job TryGiveJob(Pawn pawn)
		{
			int curTicks = Find.TickManager.TicksGame;
			minNextJobTicks.TryGetValue(pawn.ThingID, out var firstValidTickForJob);
			
			if (workGiverDef.Worker is not WorkGiver_Scanner worker ||
			    worker.ShouldSkip(pawn) ||
			    pawn.CurJobDef == JobDefOf.MechCharge || firstValidTickForJob > curTicks) return null;
			
			bool Validator(Thing x) => (!factionOnly || x.Faction == pawn.Faction) &&
			                           !x.IsForbidden(pawn) &&
			                           (x.TryGetComp<CompPowerTrader>()?.PowerOn ?? true) &&
			                           pawn.CanReserve((LocalTargetInfo)x,
				                           ignoreOtherReservations: ignoreOtherReservations);

			LocalTargetInfo target = GenClosest.ClosestThingReachable(pawn.PositionHeld, pawn.MapHeld,
				worker.PotentialWorkThingRequest, worker.PathEndMode, TraverseParms.For(pawn),
				validator: Validator) ?? worker.PotentialWorkThingsGlobal(pawn)?.Where(Validator).FirstOrFallback();
			if (target == null) return null;
			Job job = worker.JobOnThing(pawn, target.Thing);
			
			// Record any time we successfully assign a job so we can detect when we're assigning the same job repeatedly and back off
			if (job != null) minNextJobTicks[pawn.ThingID] = curTicks + (firstValidTickForJob == curTicks ? 100 : 0);

			return job;
		}

		public override float GetPriority(Pawn pawn) => overridePriority < 0f ? 9f : overridePriority;
	}
}
