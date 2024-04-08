using System.Collections.Generic;
using Blobs.Accounts;
using Solana.Unity.SDK;
using Solana.Unity.Wallet;
using UnityEngine;

public class GridView : MonoBehaviour
{
  public BlobView BlobPrefab;
  public TileView TileViewPrefab;
  public AttackBlobView AttackBlobViewPrefab;

  private BlobView SelectedBlobView;

  private List<BlobView> BlobViews = new List<BlobView>();
  private List<TileView> TileViews = new List<TileView>();
  private List<AttackBlobView> AttackBlobViews = new List<AttackBlobView>();

  private void Start()
  {
    AnchorService.OnBlobDataChanged += OnBlobDataChanged;
    CreateGrid();
  }

  private void OnDestroy()
  {
    AnchorService.OnBlobDataChanged -= OnBlobDataChanged;
  }

  private void CreateGrid()
  {
    for (ulong i = 0; i < 10; i++)
    {
      for (ulong j = 0; j < 10; j++)
      {
        var newTileView = Instantiate(TileViewPrefab, transform);
        newTileView.transform.position = new Vector3(i + 0.5f, 0, j + 0.5f);
        TileViews.Add(newTileView);
        newTileView.Init(i, j, OnClick);
      }
    }
  }

  private void OnClick(TileView tileView)
  {
    AnchorService.Instance.SpawnBlob(!Web3.Rpc.NodeAddress.AbsoluteUri.Contains("localhost"), tileView.X, tileView.Y,
      () =>
      {
        Debug.Log($"Spawn new blob successful!");
      });

    Debug.Log($"Clicked tile: {tileView.X}/{tileView.Y}");
  }

  private void OnBlobDataChanged(BlobData newBlobData)
  {
    Debug.Log($"New blob data position: {newBlobData.X}/{newBlobData.Y}");

    var foundBlobView = FindBlobView(newBlobData);

    if (foundBlobView == null)
    {
      var newBlobView = Instantiate(BlobPrefab, new Vector3(newBlobData.X + 0.5f, 0, newBlobData.Y + 0.5f), Quaternion.identity);
      newBlobView.Init(newBlobData, OnClick);
      BlobViews.Add(newBlobView);
      foundBlobView = newBlobView;
    }
    else
    {
      foundBlobView.Init(newBlobData, OnClick);
    }

    if (newBlobData.AttackTarget != PublicKey.DefaultPublicKey)
    {
      var foundAttackView = FindAttackView(newBlobData);

      if (foundAttackView == null)
      {
        var newAttackView = Instantiate(AttackBlobViewPrefab, transform);
        newAttackView.Init(foundBlobView);
        foundAttackView = newAttackView;
      }
      else
      {
        foundAttackView.Init(foundBlobView);
      }

      BlobView foundDefenderBlobView = null;
      foreach (var blobView in BlobViews)
      {
        if (blobView.PublicKey == newBlobData.AttackTarget.Key)
        {
          foundAttackView.SetDefender(blobView);
        }
      }
    }
    else
    {
      foreach (var attackView in AttackBlobViews)
      {
        if (attackView.CurrentAttackerBlobView.PublicKey == foundBlobView.PublicKey)
        {
          attackView.SetDefender(null);
          Destroy(attackView);
          AttackBlobViews.Remove(attackView);
        }
      }
    }
  }

  private AttackBlobView FindAttackView(BlobData newBlobData)
  {
    AttackBlobView foundAttackView = null;
    foreach (var attackView in AttackBlobViews)
    {
      if (attackView.CurrentAttackerBlobView.PublicKey == newBlobData.AttackTarget)
      {
        foundAttackView = attackView;
      }
    }

    return foundAttackView;
  }

  private BlobView FindBlobView(BlobData newBlobData)
  {
    BlobView foundBlobView = null;
    foreach (var blobView in BlobViews)
    {
      if (blobView.CurrentBlobData.X == newBlobData.X && blobView.CurrentBlobData.Y == newBlobData.Y)
      {
        foundBlobView = blobView;
        break;
      }
    }

    return foundBlobView;
  }

  private void OnClick(BlobView clickedBlobView)
  {
    if (SelectedBlobView != null)
    {
      AnchorService.Instance.AttackBlob(!Web3.Rpc.NodeAddress.AbsoluteUri.Contains("localhost") ,SelectedBlobView, clickedBlobView);
      SelectedBlobView = null;
      UnselectAll();
      return;
    }

    if (SelectedBlobView == clickedBlobView)
    {
      SelectedBlobView = null;
      UnselectAll();
      return;
    }

    SelectedBlobView = clickedBlobView;
    UnselectAll();
    SelectedBlobView.Select(true);
  }

  private void UnselectAll()
  {
    foreach (var blobView in BlobViews)
    {
      blobView.Select(false);
    }
  }

  private bool IsEnemyBlob(BlobView clickedBlobView)
  {
    return clickedBlobView.CurrentBlobData.Authority != Web3.Account.PublicKey;
  }
}
