using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using System.Text;
using TaskManagementAPI.Data;
using TaskManagementAPI.DTOs;
using TaskManagementAPI.Models;
using System.IdentityModel.Tokens.Jwt;
using TaskManagementAPI.Constants;
using System.Net;
using System.Security.Cryptography;


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
            // If a role is passed in the JSON body, use it. Otherwise, fall back to "User".
            string assignedRole = !string.IsNullOrWhiteSpace(registerDto.Role)
                ? registerDto.Role
                : RoleConstants.User;

            // jab normal registration vala method call hoga toh usko default role User hi jaayega agar koi specific roe dena he toh RegisterUserWithRoleAsync call karna padega
            // return await RegisterWithRoleAsync(registerDto, RoleConstants.User);
             return await RegisterWithRoleAsync(registerDto, assignedRole);

        }

        public async Task<LoginResponseDto> LoginAsync(LoginDto loginDto, string ipAddress)
        {
            // Find user by email

            var user = await _context.Users
                .Include(u => u.Role) // Eager load the role
                .FirstOrDefaultAsync(u => u.Email == loginDto.Email);

            if (user == null)
            {
                throw new UnauthorizedAccessException("Invalid email or password");
            }

            bool isPasswordValid = BCrypt.Net.BCrypt.Verify(loginDto.Password, user.PasswordHash);

            if (!isPasswordValid)
            {
                throw new UnauthorizedAccessException("Invalid email or password");
            }

            // Generate JWT token
            /*
             * OLD 
            var token = GenerateJwtToken(user
            var expiresAt = DateTime.UtcNow.AddMinutes(double.Parse(_configuration["JwtSettings:ExpiryMinutes"]!));
            */

            // Generate Tokens
            var accessToken = GenerateAccessToken(user);
            var refreshToken = GenerateRefreshToken(ipAddress);

            // Save refresh token to database
            refreshToken.UserId = user.Id;
            _context.RefreshTokens.Add(refreshToken);

            await RemoveOldRefreshTokens(user.Id);

            await _context.SaveChangesAsync();

            var accessTokenExpiry = DateTime.UtcNow.AddMinutes(
                double.Parse(_configuration["JwtSettings:AccessTokenExpiryMinutes"]!));

            return new LoginResponseDto
            {
                /* OLD
                  Token = token,

                User = new UserResponseDto
                {
                    Id = user.Id,
                    Username = user.Username,
                    Email = user.Email,
                    CreatedAt = user.CreatedAt,
                    Role = user.Role.Name
                },

                ExpiresAt = expiresAt 
                */

                AccessToken = accessToken,
                RefreshToken = refreshToken.Token,
                User = new UserResponseDto
                {
                    Id = user.Id,
                    Username = user.Username,
                    Email = user.Email,
                    CreatedAt = user.CreatedAt,
                    Role = user.Role.Name
                },

                AccessTokenExpiresAt = accessTokenExpiry,
                RefreshTokenExpiresAt = refreshToken.ExpiresAt
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
                new Claim(ClaimTypes.Role, user.Role.Name), // Add role claim
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


        public async Task<UserResponseDto> RegisterWithRoleAsync(RegisterDto registerDto, string roleName)
        {
            var existingUser = await _context.Users
                .FirstOrDefaultAsync(u => u.Email == registerDto.Email ||
                                    u.Username == registerDto.Username);

            if (existingUser != null)
            {
                throw new InvalidOperationException("User with this email or username already exists");
            }

            var role = await _context.Roles.FirstOrDefaultAsync(r => r.Name == roleName);

            if (role == null)
            {
                // Default to User if specified role does not exist
                role = await _context.Roles.FirstOrDefaultAsync(r => r.Name == RoleConstants.User);

                if (role == null)
                {
                    throw new InvalidOperationException("Default user role not found. pleaase ensure roles are seeded.");
                }
            }

            // hash pawword
            var passwordHash = BCrypt.Net.BCrypt.HashPassword(registerDto.Password);

            // create new user
            var user = new User
            {
                Username = registerDto.Username,
                Email = registerDto.Email,
                PasswordHash = passwordHash,
                RoleId = role.Id,
                CreatedAt = DateTime.UtcNow,
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();


            // Load the role navigation property / Read role from database
            await _context.Entry(user).Reference(u => u.Role).LoadAsync();

            return new UserResponseDto
            {
                Id = user.Id,
                Username = user.Username,
                Email = user.Email,
                CreatedAt = user.CreatedAt,
                Role = user.Role.Name
            };

          
        }

        public async Task<RefreshTokenResponseDto> RefreshTokenAsync(string token, string ipAddress)
        {
            var user = await GetUserByRefreshToken(token);

            var refreshToken = user.RefreshTokens.Single(rt => rt.Token == token);

            if(!refreshToken.IsActive)
            {
                throw new UnauthorizedAccessException("Invalid or expired refresh token");
            }

            // Gnerate new tokens
            var newAccessToken = GenerateAccessToken(user);
            var newRefreshToken = GenerateRefreshToken(ipAddress);   
            newRefreshToken.UserId = user.Id; 

            // Mark old refresh token as replaced
            refreshToken.RevokedAt = DateTime.UtcNow;
            refreshToken.RevokedByIp = ipAddress;
            refreshToken.ReplacedByToken = newRefreshToken.Token;

            // save new refresh token
            _context.RefreshTokens.Add(newRefreshToken);

            // Remove old refresh tokens
            await RemoveOldRefreshTokens(user.Id);

            await _context.SaveChangesAsync();

            var accessTokenExpiry = DateTime.UtcNow.AddMinutes(
                double.Parse(_configuration["JwtSettings:AccessTokenExpiryMinutes"]!));


            return new RefreshTokenResponseDto
            {
                AccessToken = newAccessToken,
                RefreshToken = newRefreshToken.Token,
                AccessTokenExpiresAt = accessTokenExpiry,
                RefreshTokenExpiresAt = newRefreshToken.ExpiresAt
            };

        }

        public async Task<bool> RevokeTokenAsync(string token, string ipAddress)
        {
            var user = await GetUserByRefreshToken(token);
            var refreshToken = user.RefreshTokens.Single(rt => rt.Token == token);

            if(!refreshToken.IsActive)
            {
                // if refresh token is not active return false
                return false;
            }

            // Revoke token
            refreshToken.RevokedAt = DateTime.UtcNow;
            refreshToken.RevokedByIp = ipAddress;

            await _context.SaveChangesAsync();

            return true;
        }


        private string GenerateAccessToken(User user)
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
                new Claim(ClaimTypes.Role, user.Role.Name), 
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


        private RefreshToken GenerateRefreshToken(string ipAddress)
        {
            var refreshTokenExpiry = double.Parse(_configuration["JwtSettings:RefreshTokenExpiryDays"]!);

            // Generate cryptographycally secure random token
            var randomBytes = new byte[64];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(randomBytes);
            var token = Convert.ToBase64String(randomBytes);

            return new RefreshToken
            {
                Token = token,
                ExpiresAt = DateTime.UtcNow.AddDays(refreshTokenExpiry),
                CreatedAt = DateTime.UtcNow,
                CreatedByIp = ipAddress
            };
        }

        private async Task RemoveOldRefreshTokens(int userId)
        {
            var tokenToRemove = await _context.RefreshTokens
                .Where(rt => rt.UserId == userId &&
                              !rt.IsActive &&
                              rt.CreatedAt.AddDays(30) < DateTime.UtcNow)
                .ToListAsync();


            if(tokenToRemove.Any())
            {
                // jitne bhi tokense honge saare ke saare range me remove ho jaayenge matlab eksaath
                _context.RefreshTokens.RemoveRange(tokenToRemove);
            }
        }

        private async Task<User> GetUserByRefreshToken(string token)
        {
            var user = await _context.Users
                .Include(u => u.Role)
                .Include(u => u.RefreshTokens)
                .SingleOrDefaultAsync(u => u.RefreshTokens.Any(rt => rt.Token == token));

            if(user == null)
            {
                throw new UnauthorizedAccessException("Invalid refresh token");
            }

            return user;
        }
    }
}
