using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Solana.Unity;
using Solana.Unity.Programs.Abstract;
using Solana.Unity.Programs.Utilities;
using Solana.Unity.Rpc;
using Solana.Unity.Rpc.Builders;
using Solana.Unity.Rpc.Core.Http;
using Solana.Unity.Rpc.Core.Sockets;
using Solana.Unity.Rpc.Types;
using Solana.Unity.Wallet;
using Blobs;
using Blobs.Program;
using Blobs.Errors;
using Blobs.Accounts;

namespace Blobs
{
    namespace Accounts
    {
        public partial class BlobData
        {
            public static ulong ACCOUNT_DISCRIMINATOR => 17324442149211179174UL;
            public static ReadOnlySpan<byte> ACCOUNT_DISCRIMINATOR_BYTES => new byte[]{166, 136, 137, 139, 88, 200, 108, 240};
            public static string ACCOUNT_DISCRIMINATOR_B58 => "Uram9xpMZqZ";
            public PublicKey Authority { get; set; }

            public byte X { get; set; }

            public byte Y { get; set; }

            public byte Level { get; set; }

            public ulong ColorValue { get; set; }

            public ulong ColorCurrent { get; set; }

            public ulong ColorMax { get; set; }

            public long LastLogin { get; set; }

            public ushort LastId { get; set; }

            public long AttackStartTime { get; set; }

            public ulong AttackDuration { get; set; }

            public ulong AttackPower { get; set; }

            public PublicKey AttackTarget { get; set; }

            public PublicKey[] Attackers { get; set; }

            public static BlobData Deserialize(ReadOnlySpan<byte> _data)
            {
                int offset = 0;
                ulong accountHashValue = _data.GetU64(offset);
                offset += 8;
                if (accountHashValue != ACCOUNT_DISCRIMINATOR)
                {
                    return null;
                }

                BlobData result = new BlobData();
                if (_data.GetBool(offset++))
                {
                    result.Authority = _data.GetPubKey(offset);
                    offset += 32;
                }

                result.X = _data.GetU8(offset);
                offset += 1;
                result.Y = _data.GetU8(offset);
                offset += 1;
                result.Level = _data.GetU8(offset);
                offset += 1;
                result.ColorValue = _data.GetU64(offset);
                offset += 8;
                result.ColorCurrent = _data.GetU64(offset);
                offset += 8;
                result.ColorMax = _data.GetU64(offset);
                offset += 8;
                result.LastLogin = _data.GetS64(offset);
                offset += 8;
                result.LastId = _data.GetU16(offset);
                offset += 2;
                result.AttackStartTime = _data.GetS64(offset);
                offset += 8;
                result.AttackDuration = _data.GetU64(offset);
                offset += 8;
                result.AttackPower = _data.GetU64(offset);
                offset += 8;
                result.AttackTarget = _data.GetPubKey(offset);
                offset += 32;
                int resultAttackersLength = (int)_data.GetU32(offset);
                offset += 4;
                result.Attackers = new PublicKey[resultAttackersLength];
                for (uint resultAttackersIdx = 0; resultAttackersIdx < resultAttackersLength; resultAttackersIdx++)
                {
                    result.Attackers[resultAttackersIdx] = _data.GetPubKey(offset);
                    offset += 32;
                }

                return result;
            }
        }

        public partial class GameData
        {
            public static ulong ACCOUNT_DISCRIMINATOR => 13758009850765924589UL;
            public static ReadOnlySpan<byte> ACCOUNT_DISCRIMINATOR_BYTES => new byte[]{237, 88, 58, 243, 16, 69, 238, 190};
            public static string ACCOUNT_DISCRIMINATOR_B58 => "ghYLwVtPH73";
            public ulong TotalWoodCollected { get; set; }

            public PublicKey[] ActiveBlobs { get; set; }

