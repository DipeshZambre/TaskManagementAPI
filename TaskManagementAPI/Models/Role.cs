namespace TaskManagementAPI.Models
{
    public class Role
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty; // Admin, Manager, User

        public string Description {  get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation property
        // Ek role kai saare Users ko assign kiya ja sakta he 
        public ICollection<User> Users { get; set; } = new List<User>();
    }
}
