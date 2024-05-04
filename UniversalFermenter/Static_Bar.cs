using UnityEngine;
using Verse;

namespace UniversalFermenterSK
{
    /// <summary>Static data for the progress bar that appears on fermenters.</summary>
    [StaticConstructorOnStartup]
    public static class Static_Bar
    {
        /// <summary>Base size of the progress bar that appears on fermenters.</summary>
        public static readonly Vector2 Size = new(0.55f, 0.1f);

        /// <summary>Color for the progress bar on fermenters at 0% progress.</summary>
        public static readonly Color ZeroProgressColor = new(0.3f, 0.3f, 0.3f);

        /// <summary>Color for the progress bar on fermenters at 100% progress.</summary>
        public static readonly Color FermentedColor = new(0.9f, 0.85f, 0.2f);

        /// <summary>Material for the progress bar on fermenters at 0% progress.</summary>
        public static readonly Material UnfilledMat = SolidColorMaterials.SimpleSolidColorMaterial(new Color(0.2f, 0.2f, 0.2f));

        /// <summary>Material for the progress bar on fermenters at 100% progress.</summary>
        public static readonly Material BlackMat = SolidColorMaterials.SimpleSolidColorMaterial(new Color(0f, 0f, 0f));

        public static readonly Material FilledMat = SolidColorMaterials.SimpleSolidColorMaterial(FermentedColor);
    }
}
