using System.Diagnostics.Eventing.Reader;
using TaskManagementAPI.DTOs;

namespace TaskManagementAPI.Services
{
    public interface ITaskService
    {
        Task<TaskResponseDto> CreateTaskAsync(CreateTaskDto createTaskDto, int userId);

        // Fetch all tasks
        Task<List<TaskResponseDto>> GetUserTasksAsync(int userId);

        Task<TaskResponseDto?> GetTasksByIdAsync(int tasksId, int userId);

        Task<TaskResponseDto?> UpdateTaskAsync(int taskId, UpdateTaskDto updateTaskDto, int userId);

        Task<bool> DeleteTaskAsync(int taskId, int userId);

        Task<TaskResponseDto?> ToggleTaskCompletionAsync(int taskId, int userId);

        // NEw admin/manager methods

        Task<List<TaskResponseDto>> GetAllTasksAsync(); // for managers and admins

        Task<TaskResponseDto?> GetAnyTaskByIdAsync(int taskId); // get any task regardless of owner

        Task<TaskResponseDto?> UpdateAnyTaskAsync(int taskId, UpdateTaskDto updateTaskDto); // Update any task

        Task<bool> DeleteAnyTaskAsync(int taskId); // Delete any task
    }
}
