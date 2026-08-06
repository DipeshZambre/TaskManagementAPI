using Microsoft.EntityFrameworkCore;
using TaskManagementAPI.Models;

namespace TaskManagementAPI.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
        {
            
        }

        public DbSet<User> Users { get; set; }

        public DbSet<TaskItem> Tasks { get; set; }

        public DbSet<Role> Roles { get; set; } // New DbSet

        // OnModel class is available in DbContext class we just overriding it
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configure the relationship between User and Task
            modelBuilder.Entity<User>()
                .HasMany(u => u.Tasks)
                .WithOne(t => t.User)
                .HasForeignKey(t => t.UserId)
                .OnDelete(DeleteBehavior.Cascade); // Optional: specify delete behavior


            modelBuilder.Entity<Role>()
                .HasMany(r => r.Users)
                .WithOne(u => u.Role)
                .HasForeignKey(u => u.RoleId)
                .OnDelete(DeleteBehavior.Restrict); // Don't allow deleting role if user exist



            // Configure indexes for better performance
            modelBuilder.Entity<User>()
                .HasIndex(u => u.Email)
                .IsUnique(); // Ensure email is unique

            modelBuilder.Entity<User>()
                .HasIndex(u => u.Username)
                .IsUnique(); // Ensure username is unique


            modelBuilder.Entity<Role>()
                .HasIndex(r => r.Name)
                .IsUnique(); // Role names must be unique


            modelBuilder.Entity<Role>().HasData(
              new Role
              {
                  Id = 1,
                  Name = "Admin",
                  Description = "Full System access - can manage users and all tasks",
                  CreatedAt = new DateTime(2026, 8, 6, 0, 0, 0, DateTimeKind.Utc)
              },
              new Role
              {
                  Id = 2,
                  Name = "Manager",
                  Description = "Can View all tasks but only modify own tasks",
                  CreatedAt = new DateTime(2026, 8, 6, 0, 0, 0, DateTimeKind.Utc)
              },
              new Role
              {
                  Id = 3,
                  Name = "User",
                  Description = "Cam only manage own tasks",
                  CreatedAt = new DateTime(2026, 8, 6, 0, 0, 0, DateTimeKind.Utc)
              }
              

              );

        }

    }
}
