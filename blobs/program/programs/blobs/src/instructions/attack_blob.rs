pub use crate::errors::GameErrorCode;
pub use crate::state::game_data::GameData;
use crate::state::{blob_data::BlobData, player_data::PlayerData};
use anchor_lang::prelude::*;
use session_keys::{Session, SessionToken};

fn euclidean_distance_squared((x1, y1): (u64, u64), (x2, y2): (u64, u64)) -> u64 {
    let dx = x2 as i64 - x1 as i64; // Convert to i64 to handle potential negative differences
    let dy = y2 as i64 - y1 as i64;
    (dx.pow(2) + dy.pow(2)) as u64 // Convert the result back to u64
}

pub fn start_attack_blob(mut ctx: Context<AttackBlob>) -> Result<()> {
    let accounts: &mut &mut AttackBlob<'_> = &mut ctx.accounts;

    if accounts.attacking_blob.attack_target != Pubkey::default() {
        return Err(GameErrorCode::AlreadyAttacking.into());
    }

    if accounts.player.authority.key() != accounts.attacking_blob.authority.unwrap() {
        return Err(GameErrorCode::NotAuthorized.into());
    }

    accounts.defending_blob.update()?;
    accounts.attacking_blob.update()?;

    accounts.attacking_blob.attack_start_time = Clock::get()?.unix_timestamp;
    let distance = euclidean_distance_squared(
        (
            accounts.attacking_blob.x as u64,
            accounts.attacking_blob.y as u64,
        ),
        (
            accounts.defending_blob.x as u64,
            accounts.defending_blob.y as u64,
        ),
    );

    msg!("Distance between attacker and defender: {}", distance);

    accounts.attacking_blob.attack_duration = distance * 5;
    accounts.attacking_blob.attack_target = accounts.defending_blob.key();

    accounts
        .defending_blob
        .attackers
        .push(accounts.attacking_blob.key());

    accounts.attacking_blob.attack_power = accounts.attacking_blob.color_current / 2;
    accounts.attacking_blob.color_current -= accounts.attacking_blob.attack_power;

    msg!(
        "Attack started from {}/{} to {}/{}.",
        ctx.accounts.defending_blob.y,
        ctx.accounts.attacking_blob.x,
        ctx.accounts.defending_blob.x,
        ctx.accounts.defending_blob.y,
    );
    Ok(())
}

pub fn finish_attack_blob(mut ctx: Context<AttackBlob>) -> Result<()> {
    let accounts: &mut &mut AttackBlob<'_> = &mut ctx.accounts;

    if accounts.attacking_blob.attack_target != accounts.defending_blob.key() {
        return Err(GameErrorCode::NotAttacking.into());
    }

    accounts.defending_blob.update()?;
    accounts.attacking_blob.update()?;

    let current_time = Clock::get()?.unix_timestamp;
    let time_passed = current_time - accounts.attacking_blob.attack_start_time;

    if (time_passed as u64) < accounts.attacking_blob.attack_duration {
        return Err(GameErrorCode::NotFinished.into());
    }

    // If same color just sum up the attacks
    if accounts.attacking_blob.color_value == accounts.defending_blob.color_value {
        accounts.defending_blob.color_current += accounts.attacking_blob.color_current;
        if accounts.defending_blob.color_current > accounts.defending_blob.color_max {
            accounts.defending_blob.color_current = accounts.defending_blob.color_max;
        }
    } else {
        let defender_color_value_before_attack: u64 = accounts.defending_blob.color_current;

        if let Some(new_value) = accounts
            .defending_blob
            .color_current
            .checked_sub(accounts.attacking_blob.attack_power)
        {
            accounts.defending_blob.color_current = new_value;
        } else {
            // Handle underflow case here, e.g., set to 0
            accounts.defending_blob.color_current = 0;
        }

        if accounts.defending_blob.color_current == 0 {
            accounts.defending_blob.authority = accounts.attacking_blob.authority;
            accounts.defending_blob.color_current =
                accounts.attacking_blob.attack_power - defender_color_value_before_attack;
            accounts.defending_blob.color_value = accounts.attacking_blob.color_value;
        }
    }

    accounts.attacking_blob.attack_target = Pubkey::default();
    accounts.attacking_blob.attack_power = 0;
    accounts.attacking_blob.attack_duration = 0;
    accounts.attacking_blob.attack_start_time = 0;

    accounts
        .attacking_blob
        .attackers
        .retain(|&attacker| attacker != accounts.defending_blob.key());

    msg!(
        "Attack finished from {}/{} to {}/{}.",
        ctx.accounts.defending_blob.y,
        ctx.accounts.attacking_blob.x,
        ctx.accounts.defending_blob.x,
        ctx.accounts.defending_blob.y,
    );

    msg!(
        "Defending blob {} color.",
        ctx.accounts.defending_blob.color_current,
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
        authority = player.authority.key()
    )]
    // Session Tokens are passed as optional accounts
    pub session_token: Option<Account<'info, SessionToken>>,

    #[account(
        mut,
        seeds = [level_seed.as_ref(), attacking_blob_x.to_le_bytes().as_ref(), attacking_blob_y.to_le_bytes().as_ref()],
        bump,
    )]
    pub attacking_blob: Account<'info, BlobData>,

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
        seeds = [b"player".as_ref(), player.authority.key().as_ref()],
        bump,
    )]
    pub player: Account<'info, PlayerData>,

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
