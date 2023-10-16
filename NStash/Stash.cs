using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ExileCore.Shared;

namespace AutoSextant.NStash;
public static class Stash
{
  public static bool IsVisible
  {
    get
    {
      return Ex.StashElement != null && Ex.StashElement.IsVisible;
    }
  }

  public static List<StashTab> Tabs
  {
    get
    {
      var tabs = new List<StashTab>();
      for (int i = 0; i < AutoSextant.Instance.GameController.Game.IngameState.IngameUi.StashElement.TotalStashes; i++)
      {
        tabs.Add(new StashTab(i));
      }
      return tabs;
    }
  }

  public static StashTab ActiveTab
  {
    get
    {
      return Tabs[Ex.StashElement.IndexVisibleStash];
    }
  }

  public static List<string> TabNames
  {
    get
    {
      return Ex.StashElement.AllStashNames.ToList();
    }
  }

  public static IEnumerator ScrollToTab(int index)
  {
    if (index < 0 || index >= Tabs.Count)
    {
      throw new ArgumentOutOfRangeException();
    }
    if (index == Ex.StashElement.IndexVisibleStash)
    {
      yield break;
    }

    Input.SetCursorPos(Ex.TabSwitchBar.GetClientRect().Center);
    yield return new WaitTime(10);
    Input.KeyDown(System.Windows.Forms.Keys.ControlKey);
    while (Ex.StashElement.IndexVisibleStash != index)
    {
      if (index < Ex.StashElement.IndexVisibleStash)
      {
        Input.VerticalScroll(true, 1);
      }
      else
      {
        Input.VerticalScroll(false, 1);
      }
      yield return new WaitTime(1);
    }
    yield return new WaitFunctionTimed(() =>
         Ex.StashElement.AllInventories[index] != null
    , false, 500);
    Input.KeyUp(System.Windows.Forms.Keys.ControlKey);
    yield break;
  }

  public static IEnumerator ClickTab(int index)
  {
    if (IsTabVisible(index))
    {
      yield return Input.ClickElement(Ex.TabButtons[index].GetClientRect().Center);
      yield return new WaitFunctionTimed(() =>
           Ex.StashElement.AllInventories[index] != null
      , false, 500);
    }
    else
    {
      yield return ScrollToTab(index);
    }
  }

  public static bool IsTabVisible(int index)
  {
    if (!Ex.StashPanel.IsVisible)
    {
      return false;
    }
    var tabNode = Ex.TabButtons[index];
    return Ex.StashPanel.GetClientRect().Intersects(tabNode.GetClientRect());
  }

  public static IEnumerator SelectTab(int index)
  {
    if (IsTabVisible(index))
    {
      yield return Input.ClickElement(Ex.TabButtons[index].GetClientRect().Center);
    }
    else
    {
      yield return ScrollToTab(index);
    }
  }
  public static IEnumerator SelectTab(string name)
  {
    return ScrollToTab(Tabs.FindIndex(t => t.Name == name));
  }

  public static int TabIndex(string name)
  {
    return Tabs.FindIndex(t => t.Name == name);
  }
}