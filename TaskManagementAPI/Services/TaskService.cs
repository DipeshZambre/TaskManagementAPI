using Microsoft.EntityFrameworkCore;
using TaskManagementAPI.Data;
using TaskManagementAPI.DTOs;
using TaskManagementAPI.Models;

namespace TaskManagementAPI.Services
{
    public class TaskService : ITaskService
    {
        private readonly ApplicationDbContext _context;
        public TaskService(ApplicationDbContext context)
        {
            _context = context;
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
            _context.Tasks.Add(task);
            await _context.SaveChangesAsync();

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
                UserId = userId
            };



        }

        public async Task<List<TaskResponseDto>> GetUserTasksAsync(int userId)
        {
            // Get all tasks for this user, ordered bt creation date (newest first)

            var tasks = await _context.Tasks
                .Where(t => t.UserId == userId)
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


            return tasks;
        }


        public async Task<TaskResponseDto?> GetTasksByIdAsync(int taskId, int userId)
        {
            // Find tasks by ID and ensures it belongs to the user

            var task = await _context.Tasks
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

            return task;
        }

        public async Task<TaskResponseDto?> UpdateTaskAsync(int taskId, UpdateTaskDto updateTaskDto, int userId)
        {
            // Find the task and verify ownership
            var task = await _context.Tasks
                .FirstOrDefaultAsync(t => t.Id == taskId && t.UserId == userId);

            if(task == null)
            {
                return null;// task not found pr user doesn't own it
            }

            // update only the fiels the fields that were provided ( not null )
            if(updateTaskDto.Title != null)
            {
                task.Title = updateTaskDto.Title;
            }

            if(updateTaskDto.Description != null)
            {
                task.Description = updateTaskDto.Description;
            }

            if(updateTaskDto.IsCompleted != null)
            {
                task.IsCompleted = updateTaskDto.IsCompleted.Value;
            }
            // Hasvalue is attribute for checking nullable

            if(updateTaskDto.Priority.HasValue)
            {
                task.Priority = updateTaskDto.Priority.Value;
            }

            if(updateTaskDto.DueDate.HasValue)
            {
                task.DueDate = updateTaskDto.DueDate;
            }

            // save changes
            await _context.SaveChangesAsync();

            return new TaskResponseDto
            {
                Id = task.Id,
                Title = task.Title,
                Description = task.Description,
                IsCompleted = task.IsCompleted,
                Priority = task.Priority,
                DueDate = task.DueDate,
                CreatedAt = task.CreatedAt,
                UserId = task.UserId
            };

        }

        public async Task<bool> DeleteTaskAsync(int taskId, int userId)
        {
            // Find the task and verify ownership

            var task = await _context.Tasks
                .FirstOrDefaultAsync(t => t.Id == taskId && t.UserId == userId);

            if(task == null)
            {
                return false; // task not found or user does not own it
            }

            // Remove the task
            _context.Tasks .Remove(task);

            await _context.SaveChangesAsync();
             
            return true;

        }

        public async Task<TaskResponseDto?> ToggleTaskCompletionAsync(int taskId, int userId)
        {
            // Find correct task with correct ownership
            var task = await _context.Tasks
                .FirstOrDefaultAsync(t => t.Id == taskId && t.UserId == userId);

            if(task == null)
            {
                return null;
            }

            // Toogle complete status
            task.IsCompleted = !task.IsCompleted;

            // Save changes

            await _context.SaveChangesAsync();

            return new TaskResponseDto
            {
                Id = task.Id,
                Title = task.Title,
                Description = task.Description,
                IsCompleted = task.IsCompleted,
                Priority = task.Priority,
                DueDate = task.DueDate,
                CreatedAt = task.CreatedAt,
                UserId = task.UserId
            };
        }

    }
}
