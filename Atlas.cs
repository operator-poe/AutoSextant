namespace AutoSextant;

public class Atlas
{
  private static AutoSextant Instance = AutoSextant.Instance;

  public static bool HasBlockMods
  {
    get
    {
      VoidStone[] blockStones = {
                new(VoidStonePosition.Left),
                new(VoidStonePosition.Right),
                new(VoidStonePosition.Bottom)
            };
      foreach (var blockStone in blockStones)
      {
        var modName = blockStone.ModName;

        var isBlocked = modName != null &&
                        (Instance.Settings.UseModsForBlockingGroup1.Value.Contains(modName) ||
                        Instance.Settings.UseModsForBlockingGroup2.Value.Contains(modName) ||
                        Instance.Settings.UseModsForBlockingGroup3.Value.Contains(modName));

        if (!isBlocked)
        {
          return false;
        }
      }
      return true;
    }
  }
}