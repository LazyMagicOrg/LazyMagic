# LazyMagic OIDC MAUI - .NET MAUI Authentication

## Overview
This library provides AWS Cognito authentication for .NET MAUI applications using native WebAuthenticator and WebView components. Unlike the WASM version, MAUI authentication does not suffer from iframe blocking issues and provides native mobile authentication experiences.

## MAUI vs WASM Authentication

### MAUI Advantages
- **No iframe issues**: Uses native WebAuthenticator, avoiding AWS Cognito's `X-Frame-Options: DENY` limitations
- **Native mobile experience**: Leverages platform-specific authentication flows 
- **Secure token storage**: Uses platform-specific secure storage (Keychain on iOS, KeyStore on Android)
- **Fast performance**: No browser-based delays or timeouts

### Architecture
```
┌─────────────────────────────────────────────────────────────┐
│                    MAUI Application                         │
├─────────────────────────────────────────────────────────────┤
│  AuthorizeView Components ←→ MauiAuthenticationStateProvider │
│                                      ↓                      │
│                               MauiOIDCService               │
│                                      ↓                      │
│                            WebAuthenticator                 │
│                                      ↓                      │
│                         Platform-Specific Auth              │
│                      (iOS Safari / Android Chrome)         │
└─────────────────────────────────────────────────────────────┘
```

## Usage

### Setup in MauiProgram.cs
```csharp
public static MauiApp CreateMauiApp()
{
    var builder = MauiApp.CreateBuilder();
    
    // Add MAUI authentication services
    builder.Services.AddLazyMagicOIDCMAUI();
    
    var app = builder.Build();
    
    // Load OIDC configuration
    await ConfigureLazyMagicOIDCMAUI.LoadConfiguration(app);
    
    return app;
}
```

## Key Services

### MauiAuthenticationStateProvider
- Provides authentication state for `AuthorizeView` components
- Automatically updates when authentication state changes
- Fast startup with background authentication checking

### MauiOIDCService  
- Handles OAuth flows using native WebAuthenticator
- Manages token storage and refresh
- Supports session restoration across app restarts

### TokenStorageService
- Secure platform-specific token storage
- Manages RememberMe functionality
- Handles token cleanup and expiration

### MauiTokenRefreshService
- Automatic token refresh before expiration
- Uses stored refresh tokens for renewal
- Background monitoring with 5-minute buffer

### WebViewAuthenticationProvider
- Custom WebView-based authentication for complex scenarios
- Alternative to WebAuthenticator for specific use cases
- Customizable authentication UI

## Performance Characteristics

| Operation | MAUI Performance | Notes |
|-----------|------------------|-------|
| App startup | < 1 second | Fast native initialization |
| Authentication check | < 100ms | Direct token validation |
| Login flow | 2-5 seconds | Platform browser handoff |
| Token refresh | < 500ms | Background token management |
| Automatic refresh | Transparent | 5-minute buffer before expiration |
| AuthorizeView rendering | Immediate | No authentication delays |

## Automatic Token Refresh

### Overview
The MAUI library includes automatic token refresh functionality that prevents authentication timeouts during active user sessions. Unlike WASM applications, MAUI apps can securely store and use refresh tokens for background token renewal.

### How It Works
```csharp
// Token refresh service uses stored refresh tokens for renewal
public class MauiTokenRefreshService : TokenRefreshServiceBase
{
    protected override async Task<bool> PerformTokenRefreshAsync()
    {
        // Get stored refresh token from secure storage
        var (_, _, refreshToken) = await _tokenStorage.GetTokensAsync();
        
        // Exchange refresh token for new access token
        var newTokens = await RefreshTokensAsync(authority, clientId, refreshToken);
        
        // Save new tokens to secure storage
        await _tokenStorage.SaveTokensAsync(newTokens.AccessToken, newTokens.IdToken, newTokens.RefreshToken);
        
        return true;
    }
}
```

### Key Features
- **Secure Storage**: Uses platform-specific secure storage (iOS Keychain, Android KeyStore)
- **Refresh Token Support**: Leverages stored refresh tokens for background renewal
- **Automatic Monitoring**: Service starts on login/session restore, stops on logout
- **5-Minute Buffer**: Refresh occurs 5 minutes before token expiration
- **Error Handling**: Graceful fallback if refresh fails
- **Session Continuity**: Prevents unexpected logouts during app usage

### Platform Benefits
- **iOS**: Uses Keychain for secure refresh token storage
- **Android**: Uses KeyStore for encrypted token storage
- **Background Operation**: No user interaction required
- **App Lifecycle**: Tokens remain valid across app suspension/resume

## Authentication UI Customization

The authentication system uses a simple interface-based approach where UI creation is separated from authentication logic. All authentication pages (sign-in, password operations, and logout) use the same `IAuthenticationUI` interface.

## Quick Start

### 1. Create Your Custom UI Class

Create a class that implements `IAuthenticationUI`:

```csharp
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;

public class MyCompanyAuthUI : IAuthenticationUI
{
    public View CreateAuthenticationLayout(
        string title,
        string headerColor,
        WebView webView,
        ActivityIndicator loadingIndicator,
        EventHandler onCloseClicked)
    {
        // Create your custom layout here
        var grid = new Grid();
        // ... add your branded UI elements
        return grid;
    }
}
```

