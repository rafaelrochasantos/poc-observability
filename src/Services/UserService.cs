// Services/UserService.cs
using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using ApiUser.Data;
using ApiUser.DTOs;
using ApiUser.Models;
using ApiUser.Telemetry;
using ApiUser.Services;

namespace ApiUser.Services
{
    public class UserService : IUserService
    {
        private readonly ApplicationDbContext _context;
        private static readonly ActivitySource ActivitySource = new("ApiUser.UserService");

        public UserService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<UserResponseDto>> GetAllUsersAsync()
        {
            using var activity = ActivitySource.StartActivity("UserService.GetAllUsers");
            activity?.SetTag("operation", "get_all_users");

            try
            {
                var users = await _context.Users.ToListAsync();
                activity?.SetTag("users.count", users.Count);
                
                return users.Select(MapToResponseDto);
            }
            catch (Exception ex)
            {
                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                throw;
            }
        }

        public async Task<UserResponseDto?> GetUserByIdAsync(int id)
        {
            using var activity = ActivitySource.StartActivity("UserService.GetUserById");
            activity?.SetTag("operation", "get_user_by_id");
            activity?.SetTag("user.id", id);

            try
            {
                var user = await _context.Users.FindAsync(id);
                
                if (user == null)
                {
                    activity?.SetTag("user.found", false);
                    return null;
                }

                activity?.SetTag("user.found", true);
                return MapToResponseDto(user);
            }
            catch (Exception ex)
            {
                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                throw;
            }
        }

        public async Task<UserResponseDto> CreateUserAsync(CreateUserDto createUserDto)
        {
            using var activity = ActivitySource.StartActivity("UserService.CreateUser");
            activity?.SetTag("operation", "create_user");
            activity?.SetTag("user.email", createUserDto.Email);

            try
            {
                var user = new User
                {
                    Name = createUserDto.Name,
                    Email = createUserDto.Email,
                    Phone = createUserDto.Phone,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                _context.Users.Add(user);
                await _context.SaveChangesAsync();

                activity?.SetTag("user.id", user.Id);
                activity?.SetTag("operation.success", true);

                return MapToResponseDto(user);
            }
            catch (Exception ex)
            {
                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                activity?.SetTag("operation.success", false);
                throw;
            }
        }

        public async Task<UserResponseDto?> UpdateUserAsync(int id, UpdateUserDto updateUserDto)
        {
            using var activity = ActivitySource.StartActivity("UserService.UpdateUser");
            activity?.SetTag("operation", "update_user");
            activity?.SetTag("user.id", id);

            try
            {
                var user = await _context.Users.FindAsync(id);
                
                if (user == null)
                {
                    activity?.SetTag("user.found", false);
                    return null;
                }

                user.Name = updateUserDto.Name;
                user.Email = updateUserDto.Email;
                user.Phone = updateUserDto.Phone;
                user.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                activity?.SetTag("user.found", true);
                activity?.SetTag("operation.success", true);

                return MapToResponseDto(user);
            }
            catch (Exception ex)
            {
                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                activity?.SetTag("operation.success", false);
                throw;
            }
        }

        public async Task<bool> DeleteUserAsync(int id)
        {
            using var activity = ActivitySource.StartActivity("UserService.DeleteUser");
            activity?.SetTag("operation", "delete_user");
            activity?.SetTag("user.id", id);

            try
            {
                var user = await _context.Users.FindAsync(id);
                
                if (user == null)
                {
                    activity?.SetTag("user.found", false);
                    return false;
                }

                _context.Users.Remove(user);
                await _context.SaveChangesAsync();

                activity?.SetTag("user.found", true);
                activity?.SetTag("operation.success", true);

                return true;
            }
            catch (Exception ex)
            {
                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                activity?.SetTag("operation.success", false);
                throw;
            }
        }

        private static UserResponseDto MapToResponseDto(User user)
        {
            return new UserResponseDto
            {
                Id = user.Id,
                Name = user.Name,
                Email = user.Email,
                Phone = user.Phone,
                CreatedAt = user.CreatedAt,
                UpdatedAt = user.UpdatedAt
            };
        }
    }
}