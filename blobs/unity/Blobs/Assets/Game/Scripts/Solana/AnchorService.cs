using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Frictionless;
using Game.Scripts.Ui;
using Blobs;
using Blobs.Accounts;
using Blobs.Program;
using Game.Scripts.Utils;
using Solana.Unity.Programs;
using Solana.Unity.Programs.Models;
using Solana.Unity.Rpc.Core.Http;
using Solana.Unity.Rpc.Messages;
using Solana.Unity.Rpc.Models;
using Solana.Unity.Rpc.Types;
using Solana.Unity.SDK;
using Solana.Unity.SessionKeys.GplSession.Accounts;
using Solana.Unity.Wallet;
using Services;
using Solana.Unity.Rpc.Core.Sockets;
using UnityEngine;
using Debug = UnityEngine.Debug;
using Random = UnityEngine.Random;

public class AnchorService : MonoBehaviour
{
    public PublicKey AnchorProgramIdPubKey = new("9aMxRDFLQwW2e185TdpfHJWAWTGhzLQwB7SuEf58WYDX");

    // Needs to be the same constants as in the anchor program
    public const int TIME_TO_REFILL_ONE_COLOR = 3;
    public const int MAX_ENERGY = 100;
    public const int MAX_WOOD_PER_TREE = 100000;

    public static AnchorService Instance { get; private set; }
    public static Action<PlayerData> OnPlayerDataChanged;
    public static Action<BlobData> OnBlobDataChanged;
    public static Action<GameData> OnGameDataChanged;
    public static Action OnInitialDataLoaded;

    public bool IsAnyBlockingTransactionInProgress => blockingTransactionsInProgress > 0;
    public bool IsAnyNonBlockingTransactionInProgress => nonBlockingTransactionsInProgress > 0;
    public PlayerData CurrentPlayerData { get; private set; }
    public GameData CurrentGameData { get; private set; }

    public int BlockingTransactionsInProgress => blockingTransactionsInProgress;
    public int NonBlockingTransactionsInProgress => nonBlockingTransactionsInProgress;
    public long LastTransactionTimeInMs => lastTransactionTimeInMs;
    public string LastError { get; set; }

    private SessionWallet sessionWallet;
    private PublicKey PlayerDataPDA;
    private PublicKey GameDataPDA;
    private bool _isInitialized;
    private BlobsClient anchorClient;
    private int blockingTransactionsInProgress;
    private int nonBlockingTransactionsInProgress;
    private long? sessionValidUntil;
    private string sessionKeyPassword = "inGame"; // Would be better to generate and save in playerprefs
    private string gameDataSeed = "gameData";
    private ushort transactionCounter = 0;

    // This is to not subscribe to blob updates multiple times
    private List<PublicKey> KnownBlobs = new List<PublicKey>();

    // These are all the blobs that are currently spanwed on the map
    public List<BlobData> activeBlobs = new List<BlobData>();

