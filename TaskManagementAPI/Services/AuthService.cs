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
using TaskManagementAPI.Repositories;
using Microsoft.EntityFrameworkCore.Metadata.Internal;


namespace TaskManagementAPI.Services
{
    public class AuthService : IAuthService
    {
        // Repository replaces direct DbContext usage
        // UserRepository   -> all user-related DB operations
        // RefreshTokenRepository   -> all token-related DB operations
        // _context kept only for role lookup (no RoleRepository in this implementation)

        private readonly IUserRepository _userRepository;
        private readonly IRefreshTokenRepository _refreshTokenRepository;
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;

        public AuthService(
            IUserRepository userRepository,
            IRefreshTokenRepository refreshTokenRepository,
            ApplicationDbContext context,
            IConfiguration configuration)
        {
            _userRepository = userRepository;
            _refreshTokenRepository = refreshTokenRepository;
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

            /* 
             * var user = await _context.Users
                .Include(u => u.Role) // Eager load the role
                .FirstOrDefaultAsync(u => u.Email == loginDto.Email); */

            // Find user by email
            // UserRepository.GetByEmailAsync() with Role eagerly loaded : Uses base.FirstOrDefaultAsync() with Role eagerly loaded
            var user = await _userRepository.GetByEmailAsync(loginDto.Email);


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


            // _context.RefreshTokens.Add(refreshToken);

            // Uses base generic AddAsync() from Repository<RefreshToken>
            await _refreshTokenRepository.AddAsync(refreshToken);

            // await RemoveOldRefreshTokens(user.Id);

            // Remove old/inactive refresh tokens for this user (optional cleanup)
            // RefreshTokenRepository.RemoveOldTokensAsync() : Uses base.RemoveRange() internally for cleanup
            await _refreshTokenRepository.RemoveOldTokensAsync(user.Id);

            // Uses base generic SaveChangesAsync() from Repository<RefreshToken>
            //  Jo specific repository he voh  generic repository he unko inherit karti he, inheritence ka rule hota he ki jo hamare public vale method hote he voh available ho jaate he 
            await _refreshTokenRepository.SaveChangesAsync();


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
           /* var existingUser = await _context.Users
                .FirstOrDefaultAsync(u => u.Email == registerDto.Email ||
                                    u.Username == registerDto.Username); */

           // UserRepository.EmailExistAsync()
           // Uses base.AnyAsync() internally
           if(await _userRepository.EmailExistsAsync(registerDto.Email))
            {
                throw new InvalidOperationException(
                    "User with this email already exists");
            }


           /* if (existingUser != null)
            {
                throw new InvalidOperationException("User with this email or username already exists");
            }
           */

           // UserRepository.UsernameExistsAsync()
           // Uses base.AnyAsync() internally
           if(await _userRepository.UsernameExistsAsync(registerDto.Username))
            {
                throw new InvalidOperationException("User with this username already exists");
            }

            /* var role = await _context.Roles.FirstOrDefaultAsync(r => r.Name == roleName);

             if (role == null)
             {
                 // Default to User if specified role does not exist
                 role = await _context.Roles.FirstOrDefaultAsync(r => r.Name == RoleConstants.User);

                 if (role == null)
                 {
                     throw new InvalidOperationException("Default user role not found. pleaase ensure roles are seeded.");
                 }
             }
            */

            // Role lookup still uses _context directly
            // We don't have a RoleRepository - its a simple lookup
            var role = await _context.Roles
                 .FirstOrDefaultAsync(r => r.Name == roleName)
                 ?? await _context.Roles
                 .FirstOrDefaultAsync(r => r.Name == RoleConstants.User)
                 ?? throw new InvalidOperationException("Default user role not found. Please ensure roles are seeded.");

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

            // Add to database 
            // Uses base generic AddAsync() from Repository<User>
            // generic method
            await _userRepository.AddAsync(user);   

            // Uses base generic SaveChangesAsync() from Repository<User>
            // generic method
            await _userRepository.SaveChangesAsync();


            /* Now no need of this
              _context.Users.Add(user);
              await _context.SaveChangesAsync();
            */

            /* OLD
             * Load the role navigation property / Read role from database
            await _context.Entry(user).Reference(u => u.Role).LoadAsync(); */

            // Load the role navigation property
            // UserRepository.GetUserWithRoleAsync() : Uses base.FirstOrDefaultAsync() with Role included
            // GetUserWithRoleAsync is User reposiory specific method
            var userWithRole = await _userRepository.GetUserWithRoleAsync(user.Id);
            

            return new UserResponseDto
            {
                Id = userWithRole!.Id,
                Username = userWithRole.Username,
                Email = userWithRole.Email,
                CreatedAt = userWithRole.CreatedAt,
                Role = userWithRole.Role.Name
            };

          
        }

