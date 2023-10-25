using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using AutoSextant.SellAssistant;
using ImGuiNET;

namespace AutoSextant;

public static class SessionWindow
{
  public static float DivinePrice
  {
    get
    {
      return CompassList.DivinePrice;
    }
  }
  public static float SextantPrice
  {
    get
    {
      return CompassList.AwakenedSextantPrice;
    }
  }
  public static bool Show
  {
    get
    {
      return AutoSextant.Instance.Settings.EnableSessionWindow && AutoSextant.Instance.GameController.Game.IngameState.IngameUi.Atlas.IsVisible;
    }
  }

  private static int _sextantsUsed = 0;
  private static Dictionary<string, int> _modCount = new Dictionary<string, int>();

  public static void AddMod(string modName)
  {
    if (!_modCount.ContainsKey(modName))
    {
      _modCount.Add(modName, 0);
    }
    _modCount[modName]++;
  }

  public static void IncreaseSextantCount()
  {
    _sextantsUsed++;
  }

  public static void Reset()
  {
    _sextantsUsed = 0;
    _modCount.Clear();
  }
  public static void Render()
  {
    if (!Show)
    {
      return;
    }
    bool show = AutoSextant.Instance.Settings.EnableSessionWindow.Value;

    if (ImGui.Begin("SessionWindow", ref show))
    {
      AutoSextant.Instance.Settings.EnableSessionWindow.Value = show;
      if (ImGui.CollapsingHeader("Session Stats", ImGuiTreeNodeFlags.DefaultOpen))
      {
        if (ImGui.BeginTable("SessionStatsTable", 5, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.Resizable | ImGuiTableFlags.NoSavedSettings))
        {
          ImGui.TableSetupColumn("Sextants Used");
          ImGui.TableSetupColumn("Session Cost");
          ImGui.TableSetupColumn("Session Value");
          ImGui.TableSetupColumn("Profit Session");
          ImGui.TableSetupColumn("");
          ImGui.TableHeadersRow();

          ImGui.TableNextRow();
          ImGui.TableNextColumn();
          ImGui.Text(_sextantsUsed.ToString());

          ImGui.TableNextColumn();
          var totalSpent = _sextantsUsed * SextantPrice;
          ImGui.Text(Util.FormatChaosPrice(totalSpent, DivinePrice));

          ImGui.TableNextColumn();
          var totalChaos = _modCount.Sum(x => CompassList.Prices[x.Key].ChaosPrice * x.Value);
          ImGui.Text(Util.FormatChaosPrice(totalChaos, DivinePrice));

          ImGui.TableNextColumn();
          ImGui.Text(Util.FormatChaosPrice(totalChaos - totalSpent, DivinePrice));

          ImGui.TableNextColumn();
          if (ImGui.Button("Reset"))
          {
            Reset();
          }

          ImGui.EndTable();
        }
        if (ImGui.BeginTable("SessionStatsTableStock", 4, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.Resizable | ImGuiTableFlags.NoSavedSettings))
        {

          ImGui.TableSetupColumn("Capacity");
          ImGui.TableSetupColumn("Free Slots");
          ImGui.TableSetupColumn("Divine Price");
          ImGui.TableSetupColumn("Sextant Price");
          ImGui.TableHeadersRow();

          ImGui.TableNextRow();
          ImGui.TableNextColumn();
          var count = Stock.Count;
          var capacity = Stock.Capacity;
          ImGui.Text($"{count}/{capacity}");

          ImGui.TableNextColumn();
          ImGui.Text((capacity - count).ToString());

          ImGui.TableNextColumn();
          ImGui.Text(DivinePrice.ToString("0.0", CultureInfo.InvariantCulture) + "c");

          ImGui.TableNextColumn();
          ImGui.Text(SextantPrice.ToString("0.0", CultureInfo.InvariantCulture) + "c");

          ImGui.EndTable();
        }

      }
      ImGui.Spacing();
      ImGui.Spacing();
      ImGui.Spacing();
      if (ImGui.CollapsingHeader("Mod Occurence", ImGuiTreeNodeFlags.DefaultOpen))
      {

        if (ImGui.BeginTable("ModOccurenceTable", 5, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.Resizable | ImGuiTableFlags.NoSavedSettings))
        {
          ImGui.TableSetupColumn("Sextant Mod");
          ImGui.TableSetupColumn("Price");
          ImGui.TableSetupColumn("Stock Count");
          ImGui.TableSetupColumn("Session Count");
          ImGui.TableSetupColumn("Profit Total");
          ImGui.TableHeadersRow();

          var modsOrdered = _modCount.Select(x => (x.Key, x.Value, CompassList.Prices[x.Key].ChaosPrice * x.Value)).OrderByDescending(x => x.Item3).ToList();

          foreach (var mod in modsOrdered)
          {
            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            ImGui.Text(mod.Item1);
            ImGui.TableNextColumn();
            ImGui.Text(Util.FormatChaosPrice(CompassList.Prices[mod.Item1].ChaosPrice, DivinePrice));
            ImGui.TableNextColumn();
            ImGui.Text(Stock.Get(CompassList.PriceToModName[mod.Item1]).ToString());
            ImGui.TableNextColumn();
            ImGui.Text(mod.Item2.ToString());
            ImGui.TableNextColumn();

            ImGui.Text(Util.FormatChaosPrice(mod.Item3, DivinePrice));
          }

          ImGui.EndTable();
        }
      }
    }
    ImGui.End();
  }
}