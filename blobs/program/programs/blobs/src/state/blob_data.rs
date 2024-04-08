use crate::constants::*;
use anchor_lang::prelude::*;

#[account]
pub struct BlobData {
    pub authority: Option<Pubkey>,
    pub x: u8,
    pub y: u8,
    pub level: u8,
    pub color_value: u64,
    pub color_current: u64,
    pub color_max: u64,
    pub last_login: i64,
    pub last_id: u16,
    pub attack_start_time: i64,
    pub attack_duration: u64,
    pub attack_power: u64,
    pub attack_target: Pubkey,
    pub attackers: Vec<Pubkey>,
}

impl BlobData {
    pub fn print(&mut self) -> Result<()> {
        // Note that logging costs a lot of compute. So don't use it too much.
        msg!(
            "Pos: {}/{} Color: {}/{}",
            self.x,
            self.y,
            self.color_current,
            self.color_max
        );
        Ok(())
    }

    pub fn update(&mut self) -> Result<()> {
        // Get the current timestamp
        let current_timestamp = Clock::get()?.unix_timestamp;

        // Calculate the time passed since the last login
        let mut time_passed: i64 = current_timestamp - self.last_login;

        // Calculate the time spent refilling energy
        let mut time_spent = 0;

        let mut time_to_gain_color = TIME_TO_GAIN_ONE_COLOR;
        // Not owned blobs refill color at half the speed
        if self.authority.is_none() {
            time_to_gain_color = TIME_TO_GAIN_ONE_COLOR * 2;
        }

        while time_passed >= time_to_gain_color && self.color_current < MAX_COLOR {
            self.color_current += 1;
            time_passed -= time_to_gain_color;
            time_spent += time_to_gain_color;
        }

        if self.color_current >= MAX_COLOR {
            self.last_login = current_timestamp;
        } else {
            self.last_login += time_spent;
        }

        Ok(())
    }

    pub fn attack_blob(&mut self, mut defender_blob: BlobData) -> Result<()> {
        defender_blob.color_current -= self.color_current;
        Ok(())
    }
}
