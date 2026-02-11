using TestWebApp.Models;

namespace TestWebApp.Services;

public class UserService
{
    private readonly List<User> _users =
    [
        new User { Username = "admin", Password = "admin123", DisplayName = "Administrator" },
        new User { Username = "user1", Password = "password1", DisplayName = "Test User 1" },
        new User { Username = "user2", Password = "password2", DisplayName = "Test User 2" },
        new User { Username = "demo", Password = "demo", DisplayName = "Demo User" }
    ];

    public User? ValidateUser(string username, string password)
    {
        return _users.FirstOrDefault(u => 
            u.Username.Equals(username, StringComparison.OrdinalIgnoreCase) && 
            u.Password == password);
    }

    public User? GetUserByUsername(string username)
    {
        return _users.FirstOrDefault(u => 
            u.Username.Equals(username, StringComparison.OrdinalIgnoreCase));
    }

    public IReadOnlyList<User> GetAllUsers() => _users.AsReadOnly();
}
