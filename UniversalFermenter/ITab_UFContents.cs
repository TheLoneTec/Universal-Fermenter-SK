using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

#nullable enable
namespace UniversalFermenterSK
{
    public class ITab_UFContents : ITab
    {
        private const float H = 28;
        private const float Icon = 24;
        private const float Pad = 4;
        private const float PaddedIcon = Pad + Icon + Pad;
        private const float ThingWidth = 230;

        public static readonly List<Pair<float, Color>> WhiteToYellowToRed = new()
        {
            new Pair<float, Color>(0, new Color(1f, 0.2f, 0.2f)),
            new Pair<float, Color>(0.5f, Color.yellow),
            new Pair<float, Color>(1, ITab_Pawn_Gear.ThingLabelColor)
        };

        private float lastDrawnHeight;
        private Vector2 scrollPosition;

        public ITab_UFContents()
        {
            labelKey = "UF_TabContents";
            size = new Vector2(600f, 450f);
        }

        protected override void FillTab()
        {
            if (SelThing is not Building_UF fermenter)
                return;

            Rect outRect = new Rect(default, size).ContractedBy(10f);
            outRect.yMin += 20f;
            Rect rect = new(0f, 0f, outRect.width, Mathf.Max(lastDrawnHeight, outRect.height));
            Text.Font = GameFont.Small;
            Widgets.BeginScrollView(outRect, ref scrollPosition, rect);
            float num = 0f;
            DoItemsLists(rect, ref num, fermenter);
            lastDrawnHeight = num;
            Widgets.EndScrollView();
        }

        protected void DoItemsLists(Rect inRect, ref float curY, Building_UF fermenter)
        {
            GUI.BeginGroup(inRect);

            GUI.color = Widgets.SeparatorLabelColor;
            Text.Anchor = TextAnchor.UpperLeft;

            Widgets.Label(new Rect(ThingWidth, curY + 3, ThingWidth, 30), "UF_Product".Translate());
            Widgets.Label(new Rect(ThingWidth + ThingWidth, curY + 3, inRect.width - ThingWidth - ThingWidth, 30), "UF_TargetQuality".Translate());
            Widgets.ListSeparator(ref curY, inRect.width, "UF_FermentingItems".Translate());

            if (fermenter.progresses.Count > 0)
                foreach (var progress in fermenter.progresses)
                    DoProgress(progress, inRect.width, ref curY);
            else
                Widgets.NoneLabel(ref curY, inRect.width);

            GUI.EndGroup();
        }

