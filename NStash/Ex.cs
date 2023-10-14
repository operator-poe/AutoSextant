using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ExileCore;
using ExileCore.PoEMemory;
using ExileCore.PoEMemory.Elements;

namespace AutoSextant.NStash;

public static class Ex
{
    public static GameController GameController
    {
        get
        {
            return AutoSextant.Instance.GameController;
        }
    }

    public static StashElement StashElement
    {
        get
        {
            if (!GameController.IngameState.IngameUi.StashElement.IsVisible)
            {
                return null;
            }
            return GameController.IngameState.IngameUi.StashElement;
        }
    }

    public static StashTopTabSwitcher TabSwitchBar
    {
        get
        {
            return StashElement.StashTabContainer.TabSwitchBar;
        }
    }
}