    // Only used to show transaction speed. Feel free to remove
    private Dictionary<ushort, Stopwatch> stopWatches = new ();
    private long lastTransactionTimeInMs;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
        }
        else
        {
            Instance = this;
        }

        Web3.OnLogin += OnLogin;
    }

    private void OnDestroy()
    {
        Web3.OnLogin -= OnLogin;
    }

    private async void OnLogin(Account account)
    {
        Debug.Log("Logged in with pubkey: " + account.PublicKey);

        await RequestAirdropIfSolValueIsLow();

        sessionWallet = await SessionWallet.GetSessionWallet(AnchorProgramIdPubKey, sessionKeyPassword);
        await UpdateSessionValid();

        FindPDAs(account);

        anchorClient = new BlobsClient(Web3.Rpc, Web3.WsRpc, AnchorProgramIdPubKey);

        await SubscribeToPlayerDataUpdates();
        await SubscribeToGameDataUpdates();

        OnInitialDataLoaded?.Invoke();
    }

    private void FindPDAs(Account account)
    {
        PublicKey.TryFindProgramAddress(new[]
                {Encoding.UTF8.GetBytes("player"), account.PublicKey.KeyBytes},
            AnchorProgramIdPubKey, out PlayerDataPDA, out byte bump);

        PublicKey.TryFindProgramAddress(new[]
                {Encoding.UTF8.GetBytes(gameDataSeed)},
            AnchorProgramIdPubKey, out GameDataPDA, out byte bump2);
    }

    private static async Task RequestAirdropIfSolValueIsLow()
    {
        var solBalance = await Web3.Instance.WalletBase.GetBalance();
        if (solBalance < 0.8f)
        {
            Debug.Log("Not enough sol. Requesting airdrop");
            var result = await Web3.Instance.WalletBase.RequestAirdrop(commitment: Commitment.Confirmed);
            if (!result.WasSuccessful)
            {
                Debug.Log("Airdrop failed. You can go to faucet.solana.com and request sol for this key: " + Web3.Instance.WalletBase.Account.PublicKey);
            }
        }
    }

    public bool IsInitialized()
    {
        return _isInitialized;
    }

    private long GetSessionKeysEndTime()
    {
        return DateTimeOffset.UtcNow.AddDays(6).ToUnixTimeSeconds();
    }

    private async Task SubscribeToPlayerDataUpdates()
    {
        AccountResultWrapper<PlayerData> playerData = null;

        try
        {
            playerData = await anchorClient.GetPlayerDataAsync(PlayerDataPDA, Commitment.Confirmed);
            if (playerData.ParsedResult != null)
            {
                CurrentPlayerData = playerData.ParsedResult;
                OnPlayerDataChanged?.Invoke(playerData.ParsedResult);
                _isInitialized = true;
            }
        }
        catch (Exception e)
        {
            Debug.Log("Probably playerData not available " + e.Message);
        }

        if (playerData != null)
        {
            await anchorClient.SubscribePlayerDataAsync(PlayerDataPDA, (state, value, playerData) =>
            {
                OnReceivedPlayerDataUpdate(playerData);
            }, Commitment.Processed);
        }
    }

    private void OnReceivedPlayerDataUpdate(PlayerData playerData)
    {
        Debug.Log($"Socket Message: Player has {playerData.Wood} wood now.");
        stopWatches[playerData.LastId].Stop();
        lastTransactionTimeInMs = stopWatches[playerData.LastId].ElapsedMilliseconds;
        CurrentPlayerData = playerData;
        OnPlayerDataChanged?.Invoke(playerData);
    }

    private async Task SubscribeToGameDataUpdates()
    {
        AccountResultWrapper<GameData> gameData = null;

        try
        {
            gameData = await anchorClient.GetGameDataAsync(GameDataPDA, Commitment.Confirmed);
            if (gameData.ParsedResult != null)
            {
                CurrentGameData = gameData.ParsedResult;
                OnGameDataChanged?.Invoke(gameData.ParsedResult);
            }
        }
        catch (Exception e)
        {
            Debug.Log("Probably game data not available " + e.Message);
        }

        if (gameData != null)
        {
          Debug.Log("There are " + gameData.ParsedResult.ActiveBlobs.Length + " blobs");

          activeBlobs.Clear();

          foreach (var blob in gameData.ParsedResult.ActiveBlobs)
          {
            var blobData = await anchorClient.GetBlobDataAsync(blob, Commitment.Processed);
            activeBlobs.Add(blobData.ParsedResult);
            OnBlobDataChanged?.Invoke(blobData.ParsedResult);
            Debug.Log($"Blob position: {blobData.ParsedResult.X} / {blobData.ParsedResult.Y}");
          }

          await SubscribeToBlobs(gameData.ParsedResult);

          await anchorClient.SubscribeGameDataAsync(GameDataPDA, async (state, value, gameData) =>
          {
                await UniTask.SwitchToMainThread();
                OnRecievedGameDataUpdate(gameData);
                await SubscribeToBlobs(gameData);
            }, Commitment.Processed);
        }
    }

    private async Task SubscribeToBlobs(GameData gameData)
    {
      foreach (var blobPubkey in gameData.ActiveBlobs)
      {
        if (!KnownBlobs.Contains(blobPubkey))
        {
          var blobData = await anchorClient.GetBlobDataAsync(blobPubkey, Commitment.Processed);
          activeBlobs.Add(blobData.ParsedResult);
          OnBlobDataChanged?.Invoke(blobData.ParsedResult);
          await anchorClient.SubscribeBlobDataAsync(blobPubkey, OnNewBlobData, Commitment.Processed);
          KnownBlobs.Add(blobPubkey);
        }
      }
    }

    private async void OnNewBlobData(SubscriptionState state, ResponseValue<AccountInfo> accountInfo, BlobData blobData)
    {
      await UniTask.SwitchToMainThread();
      OnBlobDataChanged(blobData);
    }

    private void OnRecievedGameDataUpdate(GameData gameData)
    {
        Debug.Log($"Socket Message: Total log chopped  {gameData.TotalWoodCollected}.");
        CurrentGameData = gameData;
        OnGameDataChanged?.Invoke(gameData);
    }

    public async Task InitAccounts(bool useSession)
    {
        var tx = new Transaction()
        {
            FeePayer = Web3.Account,
            Instructions = new List<TransactionInstruction>(),
            RecentBlockHash = await Web3.BlockHash()
        };

        InitPlayerAccounts accounts = new InitPlayerAccounts();
        accounts.Player = PlayerDataPDA;
        accounts.GameData = GameDataPDA;
        accounts.Signer = Web3.Account;
        accounts.SystemProgram = SystemProgram.ProgramIdKey;

        var initTx = BlobsProgram.InitPlayer(accounts, gameDataSeed, AnchorProgramIdPubKey);
        tx.Add(initTx);

        if (useSession)
        {
            if (!(await IsSessionTokenInitialized()))
            {
                var topUp = true;

                var validity = GetSessionKeysEndTime();
                var createSessionIX = sessionWallet.CreateSessionIX(topUp, validity);
                accounts.Signer = Web3.Account.PublicKey;
                tx.Add(createSessionIX);
                Debug.Log("Has no session -> partial sign");
                tx.PartialSign(new[] {Web3.Account, sessionWallet.Account});
            }
        }

        bool success = await SendAndConfirmTransaction(Web3.Wallet, tx, "initialize",
            () => { Debug.Log("Init account was successful"); }, s => { Debug.LogError("Init was not successful"); });

        await UpdateSessionValid();
        await SubscribeToPlayerDataUpdates();
        await SubscribeToGameDataUpdates();
    }

    private async Task<bool> SendAndConfirmTransaction(WalletBase wallet, Transaction transaction, string label = "",
        Action onSucccess = null, Action<string> onError = null, bool isBlocking = true)
    {
        (isBlocking ? ref blockingTransactionsInProgress : ref nonBlockingTransactionsInProgress)++;
        LastError = String.Empty;

        Debug.Log("Sending and confirming transaction: " + label);
        RequestResult<string> res;
        try
        {
            res = await wallet.SignAndSendTransaction(transaction, commitment: Commitment.Confirmed);
        }
        catch (Exception e)
        {
            Debug.Log("Transaction exception " + e);
            blockingTransactionsInProgress--;
            (isBlocking ? ref blockingTransactionsInProgress : ref nonBlockingTransactionsInProgress)--;
            LastError = e.Message;
            onError?.Invoke(e.ToString());
            return false;
        }

        if (res.WasSuccessful && res.Result != null)
        {
            Debug.Log($"Transaction sent: {res.RawRpcResponse } signature: {res.Result}" );
            await Web3.Rpc.ConfirmTransaction(res.Result, Commitment.Confirmed);
        }
        else
        {
            Debug.LogError("Transaction failed: " + res.RawRpcResponse);
            if (res.RawRpcResponse.Contains("InsufficientFundsForRent"))
            {
                Debug.Log("Trigger session top up (Not implemented)");
                // TODO: This can probably happen when the session key runs out of funds. Easiest is to just create a
                // new session in this popup. Other option would be to implement a topup popup
                ServiceFactory.Resolve<UiService>().OpenPopup(UiService.ScreenType.SessionPopup, new SessionPopupUiData());
            }

            LastError = res.RawRpcResponse;
            (isBlocking ? ref blockingTransactionsInProgress : ref nonBlockingTransactionsInProgress)--;

            onError?.Invoke(res.RawRpcResponse);
            return false;
        }

        Debug.Log($"Send transaction {label} with response: {res.RawRpcResponse}");
        (isBlocking ? ref blockingTransactionsInProgress : ref nonBlockingTransactionsInProgress)--;
        onSucccess?.Invoke();
        return true;
    }

    public async Task RevokeSession()
    {
        await sessionWallet.CloseSession();
        Debug.Log("Session closed");
    }

    public async void ChopTree(bool useSession, Action onSuccess)
    {
        if (!Instance.IsSessionValid() && useSession)
        {
            await Instance.UpdateSessionValid();
            ServiceFactory.Resolve<UiService>().OpenPopup(UiService.ScreenType.SessionPopup, new SessionPopupUiData());
            return;
        }

        // only for time tracking feel free to remove
        var stopWatch = new Stopwatch();
        stopWatch.Start();
        stopWatches[++transactionCounter] = stopWatch;

        var transaction = new Transaction()
        {
            FeePayer = Web3.Account,
            Instructions = new List<TransactionInstruction>(),
            RecentBlockHash = await Web3.BlockHash(maxSeconds: 5)
        };

        ChopTreeAccounts chopTreeAccounts = new ChopTreeAccounts
        {
            Player = PlayerDataPDA,
            GameData = GameDataPDA,
            SystemProgram = SystemProgram.ProgramIdKey
        };

        if (useSession)
        {
            transaction.FeePayer = sessionWallet.Account.PublicKey;
            chopTreeAccounts.Signer = sessionWallet.Account.PublicKey;
            chopTreeAccounts.SessionToken = sessionWallet.SessionTokenPDA;
            var chopInstruction = BlobsProgram.ChopTree(chopTreeAccounts, gameDataSeed, transactionCounter, AnchorProgramIdPubKey);
            transaction.Add(chopInstruction);
            Debug.Log("Sign and send chop tree with session");
            await SendAndConfirmTransaction(sessionWallet, transaction, "Chop Tree with session.", isBlocking: false, onSucccess: onSuccess);
        }
        else
        {
            transaction.FeePayer = Web3.Account.PublicKey;
            chopTreeAccounts.Signer = Web3.Account.PublicKey;
            var chopInstruction = BlobsProgram.ChopTree(chopTreeAccounts, gameDataSeed, transactionCounter, AnchorProgramIdPubKey);
            transaction.Add(chopInstruction);
            Debug.Log("Sign and send init without session");
            await SendAndConfirmTransaction(Web3.Wallet, transaction, "Chop Tree without session.", onSucccess: onSuccess);
        }

        if (CurrentGameData == null)
        {
            await SubscribeToGameDataUpdates();
        }
    }

    public async Task<bool> IsSessionTokenInitialized()
    {
        var sessionTokenData = await Web3.Rpc.GetAccountInfoAsync(sessionWallet.SessionTokenPDA, Commitment.Confirmed);
        if (sessionTokenData.Result != null && sessionTokenData.Result.Value != null)
        {
            return true;
        }

        return false;
    }

    public async Task<bool> UpdateSessionValid()
    {
        SessionToken sessionToken = await RequestSessionToken();

        if (sessionToken == null) return false;

        Debug.Log("Session token valid until: " + (new DateTime(1970, 1, 1)).AddSeconds(sessionToken.ValidUntil) +
                  " Now: " + DateTimeOffset.UtcNow);
        sessionValidUntil = sessionToken.ValidUntil;
        return IsSessionValid();
    }

    public async Task<SessionToken> RequestSessionToken()
    {
        ResponseValue<AccountInfo> sessionTokenData =
            (await Web3.Rpc.GetAccountInfoAsync(sessionWallet.SessionTokenPDA, Commitment.Confirmed)).Result;

        if (sessionTokenData == null) return null;
        if (sessionTokenData.Value == null || sessionTokenData.Value.Data[0] == null)
        {
            return null;
        }

        var sessionToken = SessionToken.Deserialize(Convert.FromBase64String(sessionTokenData.Value.Data[0]));

        return sessionToken;
    }

    private bool IsSessionValid()
    {
        return sessionValidUntil != null && sessionValidUntil > DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    }

    private async Task RefreshSessionWallet()
    {
        sessionWallet = await SessionWallet.GetSessionWallet(AnchorProgramIdPubKey, sessionKeyPassword,
            Web3.Wallet);
    }

    public async Task CreateNewSession()
    {
        var sessionToken = await Instance.RequestSessionToken();
        if (sessionToken != null)
        {
            await sessionWallet.CloseSession();
        }

        var transaction = new Transaction()
        {
            FeePayer = Web3.Account,
            Instructions = new List<TransactionInstruction>(),
            RecentBlockHash = await Web3.BlockHash(Commitment.Confirmed, false)
        };

        SessionWallet.Instance = null;
        await RefreshSessionWallet();
        var sessionIx = sessionWallet.CreateSessionIX(true, GetSessionKeysEndTime());
        transaction.Add(sessionIx);
        transaction.PartialSign(new[] {Web3.Account, sessionWallet.Account});

        var res = await Web3.Wallet.SignAndSendTransaction(transaction, commitment: Commitment.Confirmed);

        Debug.Log("Create session wallet: " + res.RawRpcResponse);
        await Web3.Wallet.ActiveRpcClient.ConfirmTransaction(res.Result, Commitment.Confirmed);
        var sessionValid = await UpdateSessionValid();
        Debug.Log("After create session, the session is valid: " + sessionValid);
    }

    public static byte[] UlongToLittleEndianBytes(ulong value)
    {
      byte[] bytes = BitConverter.GetBytes(value);

      // BitConverter.GetBytes returns bytes in little-endian on little-endian systems.
      // If the system is big-endian, reverse the array to make it little-endian.
      if (!BitConverter.IsLittleEndian)
      {
        Array.Reverse(bytes);
      }

      return bytes;
    }

    public async void SpawnBlob(bool useSession, ulong tileViewX, ulong tileViewY, Action onSuccess)
    {
        if (!Instance.IsSessionValid() && useSession)
        {
            await Instance.UpdateSessionValid();
            ServiceFactory.Resolve<UiService>().OpenPopup(UiService.ScreenType.SessionPopup, new SessionPopupUiData());
            return;
        }

        var transaction = new Transaction()
        {
            FeePayer = Web3.Account,
            Instructions = new List<TransactionInstruction>(),
            RecentBlockHash = await Web3.BlockHash(maxSeconds: 5)
        };

        PublicKey newBlobPDA = null;
        PublicKey.TryFindProgramAddress(new[]
            {
              Encoding.UTF8.GetBytes(gameDataSeed),
              new byte[] { (byte)tileViewX },
              new byte[] {  (byte) tileViewY }
            },
          AnchorProgramIdPubKey,
          out newBlobPDA, out byte bump);

          Debug.Log($"x: {tileViewX} and y: {tileViewX}" );
          Debug.Log($"x: {(byte) tileViewX} and y: { (byte) tileViewX}" );
          Debug.Log($"x: {UlongToLittleEndianBytes(tileViewX)} and y: {  UlongToLittleEndianBytes(tileViewY)}" );

        SpawnBlobsAccounts spawnBlobsAccounts = new SpawnBlobsAccounts
        {
            Blob = newBlobPDA,
            GameData = GameDataPDA,
            Player = PlayerDataPDA,
            SystemProgram = SystemProgram.ProgramIdKey
        };

        int seed = Web3.Account.PublicKey.GetHashCode();

        // Seed the random number generator
        Random.InitState(seed);

        // Generate random color components
        float r = Random.Range(0f, 1f);
        float g = Random.Range(0f, 1f);
        float b = Random.Range(0f, 1f);

        // Return the new random color

        var randomColorDerivedfromPubkey =  new Color(r, g, b);

        Debug.Log("Random color: "+ randomColorDerivedfromPubkey);
        ulong pickerPlayerColor = ColorUtils.RGBToUlong((int) (randomColorDerivedfromPubkey.r* 65535), (int) (randomColorDerivedfromPubkey.g* 65535), (int) (randomColorDerivedfromPubkey.b* 65535), 65535);
        ulong test = ColorUtils.RGBToUlong(65535 / 2, 65535 / 2, 65535 / 2, 65535 / 2);
        Debug.Log(test);
       // ulong pickerPlayerColor = ColorUtils.RGBToUlong(100, 0, 0, 255);

        var revert = ColorUtils.UlongToColor(pickerPlayerColor);

        if (useSession)
        {
            transaction.FeePayer = sessionWallet.Account.PublicKey;
            spawnBlobsAccounts.Signer = sessionWallet.Account.PublicKey;
            spawnBlobsAccounts.SessionToken = sessionWallet.SessionTokenPDA;
            // TODO: bad conversion from u64 to u8
            var chopInstruction = BlobsProgram.SpawnBlobs(spawnBlobsAccounts, gameDataSeed, (byte) tileViewX, (byte) tileViewY, pickerPlayerColor,  AnchorProgramIdPubKey);
            transaction.Add(chopInstruction);
            Debug.Log("Sign and send chop tree with session");
            await SendAndConfirmTransaction(sessionWallet, transaction, "Chop Tree with session.", isBlocking: false, onSucccess: onSuccess);
        }
        else
        {
            transaction.FeePayer = Web3.Account.PublicKey;
            spawnBlobsAccounts.Signer = Web3.Account.PublicKey;
            // TODO: bad conversion from u64 to u8
            var chopInstruction = BlobsProgram.SpawnBlobs(spawnBlobsAccounts, gameDataSeed, (byte) tileViewX, (byte) tileViewY, pickerPlayerColor,  AnchorProgramIdPubKey);
            transaction.Add(chopInstruction);
            Debug.Log("Sign and send init without session");
            await SendAndConfirmTransaction(Web3.Wallet, transaction, "Chop Tree without session.", onSucccess: onSuccess);
        }

        if (CurrentGameData == null)
        {
            await SubscribeToGameDataUpdates();
        }
    }

    public async void AttackBlob(bool useSession, BlobView selectedBlobView, BlobView clickedBlobView, Action onSuccess = null)
    {
        if (!Instance.IsSessionValid() && useSession)
        {
            await Instance.UpdateSessionValid();
            ServiceFactory.Resolve<UiService>().OpenPopup(UiService.ScreenType.SessionPopup, new SessionPopupUiData());
            return;
        }

        var transaction = new Transaction()
        {
            FeePayer = Web3.Account,
            Instructions = new List<TransactionInstruction>(),
            RecentBlockHash = await Web3.BlockHash(maxSeconds: 5)
        };

        PublicKey attackingBlob = null;
        PublicKey.TryFindProgramAddress(new[]
          {
            Encoding.UTF8.GetBytes(gameDataSeed),
            new byte[] { (byte) selectedBlobView.CurrentBlobData.X },
            new byte[] { (byte) selectedBlobView.CurrentBlobData.Y }
          },
          AnchorProgramIdPubKey,
          out attackingBlob, out byte bump);

        PublicKey defendingBlob = null;
        PublicKey.TryFindProgramAddress(new[]
          {
            Encoding.UTF8.GetBytes(gameDataSeed),
            new byte[] { (byte) clickedBlobView.CurrentBlobData.X },
            new byte[] { (byte) clickedBlobView.CurrentBlobData.Y },
          },
          AnchorProgramIdPubKey,
          out defendingBlob, out byte bump2);

        AttackBlobAccounts spawnBlobsAccounts = new AttackBlobAccounts
        {
            AttackingBlob = attackingBlob,
            DefendingBlob = defendingBlob,
            Player = PlayerDataPDA,
            GameData = GameDataPDA,
        };

        if (useSession)
        {
            transaction.FeePayer = sessionWallet.Account.PublicKey;
            spawnBlobsAccounts.Signer = sessionWallet.Account.PublicKey;
            spawnBlobsAccounts.SessionToken = sessionWallet.SessionTokenPDA;
            // TODO: bad conversion from u64 to u8
            var attackInstruction = BlobsProgram.AttackBlob(spawnBlobsAccounts,
              gameDataSeed, (byte)
              selectedBlobView.CurrentBlobData.X,
              (byte) selectedBlobView.CurrentBlobData.Y,
              clickedBlobView.CurrentBlobData.X,
            clickedBlobView.CurrentBlobData.Y,
              AnchorProgramIdPubKey);
            transaction.Add(attackInstruction);
            Debug.Log("Sign and send chop tree with session");
            await SendAndConfirmTransaction(sessionWallet, transaction, "Attack blob session.", isBlocking: false, onSucccess: onSuccess);
        }
        else
        {
            transaction.FeePayer = Web3.Account.PublicKey;
            spawnBlobsAccounts.Signer = Web3.Account.PublicKey;
            // TODO: bad conversion from u64 to u8
            var attackInstruction = BlobsProgram.AttackBlob(spawnBlobsAccounts,
              gameDataSeed, (byte)
              selectedBlobView.CurrentBlobData.X,
              (byte) selectedBlobView.CurrentBlobData.Y,
              clickedBlobView.CurrentBlobData.X,
              clickedBlobView.CurrentBlobData.Y,
              AnchorProgramIdPubKey);
            transaction.Add(attackInstruction);
            Debug.Log("Sign and send init without session");
            await SendAndConfirmTransaction(Web3.Wallet, transaction, "Attack blob no session.", onSucccess: onSuccess);
        }

        if (CurrentGameData == null)
        {
            await SubscribeToGameDataUpdates();
        }
    }

    public async void SendAttackFinish(bool useSession, byte attacker_x, byte attacker_y, byte defender_x, byte defender_y, Action onSuccess, Action<String> onError)
    {
        if (!Instance.IsSessionValid() && useSession)
        {
            await Instance.UpdateSessionValid();
            ServiceFactory.Resolve<UiService>().OpenPopup(UiService.ScreenType.SessionPopup, new SessionPopupUiData());
            return;
        }

        var transaction = new Transaction()
        {
            FeePayer = Web3.Account,
            Instructions = new List<TransactionInstruction>(),
            RecentBlockHash = await Web3.BlockHash(maxSeconds: 5)
        };

        PublicKey attackingBlob = null;
        PublicKey.TryFindProgramAddress(new[]
          {
            Encoding.UTF8.GetBytes(gameDataSeed),
            new byte[] { (byte) attacker_x },
            new byte[] { (byte) attacker_y }
          },
          AnchorProgramIdPubKey,
          out attackingBlob, out byte bump);

        PublicKey defendingBlob = null;
        PublicKey.TryFindProgramAddress(new[]
          {
            Encoding.UTF8.GetBytes(gameDataSeed),
            new byte[] { (byte) defender_x },
            new byte[] { (byte) defender_y },
          },
          AnchorProgramIdPubKey,
          out defendingBlob, out byte bump2);

        FinishAttackBlobAccounts spawnBlobsAccounts = new FinishAttackBlobAccounts
        {
            Player = PlayerDataPDA,
            AttackingBlob = attackingBlob,
            DefendingBlob = defendingBlob,
            GameData = GameDataPDA,
        };

        if (useSession)
        {
            transaction.FeePayer = sessionWallet.Account.PublicKey;
            spawnBlobsAccounts.Signer = sessionWallet.Account.PublicKey;
            spawnBlobsAccounts.SessionToken = sessionWallet.SessionTokenPDA;
            // TODO: bad conversion from u64 to u8
            var attackInstruction = BlobsProgram.FinishAttackBlob(spawnBlobsAccounts,
              gameDataSeed, (byte)
              attacker_x,
              attacker_y,
              defender_x,
            defender_y,
              AnchorProgramIdPubKey);
            transaction.Add(attackInstruction);
            Debug.Log("Sign and send chop tree with session");
            await SendAndConfirmTransaction(sessionWallet, transaction, "Finish attach blob session.", isBlocking: false, onSucccess: onSuccess, onError: onError);
        }
        else
        {
            transaction.FeePayer = Web3.Account.PublicKey;
            spawnBlobsAccounts.Signer = Web3.Account.PublicKey;
            // TODO: bad conversion from u64 to u8
            var attackInstruction = BlobsProgram.FinishAttackBlob(spawnBlobsAccounts,
              gameDataSeed, (byte)
              attacker_x,
              attacker_y,
              defender_x,
              defender_y,
              AnchorProgramIdPubKey);
            transaction.Add(attackInstruction);
            Debug.Log("Sign and send init without session");
            await SendAndConfirmTransaction(Web3.Wallet, transaction, "Finish Attack blob no session.", onSucccess: onSuccess, onError: onError);
        }

        if (CurrentGameData == null)
        {
            await SubscribeToGameDataUpdates();
        }
    }

    public PublicKey GetBlobPubkey(BlobData blobData)
    {
      PublicKey defendingBlob = null;
      PublicKey.TryFindProgramAddress(new[]
        {
          Encoding.UTF8.GetBytes(gameDataSeed),
          new byte[] { (byte) blobData.X },
          new byte[] { (byte) blobData.Y },
        },
        AnchorProgramIdPubKey,
        out defendingBlob, out byte bump2);
      return defendingBlob;
    }

}