            public static GameData Deserialize(ReadOnlySpan<byte> _data)
            {
                int offset = 0;
                ulong accountHashValue = _data.GetU64(offset);
                offset += 8;
                if (accountHashValue != ACCOUNT_DISCRIMINATOR)
                {
                    return null;
                }

                GameData result = new GameData();
                result.TotalWoodCollected = _data.GetU64(offset);
                offset += 8;
                int resultActiveBlobsLength = (int)_data.GetU32(offset);
                offset += 4;
                result.ActiveBlobs = new PublicKey[resultActiveBlobsLength];
                for (uint resultActiveBlobsIdx = 0; resultActiveBlobsIdx < resultActiveBlobsLength; resultActiveBlobsIdx++)
                {
                    result.ActiveBlobs[resultActiveBlobsIdx] = _data.GetPubKey(offset);
                    offset += 32;
                }

                return result;
            }
        }

        public partial class PlayerData
        {
            public static ulong ACCOUNT_DISCRIMINATOR => 9264901878634267077UL;
            public static ReadOnlySpan<byte> ACCOUNT_DISCRIMINATOR_BYTES => new byte[]{197, 65, 216, 202, 43, 139, 147, 128};
            public static string ACCOUNT_DISCRIMINATOR_B58 => "ZzeEvyxXcpF";
            public PublicKey Authority { get; set; }

            public string Name { get; set; }

            public byte Level { get; set; }

            public ulong Xp { get; set; }

            public ulong Wood { get; set; }

            public ulong Energy { get; set; }

            public long LastLogin { get; set; }

            public ushort LastId { get; set; }

            public ushort BlobsSpawned { get; set; }

            public static PlayerData Deserialize(ReadOnlySpan<byte> _data)
            {
                int offset = 0;
                ulong accountHashValue = _data.GetU64(offset);
                offset += 8;
                if (accountHashValue != ACCOUNT_DISCRIMINATOR)
                {
                    return null;
                }

                PlayerData result = new PlayerData();
                result.Authority = _data.GetPubKey(offset);
                offset += 32;
                offset += _data.GetBorshString(offset, out var resultName);
                result.Name = resultName;
                result.Level = _data.GetU8(offset);
                offset += 1;
                result.Xp = _data.GetU64(offset);
                offset += 8;
                result.Wood = _data.GetU64(offset);
                offset += 8;
                result.Energy = _data.GetU64(offset);
                offset += 8;
                result.LastLogin = _data.GetS64(offset);
                offset += 8;
                result.LastId = _data.GetU16(offset);
                offset += 2;
                result.BlobsSpawned = _data.GetU16(offset);
                offset += 2;
                return result;
            }
        }
    }

    namespace Errors
    {
        public enum BlobsErrorKind : uint
        {
            NotEnoughEnergy = 6000U,
            WrongAuthority = 6001U,
            AlreadyAttacking = 6002U,
            NotAttacking = 6003U,
            NotFinished = 6004U
        }
    }

    public partial class BlobsClient : TransactionalBaseClient<BlobsErrorKind>
    {
        public BlobsClient(IRpcClient rpcClient, IStreamingRpcClient streamingRpcClient, PublicKey programId) : base(rpcClient, streamingRpcClient, programId)
        {
        }

        public async Task<Solana.Unity.Programs.Models.ProgramAccountsResultWrapper<List<BlobData>>> GetBlobDatasAsync(string programAddress, Commitment commitment = Commitment.Finalized)
        {
            var list = new List<Solana.Unity.Rpc.Models.MemCmp>{new Solana.Unity.Rpc.Models.MemCmp{Bytes = BlobData.ACCOUNT_DISCRIMINATOR_B58, Offset = 0}};
            var res = await RpcClient.GetProgramAccountsAsync(programAddress, commitment, memCmpList: list);
            if (!res.WasSuccessful || !(res.Result?.Count > 0))
                return new Solana.Unity.Programs.Models.ProgramAccountsResultWrapper<List<BlobData>>(res);
            List<BlobData> resultingAccounts = new List<BlobData>(res.Result.Count);
            resultingAccounts.AddRange(res.Result.Select(result => BlobData.Deserialize(Convert.FromBase64String(result.Account.Data[0]))));
            return new Solana.Unity.Programs.Models.ProgramAccountsResultWrapper<List<BlobData>>(res, resultingAccounts);
        }

