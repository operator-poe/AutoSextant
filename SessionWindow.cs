using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
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
      return AutoSextant.Instance.GameController.Game.IngameState.IngameUi.Atlas.IsVisible;
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

  private static string FormatChaosPrice(float value)
  {
    if (Math.Abs(value) < DivinePrice)
      return $"{value.ToString("0.0")}c";

    int divines = (int)(value / DivinePrice);
    float chaos = value % DivinePrice;
    return $"{divines} div, {chaos.ToString("0.0")}c";
  }

  public static void Render()
  {
    if (!Show)
    {
      return;
    }

    if (ImGui.Begin("SessionWindow"))
    {
      if (ImGui.CollapsingHeader("Session Stats", ImGuiTreeNodeFlags.DefaultOpen))
      {
        if (ImGui.BeginTable("SessionStatsTable", 9, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.Resizable | ImGuiTableFlags.NoSavedSettings))
        {
          ImGui.TableSetupColumn("Sextants Used");
          ImGui.TableSetupColumn("Sextant Cost");
          ImGui.TableSetupColumn("Total Value");
          ImGui.TableSetupColumn("Profit Total");
          ImGui.TableSetupColumn("Capacity");
          ImGui.TableSetupColumn("Free Slots");
          ImGui.TableSetupColumn("Divine Price");
          ImGui.TableSetupColumn("Sextant Price");
          ImGui.TableSetupColumn("");
          ImGui.TableHeadersRow();

          ImGui.TableNextRow();
          ImGui.TableNextColumn();
          ImGui.Text(_sextantsUsed.ToString());

          ImGui.TableNextColumn();
          var totalSpent = _sextantsUsed * SextantPrice;
          ImGui.Text(FormatChaosPrice(totalSpent));

          ImGui.TableNextColumn();
          var totalChaos = _modCount.Sum(x => CompassList.Prices[x.Key].ChaosPrice * x.Value);
          ImGui.Text(FormatChaosPrice(totalChaos));

          ImGui.TableNextColumn();
          ImGui.Text(FormatChaosPrice(totalChaos - totalSpent));

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

          ImGui.TableNextColumn();
          if (ImGui.Button("Reset"))
          {
            Reset();
          }

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
            ImGui.Text(FormatChaosPrice(CompassList.Prices[mod.Item1].ChaosPrice));
            ImGui.TableNextColumn();
            ImGui.Text(Stock.Get(CompassList.PriceToModName[mod.Item1]).ToString());
            ImGui.TableNextColumn();
            ImGui.Text(mod.Item2.ToString());
            ImGui.TableNextColumn();

            ImGui.Text(FormatChaosPrice(mod.Item3));
          }

          ImGui.EndTable();
        }
      }
    }
    ImGui.End();
  }
}