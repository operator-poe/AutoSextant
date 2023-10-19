// using System;
// using System.Collections;
// using System.Collections.Generic;
// using System.Linq;
// using AutoSextant.SellAssistant;
// using ExileCore;
// using ExileCore.PoEMemory.Components;
// using ExileCore.Shared;

// namespace AutoSextant;

// public static class Stock
// {
//   public static int Get(string modName)
//   {
//     if (CompassCounts.ContainsKey(modName))
//       return CompassCounts[modName];
//     return 0;
//   }

//   public static bool Has(string modName)
//   {
//     return Get(modName) > 0;
//   }

//   private static bool _dirty = true;
//   private static Dictionary<string, int> _compassCounts = new Dictionary<string, int>();
//   public static Dictionary<string, int> CompassCounts
//   {
//     get
//     {
//       if (_dirty)
//       {
//         _compassCounts.Clear();
//         foreach (var t in Tabs)
//         {
//           foreach (var m in t.Value)
//           {
//             if (!_compassCounts.ContainsKey(m.Key))
//               _compassCounts.Add(m.Key, 0);
//             _compassCounts[m.Key] += m.Value;
//           }
//         }
//         _dirty = false;
//       }
//       return _compassCounts;
//     }
//   }
//   public static Dictionary<string, Dictionary<string, int>> Tabs = new Dictionary<string, Dictionary<string, int>>();

//   private static List<string> DumpTabNames
//   {
//     get
//     {
//       return AutoSextant.Instance.Settings.DumpTabs.Value.Split(',').Select(x => x.Trim()).ToList();
//     }
//   }

//   private static string _stockRefreshCoroutineName = "StockRefresh";
//   public static void RunRefresh(bool hard = false, Action callback = null)
//   {
//     Core.ParallelRunner.Run(new Coroutine(Refresh(hard, callback), AutoSextant.Instance, _stockRefreshCoroutineName));
//   }
//   public static void RunRefresh(Action callback)
//   {
//     RunRefresh(false, callback);
//   }

//   public static IEnumerator Refresh(bool hard = false, Action callback = null)
//   {
//     yield return Util.ForceFocus();
//     yield return AutoSextant.Instance.EnsureStash();
//     if (!NStash.Stash.AreStashesLoaded(DumpTabNames) || hard)
//       yield return NStash.Stash.EnsureStashesAreLoaded(DumpTabNames);


//     Log.Debug($"Gathering stock from {DumpTabNames.Count} tabs");
//     var tabs = DumpTabNames.Select(x => new NStash.StashTab(x)).ToList();
//     foreach (var t in tabs)
//     {
//       if (!Tabs.ContainsKey(t.Name))
//         Tabs.Add(t.Name, new Dictionary<string, int>());

//       if (t.StashElement.VisibleInventoryItems.Count != Tabs[t.Name].Values.Sum())
//         Tabs[t.Name].Clear();
//       else
//         continue;
//       foreach (var i in t.StashElement.VisibleInventoryItems)
//       {
//         if (i.Item.HasComponent<Mods>())
//         {
//           var mods = i.Item.GetComponent<Mods>();
//           foreach (var m in mods.ItemMods)
//           {
//             if (CompassList.ModNameToPrice.ContainsKey(m.RawName))
//             {
//               if (!Tabs[t.Name].ContainsKey(m.RawName))
//                 Tabs[t.Name].Add(m.RawName, 0);
//               Tabs[t.Name][m.RawName]++;
//             }
//           }
//         }
//       }
//       Log.Debug($"Tab {t.Name} has {Tabs[t.Name].Count} mods");
//     }
//     _dirty = true;
//     callback?.Invoke();
//   }
// }
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using AutoSextant.SellAssistant;
using ExileCore;
using ExileCore.PoEMemory.Components;
using ExileCore.Shared;

namespace AutoSextant
{
  public static class Stock
  {
    private static bool _dirty = true;
    private static readonly Dictionary<string, int> _compassCounts = new Dictionary<string, int>(600);
    public static readonly Dictionary<string, Dictionary<string, int>> Tabs = new Dictionary<string, Dictionary<string, int>>();

    public static int Get(string modName) => CompassCounts.TryGetValue(modName, out int value) ? value : 0;

    public static bool Has(string modName) => Get(modName) > 0;

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
      Core.ParallelRunner.Run(new Coroutine(Refresh(hard, callback), AutoSextant.Instance, "StockRefresh"));
    }

    public static void RunRefresh(Action callback)
    {
      Core.ParallelRunner.Run(new Coroutine(Refresh(false, callback), AutoSextant.Instance, "StockRefresh"));
    }

    public static IEnumerator Refresh(bool hard = false, Action callback = null)
    {
      yield return Util.ForceFocus();
      yield return AutoSextant.Instance.EnsureStash();

      if (!NStash.Stash.AreStashesLoaded(DumpTabNames) || hard)
      {
        yield return NStash.Stash.EnsureStashesAreLoaded(DumpTabNames);
      }

      Log.Debug($"Gathering stock from {DumpTabNames.Count} tabs");
      var tabs = DumpTabNames.Select(x => new NStash.StashTab(x)).ToList();

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
              }
            }
          }
        }

        Log.Debug($"Tab {t.Name} has {tabDict.Count} mods");
      }

      _dirty = true;
      callback?.Invoke();
    }
  }
}
