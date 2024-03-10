using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata.Ecma335;
using AutoSextant.SellAssistant;
using ExileCore;
using ExileCore.PoEMemory.Components;
using ExileCore.Shared;

namespace AutoSextant;

public static class Stock
{
  private static AutoSextant Instance = AutoSextant.Instance;
  private static bool _dirty = true;
  private static readonly Dictionary<string, int> _compassCounts = new Dictionary<string, int>(600);
  public static readonly Dictionary<string, Dictionary<string, int>> Tabs = new Dictionary<string, Dictionary<string, int>>();

  public static int Get(string modName) => CompassCounts.TryGetValue(modName, out int value) ? value : 0;
  public static int GetFromPriceName(string priceName) => Get(CompassList.PriceToModName[priceName]);

  public static bool Has(string modName) => Get(modName) > 0;

  private static int _capacity = 0;
  public static int Capacity
  {
    get
    {
      return _capacity;
    }
  }

  public static int Count => CompassCounts.Values.Sum();

  public static Dictionary<string, int> CompassCounts
  {
    get
    {
      if (_dirty)
      {
        RefreshCompassCounts();
        _dirty = false;
      }
      return _compassCounts;
    }
  }

  private static void RefreshCompassCounts()
  {
    _compassCounts.Clear();
    foreach (var tab in Tabs.Values)
    {
      foreach (var kvp in tab)
      {
        if (!_compassCounts.TryGetValue(kvp.Key, out int currentCount))
        {
          currentCount = 0;
        }
        _compassCounts[kvp.Key] = currentCount + kvp.Value;
      }
    }
  }

  private static List<string> DumpTabNames => AutoSextant.Instance.Settings.DumpTabs.Value.Split(',').Select(x => x.Trim()).ToList();

  public static void RunRefresh(bool hard = false, Action callback = null)
  {
    Instance.Scheduler.AddTask(Refresh(hard, callback), "Stock.Refresh");
  }

  public static void RunRefresh(Action callback)
  {
    Instance.Scheduler.AddTask(Refresh(false, callback), "Stock.Refresh");
  }

  public static async SyncTask<bool> Refresh(bool hard = false, Action callback = null)
  {
    await Util.ForceFocusAsync();
    await AutoSextant.Instance.EnsureStash();

    if (!NStash.Stash.AreStashesLoaded(DumpTabNames) || hard)
    {
      await NStash.Stash.EnsureStashesAreLoaded(DumpTabNames);
    }

    // Update capacity
    var tabs = DumpTabNames.Select(x => new NStash.StashTab(x)).ToList();
    _capacity = 0;
    foreach (var t in tabs)
    {
      _capacity += t.Capacity;
    }

    Log.Debug($"Gathering stock from {DumpTabNames.Count} tabs");

    foreach (var t in tabs)
    {
      Dictionary<string, int> tabDict;
      if (!Tabs.TryGetValue(t.Name, out tabDict))
      {
        tabDict = new Dictionary<string, int>(50);  // assuming each tab will have ~50 different mods on average
        Tabs.Add(t.Name, tabDict);
      }

      int visibleItemCount = t.StashElement.VisibleInventoryItems.Count;

      if (visibleItemCount != tabDict.Values.Sum())
      {
        tabDict.Clear();
      }
      else
      {
        continue;
      }

      foreach (var i in t.StashElement.VisibleInventoryItems)
      {
        if (i.Item.HasComponent<Mods>())
        {
          var mods = i.Item.GetComponent<Mods>();
          foreach (var m in mods.ItemMods)
          {
            if (CompassList.ModNameToPrice.ContainsKey(m.RawName))
            {
              if (!tabDict.TryGetValue(m.RawName, out int currentCount))
              {
                currentCount = 0;
              }
              tabDict[m.RawName] = currentCount + 1;
              break;
            }
          }
        }
      }

      Log.Debug($"Tab {t.Name} has {tabDict.Count} mods");
    }

    _dirty = true;
    callback?.Invoke();
    return true;
  }
}
