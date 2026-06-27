using CommunityEventManagement.Domain.Interfaces.Services;
using CommunityEventManagement.Web.ViewModels;

namespace CommunityEventManagement.Web.Services
{
    /// <summary>
    /// Manages authentication state across Blazor components.
    /// Wraps IAuthService for UI consumption.
    /// </summary>
    public class AuthStateService
    {
        private readonly IAuthService _authService;
        private readonly IParticipantService _participantService;
        private UserSessionViewModel _currentUser = new();

        public event Action? OnAuthStateChanged;

        public AuthStateService(
            IAuthService authService,
            IParticipantService participantService)
        {
            _authService = authService;
            _participantService = participantService;
        }

        public UserSessionViewModel CurrentUser => _currentUser;
        public bool IsAuthenticated => _currentUser.IsAuthenticated;
        public bool IsAdmin => _currentUser.IsAdmin;
        public bool IsParticipant => _currentUser.IsParticipant;
        public int? UserId => _currentUser.UserId;

        // ↓ virtual — needed for bUnit tests to override
        public virtual async Task<bool> LoginAsync(string email, string password)
        {
            var loginModel = new CommunityEventManagement.Domain.Models.LoginModel
            {
                Email = email,
                Password = password
            };

            var success = await _authService.LoginAsync(loginModel);
            if (!success) return false;

            var user = await _authService.GetCurrentUserAsync();
            if (user == null) return false;

            _currentUser = new UserSessionViewModel
            {
                IsAuthenticated = true,
                UserId = user.Id,
                Email = user.Email,
                FullName = user.GetFullName(),
                Role = user.Role.ToString()
            };

            NotifyStateChanged();
            return true;
        }

        // ↓ virtual — needed for bUnit tests to override
        public virtual async Task<RegisterResult> RegisterParticipantAsync(
            string firstName, string lastName, string email,
            string? phoneNumber, string password)
        {
            try
            {
                if (await _participantService.EmailExistsAsync(email))
                {
                    return new RegisterResult
                    {
                        Success = false,
                        ErrorMessage = "An account with this email already exists."
                    };
                }

                await _participantService.CreateParticipantAsync(
                    firstName, lastName, email, phoneNumber, password);

                return new RegisterResult { Success = true };
            }
            catch (CommunityEventManagement.Domain.Exceptions.RegistrationException ex)
            {
                return new RegisterResult { Success = false, ErrorMessage = ex.Message };
            }
            catch (Exception ex)
            {
                return new RegisterResult
                {
                    Success = false,
                    ErrorMessage = $"Registration failed: {ex.Message}"
                };
            }
        }

        // ↓ virtual — needed for bUnit tests to override
        public virtual async Task LogoutAsync()
        {
            await _authService.LogoutAsync();
            _currentUser = new UserSessionViewModel { IsAuthenticated = false };
            NotifyStateChanged();
        }

        private void NotifyStateChanged() => OnAuthStateChanged?.Invoke();
    }

    /// <summary>
    /// Result of a self-registration attempt.
    /// </summary>
    public class RegisterResult
    {
        public bool Success { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
    }
}