        public async Task<RefreshTokenResponseDto> RefreshTokenAsync(string token, string ipAddress)
        {
            /*
            var user = await GetUserByRefreshToken(token);

            var refreshToken = user.RefreshTokens.Single(rt => rt.Token == token);
            */

            // UserRepository.GetUserWithRefreshTokensAsync() : Custom query needs User + Role + RefreshTokens loaded
            var user = await _userRepository.GetUserWithRefreshTokensAsync(token);

            if(user == null)
            {
                throw new UnauthorizedAccessException("Invalid refresh token");
            }

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

            // Uses base generic Update() from Repository<RefreshToken>
            _refreshTokenRepository.Update(refreshToken);

            // save new refresh token
            // Uses base generic AddAsync() from Repository<RefreshToken>
            await _refreshTokenRepository.AddAsync(newRefreshToken);

            // Remove old refresh tokens
            // RefreshTokenRepository.RemoveOldTokensAsync() : Uses base.RemoveRange() internally for cleanup
            await _refreshTokenRepository.RemoveOldTokensAsync(user.Id);

            // Uses base generic SaveChangesAsync() from Repository<RefreshToken>
            await _refreshTokenRepository.SaveChangesAsync();


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

            /* var user = await GetUserByRefreshToken(token);
             var refreshToken = user.RefreshTokens.Single(rt => rt.Token == token); */

            // UserRepository.GetUserWithRefreshTokensAsync() : Custom query - needs User + RefreshTokens loaded
            var user = await _userRepository.GetUserWithRefreshTokensAsync(token);

            if(user == null)
            {
                return false;
            } 
            var refreshToken = user.RefreshTokens.Single(rt => rt.Token == token);

            if (!refreshToken.IsActive)
            {
                // if refresh token is not active return false
                return false;
            }

            // Revoke token
            refreshToken.RevokedAt = DateTime.UtcNow;
            refreshToken.RevokedByIp = ipAddress;

            // Uses base generic Update() from Repository<RefreshToken>
            _refreshTokenRepository.Update(refreshToken);

            // Uses base generic SaveChangesAsync() from Repository<RefreshToken>
            await _refreshTokenRepository.SaveChangesAsync();


            return true;
        }


        private string GenerateAccessToken(User user)
        {
            // Get JWT settings from configuration
            var secretKey = _configuration["JwtSettings:SecretKey"];
            var issuer = _configuration["JwtSettings:Issuer"];
            var audience = _configuration["JwtSettings:Audience"];
            var expiryMinutes = double.Parse(_configuration["JwtSettings:AccessTokenExpiryMinutes"]!);


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

        /*
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
        */

        private async Task RemoveOldRefreshTokens(int userId)
        {
            // 1. Fetch only the user's tokens from the database into server memory first
            var userTokens = await _context.RefreshTokens
                .Where(rt => rt.UserId == userId)
                .ToListAsync(); // 👈 This moves processing from SQL to C# memory

            // 2. Now C# can safely read your custom 'IsActive' helper property
            var tokenToRemove = userTokens
                .Where(rt => !rt.IsActive && rt.CreatedAt.AddDays(30) < DateTime.UtcNow)
                .ToList();

            if (tokenToRemove.Any())
            {
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