        public async Task<Solana.Unity.Programs.Models.ProgramAccountsResultWrapper<List<GameData>>> GetGameDatasAsync(string programAddress, Commitment commitment = Commitment.Finalized)
        {
            var list = new List<Solana.Unity.Rpc.Models.MemCmp>{new Solana.Unity.Rpc.Models.MemCmp{Bytes = GameData.ACCOUNT_DISCRIMINATOR_B58, Offset = 0}};
            var res = await RpcClient.GetProgramAccountsAsync(programAddress, commitment, memCmpList: list);
            if (!res.WasSuccessful || !(res.Result?.Count > 0))
                return new Solana.Unity.Programs.Models.ProgramAccountsResultWrapper<List<GameData>>(res);
            List<GameData> resultingAccounts = new List<GameData>(res.Result.Count);
            resultingAccounts.AddRange(res.Result.Select(result => GameData.Deserialize(Convert.FromBase64String(result.Account.Data[0]))));
            return new Solana.Unity.Programs.Models.ProgramAccountsResultWrapper<List<GameData>>(res, resultingAccounts);
        }

        public async Task<Solana.Unity.Programs.Models.ProgramAccountsResultWrapper<List<PlayerData>>> GetPlayerDatasAsync(string programAddress, Commitment commitment = Commitment.Finalized)
        {
            var list = new List<Solana.Unity.Rpc.Models.MemCmp>{new Solana.Unity.Rpc.Models.MemCmp{Bytes = PlayerData.ACCOUNT_DISCRIMINATOR_B58, Offset = 0}};
            var res = await RpcClient.GetProgramAccountsAsync(programAddress, commitment, memCmpList: list);
            if (!res.WasSuccessful || !(res.Result?.Count > 0))
                return new Solana.Unity.Programs.Models.ProgramAccountsResultWrapper<List<PlayerData>>(res);
            List<PlayerData> resultingAccounts = new List<PlayerData>(res.Result.Count);
            resultingAccounts.AddRange(res.Result.Select(result => PlayerData.Deserialize(Convert.FromBase64String(result.Account.Data[0]))));
            return new Solana.Unity.Programs.Models.ProgramAccountsResultWrapper<List<PlayerData>>(res, resultingAccounts);
        }

        public async Task<Solana.Unity.Programs.Models.AccountResultWrapper<BlobData>> GetBlobDataAsync(string accountAddress, Commitment commitment = Commitment.Finalized)
        {
            var res = await RpcClient.GetAccountInfoAsync(accountAddress, commitment);
            if (!res.WasSuccessful)
                return new Solana.Unity.Programs.Models.AccountResultWrapper<BlobData>(res);
            var resultingAccount = BlobData.Deserialize(Convert.FromBase64String(res.Result.Value.Data[0]));
            return new Solana.Unity.Programs.Models.AccountResultWrapper<BlobData>(res, resultingAccount);
        }

        public async Task<Solana.Unity.Programs.Models.AccountResultWrapper<GameData>> GetGameDataAsync(string accountAddress, Commitment commitment = Commitment.Finalized)
        {
            var res = await RpcClient.GetAccountInfoAsync(accountAddress, commitment);
            if (!res.WasSuccessful)
                return new Solana.Unity.Programs.Models.AccountResultWrapper<GameData>(res);
            var resultingAccount = GameData.Deserialize(Convert.FromBase64String(res.Result.Value.Data[0]));
            return new Solana.Unity.Programs.Models.AccountResultWrapper<GameData>(res, resultingAccount);
        }

