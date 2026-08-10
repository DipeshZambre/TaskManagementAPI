using Microsoft.AspNetCore.Mvc.TagHelpers.Cache;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;
using TaskManagementAPI.Data;

namespace TaskManagementAPI.Repositories
{
    /// <summary>
    /// Generic repository implementation.
    /// ALl specific repositories (UserRepository, TaskRepository, RefreshTokenRepository)
    /// inherit from this class and get these methods for free.
    /// Specific repositories only add entity-specific queries on top.
    /// </summary>


    public class Repository<T> : IRepository<T> where T : class
    {
        protected readonly ApplicationDbContext _context;
        protected readonly DbSet<T> _dbSet;

        public Repository(ApplicationDbContext context)
        {
            _context = context;
            _dbSet  = context.Set<T>();
        }

        // QUERY IMPLEMENTATION

        // Used directly in TaskService for admin operations
         
        public virtual async Task<T?> GetByIdAsync(int id)
        {
            return await _dbSet.FindAsync(id);
        }

        // Used as building block inside specific repositories
        public virtual async Task<T?> FirstOrDefaultAsync(Expression<Func<T, bool>> predicate)
        {
            return await _dbSet.FirstOrDefaultAsync(predicate);
        }


        // Used as building block inside specific repositories
        public virtual async Task<bool> AnyAsync(Expression<Func<T, bool>> predicate)
        {
            return await _dbSet.AnyAsync(predicate);
        }


        // Used as building block inside reposirories
        public virtual async Task<int> CountAsync(Expression<Func<T, bool>> predicate)
        {
            return await _dbSet.CountAsync(predicate);
        }






        // COMMAND IMPLEMENTATION

        // Used directly in AuthService and TaskService
        public virtual async Task<T> AddAsync(T entity)
        {
            await _dbSet.AddAsync(entity);
            return entity;
        }


        // Used directly in AuthService and TaskService
        public virtual void Update(T entity)
        {
            _dbSet.Update(entity);
        }

        // Used directly in TaskService
        public virtual void Remove(T entity)
        {
            _dbSet.Remove(entity);
        }

        public virtual void RemoveRange(IEnumerable<T> entities)
        {
            _dbSet.RemoveRange(entities);
        }






        //PERSISTENCE

        // Used in every service after data modification modifications
        public virtual async Task<int> SaveChangesAsync()
        {
            return await _context.SaveChangesAsync();
        }


    }
}
