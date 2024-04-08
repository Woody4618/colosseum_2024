pub use crate::errors::GameErrorCode;
pub use anchor_lang::prelude::*;
pub use session_keys::{session_auth_or, Session, SessionError};
pub mod constants;
pub mod errors;
pub mod instructions;
pub mod state;
use instructions::*;

declare_id!("9aMxRDFLQwW2e185TdpfHJWAWTGhzLQwB7SuEf58WYDX");

#[program]
pub mod blobs {

    use super::*;

    pub fn init_player(ctx: Context<InitPlayer>, _level_seed: String) -> Result<()> {
        init_player::init_player(ctx)
    }

    pub fn spawn_blobs(
        ctx: Context<SpawnBlob>,
        _level_seed: String,
        x: u8,
        y: u8,
        player_color: u64,
    ) -> Result<()> {
        spawn_blob::spawn_blob(ctx, x, y, player_color)
    }

    pub fn attack_blob(
        ctx: Context<AttackBlob>,
        _level_seed: String,
        _attacking_blob_x: u8,
        _attacking_blob_y: u8,
        _defending_blob_x: u8,
        _defending_blob_y: u8,
    ) -> Result<()> {
        attack_blob::start_attack_blob(ctx)
    }

    pub fn finish_attack_blob(
        ctx: Context<AttackBlob>,
        _level_seed: String,
        _attacking_blob_x: u8,
        _attacking_blob_y: u8,
        _defending_blob_x: u8,
        _defending_blob_y: u8,
    ) -> Result<()> {
        attack_blob::finish_attack_blob(ctx)
    }

    // This function lets the player chop a tree and get 1 wood. The session_auth_or macro
    // lets the player either use their session token or their main wallet. (The counter is only
    // there so that the player can do multiple transactions in the same block. Without it multiple transactions
    // in the same block would result in the same signature and therefore fail.)
    #[session_auth_or(
        ctx.accounts.player.authority.key() == ctx.accounts.signer.key(),
        GameErrorCode::WrongAuthority
    )]
    pub fn chop_tree(ctx: Context<ChopTree>, _level_seed: String, counter: u16) -> Result<()> {
        chop_tree::chop_tree(ctx, counter, 1)
    }
}