        public async Task<Solana.Unity.Programs.Models.AccountResultWrapper<PlayerData>> GetPlayerDataAsync(string accountAddress, Commitment commitment = Commitment.Finalized)
        {
            var res = await RpcClient.GetAccountInfoAsync(accountAddress, commitment);
            if (!res.WasSuccessful)
                return new Solana.Unity.Programs.Models.AccountResultWrapper<PlayerData>(res);
            var resultingAccount = PlayerData.Deserialize(Convert.FromBase64String(res.Result.Value.Data[0]));
            return new Solana.Unity.Programs.Models.AccountResultWrapper<PlayerData>(res, resultingAccount);
        }

        public async Task<SubscriptionState> SubscribeBlobDataAsync(string accountAddress, Action<SubscriptionState, Solana.Unity.Rpc.Messages.ResponseValue<Solana.Unity.Rpc.Models.AccountInfo>, BlobData> callback, Commitment commitment = Commitment.Finalized)
        {
            SubscriptionState res = await StreamingRpcClient.SubscribeAccountInfoAsync(accountAddress, (s, e) =>
            {
                BlobData parsingResult = null;
                if (e.Value?.Data?.Count > 0)
                    parsingResult = BlobData.Deserialize(Convert.FromBase64String(e.Value.Data[0]));
                callback(s, e, parsingResult);
            }, commitment);
            return res;
        }

        public async Task<SubscriptionState> SubscribeGameDataAsync(string accountAddress, Action<SubscriptionState, Solana.Unity.Rpc.Messages.ResponseValue<Solana.Unity.Rpc.Models.AccountInfo>, GameData> callback, Commitment commitment = Commitment.Finalized)
        {
            SubscriptionState res = await StreamingRpcClient.SubscribeAccountInfoAsync(accountAddress, (s, e) =>
            {
                GameData parsingResult = null;
                if (e.Value?.Data?.Count > 0)
                    parsingResult = GameData.Deserialize(Convert.FromBase64String(e.Value.Data[0]));
                callback(s, e, parsingResult);
            }, commitment);
            return res;
        }

        public async Task<SubscriptionState> SubscribePlayerDataAsync(string accountAddress, Action<SubscriptionState, Solana.Unity.Rpc.Messages.ResponseValue<Solana.Unity.Rpc.Models.AccountInfo>, PlayerData> callback, Commitment commitment = Commitment.Finalized)
        {
            SubscriptionState res = await StreamingRpcClient.SubscribeAccountInfoAsync(accountAddress, (s, e) =>
            {
                PlayerData parsingResult = null;
                if (e.Value?.Data?.Count > 0)
                    parsingResult = PlayerData.Deserialize(Convert.FromBase64String(e.Value.Data[0]));
                callback(s, e, parsingResult);
            }, commitment);
            return res;
        }

        public async Task<RequestResult<string>> SendInitPlayerAsync(InitPlayerAccounts accounts, string levelSeed, PublicKey feePayer, Func<byte[], PublicKey, byte[]> signingCallback, PublicKey programId)
        {
            Solana.Unity.Rpc.Models.TransactionInstruction instr = Program.BlobsProgram.InitPlayer(accounts, levelSeed, programId);
            return await SignAndSendTransaction(instr, feePayer, signingCallback);
        }

        public async Task<RequestResult<string>> SendSpawnBlobsAsync(SpawnBlobsAccounts accounts, string levelSeed, byte x, byte y, ulong playerColor, PublicKey feePayer, Func<byte[], PublicKey, byte[]> signingCallback, PublicKey programId)
        {
            Solana.Unity.Rpc.Models.TransactionInstruction instr = Program.BlobsProgram.SpawnBlobs(accounts, levelSeed, x, y, playerColor, programId);
            return await SignAndSendTransaction(instr, feePayer, signingCallback);
        }

