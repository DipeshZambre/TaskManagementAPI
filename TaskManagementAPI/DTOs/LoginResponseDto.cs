namespace TaskManagementAPI.DTOs
{
    public class LoginResponseDto
    {
       // public string Token { get; set; } = string.Empty;

        public string AccessToken { get; set; } = string.Empty; // Renamed from Token

        public string RefreshToken {  get; set; } = string.Empty; // NEW

        public UserResponseDto User { get; set; } = null!;

       // public DateTime ExpiresAt { get; set; }

        public DateTime AccessTokenExpiresAt { get; set; } // Renamed from ExpiresAt

        public DateTime RefreshTokenExpiresAt { get; set; } // NEW

    }
}

