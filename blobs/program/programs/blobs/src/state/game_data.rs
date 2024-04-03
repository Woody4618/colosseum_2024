use anchor_lang::prelude::*;

use crate::constants::MAX_WOOD_PER_TREE;

use super::blob_data::BlobData;

#[account]
pub struct GameData {
    pub total_wood_collected: u64,
    pub active_blobs: Vec<Pubkey>,
    // TODO: Add game config like color fill up
}

impl GameData {
    pub fn on_tree_chopped(&mut self, amount_chopped: u64) -> Result<()> {
        match self.total_wood_collected.checked_add(amount_chopped) {
            Some(v) => {
                if self.total_wood_collected >= MAX_WOOD_PER_TREE {
                    self.total_wood_collected = 0;
                    msg!("Tree successfully chopped. New Tree coming up.");
                } else {
                    self.total_wood_collected = v;
                    msg!("Total wood chopped: {}", v);
                }
            }
            None => {
                msg!("The ever tree is completly chopped!");
            }
        };

        Ok(())
    }

    pub fn on_new_blob_spanwed(&mut self, new_blob: BlobData) -> Result<()> {
        Ok(())
    }

    pub fn on_new_blob_spanwed_pubkey(&mut self, new_blob: Pubkey) -> Result<()> {
        self.active_blobs.push(new_blob);
        Ok(())
    }
}