        public async Task<RequestResult<string>> SendAttackBlobAsync(AttackBlobAccounts accounts, string levelSeed, byte attackingBlobX, byte attackingBlobY, byte defendingBlobX, byte defendingBlobY, PublicKey feePayer, Func<byte[], PublicKey, byte[]> signingCallback, PublicKey programId)
        {
            Solana.Unity.Rpc.Models.TransactionInstruction instr = Program.BlobsProgram.AttackBlob(accounts, levelSeed, attackingBlobX, attackingBlobY, defendingBlobX, defendingBlobY, programId);
            return await SignAndSendTransaction(instr, feePayer, signingCallback);
        }

        public async Task<RequestResult<string>> SendFinishAttackBlobAsync(FinishAttackBlobAccounts accounts, string levelSeed, byte attackingBlobX, byte attackingBlobY, byte defendingBlobX, byte defendingBlobY, PublicKey feePayer, Func<byte[], PublicKey, byte[]> signingCallback, PublicKey programId)
        {
            Solana.Unity.Rpc.Models.TransactionInstruction instr = Program.BlobsProgram.FinishAttackBlob(accounts, levelSeed, attackingBlobX, attackingBlobY, defendingBlobX, defendingBlobY, programId);
            return await SignAndSendTransaction(instr, feePayer, signingCallback);
        }

        public async Task<RequestResult<string>> SendChopTreeAsync(ChopTreeAccounts accounts, string levelSeed, ushort counter, PublicKey feePayer, Func<byte[], PublicKey, byte[]> signingCallback, PublicKey programId)
        {
            Solana.Unity.Rpc.Models.TransactionInstruction instr = Program.BlobsProgram.ChopTree(accounts, levelSeed, counter, programId);
            return await SignAndSendTransaction(instr, feePayer, signingCallback);
        }

        protected override Dictionary<uint, ProgramError<BlobsErrorKind>> BuildErrorsDictionary()
        {
            return new Dictionary<uint, ProgramError<BlobsErrorKind>>{{6000U, new ProgramError<BlobsErrorKind>(BlobsErrorKind.NotEnoughEnergy, "Not enough energy")}, {6001U, new ProgramError<BlobsErrorKind>(BlobsErrorKind.WrongAuthority, "Wrong Authority")}, {6002U, new ProgramError<BlobsErrorKind>(BlobsErrorKind.AlreadyAttacking, "Already attacking")}, {6003U, new ProgramError<BlobsErrorKind>(BlobsErrorKind.NotAttacking, "Not attacking")}, {6004U, new ProgramError<BlobsErrorKind>(BlobsErrorKind.NotFinished, "Not finished")}, };
        }
    }

    namespace Program
    {
        public class InitPlayerAccounts
        {
            public PublicKey Player { get; set; }

            public PublicKey GameData { get; set; }

            public PublicKey Signer { get; set; }

            public PublicKey SystemProgram { get; set; }
        }

        public class SpawnBlobsAccounts
        {
            public PublicKey SessionToken { get; set; }

            public PublicKey Blob { get; set; }

            public PublicKey GameData { get; set; }

            public PublicKey Player { get; set; }

            public PublicKey Signer { get; set; }

            public PublicKey SystemProgram { get; set; }
        }

        public class AttackBlobAccounts
        {
            public PublicKey SessionToken { get; set; }

            public PublicKey AttackingBlob { get; set; }

            public PublicKey DefendingBlob { get; set; }

            public PublicKey Player { get; set; }

            public PublicKey GameData { get; set; }

            public PublicKey Signer { get; set; }
        }

        public class FinishAttackBlobAccounts
        {
            public PublicKey SessionToken { get; set; }

            public PublicKey AttackingBlob { get; set; }

            public PublicKey DefendingBlob { get; set; }

            public PublicKey Player { get; set; }

            public PublicKey GameData { get; set; }

            public PublicKey Signer { get; set; }
        }

        public class ChopTreeAccounts
        {
            public PublicKey SessionToken { get; set; }

