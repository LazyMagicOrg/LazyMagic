# Customizing Authentication UI in MAUI

This guide explains how to customize the authentication UI for sign-in, password operations, and logout pages in your MAUI Blazor application.

## Overview

The authentication system uses a simple interface-based approach where UI creation is separated from authentication logic. All authentication pages (sign-in, password reset/change, logout) use the same `IAuthenticationUI` interface.

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