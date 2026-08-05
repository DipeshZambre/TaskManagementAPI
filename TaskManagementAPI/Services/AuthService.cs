using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using System.Text;
using TaskManagementAPI.Data;
using TaskManagementAPI.DTOs;
using TaskManagementAPI.Models;
using System.IdentityModel.Tokens.Jwt;


namespace TaskManagementAPI.Services
{
    public class AuthService : IAuthService
    {

        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;

        public AuthService(ApplicationDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        public async Task<UserResponseDto> RegisterAsync(RegisterDto registerDto)
        {
            var existimgUser = await _context.Users
                .FirstOrDefaultAsync(u => u.Email == registerDto.Email || u.Username == registerDto.Username);


            if(existimgUser != null)
            {
                throw new InvalidOperationException("User with this email or username already exists");
            }

            // Hash the password
            var passwordHash = BCrypt.Net.BCrypt.HashPassword(registerDto.Password);

            var user = new User
            {
                Username = registerDto.Username,
                Email = registerDto.Email,
                PasswordHash = passwordHash,
                CreatedAt = DateTime.UtcNow

            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            return new UserResponseDto
            {
                Id = user.Id,
                Username = user.Username,
                Email = user.Email,
                CreatedAt = user.CreatedAt
            };
        }

        public async Task<LoginResponseDto> LoginAsync(LoginDto loginDto)
        {
            // Find user by email

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == loginDto.Email);

            if(user == null)
            {
                throw new UnauthorizedAccessException("Invalid email or password");
            }

            bool isPasswordValid = BCrypt.Net.BCrypt.Verify(loginDto.Password, user.PasswordHash);

            if (!isPasswordValid)
            {
                throw new UnauthorizedAccessException("Invalid email or password");
            }

            // Generate JWT token

            var token = GenerateJwtToken(user);

            var expiresAt = DateTime.UtcNow.AddMinutes(double.Parse(_configuration["JwtSettings:ExpiryMinutes"]!));

            return new LoginResponseDto
            {
                Token = token,

                User = new UserResponseDto
                {
                    Id = user.Id,
                    Username = user.Username,
                    Email = user.Email,
                    CreatedAt = user.CreatedAt
                },

                ExpiresAt = expiresAt
            };

        }

        private string GenerateJwtToken(User user)
        {
            // Get JWT settings from configuration
            var secretKey = _configuration["JwtSettings:SecretKey"];
            var issuer = _configuration["JwtSettings:Issuer"];
            var audience = _configuration["JwtSettings:Audience"];
            var expiryMinutes = double.Parse(_configuration["JwtSettings:ExpiryMinutes"]!);


            // Create security key

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey!));
            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            // Define claims (user information to include in token)
            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Email, user.Email),
                new Claim(JwtRegisteredClaimNames.UniqueName, user.Username),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };

            // Create the token

            var token = new JwtSecurityToken(
                issuer: issuer,
                audience: audience,
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(expiryMinutes),
                signingCredentials: credentials
            );

            // Return the serialized token
            return new JwtSecurityTokenHandler().WriteToken(token);
        }

    }
}