            public PublicKey Player { get; set; }

            public PublicKey GameData { get; set; }

            public PublicKey Signer { get; set; }

            public PublicKey SystemProgram { get; set; }
        }

        public static class BlobsProgram
        {
            public static Solana.Unity.Rpc.Models.TransactionInstruction InitPlayer(InitPlayerAccounts accounts, string levelSeed, PublicKey programId)
            {
                List<Solana.Unity.Rpc.Models.AccountMeta> keys = new()
                {Solana.Unity.Rpc.Models.AccountMeta.Writable(accounts.Player, false), Solana.Unity.Rpc.Models.AccountMeta.Writable(accounts.GameData, false), Solana.Unity.Rpc.Models.AccountMeta.Writable(accounts.Signer, true), Solana.Unity.Rpc.Models.AccountMeta.ReadOnly(accounts.SystemProgram, false)};
                byte[] _data = new byte[1200];
                int offset = 0;
                _data.WriteU64(4819994211046333298UL, offset);
                offset += 8;
                offset += _data.WriteBorshString(levelSeed, offset);
                byte[] resultData = new byte[offset];
                Array.Copy(_data, resultData, offset);
                return new Solana.Unity.Rpc.Models.TransactionInstruction{Keys = keys, ProgramId = programId.KeyBytes, Data = resultData};
            }

            public static Solana.Unity.Rpc.Models.TransactionInstruction SpawnBlobs(SpawnBlobsAccounts accounts, string levelSeed, byte x, byte y, ulong playerColor, PublicKey programId)
            {
                List<Solana.Unity.Rpc.Models.AccountMeta> keys = new()
                {Solana.Unity.Rpc.Models.AccountMeta.ReadOnly(accounts.SessionToken == null ? programId : accounts.SessionToken, false), Solana.Unity.Rpc.Models.AccountMeta.Writable(accounts.Blob, false), Solana.Unity.Rpc.Models.AccountMeta.Writable(accounts.GameData, false), Solana.Unity.Rpc.Models.AccountMeta.Writable(accounts.Player, false), Solana.Unity.Rpc.Models.AccountMeta.Writable(accounts.Signer, true), Solana.Unity.Rpc.Models.AccountMeta.ReadOnly(accounts.SystemProgram, false)};
                byte[] _data = new byte[1200];
                int offset = 0;
                _data.WriteU64(18180153710071007331UL, offset);
                offset += 8;
                offset += _data.WriteBorshString(levelSeed, offset);
                _data.WriteU8(x, offset);
                offset += 1;
                _data.WriteU8(y, offset);
                offset += 1;
                _data.WriteU64(playerColor, offset);
                offset += 8;
                byte[] resultData = new byte[offset];
                Array.Copy(_data, resultData, offset);
                return new Solana.Unity.Rpc.Models.TransactionInstruction{Keys = keys, ProgramId = programId.KeyBytes, Data = resultData};
            }

            public static Solana.Unity.Rpc.Models.TransactionInstruction AttackBlob(AttackBlobAccounts accounts, string levelSeed, byte attackingBlobX, byte attackingBlobY, byte defendingBlobX, byte defendingBlobY, PublicKey programId)
            {
                List<Solana.Unity.Rpc.Models.AccountMeta> keys = new()
                {Solana.Unity.Rpc.Models.AccountMeta.ReadOnly(accounts.SessionToken == null ? programId : accounts.SessionToken, false), Solana.Unity.Rpc.Models.AccountMeta.Writable(accounts.AttackingBlob, false), Solana.Unity.Rpc.Models.AccountMeta.Writable(accounts.DefendingBlob, false), Solana.Unity.Rpc.Models.AccountMeta.Writable(accounts.Player, false), Solana.Unity.Rpc.Models.AccountMeta.Writable(accounts.GameData, false), Solana.Unity.Rpc.Models.AccountMeta.Writable(accounts.Signer, true)};
                byte[] _data = new byte[1200];
                int offset = 0;
                _data.WriteU64(2699594283024781515UL, offset);
                offset += 8;
                offset += _data.WriteBorshString(levelSeed, offset);
                _data.WriteU8(attackingBlobX, offset);
                offset += 1;
                _data.WriteU8(attackingBlobY, offset);
                offset += 1;
                _data.WriteU8(defendingBlobX, offset);
                offset += 1;
                _data.WriteU8(defendingBlobY, offset);
                offset += 1;
                byte[] resultData = new byte[offset];
                Array.Copy(_data, resultData, offset);
                return new Solana.Unity.Rpc.Models.TransactionInstruction{Keys = keys, ProgramId = programId.KeyBytes, Data = resultData};
            }

