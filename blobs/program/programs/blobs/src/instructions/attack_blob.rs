pub use crate::errors::GameErrorCode;
use crate::state::blob_data::BlobData;
pub use crate::state::game_data::GameData;
use anchor_lang::prelude::*;
use session_keys::{Session, SessionToken};

pub fn start_attack_blob(mut ctx: Context<AttackBlob>, x: u8, y: u8) -> Result<()> {
    let accounts: &mut &mut AttackBlob<'_> = &mut ctx.accounts;

    if accounts.attacking_blob.attack_target != Pubkey::default() {
        return Err(GameErrorCode::AlreadyAttacking.into());
    }

    accounts.defending_blob.update()?;
    accounts.attacking_blob.update()?;

    accounts.attacking_blob.attack_start_time = Clock::get()?.unix_timestamp;
    accounts.attacking_blob.attack_duration = 1000;
    accounts.attacking_blob.attack_target = accounts.defending_blob.key();
    accounts
        .defending_blob
        .attackers
        .push(accounts.attacking_blob.key());
    accounts.attacking_blob.attack_power = accounts.attacking_blob.color / 2;
    accounts.attacking_blob.color -= accounts.attacking_blob.attack_power;

    msg!(
        "Attack started from {}/{} to {}/{}.",
        ctx.accounts.defending_blob.y,
        ctx.accounts.attacking_blob.x,
        ctx.accounts.defending_blob.x,
        ctx.accounts.defending_blob.y,
    );
    Ok(())
}

pub fn finish_attack_blob(mut ctx: Context<AttackBlob>, x: u8, y: u8) -> Result<()> {
    let accounts: &mut &mut AttackBlob<'_> = &mut ctx.accounts;

    msg!(
        "Attack started from {}/{} to {}/{}.",
        ctx.accounts.defending_blob.y,
        ctx.accounts.attacking_blob.x,
        ctx.accounts.defending_blob.x,
        ctx.accounts.defending_blob.y,
    );
    Ok(())
}

#[derive(Accounts, Session)]
#[instruction(level_seed: String, attacking_blob_x: u8, attacking_blob_y: u8, defending_blob_x: u8, defending_blob_y: u8)]
pub struct AttackBlob<'info> {
    #[session(
        // The ephemeral key pair signing the transaction
        signer = signer,
        // The authority of the user account which must have created the session
        authority = attacking_blob.authority.key()
    )]
    // Session Tokens are passed as optional accounts
    pub session_token: Option<Account<'info, SessionToken>>,

    // There is one PlayerData account
    #[account(
        mut,
        seeds = [level_seed.as_ref(), attacking_blob_x.to_le_bytes().as_ref(), attacking_blob_y.to_le_bytes().as_ref()],
        bump,
    )]
    pub attacking_blob: Account<'info, BlobData>,

    // There is one PlayerData account
    #[account(
        mut,
        seeds = [level_seed.as_ref(), defending_blob_x.to_le_bytes().as_ref(), defending_blob_y.to_le_bytes().as_ref()],
        bump,
    )]
    pub defending_blob: Account<'info, BlobData>,

    // There can be multiple levels the seed for the level is passed in the instruction
    // First player starting a new level will pay for the account in the current setup
    #[account(
        mut,
        seeds = [level_seed.as_ref()],
        bump,
    )]
    pub game_data: Account<'info, GameData>,

    #[account(mut)]
    pub signer: Signer<'info>,
}