        protected void DoProgress(UF_Progress progress, float width, ref float y)
        {
            // Areas
            Rect ingredientArea = new(0, y, ThingWidth, H);
            Rect productArea = new(ingredientArea.xMax, y, ThingWidth, H);

            Rect qualityArea = new(productArea.xMax, y, 50, H);
            Rect percentAndSpeed = new(width - 70, y, 75, H);

            if (Mouse.IsOver(new Rect(0, y, width, H)))
                TargetHighlighter.Highlight(progress.DisplayedIngredient);

            // Hover over percentage or speed icon
            if (Mouse.IsOver(percentAndSpeed))
            {
                GUI.color = ITab_Pawn_Gear.HighlightColor;
                GUI.DrawTexture(percentAndSpeed, TexUI.HighlightTex);
            }

            // Draw ✓, ~, or X based on the current speed
            Texture2D runningTexture = progress.CurrentSpeedFactor <= 0 ? Widgets.CheckboxOffTex
                : progress.CurrentSpeedFactor <= UF_Progress.SlowAtSpeedFactor ? Widgets.CheckboxPartialTex
                : Widgets.CheckboxOnTex;

            GUI.color = Color.white;
            GUI.DrawTexture(new Rect((percentAndSpeed.xMax - PaddedIcon) + Pad, y, Icon, Icon), runningTexture);

            // Progress %
            GUI.color = ITab_Pawn_Gear.ThingLabelColor;
            Text.Anchor = TextAnchor.MiddleRight;
            Widgets.Label(new Rect(percentAndSpeed.x, y, percentAndSpeed.xMax - percentAndSpeed.x - PaddedIcon, H), progress.ProgressPercentFlooredString);

            // Hover over progress %
            TooltipHandler.TipRegion(percentAndSpeed, () => progress.ProgressTooltip, 23492376);

            // Quality label or dropdown to change the quality
            if (progress.Process.UsesQuality)
            {
                Widgets.Dropdown(qualityArea, progress, p => p?.TargetQuality ?? QualityCategory.Normal, GetProgressQualityDropdowns,
                    progress.TargetQuality.GetLabel().CapitalizeFirst(),
                    (Texture2D) UF_Utility.qualityMaterials[progress.TargetQuality].mainTexture);
            }
            else
            {
                if (Mouse.IsOver(qualityArea))
                {
                    GUI.color = ITab_Pawn_Gear.HighlightColor;
                    GUI.DrawTexture(qualityArea, TexUI.HighlightTex);
                }

                GUI.color = ITab_Pawn_Gear.ThingLabelColor;
                Text.Anchor = TextAnchor.MiddleCenter;
                Widgets.Label(qualityArea, "UF_NA".Translate());
            }

            TooltipHandler.TipRegion(qualityArea, () => progress.QualityTooltip, "UF_QualityTooltip1".GetHashCode());

            GUI.color = Color.white;

            // Info buttons
            Widgets.InfoCardButton(ingredientArea.xMax - Pad - Icon, y, progress.DisplayedIngredient);
            Widgets.InfoCardButton(productArea.xMax - Pad - Icon, y, progress.Process.DisplayedProduct.thingDef);

            // Hover over product or ingredient
            Rect ingredientHover = new(ingredientArea.x, y, ingredientArea.width - PaddedIcon, H);
            if (Mouse.IsOver(ingredientHover))
            {
                GUI.color = ITab_Pawn_Gear.HighlightColor;
                GUI.DrawTexture(ingredientHover, TexUI.HighlightTex);
            }

            Rect productHover = new(productArea.x, y, productArea.width - PaddedIcon, H);
            if (Mouse.IsOver(productHover))
            {
                GUI.color = ITab_Pawn_Gear.HighlightColor;
                GUI.DrawTexture(productHover, TexUI.HighlightTex);
            }

            GUI.color = Color.white;

            // Icons for product and ingredient
            if (progress.DisplayedIngredient.def.DrawMatSingle?.mainTexture != null)
                Widgets.ThingIcon(new Rect(ingredientArea.x, y, PaddedIcon, H), progress.DisplayedIngredient);

            if (progress.Process.DisplayedProduct.thingDef.DrawMatSingle?.mainTexture != null)
                Widgets.ThingIcon(new Rect(productArea.x, y, PaddedIcon, H), progress.Process.DisplayedProduct.thingDef);

            // Product and ingredient labels
            Text.Anchor = TextAnchor.MiddleLeft;
            GUI.color = GenUI.LerpColor(WhiteToYellowToRed, 1.0f - progress.ruinedPercent);

            var ingredientLabel = progress.DisplayedIngredient.LabelCap;
            var productLabel = progress.ProductLabel;

            Text.WordWrap = false;
            Widgets.Label(new Rect(ingredientArea.x + PaddedIcon, y, 200 - PaddedIcon, H), ingredientLabel.Truncate(ingredientArea.width));
            Widgets.Label(new Rect(productArea.x + PaddedIcon, y, 200 - PaddedIcon, H), productLabel.Truncate(productArea.width));

            Text.WordWrap = true;
            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = Color.white;
            TooltipHandler.TipRegion(new Rect(productArea.x, y, productArea.width - PaddedIcon, H), () => progress.ProcessTooltip(ingredientLabel, productLabel), "UF_CreatingTooltip1".GetHashCode());
            TooltipHandler.TipRegion(new Rect(ingredientArea.x, y, productArea.width - PaddedIcon, H), () => progress.ProcessTooltip(ingredientLabel, productLabel), "UF_CreatingTooltip2".GetHashCode());

            y += H;
        }

        private IEnumerable<Widgets.DropdownMenuElement<QualityCategory>> GetProgressQualityDropdowns(UF_Progress? progress)
        {
            if (progress == null)
                yield break;

            foreach (QualityCategory quality in QualityUtility.AllQualityCategories)
            {
                Material iconMaterial = UF_Utility.qualityMaterials[quality];
                yield return new Widgets.DropdownMenuElement<QualityCategory>
                {
                    option = new FloatMenuOption(
                        quality.GetLabel().CapitalizeFirst(),
                        () => progress.TargetQuality = quality,
                        (Texture2D) iconMaterial.mainTexture,
                        iconMaterial.color),
                    payload = quality
                };
            }
        }
    }
}
