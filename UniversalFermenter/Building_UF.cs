#nullable enable
using RimWorld;
using SK;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using Verse;

namespace UniversalFermenterSK
{
	public class Building_UF : Building_AnimatedWorktableUF, IThingHolder
	{
		private readonly Cacheable<string> inspectStringExtra;

		/// <summary>How much of the building is under a roof.</summary>
		public readonly Cacheable<float> RoofCoverage;

		/// <summary>Possible flickable component on the fermenter.</summary>
		public CompFlickable? flickComp;

		/// <summary>Possible power trader component on the fermenter.</summary>
		public CompPowerTrader? powerTradeComp;

		/// <summary>Possible refuelable component on the fermenter.</summary>
		public CompRefuelable? refuelComp;

		/// <summary>Possible fuelable SK component on the fermenter.</summary>
		public CompFueled? fueledCompSK;

		public CompPowerLowIdleDraw? CompPowerLowIdleDraw => this.TryGetComp<CompPowerLowIdleDraw>();

		public Building_UF()
        {
			RoofCoverage = new Cacheable<float>(CalcRoofCoverage);
			inspectStringExtra = new Cacheable<string>(GetInspectStringExtra);

			innerContainer = new ThingOwner<Thing>(this);
		}

        #region Save/Load

        public List<UF_Progress> progresses = new();

		/// <summary>Selected target quality for fermentation.</summary>
		public QualityCategory targetQuality = QualityCategory.Normal;

		public override void ExposeData()
		{
			base.ExposeData();

			Scribe_Values.Look(ref targetQuality, "UF_targetQuality", QualityCategory.Normal);
			Scribe_Deep.Look(ref innerContainer, "UF_innerContainer", this);
			Scribe_Collections.Look(ref progresses, "UF_progresses", LookMode.Deep, this);
		}

		#endregion

		/// <summary>Gets the component properties for this component.</summary>
		public ThingDef_UF DefUF => (ThingDef_UF)def;

		/// <summary>Gets whether the fermenter is empty.</summary>
		public bool Empty => progresses.Count == 0;

		public override Color DrawColorTwo
        {
            get
            {
                if (SingleProcess?.colorCoded ?? false)
                    return SingleProcess.color;

                return DrawColor;
            }
        }

		public int FilledSlots => progresses.Select(p => p.Process.slotsRequired).Sum();
		public int LeftSlots => DefUF.slotsCount - FilledSlots;

		public RecipeDef_UF? SingleProcess
		{
			get
			{
				if (DefUF.slotsCount == 1)
				{
					if (DefUF.Processes.Count == 1)
						return DefUF.Processes[0];

					if (!Empty)
						return progresses[0].Process;
				}

				if (progresses.Select(p => p.Process).Distinct().Count() == 1)
					return progresses[0].Process;

				return null;
			}
		}

		public bool Fueled => refuelComp == null || refuelComp.HasFuel;
		public bool FueledSK => fueledCompSK == null || fueledCompSK.ReadyForWork;

		public bool Powered => powerTradeComp == null || powerTradeComp.PowerOn;

		public bool FlickedOn => flickComp == null || flickComp.SwitchIsOn;

		public bool AnyFinished => progresses.Any(p => p.Finished);

		public bool AnyRuined => progresses.Any(p => p.Ruined);

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);

			Map.GetComponent<MapComponent_UF>().Register(this);

			refuelComp = GetComp<CompRefuelable>();
			fueledCompSK = GetComp<CompFueled>();
			powerTradeComp = GetComp<CompPowerTrader>();
			flickComp = GetComp<CompFlickable>();

