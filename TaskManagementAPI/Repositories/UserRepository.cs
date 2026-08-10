using Microsoft.EntityFrameworkCore;
using TaskManagementAPI.Data;
using TaskManagementAPI.Models;

namespace TaskManagementAPI.Repositories
{
    public class UserRepository : Repository<User>, IUserRepository
    {
        // UserRepository ka constructor banaya agar UserRepository ka inheritence samze toh , UserRepository sabse pehle Generic Reository (Repository<User>) ko Inherit karti he aur IUserRepository ko implement kar rahi he, toh UserRepository ki base class Repository he 
        // toh ApplicationDbContext inject kiya (context) aur context base class(Repository) me pass kar diya toh repository me bhi ApplicationDbContext inject kiya he toh constructor me jo context pass hoga voh User ka context hoga (protected readonly ApplicationDbContext _context;) yahi change hota rahega ex. TaskItem ka context pass hoga , RefreshTokwn ka context pass hoga etc...
        public UserRepository(ApplicationDbContext context) : base(context)
        {

        }

        // USER-SPECIFIC IMPLEMENTATIONS
        // These use base class methods as building blocks


        ///<summary>
        /// Gets user by email with Role loaded.
        /// Role is always needed in AuthService for JWT token generation.
        /// Uses base.FirstOrDefaultAsync() with case-sensitive comparison.
        /// </summary>

        public async Task<User?> GetByEmailAsync(string email)
        {
            // Include role because AuthService needs user.Role.name for JWT claims
            return await _dbSet
                .Include(u => u.Role)
                .FirstOrDefaultAsync(u => u.Email.ToLower() == email.ToLower());
        }


        ///<summary>
        /// Checks email uniquness during registration.
        /// More efficient than GetByEmail - stops as soon as match is found.
        /// Uses base.AnyAsync() as building block.
        /// </summary>
        /// 

        public async Task<bool> EmailExistsAsync(string email)
        {
            // Uses base generic AnuAsync - no need to rewrite the DbSet logic.
            return await AnyAsync(u => u.Email.ToLower() == email.ToLower());
        }



        ///<summary>
        /// Checks username uniquness during registration.
        /// More efficient than GetByUsername - stops as soon as match is found.
        /// Uses base.AnyAsync() as building block.
        /// </summary>
        /// 
        public async Task<bool> UsernameExistsAsync(string username)
        {
            return await AnyAsync(u => u.Username.ToLower() == username.ToLower());
        }



        ///<summary>
        /// Gets user with Role loaded after registration.
        /// Role is needed to include in the registration response.
        /// Uses base.FirstOrDefaultAsync() with Role included.
        /// </summary>
        public async Task<User?> GetUserWithRoleAsync(int userId)
        {
            return await _dbSet
                .Include(u => u.Role)
                .FirstOrDefaultAsync(u => u.Id == userId);
        }




        ///<summary>
        /// Gets user with both Role AND RefreshTokens loaded.
        /// Needed for token refresh and revocation operatios.
        /// Cannot use base.FirstOrDefaultAsync() because we need multiple Includes.
        /// </summary>
        public async Task<User?> GetUserWithRefreshTokensAsync(string refreshToken)
        {
            // Must write custom query here because we need TWO levels of eager loading:
            // 1. Include Role    -> needed for jWT generation when refreshing
            // 2. Include RefreshTokens    -> needed to find and validate the specific token

            return await _dbSet
                .Include(u => u.Role)
                .Include(u => u.RefreshTokens)
                .FirstOrDefaultAsync(u => u.RefreshTokens
                .Any(rt => rt.Token == refreshToken));

        }

    }
}
