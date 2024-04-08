using System;
using Blobs.Accounts;
using DG.Tweening;
using Game.Scripts.Utils;
using Solana.Unity.Wallet;
using TMPro;
using UnityEngine;

public class BlobView : MonoBehaviour
{
    public TextMeshPro CoordinatesText;
    public TextMeshPro CurrentColorLevelText;
    public MeshRenderer MeshRenderer;
    public GameObject SelectionIndicator;
    public GameObject BlobViewScaleRoot;
    public GameObject CountDownSlider;
    public PublicKey PublicKey;

    public BlobData CurrentBlobData;

    private Action<BlobView> onClick;

    public void Init(BlobData blobData, Action<BlobView> onClick)
    {
        UpdateTexts(blobData);
        MeshRenderer.material.color = ColorUtils.UlongToColor(blobData.ColorValue);
        CurrentBlobData = blobData;
        this.onClick = onClick;
        PublicKey = AnchorService.Instance.GetBlobPubkey(CurrentBlobData);
    }

    public void Select(bool selected)
    {
      SelectionIndicator.gameObject.SetActive(selected);
    }

    public void OnMouseUp()
    {
      this.onClick?.Invoke(this);
      Debug.Log($"Mouse up: {CurrentBlobData.X}/{CurrentBlobData.Y}");
    }

    private void UpdateTexts(BlobData blobData)
    {
      CoordinatesText.text = $"{blobData.X}/{blobData.Y}";
      CurrentColorLevelText.text = $"{blobData.ColorCurrent}";
    }

    public void Update()
    {
      var lastLoginTime = CurrentBlobData.LastLogin;
      var timePassed = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - lastLoginTime;

      var timeToGetOneColor = CurrentBlobData.Authority == null ? AnchorService.TIME_TO_REFILL_ONE_COLOR * 2 : AnchorService
        .TIME_TO_REFILL_ONE_COLOR;

      while (
        timePassed >= timeToGetOneColor &&
        CurrentBlobData.ColorCurrent < CurrentBlobData.ColorMax
      ) {
        CurrentBlobData.ColorCurrent += 1;
        UpdateTexts(CurrentBlobData);
        CurrentBlobData.LastLogin += timeToGetOneColor;
        timePassed -= timeToGetOneColor;
      }

      BlobViewScaleRoot.transform.localScale = Vector3.one * (CurrentBlobData.ColorCurrent / (float) CurrentBlobData.ColorMax);

      var timeUntilNextRefill = timeToGetOneColor - timePassed;

      if (timeUntilNextRefill > 0)
      {
        CountDownSlider.transform.DOScale(new Vector3(1 - (timePassed / (float) timeToGetOneColor), 1, 1), 2f);
        //CountDownSlider.transform.localScale = new Vector3(1 - (timePassed / (float) timeToGetOneColor), 1, 1);
      }
      else
      {
        CountDownSlider.transform.localScale = new Vector3(1, 1, 1);
      }
    }
}
