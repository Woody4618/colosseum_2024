using System;
using UnityEngine;

public class TileView : MonoBehaviour
{
  public ulong X;
  public ulong Y;

  private Action<TileView> onClickAction;

  public void Init(ulong x, ulong y, Action<TileView> onClick)
  {
    X = x;
    Y = y;
    onClickAction = onClick;
  }

  public void OnMouseUpAsButton()
  {
    onClickAction?.Invoke(this);
  }
}
