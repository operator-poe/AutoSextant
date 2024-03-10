using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ExileCore.PoEMemory.Elements.InventoryElements;

namespace AutoSextant;

public class Stash
{
  private static AutoSextant Instance = AutoSextant.Instance;
  private const int MaxShownSidebarStashTabs = 31;

  public static bool IsVisible
  {
    get
    {
      return AutoSextant.Instance.GameController.IngameState.IngameUi.StashElement.IsVisible;
    }
  }
  public static IList<string> StashTabNames
  {
    get
    {
      return AutoSextant.Instance.GameController.IngameState.IngameUi.StashElement.AllStashNames;
    }
  }

  public static int StashTabCount
  {
    get
    {
      return (int)Instance.GameController.Game.IngameState.IngameUi.StashElement.TotalStashes;
    }
  }

  public static IList<NormalInventoryItem> StashTabItems
  {
    get
    {
      if (!IsVisible)
      {
        return null;
      }
      return Instance.GameController.Game.IngameState.IngameUi.StashElement.VisibleStash.VisibleInventoryItems;
    }
  }

  public static List<NormalInventoryItem> Compasses
  {
    get
    {
      return GetItemTypeFromStash("Surveyor's Compass");
    }
  }

  public static List<NormalInventoryItem> Sextants
  {
    get
    {
      return GetItemTypeFromStash("Awakened Sextant");
    }
  }

  public static Item NextSextant

  {
    get
    {
      var s = Sextants;
      if (s.Count == 0)
        return null;
      return new Item(s.First());
    }
  }

  public static Item NextCompass
  {
    get
    {
      var c = Compasses;
      if (c == null)
        return null;
      return new Item(c.First());
    }
  }

  public static int GetStashTabIndexForName(string name)
  {
    return StashTabNames.IndexOf(name);
  }

  private static bool StashLabelIsClickable(int index)
  {
    return index + 1 < MaxShownSidebarStashTabs;
  }

  private static bool SliderPresent()
  {
    return StashTabCount > MaxShownSidebarStashTabs;
  }

  public static NormalInventoryItem GetFirstItemTypeFromStash(string name)
  {
    try { return GetItemTypeFromStash(name).First(); }
    catch { return null; }
  }

  public static List<NormalInventoryItem> GetItemTypeFromStash(string name)
  {
    var items = new List<NormalInventoryItem>();
    var stashItems = StashTabItems;
    if (stashItems == null)
    {
      return null;
    }
    foreach (var item in stashItems)
    {
      if (item?.Item == null)
      {
        continue;
      }
      var baseItem = Instance.GameController.Files.BaseItemTypes.Translate(item.Item.Path);
      if (baseItem == null)
      {
        continue;
      }
      if (baseItem.BaseName == name)
      {
        items.Add(item);
      }
    }
    return items;
  }
}