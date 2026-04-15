using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using PainFinder.Application.Services;
using PainFinder.Shared.Contracts;
using PainFinder.Shared.DTOs;

namespace PainFinder.Web.Services;

public class AuthService
{
    private readonly IAuthService _authService;
    private readonly ProtectedLocalStorage _localStorage;

    private const string UserKey = "pf_user";

    public UserDto? User { get; private set; }
    public bool IsLoading { get; private set; } = true;

    public event Action? OnChange;

    public AuthService(IAuthService authService, ProtectedLocalStorage localStorage)
    {
        _authService = authService;
        _localStorage = localStorage;
    }

    public async Task InitializeAsync()
    {
        try
        {
            var result = await _localStorage.GetAsync<UserDto>(UserKey);
            if (result.Success && result.Value is not null)
            {
                User = result.Value;
            }
        }
        catch
        {
            User = null;
        }

        IsLoading = false;
        OnChange?.Invoke();
    }

    public async Task LoginAsync(string email, string password)
    {
        var result = await _authService.LoginAsync(new LoginRequest(email, password));
        User = result.User;
        await _localStorage.SetAsync(UserKey, result.User);
        OnChange?.Invoke();
    }

    public async Task LoginWithGoogleAsync(string idToken)
    {
        var result = await _authService.GoogleLoginAsync(new GoogleLoginRequest(idToken));
        User = result.User;
        await _localStorage.SetAsync(UserKey, result.User);
        OnChange?.Invoke();
    }

    public async Task RegisterAsync(string fullName, string email, string password)
    {
        var result = await _authService.RegisterAsync(new RegisterRequest(fullName, email, password));
        User = result.User;
        await _localStorage.SetAsync(UserKey, result.User);
        OnChange?.Invoke();
    }

    public async Task LogoutAsync()
    {
        User = null;
        await _localStorage.DeleteAsync(UserKey);
        OnChange?.Invoke();
    }
}
