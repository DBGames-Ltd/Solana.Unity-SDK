using System;
using System.Text;
using System.Threading.Tasks;
using Solana.Unity.Rpc;
using Solana.Unity.Wallet;
using Solana.Unity.Programs;
using Solana.Unity.Rpc.Builders;
using Solana.Unity.Rpc.Models;
using Solana.Unity.Metaplex.Candymachine;

namespace CandyMachineV2
{
    
    public static class CandyMachineCommands {
        public static readonly PublicKey TokenMetadataProgramId = new("metaqbxxUerdq28cj1RbAWkYQm3ybzjb6a8bt518x1s");
        public static readonly PublicKey CandyMachineProgramId = new("cndy3Z4yapfJBmL3ShUp5exZKqR3z33thTzeNMm2gRZ");
        public static readonly PublicKey instructionSysVarAccount = new("Sysvar1nstructions1111111111111111111111111");

        public static async Task<Transaction> InitializeCandyMachine(
            Account account, 
            Solana.Unity.Metaplex.Candymachine.Types.CandyMachineData candyMachineData,
            IRpcClient rpc
        )
        {
            var candyMachineAccount = new Account();
            var initializeCandyMachineAccounts = new InitializeCandyMachineAccounts {
                Authority = account,
                CandyMachine = candyMachineAccount,
                Payer = account,
                Rent = SysVars.RentKey,
                SystemProgram = SystemProgram.ProgramIdKey,
                Wallet = account
            };
            var candyMachineInstruction = CandyMachineProgram.InitializeCandyMachine(
                initializeCandyMachineAccounts, 
                candyMachineData, 
                CandyMachineProgramId
            );
            var blockHash = await rpc.GetRecentBlockHashAsync();

            var transaction = new TransactionBuilder()
                .SetRecentBlockHash(blockHash.Result.Value.Blockhash)
                .SetFeePayer(account)
                .AddInstruction(candyMachineInstruction);

            var tx = Transaction.Deserialize(transaction.Serialize());
            tx.PartialSign(candyMachineAccount);
            return tx;
        }
        
        /// <summary>
        /// Mint one token from the Candy Machine
        /// </summary>
        /// <param name="account">The target account used for minting the token</param>
        /// <param name="candyMachineKey">The CandyMachine public key</param>
        /// <param name="rpc">The RPC instance</param>
        public static async Task<Transaction> MintOneToken(Account account, PublicKey candyMachineKey, IRpcClient rpc)
        {
            var mint = new Account();
            var associatedTokenAccount = AssociatedTokenAccountProgram.DeriveAssociatedTokenAccount(account, mint.PublicKey);
            
            var candyMachineClient = new CandyMachineClient(rpc, null, CandyMachineProgramId);
            var candyMachineWrap  = await candyMachineClient.GetCandyMachineAsync(candyMachineKey);
            var candyMachine = candyMachineWrap.ParsedResult;
            
            var (candyMachineCreator, creatorBump) = getCandyMachineCreator(candyMachineKey);
            

            var mintNftAccounts = new MintNftAccounts
            {
                CandyMachine = candyMachineKey,
                CandyMachineCreator = candyMachineCreator,
                Clock = SysVars.ClockKey,
                InstructionSysvarAccount = instructionSysVarAccount,
                MasterEdition = getMasterEdition(mint.PublicKey),
                Metadata = getMetadata(mint.PublicKey),
                Mint = mint.PublicKey,
                MintAuthority = account,
                Payer = account,
                RecentBlockhashes = SysVars.RecentBlockHashesKey,
                Rent = SysVars.RentKey,
                SystemProgram = SystemProgram.ProgramIdKey,
                TokenMetadataProgram = TokenMetadataProgramId,
                TokenProgram = TokenProgram.ProgramIdKey,
                UpdateAuthority = account,
                Wallet = candyMachine.Wallet
            };

            var candyMachineInstruction = CandyMachineProgram.MintNft(mintNftAccounts, creatorBump, CandyMachineProgramId);

            var blockHash = await rpc.GetRecentBlockHashAsync();
            var minimumRent = await rpc.GetMinimumBalanceForRentExemptionAsync(TokenProgram.MintAccountDataSize);

            var transaction = new TransactionBuilder()
                .SetRecentBlockHash(blockHash.Result.Value.Blockhash)
                .SetFeePayer(account)
                .AddInstruction(
                    SystemProgram.CreateAccount(
                        account,
                        mint.PublicKey,
                        minimumRent.Result,
                        TokenProgram.MintAccountDataSize,
                        TokenProgram.ProgramIdKey))
                .AddInstruction(
                    TokenProgram.InitializeMint(
                        mint.PublicKey,
                        0,
                        account,
                        account))
                .AddInstruction(
                    AssociatedTokenAccountProgram.CreateAssociatedTokenAccount(
                        account,
                        account,
                        mint.PublicKey))
                .AddInstruction(
                    TokenProgram.MintTo(
                        mint.PublicKey,
                        associatedTokenAccount,
                        1,
                        account))
                .AddInstruction(candyMachineInstruction);
            
            var tx = Transaction.Deserialize(transaction.Serialize());
            tx.PartialSign(mint);
            return tx;
        }

        public static PublicKey getMasterEdition(PublicKey mint)
        {
            if (!PublicKey.TryFindProgramAddress(
                    new[]
                    {
                        Encoding.UTF8.GetBytes("metadata"),
                        TokenMetadataProgramId.KeyBytes,
                        mint.KeyBytes,
                        Encoding.UTF8.GetBytes("edition"),
                    },
                    TokenMetadataProgramId,
                    out PublicKey masterEdition, out _))
            {
                throw new InvalidProgramException();
            }
            return masterEdition;
        }
        
        public static PublicKey getMetadata(PublicKey mint)
        {
            if (!PublicKey.TryFindProgramAddress(
                    new[]
                    {
                        Encoding.UTF8.GetBytes("metadata"),
                        TokenMetadataProgramId.KeyBytes,
                        mint.KeyBytes,
                    },
                    TokenMetadataProgramId,
                    out PublicKey metadataAddress, out _))
            {
                throw new InvalidProgramException();
            }
            return metadataAddress;
        }
        
        public static (PublicKey candyMachineCreator, byte creatorBump) getCandyMachineCreator(PublicKey candyMachineAddress)
        {
            if (!PublicKey.TryFindProgramAddress(
                    new[] {Encoding.UTF8.GetBytes("candy_machine"), candyMachineAddress.KeyBytes},
                    CandyMachineProgramId,
                    out PublicKey candyMachineCreator,
                    out byte creatorBump))
            {
                throw new InvalidProgramException();
            }
            return (candyMachineCreator, creatorBump);
        }
    }
}