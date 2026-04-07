// Controllers/UsersController.cs
using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using ApiUser.DTOs;
using ApiUser.Services;

namespace ApiUser.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class UsersController : ControllerBase
    {
        private readonly IUserService _userService;
        private static readonly ActivitySource ActivitySource = new("ApiUser.UsersController");

        public UsersController(IUserService userService)
        {
            _userService = userService;
        }

        /// <summary>
        /// Obtém todos os usuários
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<IEnumerable<UserResponseDto>>> GetAllUsers()
        {
            using var activity = ActivitySource.StartActivity("UsersController.GetAllUsers");
            activity?.SetTag("http.method", "GET");
            activity?.SetTag("http.route", "/api/users");

            try
            {
                var users = await _userService.GetAllUsersAsync();
                return Ok(users);
            }
            catch (Exception ex)
            {
                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                return StatusCode(500, new { message = "Erro interno do servidor" });
            }
        }

        /// <summary>
        /// Obtém um usuário por ID
        /// </summary>
        [HttpGet("{id}")]
        public async Task<ActionResult<UserResponseDto>> GetUserById(int id)
        {
            using var activity = ActivitySource.StartActivity("UsersController.GetUserById");
            activity?.SetTag("http.method", "GET");
            activity?.SetTag("http.route", "/api/users/{id}");
            activity?.SetTag("user.id", id);

            try
            {
                var user = await _userService.GetUserByIdAsync(id);
                
                if (user == null)
                {
                    return NotFound(new { message = "Usuário não encontrado" });
                }

                return Ok(user);
            }
            catch (Exception ex)
            {
                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                return StatusCode(500, new { message = "Erro interno do servidor" });
            }
        }

        /// <summary>
        /// Cria um novo usuário
        /// </summary>
        [HttpPost]
        public async Task<ActionResult<UserResponseDto>> CreateUser([FromBody] CreateUserDto createUserDto)
        {
            using var activity = ActivitySource.StartActivity("UsersController.CreateUser");
            activity?.SetTag("http.method", "POST");
            activity?.SetTag("http.route", "/api/users");

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var user = await _userService.CreateUserAsync(createUserDto);
                return CreatedAtAction(nameof(GetUserById), new { id = user.Id }, user);
            }
            catch (Exception ex)
            {
                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                
                if (ex.Message.Contains("duplicate") || ex.Message.Contains("unique"))
                {
                    return Conflict(new { message = "Email já está em uso" });
                }
                
                return StatusCode(500, new { message = "Erro interno do servidor" });
            }
        }

        /// <summary>
        /// Atualiza um usuário existente
        /// </summary>
        [HttpPut("{id}")]
        public async Task<ActionResult<UserResponseDto>> UpdateUser(int id, [FromBody] UpdateUserDto updateUserDto)
        {
            using var activity = ActivitySource.StartActivity("UsersController.UpdateUser");
            activity?.SetTag("http.method", "PUT");
            activity?.SetTag("http.route", "/api/users/{id}");
            activity?.SetTag("user.id", id);

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var user = await _userService.UpdateUserAsync(id, updateUserDto);
                
                if (user == null)
                {
                    return NotFound(new { message = "Usuário não encontrado" });
                }

                return Ok(user);
            }
            catch (Exception ex)
            {
                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                
                if (ex.Message.Contains("duplicate") || ex.Message.Contains("unique"))
                {
                    return Conflict(new { message = "Email já está em uso" });
                }
                
                return StatusCode(500, new { message = "Erro interno do servidor" });
            }
        }

        /// <summary>
        /// Remove um usuário
        /// </summary>
        [HttpDelete("{id}")]
        public async Task<ActionResult> DeleteUser(int id)
        {
            using var activity = ActivitySource.StartActivity("UsersController.DeleteUser");
            activity?.SetTag("http.method", "DELETE");
            activity?.SetTag("http.route", "/api/users/{id}");
            activity?.SetTag("user.id", id);

            try
            {
                var deleted = await _userService.DeleteUserAsync(id);
                
                if (!deleted)
                {
                    return NotFound(new { message = "Usuário não encontrado" });
                }

                return NoContent();
            }
            catch (Exception ex)
            {
                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                return StatusCode(500, new { message = "Erro interno do servidor" });
            }
        }
    }
}