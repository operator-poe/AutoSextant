using System.Collections;
using System.Windows.Forms;
using ExileCore.Shared;
using SharpDX;

namespace AutoSextant;

public class Input : ExileCore.Input
{
  private static AutoSextant Instance = AutoSextant.Instance;
  public static IEnumerator ClickElement(Vector2 pos, MouseButtons mouseButton = MouseButtons.Left, int delay = 30)
  {
    MoveMouseToElement(pos);
    yield return Delay(delay);
    Click(mouseButton);
    yield return Delay(delay);
  }


  public static IEnumerator ClickElement(Vector2 pos, int delay)
  {
    yield return ClickElement(pos, MouseButtons.Left, delay);
  }

  // public static new IEnumerator Click(MouseButtons mouseButton = MouseButtons.Left)
  // {
  //   ExileCore.Input.Click(mouseButton);
  //   yield return Delay();
  // }

  public static void MoveMouseToElement(Vector2 pos)
  {
    SetCursorPos(pos + Instance.GameController.Window.GetWindowRectangle().TopLeft);
  }

  public static IEnumerator Delay(int ms = 0)
  {
    yield return new WaitTime(Instance.Settings.ExtraDelay.Value + ms);
  }

  public static new void VerticalScroll(bool scrollUp, int clicks)
  {
    const int wheelDelta = 120;
    if (scrollUp)
      WinApi.mouse_event(MOUSE_EVENT_WHEEL, 0, 0, clicks * wheelDelta, 0);
    else
      WinApi.mouse_event(MOUSE_EVENT_WHEEL, 0, 0, -(clicks * wheelDelta), 0);
  }

  private static Vector2 LastMousePosition = Vector2.Zero;
  public static void StorePosition()
  {
    LastMousePosition = Input.ForceMousePosition;
  }

  public static void RestorePosition()
  {
    Input.SetCursorPos(LastMousePosition);
    LastMousePosition = Vector2.Zero;
  }
}