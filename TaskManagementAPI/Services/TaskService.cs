using Microsoft.EntityFrameworkCore;
using TaskManagementAPI.Data;
using TaskManagementAPI.DTOs;
using TaskManagementAPI.Models;
using TaskManagementAPI.Repositories;

namespace TaskManagementAPI.Services
{
    public class TaskService : ITaskService
    {
        // TaskRepository replaces all direct DbContext usage for task operations
        // aLl data access now goes through the repository

        private readonly ITaskRepository _taskRepository;
        public TaskService(ITaskRepository taskRepository)
        {
            _taskRepository = taskRepository;
        }


        public async Task<TaskResponseDto> CreateTaskAsync(CreateTaskDto createTaskDto, int userId)
        {
            var task = new TaskItem
            {
                Title = createTaskDto.Title,
                Description = createTaskDto.Description,
                Priority = createTaskDto.Priority,
                DueDate = createTaskDto.DueDate,
                IsCompleted = false,
                CreatedAt = DateTime.UtcNow,
                UserId = userId // Associated with current user
            };

            // Add to database
            // Uses base generic AddAsync() from Repository<TaskItem>
            await _taskRepository.AddAsync(task);

            // Uses base generic SaveChangesAsync() from Repository<TaskItem>
            await _taskRepository.SaveChangesAsync();

            // Return response DTO
            return MapToResponseDto(task);

        }

        public async Task<List<TaskResponseDto>> GetUserTasksAsync(int userId)
        {
            // Get all tasks for this user, ordered bt creation date (newest first)

            // TaskRepository.GetUserTasksAsync() : Custom query - needs userId filter + ordering
            var tasks = await _taskRepository.GetUserTasksAsync(userId);
            return tasks.Select(MapToResponseDto).ToList();
          
        }


        public async Task<TaskResponseDto?> GetTaskByIdAsync(int taskId, int userId)
        {
            // Find tasks by ID and ensures it belongs to the user

          /*  var task = await _context.Tasks
                .Where(t => t.Id == taskId && t.UserId == userId)
                .Select(t => new TaskResponseDto
                {
                    Id = t.Id,
                    Title = t.Title,
                    Description = t.Description,
                    IsCompleted = t.IsCompleted,
                    Priority = t.Priority,
                    DueDate = t.DueDate,
                    CreatedAt = t.CreatedAt,
                    UserId = t.UserId

                })
                .FirstOrDefaultAsync();

            return task; */

            // TaskRepository.GetUserTaskByIdAsync() : Custom query - enforces both taskId AND userId for security
            var task = await _taskRepository.GetUserTaskByIdAsync(taskId, userId);
            return task == null ? null : MapToResponseDto(task);
        }

        public async Task<TaskResponseDto?> UpdateTaskAsync(int taskId, UpdateTaskDto updateTaskDto, int userId)
        {
            // Find the task and verify ownership
            // TaskRepository.GetUserTaskByIdAsync() : Security check : ensures task belongs to requesting user
            var task = await _taskRepository.GetUserTaskByIdAsync(taskId, userId);

            if (task == null)
            {
                return null;// task not found pr user doesn't own it
            }

            ApplyUpdates(task, updateTaskDto);

            // save changes
            // Uses base generic Update() from Repository from Repository<TaskItem>
            _taskRepository.Update(task);

            // Uses base generic SaveChangesAsync() form Repository<TaskItem
            await _taskRepository.SaveChangesAsync();


           /* return new TaskResponseDto
            {
                Id = task.Id,
                Title = task.Title,
                Description = task.Description,
                IsCompleted = task.IsCompleted,
                Priority = task.Priority,
                DueDate = task.DueDate,
                CreatedAt = task.CreatedAt,
                UserId = task.UserId
            }; */

            return MapToResponseDto(task);

        }

        public async Task<bool> DeleteTaskAsync(int taskId, int userId)
        {
            // Find the task and verify ownership
            // TaskRepository.GetUserTaskByIdAsync() : Security check : ensures task belongs to requesting user
            var task = await _taskRepository.GetUserTaskByIdAsync(taskId, userId);

            if (task == null)
            {
                return false; // task not found or user does not own it
            }

            // Remove the task
            // Uses base generic Remove() from Repository<TaskItem>
            _taskRepository.Remove(task);

            // Uses base generic SaveChangesAsync() from Repository<TaskItem>
            await _taskRepository.SaveChangesAsync();

            return true;

        }

        public async Task<TaskResponseDto?> ToggleTaskCompletionAsync(int taskId, int userId)
        {
            // Find correct task with correct ownership
            /* var task = await _context.Tasks
                 .FirstOrDefaultAsync(t => t.Id == taskId && t.UserId == userId); */

            // Find the task and verify ownership
            // TaskRepository.GetUsertasksByIdAsync() : Security check : ensures task belongs to requesting user

            var task = await _taskRepository.GetUserTaskByIdAsync(taskId, userId);


            if (task == null)
            {
                return null;
            }

            // Toogle complete status
            task.IsCompleted = !task.IsCompleted;

            // Save changes
            // Uses base generic Update() from Repository<TaskItem>
            _taskRepository.Update(task);

            // Uses base generic SaveChangesAsync() from epository<taskItem>
            await _taskRepository.SaveChangesAsync();
            
            return MapToResponseDto(task);
        }

