import * as anchor from "@coral-xyz/anchor";
import { Program } from "@coral-xyz/anchor";
import { Blobs } from "../target/types/blobs";
import { BN } from "bn.js";
import { assert } from "chai";

describe("blobs", () => {

  const provider = anchor.AnchorProvider.env()
  anchor.setProvider(provider)
  const program = anchor.workspace.Blobs as Program<Blobs>;
  const payer = provider.wallet as anchor.Wallet
  const gameDataSeed = "gameData";
  const secondPlayer = new anchor.web3.Keypair();
  const sleepTimeBetweenAttacks = 15000;

  const [playerPDA] = anchor.web3.PublicKey.findProgramAddressSync(
    [
      Buffer.from("player"),
      payer.publicKey.toBuffer(),        
    ],
    program.programId
  );

  const [secondPlayerPDA] = anchor.web3.PublicKey.findProgramAddressSync(
    [
      Buffer.from("player"),
      secondPlayer.publicKey.toBuffer(),        
    ],
    program.programId
  );

  const [attackingBlobPDA] = anchor.web3.PublicKey.findProgramAddressSync(
    [
      Buffer.from(gameDataSeed), new BN(0).toBuffer(), new BN(0).toBuffer()   
    ],
    program.programId
  );

  const [secondAttackingBlobPDA] = anchor.web3.PublicKey.findProgramAddressSync(
    [
      Buffer.from(gameDataSeed), new BN(1).toBuffer(), new BN(1).toBuffer()   
    ],
    program.programId
  );

  const [defendingBlobPDA] = anchor.web3.PublicKey.findProgramAddressSync(
    [
      Buffer.from(gameDataSeed), new BN(1).toBuffer(), new BN(1).toBuffer()   
    ],
    program.programId
  );
     
  const [gameDataPDA] = anchor.web3.PublicKey.findProgramAddressSync(
    [
      Buffer.from(gameDataSeed)       
    ],
    program.programId
  );

  it("Init player and spawn a blob!", async () => {
    await provider.connection.confirmTransaction(
      await provider.connection.requestAirdrop(secondPlayer.publicKey, 1e9), "confirmed"
    );

    console.log("Payer address", payer.publicKey.toBase58());   
    console.log("Seconde Player address", secondPlayer.publicKey.toBase58());   
    console.log("Local address", payer.publicKey.toBase58());

    const balance = await anchor.getProvider().connection.getBalance(payer.publicKey);
    
    if (balance < 1e8) {
      const res = await anchor.getProvider().connection.requestAirdrop(payer.publicKey, 1e9);
      await anchor.getProvider().connection.confirmTransaction(res, "confirmed");  
    }

    console.log("Player PDA", playerPDA.toBase58());

    const [gameDataPDA] = anchor.web3.PublicKey.findProgramAddressSync(
      [
        Buffer.from(gameDataSeed)       
      ],
      program.programId
    );

    try {
      let tx = await program.methods.initPlayer(gameDataSeed)
      .accounts(
        {
          player: playerPDA,
          gameData: gameDataPDA,
          signer: payer.publicKey,        
          systemProgram: anchor.web3.SystemProgram.programId,
        }    
      )
      .rpc({skipPreflight: true});
      console.log("Init transaction", tx);
      
      await anchor.getProvider().connection.confirmTransaction(tx, "confirmed");
      console.log("Confirmed", tx);

    } catch (e) {
      console.log("Player already exists: ", e);
    }

    console.log("SecondPlayerPDA ", secondPlayerPDA.toBase58());

    try {
      let tx = await program.methods.initPlayer(gameDataSeed)
      .accounts(
        {
          player: secondPlayerPDA,
          gameData: gameDataPDA,
          signer: secondPlayer.publicKey,        
          systemProgram: anchor.web3.SystemProgram.programId,
        }    
      ).signers([secondPlayer])
      .rpc({skipPreflight: true});

      console.log("Init second player transaction", tx);
      
      await anchor.getProvider().connection.confirmTransaction(tx, "confirmed");
      console.log("Confirmed", tx);

    } catch (e) {
      console.log("Player already exists: ", e);
    }
    
    const accountInfo = await anchor.getProvider().connection.getAccountInfo(
      playerPDA, "confirmed"
    );
    const decoded = program.coder.accounts.decode("PlayerData", accountInfo.data);
    console.log("Player account info", JSON.stringify(decoded));
  });

  it("Spawn 4 friendly blobs and one enemy blob!", async () => {

    for (let i = 0; i < 4; i++) {
      console.log(`Spawn blob instruction ${i}`);

      const [blobPDA] = anchor.web3.PublicKey.findProgramAddressSync(
        [
          Buffer.from(gameDataSeed), new BN(i).toBuffer(), new BN(i).toBuffer()   
        ],
        program.programId
      );
  
      console.log("Blob PDA", blobPDA.toBase58());

      let color = colorToU64BN(65535, 0, 0, 65535);

      let tx = await program.methods
      .spawnBlobs(gameDataSeed, i, i, color)
      .accounts(
        {
          player: playerPDA,
          sessionToken: null,
          blob: blobPDA,
          gameData: gameDataPDA,
          systemProgram: anchor.web3.SystemProgram.programId,
          signer: payer.publicKey
        }    
      )
      .rpc();

      console.log("Spawn Blob instruction", tx);
      await anchor.getProvider().connection.confirmTransaction(tx, "confirmed");

      const accountInfo = await anchor.getProvider().connection.getAccountInfo(
        blobPDA, "confirmed"
      );

      const decoded = program.coder.accounts.decode("BlobData", accountInfo.data);
      console.log("Player account info", JSON.stringify(decoded));

      const accountInfoGameData = await anchor.getProvider().connection.getAccountInfo(
        gameDataPDA, "confirmed"
      );

      const decodedGameData = program.coder.accounts.decode("GameData", accountInfoGameData.data);
      console.log("Game Data info", JSON.stringify(decodedGameData));
    }

    const [enemyBlobPDA] = anchor.web3.PublicKey.findProgramAddressSync(
      [
        Buffer.from(gameDataSeed), new BN(0).toBuffer(), new BN(1).toBuffer()   
      ],
      program.programId
    );

    let color = colorToU64BN(0, 0, 0, 65535);
    let bnString = new BN(color.toString());

    let tx = await program.methods
      .spawnBlobs(gameDataSeed, 0, 1, bnString)
      .accounts(
        {
          player: secondPlayerPDA,
          sessionToken: null,
          blob: enemyBlobPDA,
          gameData: gameDataPDA,
          systemProgram: anchor.web3.SystemProgram.programId,
          signer: secondPlayer.publicKey
        }    
      ).signers([secondPlayer])
      .rpc();
      console.log("Spawn Enemy Blob instruction", tx);

  });

  it("Start attacking a blob!", async () => {

    console.log(`Attacking blob2 from blob 1`);

    let tx = await program.methods
    .attackBlob(gameDataSeed, 0, 0, 1, 1)
    .accounts(
      {
        player: playerPDA,
        sessionToken: null,
        attackingBlob: attackingBlobPDA,
        defendingBlob: defendingBlobPDA,
        gameData: gameDataPDA,
        signer: payer.publicKey
      }    
    )
    .rpc();

    console.log("Attacking blob instruction", tx);
    await anchor.getProvider().connection.confirmTransaction(tx, "confirmed");

    const attackerAccountInfo = await anchor.getProvider().connection.getAccountInfo(
      attackingBlobPDA, "confirmed"
    );

    const attackerDecoded = program.coder.accounts.decode("BlobData", attackerAccountInfo.data);
    console.log("Attacker ", JSON.stringify(attackerDecoded));

    const defenderAccountInfo = await anchor.getProvider().connection.getAccountInfo(
      defendingBlobPDA, "confirmed"
    );

    const defenderDecoded = program.coder.accounts.decode("BlobData", defenderAccountInfo.data);
    console.log("Defender ", JSON.stringify(defenderDecoded));

    const accountInfoGameData = await anchor.getProvider().connection.getAccountInfo(
      gameDataPDA, "confirmed"
    );

    const decodedGameData = program.coder.accounts.decode("GameData", accountInfoGameData.data);
    console.log("Game Data info", JSON.stringify(decodedGameData));
  });

  it("End attack a blob!", async () => {

    console.log(`Finish attack blob2 from blob 1`);
    await sleep(sleepTimeBetweenAttacks);

    let tx = await program.methods
    .finishAttackBlob(gameDataSeed, 0, 0, 1, 1)
    .accounts(
      {
        player: playerPDA,
        sessionToken: null,
        attackingBlob: attackingBlobPDA,
        defendingBlob: defendingBlobPDA,
        gameData: gameDataPDA,
        signer: payer.publicKey
      }    
    )
    .rpc({skipPreflight: true});

    console.log("Finish Attacking blob instruction", tx);
    await anchor.getProvider().connection.confirmTransaction(tx, "confirmed");

    const attackerAccountInfo = await anchor.getProvider().connection.getAccountInfo(
      attackingBlobPDA, "confirmed"
    );
  
    const attackerDecoded = program.coder.accounts.decode("BlobData", attackerAccountInfo.data);
    console.log("Attacker ", JSON.stringify(attackerDecoded));
  
    const defenderAccountInfo = await anchor.getProvider().connection.getAccountInfo(
      defendingBlobPDA, "confirmed"
    );
  
    const defenderDecoded = program.coder.accounts.decode("BlobData", defenderAccountInfo.data);
    console.log("Defender ", JSON.stringify(defenderDecoded));
  
    const accountInfoGameData = await anchor.getProvider().connection.getAccountInfo(
      gameDataPDA, "confirmed"
    );

    assert.equal(55, new BN(attackerDecoded.colorCurrent).toNumber());

    const decodedGameData = program.coder.accounts.decode("GameData", accountInfoGameData.data);
    console.log("Game Data info", JSON.stringify(decodedGameData));
  });

  it("Multiple attacks conquer a blob!", async () => {

    console.log(`Finish attack blob2 from blob 1`);

    for (let i = 0; i < 3; i++) {

        let attachIx = await program.methods
        .attackBlob(gameDataSeed, 0, 0, 1, 1)
        .accounts(
          {
            player: playerPDA,
            sessionToken: null,
            attackingBlob: attackingBlobPDA,
            defendingBlob: defendingBlobPDA,
            gameData: gameDataPDA,
            signer: payer.publicKey
          }    
        )
        .rpc();

        await sleep(sleepTimeBetweenAttacks);

        let defendIx = await program.methods
        .finishAttackBlob(gameDataSeed, 0, 0, 1, 1)
        .accounts(
          {
            player: playerPDA,
            sessionToken: null,
            attackingBlob: attackingBlobPDA,
            defendingBlob: defendingBlobPDA,
            gameData: gameDataPDA,
            signer: payer.publicKey
          }    
        )
        .rpc();

        console.log("Defend blob instruction", defendIx);
        await anchor.getProvider().connection.confirmTransaction(defendIx, "confirmed");
    }

    const accountInfoGameData = await PrintAttackerAndDefender(attackingBlobPDA, program, defendingBlobPDA, gameDataPDA);

    const decodedGameData = program.coder.accounts.decode("GameData", accountInfoGameData.data);
    console.log("Game Data info", JSON.stringify(decodedGameData));
  });

});

