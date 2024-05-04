using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace UniversalFermenterSK
{
    public static class UF_Clipboard
    {
        private static readonly Dictionary<ThingDef, List<Bill>> Copies = new();

        public static bool HasCopiedSettings(Building_UF fermenter)
        {
            return Copies.ContainsKey(fermenter.def);
        }

        public static void Copy(Building_UF fermenter)
        {
            var bills = new List<Bill>();
            foreach (var bill in fermenter.BillStack)
            {
                var billCopy = bill.Clone();
                billCopy.InitializeAfterClone();
                bills.Add(billCopy);
            }
            Copies[fermenter.def] = bills;
            SoundDefOf.Tick_Low.PlayOneShotOnCamera(null);
        }

        private static void PasteInto(Building_UF fermenter)
        {
            if (!Copies.TryGetValue(fermenter.def, out var bills))
                return;

            foreach (var bill in bills)
            {
                var billCopy = bill.Clone();
                billCopy.InitializeAfterClone();
                fermenter.BillStack.AddBill(billCopy);
            }

            SoundDefOf.Tick_Low.PlayOneShotOnCamera(null);
        }

        public static IEnumerable<Gizmo> CopyPasteGizmosFor(Building_UF fermenter)
        {
            yield return new Command_Action
            {
                icon = ContentFinder<Texture2D>.Get("UI/Commands/CopySettings"),
                defaultLabel = "CommandCopyZoneSettingsLabel".Translate(),
                defaultDesc = "CommandCopyZoneSettingsDesc".Translate(),
                action = () =>
                {
                    SoundDefOf.Tick_High.PlayOneShotOnCamera();
                    Copy(fermenter);
                },
                hotKey = KeyBindingDefOf.Misc4
            };

            Command_Action paste = new()
            {
                icon = ContentFinder<Texture2D>.Get("UI/Commands/PasteSettings"),
                defaultLabel = "CommandPasteZoneSettingsLabel".Translate(),
                defaultDesc = "CommandPasteZoneSettingsDesc".Translate(),
                action = () =>
                {
                    SoundDefOf.Tick_High.PlayOneShotOnCamera();
                    PasteInto(fermenter);
                },
                hotKey = KeyBindingDefOf.Misc5
            };

            if (!HasCopiedSettings(fermenter))
                paste.Disable();

            yield return paste;
        }
    }
}