        public async Task<List<TaskResponseDto>> GetAllTasksAsync()
        {
           /* var tasks = await _context.Tasks
               .OrderByDescending(t => t.CreatedAt)
               .Select(t => new TaskResponseDto
               {
                   Id = t.Id,
                   Title = t.Title,
                   Description = t.Description,
                   IsCompleted = t.IsCompleted,
                   Priority = t.Priority,
                   DueDate = t.DueDate,
                   CreatedAt = t.CreatedAt,
                   UserId = t.UserId
               })
               .ToListAsync();

            return tasks; */

            // TaskRepository.GetAllTasksAsync() : : Custom query - no userId (adin/manager access only)
            var tasks = await _taskRepository.GetAllTasksAsync();
            return tasks.Select(MapToResponseDto).ToList();

        }

        public async Task<TaskResponseDto?> GetAnyTaskByIdAsync(int taskId)
        {
          /*  var task = await _context.Tasks
                .Where(t => t.Id == taskId)
                .Select(t => new TaskResponseDto
                {
                    Id = t.Id,
                    Title = t.Title,
                    Description = t.Description,
                    IsCompleted = t.IsCompleted,
                    Priority = t.Priority,
                    DueDate = t.DueDate,
                    CreatedAt = t.CreatedAt,
                    UserId = t.UserId
                })
                .FirstOrDefaultAsync();

            return task; */

            // uses base generic GetByIdAsync() from Repository<TaskItem>
            // No userId filter needed - admin operation
            var task = await _taskRepository.GetByIdAsync(taskId);
            return task == null ? null : MapToResponseDto(task);


        }

        public async Task<TaskResponseDto?> UpdateAnyTaskAsync(int taskId, UpdateTaskDto updateTaskDto)
        {
            // find task without filtering by userId
            /* var task = await _context.Tasks
                 .FirstOrDefaultAsync(t => t.Id == taskId); */

            // Find the task WITHOUT filtering by userId
            // Uses base generic GetByIdAsync() from Repository<TaskItem>
            // No userId ckeck needed - admin operation

            var task = await _taskRepository.GetByIdAsync(taskId);

            if(task == null)
            {
                return null;
            }

            // Update Fields
            ApplyUpdates(task, updateTaskDto);


            // save changes
            //await _context.SaveChangesAsync();

            // Uses base generic Update() from Repository<TaskItem>
            _taskRepository.Update(task);

            // Uses base generic SaveChangesAsync() from Repository<TaskItem>

            await _taskRepository.SaveChangesAsync();

            return MapToResponseDto(task);

        }

        public async Task<bool> DeleteAnyTaskAsync(int taskId)
        {
            // Uses base generic GetByIdAsync() from Repository<TaskItem>
            // No userId check - admin operation
            var task = await _taskRepository.GetByIdAsync(taskId);


            if(task == null)
            {
                return false;
            }

            // Uses base generic Remove() from repository<TaskItem>
            _taskRepository.Remove(task);

            // Uses base generic SaveChangesAsync() from Repository<TakskItem>
            await _taskRepository.SaveChangesAsync();

            return true;
        }

        ///<summary>
        /// Maps a TaskItem entity to TaskResponseDto.
        /// Centralized mapping - used by all methods that return task data.
        /// </summary>
        private TaskResponseDto MapToResponseDto(TaskItem task)
        {

            // Return response DTO
            return new TaskResponseDto
            {
                Id = task.Id,
                Title = task.Title,
                Description = task.Description,
                IsCompleted = task.IsCompleted,
                Priority = task.Priority,
                DueDate = task.DueDate,
                CreatedAt = DateTime.UtcNow,
                UserId = task.UserId
            };

        }

        ///<summary>
        /// Applies partial updates to a task entity.
        /// Only updates fields that are provided (not null).
        /// Used by both UpdateTaskAsync and UpdateAnyTaskAsync.
        /// </summary>
        
        private void ApplyUpdates(TaskItem task, UpdateTaskDto updateTaskDto)
        {
            // update only the fiels the fields that were provided ( not null )
            if (updateTaskDto.Title != null)
            {
                task.Title = updateTaskDto.Title;
            }

            if (updateTaskDto.Description != null)
            {
                task.Description = updateTaskDto.Description;
            }

            if (updateTaskDto.IsCompleted != null)
            {
                task.IsCompleted = updateTaskDto.IsCompleted.Value;
            }
            // Hasvalue is attribute for checking nullable

            if (updateTaskDto.Priority.HasValue)
            {
                task.Priority = updateTaskDto.Priority.Value;
            }

            if (updateTaskDto.DueDate.HasValue)
            {
                task.DueDate = updateTaskDto.DueDate;
            }
        }


    }
}
