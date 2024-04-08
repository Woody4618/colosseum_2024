using System;
using Blobs.Accounts;
using DG.Tweening;
using Game.Scripts.Utils;
using Solana.Unity.SDK;
using Solana.Unity.Wallet;
using TMPro;
using UnityEngine;

public class AttackBlobView : MonoBehaviour
{
    public TextMeshPro CurrentColorLevelText;
    public TextMeshPro CountdownText;
    public MeshRenderer MeshRenderer;
    public GameObject Renderer;
    public GameObject BlobViewScaleRoot;
    public PublicKey PublicKey;

    public BlobData CurrentDefenerBlobData;
    public BlobView CurrentDefenderBlobView;
    public BlobView CurrentAttackerBlobView;

    private bool sentAttackFinish;

    public void Init(BlobView attackerBlobView)
    {
        UpdateTexts(attackerBlobView.CurrentBlobData);
        MeshRenderer.material.color = ColorUtils.UlongToColor(attackerBlobView.CurrentBlobData.ColorValue);
        CurrentAttackerBlobView = attackerBlobView;
        PublicKey = AnchorService.Instance.GetBlobPubkey(attackerBlobView.CurrentBlobData);
        transform.position = attackerBlobView.transform.position;
    }

    private void UpdateTexts(BlobData blobData)
    {
      CurrentColorLevelText.text = $"{blobData.AttackPower}";
    }

    public void Update()
    {
      Renderer.SetActive(CurrentDefenerBlobData != null);
      if (CurrentDefenerBlobData == null)
      {
        return;
      }

      if (CurrentAttackerBlobView.CurrentBlobData.AttackDuration == 0)
      {
        CurrentDefenerBlobData = null;
        Destroy(gameObject);
        return;
      }

      var lastLoginTime = CurrentAttackerBlobView.CurrentBlobData.AttackStartTime;
      var timePassed = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - lastLoginTime;

      float travelRatio = (float) timePassed / CurrentAttackerBlobView.CurrentBlobData.AttackDuration;

      Vector3 direction = CurrentDefenderBlobView.transform.position - CurrentAttackerBlobView.transform.position;
      if (travelRatio > 1)
      {
        travelRatio = 1;
      }
      direction *= travelRatio;

      //transform.position = CurrentAttackerBlobView.transform.position + direction;
      transform.DOMove(CurrentAttackerBlobView.transform.position + direction, 1);

      var timeUntilNextRefill = AnchorService.TIME_TO_REFILL_ONE_COLOR - timePassed;

      BlobViewScaleRoot.transform.localScale = Vector3.one * (CurrentAttackerBlobView.CurrentBlobData.AttackPower / (float) CurrentAttackerBlobView.CurrentBlobData.ColorMax);

      // TODO: maybe better trigger this on a timer instead from the view? :thinking:
      if (!sentAttackFinish && timePassed - 2 >= (long) CurrentAttackerBlobView.CurrentBlobData.AttackDuration && CurrentAttackerBlobView.CurrentBlobData.Authority == Web3.Account.PublicKey)
      {
        SendAttack();
      }

      if (timeUntilNextRefill > 0)
      {
        CountdownText.text = timeUntilNextRefill.ToString();
      }
      else
      {
        CountdownText.text = "";
      }
    }

    private void SendAttack()
    {
      Renderer.transform.DOLocalJump(Renderer.transform.localPosition, 2, 1, 1);

      AnchorService.Instance.SendAttackFinish(
        !Web3.Rpc.NodeAddress.AbsoluteUri.Contains("localhost"),
        CurrentAttackerBlobView.CurrentBlobData.X,
        CurrentAttackerBlobView.CurrentBlobData.Y,
        CurrentDefenderBlobView.CurrentBlobData.X,
        CurrentDefenderBlobView.CurrentBlobData.Y, OnSucces, OnError);
      sentAttackFinish = true;
    }

    private void OnError(string error)
    {
      SendAttack();
    }

    private void OnSucces()
    {
      Destroy(gameObject);
    }

    public void SetDefender(BlobView defenderBlobView)
    {
      CurrentDefenerBlobData = defenderBlobView.CurrentBlobData;
      CurrentDefenderBlobView = defenderBlobView;
    }
}
