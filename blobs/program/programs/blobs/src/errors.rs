use anchor_lang::error_code;

#[error_code]
pub enum GameErrorCode {
    #[msg("Not enough energy")]
    NotEnoughEnergy,
    #[msg("Wrong Authority")]
    WrongAuthority,
    #[msg("Already attacking")]
    AlreadyAttacking,
    #[msg("Not attacking")]
    NotAttacking,
    #[msg("Not finished")]
    NotFinished,
    #[msg("TooManyBlobs")]
    TooManyBlobs,
    #[msg("NotAuthorized")]
    NotAuthorized,
}
