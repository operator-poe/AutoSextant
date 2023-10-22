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
  public static IEnumerator ClickDropDownMenuStashTabLabel(int tabIndex)
  {
    var dropdownMenu = Instance.GameController.Game.IngameState.IngameUi.StashElement.ViewAllStashPanel;
    var stashTabLabels = dropdownMenu.GetChildAtIndex(1);

    //if the stash tab index we want to visit is less or equal to 30, then we scroll all the way to the top.
    //scroll amount (clicks) should always be (stash_tab_count - 31);
    //TODO(if the guy has more than 31*2 tabs and wants to visit stash tab 32 fx, then we need to scroll all the way up (or down) and then scroll 13 clicks after.)

    var clickable = StashLabelIsClickable(tabIndex);
    // we want to go to stash 32 (index 31).
    // 44 - 31 = 13
    // 31 + 45 - 44 = 30
    // MaxShownSideBarStashTabs + _stashCount - tabIndex = index
    var index = clickable ? tabIndex : tabIndex - (StashTabCount - 1 - (MaxShownSidebarStashTabs - 1));
    var pos = stashTabLabels.GetChildAtIndex(index).GetClientRect().Center;
    Input.MoveMouseToElement(pos);
    if (SliderPresent())
    {
      var clicks = StashTabCount - MaxShownSidebarStashTabs;
      yield return Input.Delay(3);
      Input.VerticalScroll(scrollUp: clickable, clicks: clicks);
      yield return Input.Delay(3);
    }

    Input.Click(System.Windows.Forms.MouseButtons.Left);

    yield return Input.Delay(100);
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