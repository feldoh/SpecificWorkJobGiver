﻿using System.Linq;
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

		protected override Job TryGiveJob(Pawn pawn)
		{
			if (workGiverDef.Worker is WorkGiver_Scanner worker && !worker.ShouldSkip(pawn))
			{
				bool Validator(Thing x) => (!factionOnly || x.Faction == pawn.Faction) &&
				                           pawn.CanReserve((LocalTargetInfo)x,
					                           ignoreOtherReservations: ignoreOtherReservations);

				LocalTargetInfo target = GenClosest.ClosestThingReachable(pawn.PositionHeld, pawn.MapHeld,
					worker.PotentialWorkThingRequest, worker.PathEndMode, TraverseParms.For(pawn),
					validator: Validator) ?? worker.PotentialWorkThingsGlobal(pawn)?.Where(Validator).FirstOrFallback();
				return target == null ? null : worker.JobOnThing(pawn, target.Thing);
			}

			return null;
		}

		public override float GetPriority(Pawn pawn) => overridePriority < 0f ? 9f : overridePriority;
	}
}