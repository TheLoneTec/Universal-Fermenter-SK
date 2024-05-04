#nullable enable
using SK;
using System;
using System.Linq;
using UnityEngine;
using Verse;

namespace UniversalFermenterSK
{
    [StaticConstructorOnStartup]
	public class Building_AnimatedWorktableUF : Building_WorkTable_HeatPushAdvanced
	{
		private string? FramePath = null;
		private string AnimationType = "standart";
		private int FrameCount = 6;
		private int multispeed = 5;

		private int timer;
		private int cycle;

		private Graphic[] TexResFrames = null!;
		private Graphic TexMain = null!;

		private void SetWorkVariables()
		{
			if (!(this.def is ThingDef_AnimatedWorktable))
				return;

			ThingDef_AnimatedWorktable thingDef_AnimatedWorktable = (ThingDef_AnimatedWorktable)this.def;
			this.FramePath = thingDef_AnimatedWorktable.FramePath!;
			this.AnimationType = thingDef_AnimatedWorktable.AnimationType!;
			this.FrameCount = thingDef_AnimatedWorktable.FrameCount;
			this.multispeed = thingDef_AnimatedWorktable.multispeed;
		}

		public override void ExposeData()
		{
			base.ExposeData();
			this.SetWorkVariables();
		}

		public override void SpawnSetup(Map map, bool respawningAfterLoad)
		{
			base.SpawnSetup(map, respawningAfterLoad);
			this.SetWorkVariables();
			LongEventHandler.ExecuteWhenFinished(new Action(this.AnimFrame));
		}

		public void AnimFrame()
		{
			if (!(this.def is ThingDef_AnimatedWorktable))
				return;

			if (this.def.graphic is Graphic_Single)
			{
				Graphic[] texResFrames = new Graphic_Single[this.FrameCount];
				this.TexResFrames = texResFrames;
				for (int i = 0; i < this.FrameCount; i++)
				{
					this.TexResFrames[i] = GraphicDatabase.Get<Graphic_Single>(this.FramePath + (i + 1).ToString(), this.def.graphicData.Graphic.Shader);
					this.TexResFrames[i].drawSize = this.def.graphicData.drawSize;
				}
			}
			if (this.def.graphic is Graphic_Multi)
			{
				Graphic[] texResFrames = new Graphic_Multi[this.FrameCount];
				this.TexResFrames = texResFrames;
				for (int j = 0; j < this.FrameCount; j++)
				{
					this.TexResFrames[j] = GraphicDatabase.Get<Graphic_Multi>(this.FramePath + (j + 1).ToString(), this.def.graphicData.Graphic.Shader);
					this.TexResFrames[j].drawSize = this.def.graphicData.drawSize;
				}
			}
		}

		protected void AnimationTick()
		{
			if (!(this.def is ThingDef_AnimatedWorktable))
				return;

			this.handleAnimation();
			if (this.AnimationType == "standart")
			{
				if ((float)Find.TickManager.TicksGame % TimeScale.timescalefloat == 0f)
					this.timer++;

				if (this.timer >= this.TexResFrames.Count<Graphic>() * this.multispeed)
					this.timer = 0;
			}

			if (this.AnimationType == "revert")
			{
				if (this.cycle == 0 && (float)Find.TickManager.TicksGame % TimeScale.timescalefloat == 0f)
					this.timer++;

				if (this.cycle == 1 && (float)Find.TickManager.TicksGame % TimeScale.timescalefloat == 0f)
					this.timer--;

				if (this.timer >= this.TexResFrames.Count<Graphic>() * this.multispeed)
					this.cycle = 1;
			}
		}

		private void handleAnimation()
		{
				if (this.TexResFrames == null)
				{
        return;
    }
    if (this.timer < this.TexResFrames.Count<Graphic>() * this.multispeed)
			{
				if (this.AnimationType == "standart")
				{
     int num = this.timer / this.multispeed;
					this.TexMain = this.TexResFrames[num];
					this.TexMain.color = base.Graphic.color;
				}
    if (this.AnimationType == "revert")
				{
     if (this.timer <= 0)
						this.cycle = 0;
     int num2 = this.timer / this.multispeed;
					this.TexMain = this.TexResFrames[num2];
					this.TexMain.color = base.Graphic.color;
				}
			}
		}

		protected void DrawAnimation()
		{
			if (this.TexMain != null)
			{
				Mesh mesh = this.TexMain.MeshAt(base.Rotation);
				Material material = this.TexMain.MatAt(base.Rotation, null);

				if (this.def.graphic is Graphic_Single)
					Graphics.DrawMesh(mesh, this.DrawPos + Altitudes.AltIncVect, base.Rotation.AsQuat, material, 0);

				if (this.def.graphic is Graphic_Multi)
					Graphics.DrawMesh(mesh, this.DrawPos + Altitudes.AltIncVect, Quaternion.identity, material, 0);
			}
		}
	}
}