### 2. Register in Dependency Injection

In your `MauiProgram.cs`:

```csharp
public static MauiApp CreateMauiApp()
{
    var builder = MauiApp.CreateBuilder();
    
    // Register your custom UI
    builder.Services.AddSingleton<IAuthenticationUI, MyCompanyAuthUI>();
    
    // Register the authentication provider (it will use your custom UI)
    // If you have not registered a custom UI, DefaultAuthenticationUI will be used
    builder.Services.AddLazyMagicBlazorMAUI();
    
    return builder.Build();
}
```

That's it! Your custom UI will now be used for all authentication pages.

## Customization Examples

### Adding Company Branding

```csharp
public class BrandedAuthUI : IAuthenticationUI
{
    public View CreateAuthenticationLayout(
        string title,
        string headerColor,
        WebView webView,
        ActivityIndicator loadingIndicator,
        EventHandler onCloseClicked)
    {
        var grid = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition { Height = GridLength.Auto },  // Header
                new RowDefinition { Height = GridLength.Star },  // Content
                new RowDefinition { Height = GridLength.Auto }   // Footer
            }
        };

        // Custom header with logo
        var header = new StackLayout
        {
            BackgroundColor = Color.FromArgb("#2E8B57"),
            Padding = new Thickness(20),
            Children =
            {
                new Image { Source = "company_logo.png", HeightRequest = 40 },
                new Label 
                { 
                    Text = title,
                    TextColor = Colors.White,
                    FontSize = 20,
                    FontAttributes = FontAttributes.Bold
                }
            }
        };
        Grid.SetRow(header, 0);
        grid.Children.Add(header);

        // Add WebView and loading indicator
        Grid.SetRow(webView, 1);
        grid.Children.Add(webView);
        Grid.SetRow(loadingIndicator, 1);
        grid.Children.Add(loadingIndicator);

        // Custom footer
        var footer = new Label
        {
            Text = "© 2024 Your Company - Secure Authentication",
            FontSize = 12,
            TextColor = Colors.Gray,
            HorizontalTextAlignment = TextAlignment.Center,
            Padding = new Thickness(10)
        };
        Grid.SetRow(footer, 2);
        grid.Children.Add(footer);

        return grid;
    }
}
```

### Different UI for Different Operations

You can check the `title` parameter to provide different layouts:

```csharp
public View CreateAuthenticationLayout(
    string title,
    string headerColor,
    WebView webView,
    ActivityIndicator loadingIndicator,
    EventHandler onCloseClicked)
{
    if (title.Contains("Sign In"))
    {
        // Return login-specific UI
        return CreateLoginLayout(webView, loadingIndicator, onCloseClicked);
    }
    else if (title.Contains("Password"))
    {
        // Return password operation UI
        return CreatePasswordLayout(title, webView, loadingIndicator, onCloseClicked);
    }
    else if (title.Contains("Log"))
    {
        // Return logout UI
        return CreateLogoutLayout(webView, loadingIndicator, onCloseClicked);
    }
    
    // Default layout
    return CreateDefaultLayout(title, headerColor, webView, loadingIndicator, onCloseClicked);
}
```

## Key Components

### IAuthenticationUI Interface

The interface has a single method:

```csharp
public interface IAuthenticationUI
{
    View CreateAuthenticationLayout(
        string title,              // Page title (e.g., "Sign In", "Password Reset")
        string headerColor,         // Suggested header color
        WebView webView,           // The WebView for authentication
        ActivityIndicator loadingIndicator,  // Loading spinner
        EventHandler onCloseClicked);       // Handler for close button
}
```

### Parameters Explained

- **title**: The operation being performed ("Sign In", "Password Reset", "Signing Out", etc.)
- **headerColor**: A suggested color for the header (you can ignore this and use your own)
- **webView**: The WebView control that displays the authentication page (must be added to your layout)
- **loadingIndicator**: A loading spinner to show while pages load (must be added to your layout)
- **onCloseClicked**: Event handler for when user wants to close/cancel (attach to your close button)

## Important Notes

1. **WebView is Required**: Always include the provided WebView in your layout - it handles the actual authentication
2. **Loading Indicator**: Include the loadingIndicator to provide feedback during page loads
3. **Close Functionality**: Provide a way for users to close/cancel (button, gesture, etc.) that calls onCloseClicked
4. **Same UI for All Pages**: Your UI will be used for sign-in, password operations, and logout
5. **No Inheritance Needed**: Just implement the interface - no need to inherit from base classes

## Default Implementation

If you don't provide a custom UI, the system uses `DefaultAuthenticationUI` which provides a clean, functional interface with:
- Header with title and close button
- WebView for authentication
- Loading indicator
- Responsive layout

## Testing Your Custom UI

1. Run your app and trigger authentication
2. Verify your custom UI appears for sign-in
3. Test password reset/change operations
4. Test logout flow
5. Ensure close/cancel functionality works
6. Test on different screen sizes/orientations

## Troubleshooting

- **UI not appearing**: Ensure your custom class is registered in DI before the authentication provider
- **WebView not visible**: Make sure you're adding the webView to your layout and it has appropriate sizing
- **Close not working**: Verify you're attaching the onCloseClicked handler to your close button's Clicked event
- **Loading indicator stuck**: Ensure the loadingIndicator is added to your layout (it auto-hides when pages load)