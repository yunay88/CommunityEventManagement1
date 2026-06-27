using CommunityEventManagement.Domain.Entities;
using CommunityEventManagement.Domain.Exceptions;
using System.Text.RegularExpressions;

namespace CommunityEventManagement.Infrastructure.Patterns.Factory
{
    public class CreateParticipantRequest
    {
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string? PhoneNumber { get; set; }
    }

    public class ParticipantFactory : IEntityFactory<Participant, CreateParticipantRequest>
    {
        private static readonly Regex EmailRegex = new(
            @"^[^@\s]+@[^@\s]+\.[^@\s]+$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex PhoneRegex = new(
            @"^(\+44|0)[\d\s\-]{9,14}$",
            RegexOptions.Compiled);

        public Participant Create(CreateParticipantRequest request)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            var errors = Validate(request).ToList();
            if (errors.Any())
                throw new RegistrationException(
                    $"Cannot create participant. Errors: {string.Join(", ", errors)}");

            var passwordHash = HashPassword(request.Password);

            return new Participant(
                request.FirstName.Trim(),
                request.LastName.Trim(),
                request.Email.Trim().ToLowerInvariant(),
                passwordHash,
                request.PhoneNumber?.Trim());
        }

        public IEnumerable<string> Validate(CreateParticipantRequest request)
        {
            if (request == null) { yield return "Request cannot be null."; yield break; }
            if (string.IsNullOrWhiteSpace(request.FirstName)) yield return "First name is required.";
            if (request.FirstName?.Length < 2) yield return "First name must be at least 2 characters.";
            if (string.IsNullOrWhiteSpace(request.LastName)) yield return "Last name is required.";
            if (request.LastName?.Length < 2) yield return "Last name must be at least 2 characters.";
            if (string.IsNullOrWhiteSpace(request.Email)) yield return "Email address is required.";
            else if (!EmailRegex.IsMatch(request.Email)) yield return "Email address is not in a valid format.";
            if (string.IsNullOrWhiteSpace(request.Password)) yield return "Password is required.";
            else if (request.Password.Length < 6) yield return "Password must be at least 6 characters.";
            if (!string.IsNullOrWhiteSpace(request.PhoneNumber) && !PhoneRegex.IsMatch(request.PhoneNumber))
                yield return "Phone number is not in a valid UK format.";
        }

        public Participant CreateWithHashedPassword(
            string firstName, string lastName, string email,
            string passwordHash, string? phoneNumber = null)
        {
            if (string.IsNullOrWhiteSpace(firstName))
                throw new ArgumentException("First name required.", nameof(firstName));
            if (string.IsNullOrWhiteSpace(email))
                throw new ArgumentException("Email required.", nameof(email));

            return new Participant(firstName.Trim(), lastName.Trim(),
                email.Trim().ToLowerInvariant(), passwordHash, phoneNumber?.Trim());
        }

        // ✅ BUG #5 FIX: real BCrypt, not base64.
        private static string HashPassword(string password)
        {
            return BCrypt.Net.BCrypt.HashPassword(password, workFactor: 11);
        }
    }
}