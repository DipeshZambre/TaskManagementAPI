using System.ComponentModel.DataAnnotations;

namespace TaskManagementAPI.Models
{
    public class TaskItem
    {
        [Key]
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public bool IsCompleted { get; set; } = false;
        
        public int Priority { get; set; } = 0; // 0: Low, 1: Medium, 2: High

        public DateTime? DueDate { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Foreign key 
         public int UserId { get; set; }

        // Navigation property 
        public User User { get; set; } = null!;


    }
}
