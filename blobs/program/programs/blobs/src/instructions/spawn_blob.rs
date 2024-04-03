pub use crate::errors::GameErrorCode;
use crate::state::blob_data::BlobData;
pub use crate::state::game_data::GameData;
use anchor_lang::prelude::*;
use session_keys::{Session, SessionToken};

pub fn spawn_blob(mut ctx: Context<SpawnBlob>, x: u8, y: u8) -> Result<()> {
    let accounts: &mut &mut SpawnBlob<'_> = &mut ctx.accounts;

    // TODO: save new blobs (stream?)
    //account
    //    .game_data
    //    .on_new_blob_spanwed(ctx.accounts.blob)?;

    accounts
        .game_data
        .on_new_blob_spanwed_pubkey(accounts.blob.key())?;

    /*accounts
    .game_data
    .on_new_blob_spanwed(BlobData{
        x: accounts.blob.x,
        y: accounts.blob.y,
        level: accounts.blob.level,
        last_login: accounts.blob.last_login,

        accounts.blob.last_login,
        accounts.blob.authority,
        accounts.blob.color,
        accounts.blob.color_max
    })?;*/

    // Probably check if there is a blob already at this location
    // If the player doesnt have a blob yet make it home planet

    //account.blob.last_id = counter; // probably not needed
    accounts.blob.x = x;
    accounts.blob.y = y;
    accounts.blob.level = 1;
    accounts.blob.last_login = Clock::get()?.unix_timestamp;
    accounts.blob.authority = accounts.signer.key();
    accounts.blob.color = 0;
    accounts.blob.color_max = 100;

    accounts.blob.update()?;
    accounts.blob.print()?;

    // account.blob.chop_tree(amount)?;

    msg!(
        "New blob spawned at {}/{} with color {}/{}.",
        ctx.accounts.blob.x,
        ctx.accounts.blob.y,
        ctx.accounts.blob.color,
        ctx.accounts.blob.color_max
    );
    Ok(())
}

#[derive(Accounts, Session)]
#[instruction(level_seed: String, x: u8, y: u8)]
pub struct SpawnBlob<'info> {
    #[session(
        // The ephemeral key pair signing the transaction
        signer = signer,
        // The authority of the user account which must have created the session
        authority = blob.authority.key()
    )]
    // Session Tokens are passed as optional accounts
    pub session_token: Option<Account<'info, SessionToken>>,

    // There is one PlayerData account
    #[account(
        init,
        seeds = [level_seed.as_ref(), x.to_le_bytes().as_ref(), y.to_le_bytes().as_ref()],
        bump,
        space = 1000,
        payer = signer,
    )]
    pub blob: Account<'info, BlobData>,

    // There can be multiple levels the seed for the level is passed in the instruction
    // First player starting a new level will pay for the account in the current setup
    #[account(
        init_if_needed,
        payer = signer,
        space = 1000,
        seeds = [level_seed.as_ref()],
        bump,
    )]
    pub game_data: Account<'info, GameData>,

    #[account(mut)]
    pub signer: Signer<'info>,
    pub system_program: Program<'info, System>,
}
