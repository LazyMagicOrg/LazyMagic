using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;

namespace LazyMagic.OIDC.MAUI;

/// <summary>
/// Example of a custom authentication UI implementation with company branding
/// </summary>
public class CustomAuthenticationUI : IAuthenticationUI
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
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Star },
                new RowDefinition { Height = GridLength.Auto }
            }
        };

        // Custom header with company branding
        var headerGrid = new Grid
        {
            BackgroundColor = Color.FromArgb("#2E8B57"), // Custom sea green color
            Padding = new Thickness(16, 12),
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Auto }
            }
        };

        // Company logo (placeholder)
        var logoLabel = new Label
        {
            Text = "🏢", // Replace with your company logo
            FontSize = 20,
            TextColor = Colors.White,
            VerticalOptions = LayoutOptions.Center
        };
        Grid.SetColumn(logoLabel, 0);
        headerGrid.Children.Add(logoLabel);

        var titleLabel = new Label
        {
            Text = $"Company Portal - {title}",
            FontSize = 18,
            FontAttributes = FontAttributes.Bold,
            TextColor = Colors.White,
            VerticalOptions = LayoutOptions.Center,
            HorizontalOptions = LayoutOptions.Center
        };
        Grid.SetColumn(titleLabel, 1);
        headerGrid.Children.Add(titleLabel);

        var closeButton = new Button
        {
            Text = "✕",
            BackgroundColor = Colors.Transparent,
            TextColor = Colors.White,
            BorderWidth = 0,
            FontSize = 18,
            WidthRequest = 40,
            HeightRequest = 40
        };
        closeButton.Clicked += onCloseClicked;
        Grid.SetColumn(closeButton, 2);
        headerGrid.Children.Add(closeButton);

        Grid.SetRow(headerGrid, 0);
        grid.Children.Add(headerGrid);

        // WebView
        Grid.SetRow(webView, 1);
        grid.Children.Add(webView);

        // Custom loading indicator with company branding
        var loadingStack = new StackLayout
        {
            VerticalOptions = LayoutOptions.Center,
            HorizontalOptions = LayoutOptions.Center,
            Spacing = 20
        };

        loadingIndicator.Color = Color.FromArgb("#2E8B57"); // Match header color
        loadingStack.Children.Add(loadingIndicator);

        var loadingLabel = new Label
        {
            Text = "Connecting to secure portal...",
            FontSize = 16,
            TextColor = Color.FromArgb("#666666"),
            HorizontalTextAlignment = TextAlignment.Center
        };
        loadingStack.Children.Add(loadingLabel);

        Grid.SetRow(loadingStack, 1);
        grid.Children.Add(loadingStack);

        // Custom footer with company info
        var footerLabel = new Label
        {
            Text = "Powered by Your Company Name | Secure Authentication",
            FontSize = 12,
            TextColor = Color.FromArgb("#999999"),
            HorizontalTextAlignment = TextAlignment.Center,
            Padding = new Thickness(10)
        };
        Grid.SetRow(footerLabel, 2);
        grid.Children.Add(footerLabel);

        return grid;
    }
}

/*
=== HOW TO USE CUSTOM AUTHENTICATION UI ===

The new architecture separates UI from logic, making it much simpler to customize.

🎯 SIMPLE APPROACH: Register your custom UI in DI
   In your MauiProgram.cs ConfigureServices method:

   // Register your custom UI implementation
   services.TryAddScoped<IAuthenticationUI, CustomAuthenticationUI>();
   
   // Register the authentication provider (it will use your custom UI)
   services.AddScoped<IWebViewAuthenticationProvider, MauiWebViewAuthenticationProvider>();

🎨 FLEXIBLE APPROACH: Create different UIs for different scenarios
   You can create multiple UI implementations and inject them where needed:

   // Register multiple UI implementations
   services.TryAddScoped<IAuthenticationUI, CustomAuthenticationUI>();
   services.TryAddScoped<CustomAuthenticationUI>(); // Also register concrete type
   services.TryAddScoped<DefaultAuthenticationUI>();
   
   // Use specific UI in specific places
   services.AddScoped<IWebViewAuthenticationProvider>(provider => 
       new MauiWebViewAuthenticationProvider(
           provider.GetRequiredService<ILoggerFactory>(),
           provider.GetRequiredService<CustomAuthenticationUI>() // Use specific UI
       ));

📋 BENEFITS:
- Clean separation of UI from authentication logic
- No inheritance needed - just implement IAuthenticationUI
- Same UI can be used for authentication, password, and logout pages
- Easy to test and maintain
- No complex factory patterns

💡 TIP: The IAuthenticationUI interface is simple - just one method to implement!
*/