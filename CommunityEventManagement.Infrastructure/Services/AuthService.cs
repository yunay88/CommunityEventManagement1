using CommunityEventManagement.Domain.Entities;
using CommunityEventManagement.Domain.Exceptions;
using CommunityEventManagement.Domain.Interfaces.Services;
using CommunityEventManagement.Domain.Models;
using CommunityEventManagement.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CommunityEventManagement.Infrastructure.Services
{
    public class AuthService : IAuthService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<AuthService> _logger;
        private AppUser? _currentUser;

        public AuthService(ApplicationDbContext context, ILogger<AuthService> logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<bool> LoginAsync(LoginModel loginModel)
        {
            if (loginModel == null)
                throw new ArgumentNullException(nameof(loginModel));

            try
            {
                _logger.LogInformation("Login attempt for: {Email}", loginModel.Email);

                var user = await _context.Set<Person>()
                    .OfType<AppUser>()
                    .FirstOrDefaultAsync(u => u.Email.ToLower() == loginModel.Email.ToLower());

                if (user == null)
                {
                    _logger.LogWarning("Login failed – user not found: {Email}", loginModel.Email);
                    return false;
                }

                if (!user.IsActive)
                {
                    _logger.LogWarning("Login failed – account inactive: {Email}", loginModel.Email);
                    throw new AuthenticationException("Account is deactivated.");
                }

                // ✅ BUG #5 FIX: BCrypt.Verify replaces the broken base64 comparison.
                if (!BCrypt.Net.BCrypt.Verify(loginModel.Password, user.PasswordHash))
                {
                    _logger.LogWarning("Login failed – invalid password: {Email}", loginModel.Email);
                    return false;
                }

                _currentUser = user;
                user.RecordLogin();
                await _context.SaveChangesAsync();

                _logger.LogInformation("Login successful: {Email} Role={Role}",
                    loginModel.Email, user.Role);

                return true;
            }
            catch (AuthenticationException) { throw; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during login for {Email}", loginModel.Email);
                throw new AuthenticationException($"Login failed for {loginModel.Email}.");
            }
            finally
            {
                _logger.LogDebug("LoginAsync completed for {Email}", loginModel.Email);
            }
        }

        public Task LogoutAsync()
        {
            if (_currentUser != null)
                _logger.LogInformation("User logged out: {Email}", _currentUser.Email);
            _currentUser = null;
            return Task.CompletedTask;
        }

        public Task<bool> IsAuthenticatedAsync() => Task.FromResult(_currentUser != null);
        public Task<string?> GetCurrentUserEmailAsync() => Task.FromResult(_currentUser?.Email);
        public Task<string?> GetCurrentUserRoleAsync() => Task.FromResult(_currentUser?.Role.ToString());
        public Task<bool> IsInRoleAsync(string role) =>
            Task.FromResult(_currentUser != null &&
                string.Equals(_currentUser.Role.ToString(), role, StringComparison.OrdinalIgnoreCase));

        public Task<AppUser?> GetCurrentUserAsync() => Task.FromResult(_currentUser);

        // <-- ENTERPRISE FIX: SECURE PASSWORD RESET IMPLEMENTATION
        public async Task<bool> ResetPasswordAsync(string email, string newPassword)
        {
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(newPassword)) return false;
            try
            {
                var user = await _context.Set<Person>()
                    .OfType<AppUser>()
                    .FirstOrDefaultAsync(u => u.Email.ToLower() == email.ToLower());

                if (user == null) return false;

                user.UpdatePasswordHash(BCrypt.Net.BCrypt.HashPassword(newPassword));
                _context.Update(user);
                await _context.SaveChangesAsync();
                _logger.LogInformation("Password successfully reset for: {Email}", email);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resetting password for: {Email}", email);
                return false;
            }
        }
    }
}