			if (!Empty)
				graphicChangeQueued = true;
		}

        public override void DeSpawn(DestroyMode mode = DestroyMode.Vanish)
        {
			Map.GetComponent<MapComponent_UF>().Deregister(this);
			base.DeSpawn(mode);
		}

        public override IEnumerable<Gizmo> GetGizmos()
        {
			//Dev options			
			if (Prefs.DevMode)
				yield return UF_Utility.DebugGizmo();

			//Default buttons
			foreach (Gizmo gizmo in base.GetGizmos())
				yield return gizmo;

			if (DefUF.Processes.Any(process => process.UsesQuality))
				yield return UF_Utility.qualityGizmos[targetQuality];

			foreach (var gizmo in UF_Clipboard.CopyPasteGizmosFor(this))
				yield return gizmo;
		}

		#region Draw

		/// <summary>Is a graphics change request queued?</summary>
		public bool graphicChangeQueued;

		public override void Draw()
		{
			base.Draw();

			if (Fueled && FueledSK && Powered && FlickedOn && !Empty)
				DrawAnimation();

			if (!Empty)
			{
				if (graphicChangeQueued)
				{
					GraphicChange(false);
					graphicChangeQueued = false;
				}

				bool showCurrentQuality = UF_Settings.showCurrentQualityIcon && DefUF.slotsCount == 1 && progresses[0].Process.UsesQuality;
				Vector3 drawPos = DrawPos;
				drawPos.x += DefUF.barOffset.x - (showCurrentQuality ? 0.1f : 0f);
				drawPos.y += 0.05f;
				drawPos.z += DefUF.barOffset.y;

				Vector2 size = Static_Bar.Size * DefUF.barScale;

				// Border
				Graphics.DrawMesh(MeshPool.plane10, Matrix4x4.TRS(drawPos, Quaternion.identity, new Vector3(size.x + 0.1f, 1, size.y + 0.1f)), Static_Bar.UnfilledMat, 0);

				float xPosAccum = 0;
				for (int i = 0; i < progresses.Count; i++)
				{
					UF_Progress? progress = progresses[i];
					float width = size.x / DefUF.slotsCount * progress.Process.slotsRequired;
					float xPos = (drawPos.x - (size.x / 2.0f)) + (width / 2.0f) + xPosAccum;
					xPosAccum += width;
					var material = DefUF.slotsCount == 1 ? progress.ProgressColorMaterial : Static_Bar.FilledMat;
					Graphics.DrawMesh(MeshPool.plane10, Matrix4x4.TRS(new Vector3(xPos, drawPos.y + 0.01f, drawPos.z), Quaternion.identity, new Vector3(width, 1, size.y)), material, 0);
				}

				if (showCurrentQuality) // show small icon for current quality over bar
				{
					drawPos.y += 0.02f;
					drawPos.x += 0.45f * DefUF.barScale.x;
					Matrix4x4 matrix2 = default;
					matrix2.SetTRS(drawPos, Quaternion.identity, new Vector3(0.2f * DefUF.barScale.x, 1f, 0.2f * DefUF.barScale.y));
					Graphics.DrawMesh(MeshPool.plane10, matrix2, UF_Utility.qualityMaterials[progresses[0].CurrentQuality], 0);
				}
			}

			RecipeDef_UF? singleProcess = SingleProcess;
			if (UF_Settings.showProcessIconGlobal && !Empty && DefUF.showProductIcon && singleProcess != null)
			{
				Vector3 drawPos = DrawPos;
				float sizeX = UF_Settings.processIconSize * DefUF.productIconSize.x;
				float sizeZ = UF_Settings.processIconSize * DefUF.productIconSize.y;
				if (DefUF.Processes.Count == 1 && singleProcess.UsesQuality) // show larger, centered quality icon if object has only one process
				{
					drawPos.y += 0.02f;
					drawPos.z += 0.05f;
					Matrix4x4 matrix = default;
					matrix.SetTRS(drawPos, Quaternion.identity, new Vector3(0.6f * sizeX, 1f, 0.6f * sizeZ));
					Graphics.DrawMesh(MeshPool.plane10, matrix, UF_Utility.qualityMaterials[progresses[0].TargetQuality], 0);
				}
				else if (DefUF.Processes.Count > 1) // show process icon if object has more than one process
				{
					drawPos.y += 0.02f;
					drawPos.z += 0.05f;
					Matrix4x4 matrix = default;
					matrix.SetTRS(drawPos, Quaternion.identity, new Vector3(sizeX, 1f, sizeZ));
					Graphics.DrawMesh(MeshPool.plane10, matrix, UF_Utility.processMaterials[singleProcess], 0);
					if (progresses.Count > 0 && singleProcess.UsesQuality && UF_Settings.showTargetQualityIcon) // show small offset quality icon if object also uses quality
					{
						drawPos.y += 0.01f;
						drawPos.x += 0.25f * sizeX;
						drawPos.z -= 0.35f * sizeZ;
						Matrix4x4 matrix2 = default;
						matrix2.SetTRS(drawPos, Quaternion.identity, new Vector3(0.4f * sizeX, 1f, 0.4f * sizeZ));
						Graphics.DrawMesh(MeshPool.plane10, matrix2, UF_Utility.qualityMaterials[progresses[0].TargetQuality], 0);
					}
				}
			}
		}

		#endregion

		// Inspector string eats max. 5 lines - there is room for one more
		public override string GetInspectString()
        {
			StringBuilder stringBuilder = new();
			stringBuilder.Append(inspectStringExtra);

			string baseString = base.GetInspectString().TrimEndNewlines();
			if (baseString != "")
			{
				stringBuilder.AppendLine();
				stringBuilder.Append(base.GetInspectString());
			}

			return stringBuilder.ToString();
		}

		private string GetInspectStringExtra()
		{
			// Perf: Only recalculate this inspect string periodically
			StringBuilder str = new();

			// Line 1. Show the filled slots and products in the fermenter
			if (!Empty)
			{
				if (DefUF.slotsCount == 1)
				{
					var progress = progresses[0];
					str.AppendTagged("UF_ContainsSingle".Translate(progress.Process.DisplayedProduct.thingDef.label));
					if (progress.Process.UsesQuality && progress.ProgressDays >= progress.Process.qualityDays.awful)
						str.AppendTagged("UF_ContainsQuality".Translate(progress.CurrentQuality.GetLabel().ToLower().Named("QUALITY")));
				}
				else
				{
					var products = progresses.Select(p => p.Process.DisplayedProduct.thingDef).Distinct().Select(t => t.LabelCap).ToList();
					str.AppendTagged("UF_ContainsMultipleMixed".Translate(FilledSlots, DefUF.slotsCount, string.Join(", ", products).Named("PRODUCTS")));
				}
			}
			else
			{
				str.AppendTagged("UF_NoIngredient".TranslateSimple());
			}
			str.AppendLine();

			// Line 2. Show how many processes are running, or the current status of the process
			if (!Empty)
			{
				if (DefUF.slotsCount != 1)
				{
					int running = progresses.Count(p => p.Running);
					str.AppendTagged("UF_NumProcessing".Translate(running));

					int slow = progresses.Count(p => p.CurrentSpeedFactor < UF_Progress.SlowAtSpeedFactor);
					if (slow > 0)
						str.AppendTagged("UF_RunningCountSlow".Translate(slow));

					int finished = progresses.Count(p => p.Finished);
					if (finished > 0)
						str.AppendTagged("UF_RunningCountFinished".Translate(finished));

					int ruined = progresses.Count(p => p.Ruined);
					if (ruined > 0)
						str.AppendTagged("UF_RunningCountRuined".Translate(ruined));
				}
				else
				{
					if (progresses[0].Finished)
						str.AppendTagged("UF_Finished".Translate());
					else if (progresses[0].Ruined)
						str.AppendTagged("UF_Ruined".Translate());
					else if (progresses[0].CurrentSpeedFactor < UF_Progress.SlowAtSpeedFactor)
						str.AppendTagged("UF_RunningSlow".Translate(progresses[0].CurrentSpeedFactor.ToStringPercent(), progresses[0].ProgressPercentFlooredString.Value));
					else
						str.AppendTagged("UF_RunningInfo".Translate(progresses[0].ProgressPercentFlooredString.Value));
				}

				str.AppendLine();
			}

			// Line 3. Show the ambient temperature, and if overheating/freezing
			RecipeDef_UF? singleProcess = SingleProcess;
			if (progresses.Any(p => p.Process.usesTemperature))
			{
				float ambientTemperature = AmbientTemperature;
				str.AppendFormat("{0}: {1}", "Temperature".TranslateSimple(), ambientTemperature.ToStringTemperature("F0"));

				if (singleProcess != null)
				{
					if (singleProcess.temperatureSafe.Includes(ambientTemperature))
					{
						str.AppendFormat(" ({0})", singleProcess.temperatureIdeal.Includes(ambientTemperature) ? "UF_Ideal".TranslateSimple() : "UF_Safe".TranslateSimple());
					}
					else
					{
						bool overheating = ambientTemperature < singleProcess.temperatureSafe.TrueMin;
						str.AppendFormat(" ({0}{1})".Colorize(overheating ? new Color(235, 125, 75) : new Color(95, 170, 195)),
							overheating ? "Freezing".TranslateSimple() : "Overheating".TranslateSimple(),
							DefUF.slotsCount == 1 ? $" {progresses[0].ruinedPercent.ToStringPercent()}" : "");
					}
				}
				else
				{
					bool abort = false;
					foreach (UF_Progress progress in progresses)
					{
						if (ambientTemperature > progress.Process.temperatureSafe.TrueMax)
						{
							str.AppendFormat(" ({0})", "Freezing".TranslateSimple());
							abort = true;
							break;
						}

						if (ambientTemperature < progress.Process.temperatureSafe.TrueMin)
						{
							str.AppendFormat(" ({0})", "Overheating".TranslateSimple());
							abort = true;
							break;
						}
					}

					if (!abort)
					{
						foreach (UF_Progress progress in progresses)
						{
							if (progress.Process.temperatureIdeal.Includes(ambientTemperature))
							{
								str.AppendFormat(" ({0})", "UF_Safe".TranslateSimple());
								abort = true;
								break;
							}
						}
					}

					if (!abort)
					{
						str.AppendFormat(" ({0})", "UF_Ideal".TranslateSimple());
					}
				}

				str.AppendLine();
			}

			// Line 4. Ideal temp range
			if (singleProcess?.usesTemperature ?? false)
			{
				str.AppendFormat("{0}: {1}~{2} ({3}~{4})", "UF_IdealSafeProductionTemperature".TranslateSimple(),
					singleProcess.temperatureIdeal.min.ToStringTemperature("F0"),
					singleProcess.temperatureIdeal.max.ToStringTemperature("F0"),
					singleProcess.temperatureSafe.min.ToStringTemperature("F0"),
					singleProcess.temperatureSafe.max.ToStringTemperature("F0"));
			}

			return str.ToString().TrimEndNewlines();
		}

        private float CalcRoofCoverage()
		{
			if (Map == null) return 0f;

			int allTiles = 0;
			int roofedTiles = 0;
			foreach (IntVec3 current in this.OccupiedRect())
			{
				allTiles++;
				if (Map.roofGrid.Roofed(current))
					roofedTiles++;
			}

			return roofedTiles / (float)allTiles;
		}

		#region Tick

		private int tickCounter;

		void CompFueledTick()
        {
			if (compFueled != null)
			{
				if (compFlickable != null && !compFlickable.SwitchIsOn)
				{
					compFueled.shouldWorkForced = false;
					return;
				}
				if (!Empty)
				{
					compFueled.shouldWorkForced = true;
					return;
				}
				compFueled.shouldWorkForced = false;
			}
		}

		public override void Tick()
        {
			DoTicks(1);

			if (compFueled != null)
			{
				// Return compFueled.ShouldWork old value because we calc it in CompFueledTick method.
				// At compFueled.ShouldBurn should use true compFueled.ShouldWork value.
				bool shouldWork = compFueled.ShouldWork;
				base.Tick();
				compFueled.shouldWorkForced = shouldWork;
			}
			else
				base.Tick();

			if (Spawned && Fueled && FueledSK && Powered && FlickedOn)
				base.AnimationTick();

			CompFueledTick();

			if (CompPowerLowIdleDraw != null)
            {
				if (!Empty && CompPowerLowIdleDraw.LowPowerMode)
					VanillaPrivate.TogglePower(CompPowerLowIdleDraw);

				if (Empty && !CompPowerLowIdleDraw.LowPowerMode)
					VanillaPrivate.TogglePower(CompPowerLowIdleDraw);
			}
		}

		public override void TickRare()
		{
			DoTicks(GenTicks.TickRareInterval);
		}

		public override void TickLong()
		{
			DoTicks(GenTicks.TickLongInterval);
		}

		public void DoTicks(int ticks)
		{
			tickCounter += ticks;

			foreach (UF_Progress progress in progresses)
				progress.DoTicks(ticks);

			// Note! fueledCompSK consume fuel at CompTick
			if (Fueled && refuelComp?.Props.consumeFuelOnlyWhenUsed == true && !Empty)
				refuelComp.ConsumeFuel((refuelComp.Props.fuelConsumptionRate / GenDate.TicksPerDay) * ticks);

			if (tickCounter >= GenTicks.TickRareInterval)
			{
				while (tickCounter >= GenTicks.TickRareInterval)
					tickCounter -= GenTicks.TickRareInterval;

				CachesInvalid(true);

				foreach (UF_Progress progress in progresses)
				{
					progress.TickRare();
				}
			}
		}

		#endregion

		public void AddProgress(List<Thing> ingredients, Thing? dominantIngredient, RecipeDef_UF process)
		{
			try
			{
				if (!Empty && LeftSlots == 0)
					throw new UFException($"Tried to add {process} to {Label}, but all {DefUF.slotsCount} fermenter slots is used already.");

				bool wasEmpty = Empty;

				// Add UF_Progress
				progresses.Add(new UF_Progress(this, ingredients, dominantIngredient, process)
				{
					TargetQuality = targetQuality,
				});

				// Save ingredients to innerContainer
				foreach (var ingredient in ingredients)
				{
					bool added = ingredient.holdingOwner?.TryTransferToContainer(ingredient, innerContainer, false)
							 ?? innerContainer.TryAdd(ingredient, false);

					if (!added)
						throw new UFException($"Tried to add ingredient {ingredient} to innerContainer of {Label} but it did not accept the item.");
				}

				if (wasEmpty && !Empty)
					GraphicChange(false);

				CachesInvalid();
			}
			catch (UFException ex)
			{
				Log.Error(ex.Message);
				/*foreach (var ingredient in ingredients)
                    ingredient.Destroy();*/
			}
		}

		public void CachesInvalid(bool rareTick = false)
		{
			if (rareTick)
			{
				// Check periodically
				RoofCoverage.Invalidate();
			}

			inspectStringExtra.Invalidate();
		}

		public List<Thing> TakeOutProduct(UF_Progress progress, Pawn actor)
		{
			try
			{
				RecipeDef_UF process = progress.Process;

				if (!progress.Finished && !process.UsesQuality && !progress.Ruined)
					throw new UFException($"Tried to get product {process} from {Label}, but it is not done fermenting yet ({progress.ProgressPercent.ToStringPercent()}).");

				if (process.UsesQuality && !progress.Ruined && progress.CurrentQuality < progress.TargetQuality)
					throw new UFException($"Tried to get product {process} from {Label}, but it has not reached the target quality yet (is {progress.CurrentQuality}, wants {progress.TargetQuality}");

				List<Thing> products = new();
				if (!progress.Ruined)
					products = GenRecipe.MakeRecipeProducts(progress.Process, actor, progress.ingredients, progress.dominantIngredient, this).ToList();

				// Remove UF_Progress
				progresses.Remove(progress);

				// Remove ingredients from innerContainer
				foreach (var ingredient in progress.ingredients)
					innerContainer.Take(ingredient, ingredient.stackCount);

				// Disable FoodPoisonCause.IncompetentCook
				foreach (var product in products)
				{
					var comp = product.TryGetComp<CompFoodPoisonable>();
					if (comp != null && comp.cause == FoodPoisonCause.IncompetentCook)
					{
						comp.cause = FoodPoisonCause.Unknown;
						comp.setPoisonPct(0);
					}
				}

				// Scale products on scaleToMeatAmountBase if needs
				if (progress.ScaleToMeatAmount)
				{
					float multilier = progress.ScaleToMeatAmountMultiplier;

					var productsCopy = products.ToList();
					foreach (var product in productsCopy)
					{
						product.stackCount = Mathf.RoundToInt(product.stackCount * multilier);
						if (product.stackCount == 0)
							products.Remove(product);
					}
				}

				if (Empty)
					GraphicChange(true);

				CachesInvalid();

				return products;
			}
			catch (UFException ex)
			{
				Log.Error(ex.Message);
				return new List<Thing>();
			}
		}

		public void GraphicChange(bool toEmpty)
		{
			if (DefUF.Processes.All(p => p.graphicSuffix == null))
				return;

			string? texPath = def.graphicData.texPath;
			string? suffix = progresses.FirstOrDefault()?.Process.graphicSuffix;

			if (!toEmpty && suffix != null)
				texPath += suffix;

			this.ReloadGraphic(texPath);
		}

		#region IThingHolder

		/// <summary>Container for ingridients</summary>
		public ThingOwner<Thing> innerContainer;

		public ThingOwner GetDirectlyHeldThings()
		{
			return innerContainer;
		}

		public void GetChildHolders(List<IThingHolder> outChildren)
		{
			ThingOwnerUtility.AppendThingHoldersFromThings(outChildren, GetDirectlyHeldThings());
		}

		#endregion
	}
}
