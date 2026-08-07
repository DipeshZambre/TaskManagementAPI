using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using System.Text;
using TaskManagementAPI.Data;
using TaskManagementAPI.DTOs;
using TaskManagementAPI.Models;
using System.IdentityModel.Tokens.Jwt;
using TaskManagementAPI.Constants;


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

        public async Task<LoginResponseDto> LoginAsync(LoginDto loginDto)
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
                    CreatedAt = user.CreatedAt,
                    Role = user.Role.Name
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
    }
}