function sleep(milliseconds) {
  return new Promise(resolve => setTimeout(resolve, milliseconds));
}

async function PrintAttackerAndDefender(attackingBlobPDA: anchor.web3.PublicKey, program: anchor.Program<Blobs>, defendingBlobPDA: anchor.web3.PublicKey, gameDataPDA: anchor.web3.PublicKey) {
  const attackerAccountInfo = await anchor.getProvider().connection.getAccountInfo(
    attackingBlobPDA, "confirmed"
  );

  const attackerDecoded = program.coder.accounts.decode("BlobData", attackerAccountInfo.data);
  console.log("Attacker ", JSON.stringify(attackerDecoded));

  const defenderAccountInfo = await anchor.getProvider().connection.getAccountInfo(
    defendingBlobPDA, "confirmed"
  );

  const defenderDecoded = program.coder.accounts.decode("BlobData", defenderAccountInfo.data);
  console.log("Defender ", JSON.stringify(defenderDecoded));

  const accountInfoGameData = await anchor.getProvider().connection.getAccountInfo(
    gameDataPDA, "confirmed"
  );
  return accountInfoGameData;
}

function colorToU64BN(red, green, blue, alpha) {
  // Ensure input values are within the 0 - 65535 range
  red = Math.max(0, Math.min(65535, red));
  green = Math.max(0, Math.min(65535, green));
  blue = Math.max(0, Math.min(65535, blue));
  alpha = Math.max(0, Math.min(65535, alpha));

  // Create BN instances for each color component
  const redBN = new BN(red);
  const greenBN = new BN(green);
  const blueBN = new BN(blue);
  const alphaBN = new BN(alpha);

  // Calculate the shift amounts by multiplying with 2 to the power of respective shifts
  const redShifted = redBN.mul(new BN(2).pow(new BN(48)));
  const greenShifted = greenBN.mul(new BN(2).pow(new BN(32)));
  const blueShifted = blueBN.mul(new BN(2).pow(new BN(16)));

  // Combine the shifted values and alpha by adding them
  const colorValue = redShifted.add(greenShifted).add(blueShifted).add(alphaBN);

  return colorValue; // Return the BN object representing the u64 color value
}