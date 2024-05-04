#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace UniversalFermenterSK
{
    public class Command_Quality : Command_Action
    {
        public QualityCategory qualityToTarget;

        public override IEnumerable<FloatMenuOption> RightClickFloatMenuOptions
        {
            get
            {
                List<FloatMenuOption> qualityfloatMenuOptions = new();
                foreach (QualityCategory quality in Enum.GetValues(typeof(QualityCategory)))
                {
                    qualityfloatMenuOptions.Add(
                        new FloatMenuOption(
                            quality.GetLabel(),
                            () => ChangeQuality(qualityToTarget, quality),
                            (Texture2D) UF_Utility.qualityMaterials[quality].mainTexture,
                            Color.white
                        )
                    );
                }

                return qualityfloatMenuOptions;
            }
        }

        internal static void ChangeQuality(QualityCategory qualityToTarget, QualityCategory quality)
        {
            foreach (Building_UF thing in Find.Selector.SelectedObjects.OfType<Building_UF>())
            {
                if (thing.DefUF.Processes.Any(p => p.UsesQuality) && thing.targetQuality == qualityToTarget)
                {
                    thing.targetQuality = quality;
                }
            }
        }
    }
}