            public static Solana.Unity.Rpc.Models.TransactionInstruction FinishAttackBlob(FinishAttackBlobAccounts accounts, string levelSeed, byte attackingBlobX, byte attackingBlobY, byte defendingBlobX, byte defendingBlobY, PublicKey programId)
            {
                List<Solana.Unity.Rpc.Models.AccountMeta> keys = new()
                {Solana.Unity.Rpc.Models.AccountMeta.ReadOnly(accounts.SessionToken == null ? programId : accounts.SessionToken, false), Solana.Unity.Rpc.Models.AccountMeta.Writable(accounts.AttackingBlob, false), Solana.Unity.Rpc.Models.AccountMeta.Writable(accounts.DefendingBlob, false), Solana.Unity.Rpc.Models.AccountMeta.Writable(accounts.Player, false), Solana.Unity.Rpc.Models.AccountMeta.Writable(accounts.GameData, false), Solana.Unity.Rpc.Models.AccountMeta.Writable(accounts.Signer, true)};
                byte[] _data = new byte[1200];
                int offset = 0;
                _data.WriteU64(14676763494536724465UL, offset);
                offset += 8;
                offset += _data.WriteBorshString(levelSeed, offset);
                _data.WriteU8(attackingBlobX, offset);
                offset += 1;
                _data.WriteU8(attackingBlobY, offset);
                offset += 1;
                _data.WriteU8(defendingBlobX, offset);
                offset += 1;
                _data.WriteU8(defendingBlobY, offset);
                offset += 1;
                byte[] resultData = new byte[offset];
                Array.Copy(_data, resultData, offset);
                return new Solana.Unity.Rpc.Models.TransactionInstruction{Keys = keys, ProgramId = programId.KeyBytes, Data = resultData};
            }

            public static Solana.Unity.Rpc.Models.TransactionInstruction ChopTree(ChopTreeAccounts accounts, string levelSeed, ushort counter, PublicKey programId)
            {
                List<Solana.Unity.Rpc.Models.AccountMeta> keys = new()
                {Solana.Unity.Rpc.Models.AccountMeta.ReadOnly(accounts.SessionToken == null ? programId : accounts.SessionToken, false), Solana.Unity.Rpc.Models.AccountMeta.Writable(accounts.Player, false), Solana.Unity.Rpc.Models.AccountMeta.Writable(accounts.GameData, false), Solana.Unity.Rpc.Models.AccountMeta.Writable(accounts.Signer, true), Solana.Unity.Rpc.Models.AccountMeta.ReadOnly(accounts.SystemProgram, false)};
                byte[] _data = new byte[1200];
                int offset = 0;
                _data.WriteU64(2027946759707441272UL, offset);
                offset += 8;
                offset += _data.WriteBorshString(levelSeed, offset);
                _data.WriteU16(counter, offset);
                offset += 2;
                byte[] resultData = new byte[offset];
                Array.Copy(_data, resultData, offset);
                return new Solana.Unity.Rpc.Models.TransactionInstruction{Keys = keys, ProgramId = programId.KeyBytes, Data = resultData};
            }
        }
    }
}
