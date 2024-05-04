// Decompiled with JetBrains decompiler
// Type: UniversalFermenterSK.BonusOutput
// Assembly: UniversalFermenter_SK, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: F71925FF-C036-47EA-B3F6-7C0C1783DA95
// Assembly location: E:\AlternativeSteamVersions\RimWorldHSK_1.4\Mods\Core_SK\Assemblies\UniversalFermenter_SK.dll

using System;
using System.Globalization;
using System.Xml;
using Verse;


#nullable enable
namespace UniversalFermenterSK
{
  public class BonusOutput
  {
    public ThingDef ?thingDef;
    public float chance;
    public int amount;
    public bool isRuinedProduct;

    public void LoadDataFromXmlCustom(XmlNode xmlRoot)
    {
      if (xmlRoot.ChildNodes.Count != 1)
      {
        Log.Error("PF: RandomProductList configured incorrectly");
      }
      else
      {
        string[] strArray = xmlRoot.FirstChild.Value.TrimStart('(').TrimEnd(')').Split(',');
        CultureInfo invariantCulture = CultureInfo.InvariantCulture;
        this.chance = Convert.ToSingle(strArray[0], (IFormatProvider) invariantCulture);
        this.amount = Convert.ToInt32(strArray[1], (IFormatProvider) invariantCulture);
        this.isRuinedProduct = Convert.ToBoolean(strArray[2], (IFormatProvider) invariantCulture);
        DirectXmlCrossRefLoader.RegisterObjectWantsCrossRef((object) this, "thingDef", xmlRoot.Name);
      }
    }
  }
}
