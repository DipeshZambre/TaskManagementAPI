using TaskManagementAPI.Models;

namespace TaskManagementAPI.Repositories
{
    /// <summary>
    /// Task-specific repository.
    /// Inherits from IRepository<TaskItem> Which provides
    ///     - GetByIdAsync()    -> used in taskService for admin operations
    ///     - AddAsync()        -> used in TaskService.CreateTaskAsync()
    ///     - Update()          -> used in TaskService.UpdateTaskAsync()
    ///     - Remove()          -> used in TaskService.DeleteTaskAsync()
    ///     - SaveChangesAsync()-> used in TaskService after every modification
    /// This interface adds task-specific queries on top.
    /// </summary>
    public interface ITaskRepository : IRepository<TaskItem>
    {
        // TASK-SPECIFIC QUERIES
        // These are additional to what IRepository<TaskItem> provides

        ///<summary>
        /// Gets all tasks for a specific user (newest first)
        /// Used in: TaskService.GetUserTasksAsync()    -> regular user task list
        /// Internally uses: custom query with Where + OrderBy
        /// </summary>
        Task<IEnumerable<TaskItem>> GetUserTasksAsync(int userId);




        ///<summary>
        /// Gets All tasks in the system (admin/manager access)
        /// Used in: TaskService.GetAllTasksAsync() + admin/manager task list
        /// Internally uses: custom query with OrderBy only (no user filter
        /// </summary>
        Task<IEnumerable<TaskItem>> GetAllTasksAsync();



        ///<summary>
        /// Gets a specific task ONLY if it belongs to the user.
        /// Used in: TaskService for get/update/delete/toogle operations
        /// Security: enforces data isolation at repository level
        /// Internally uses: custom query with both taskId AND userId filter
        /// </summary>
        Task<TaskItem?> GetUserTaskByIdAsync(int taskId, int userId);




        ///<summary>
        /// gets total task count for user.
        /// Used in: AdminController.GetUserById() to show task stats
        /// Internally uses: base.CountAsync()
        /// </summary>
        Task<int> GetUserTaskCountAsync(int userId);
    }
}
