using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;

namespace LazyMagic.OIDC.MAUI;

/// <summary>
/// Default implementation of authentication UI components
/// </summary>
public class DefaultAuthenticationUI : IAuthenticationUI
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
                new RowDefinition { Height = GridLength.Star }
            }
        };

        // Header
        var headerGrid = new Grid
        {
            BackgroundColor = Color.FromArgb(headerColor),
            Padding = new Thickness(16, 12),
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Auto }
            }
        };

        var titleLabel = new Label
        {
            Text = title,
            FontSize = 18,
            FontAttributes = FontAttributes.Bold,
            TextColor = Colors.White,
            VerticalOptions = LayoutOptions.Center,
            HorizontalOptions = LayoutOptions.Center
        };
        Grid.SetColumn(titleLabel, 0);
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
        Grid.SetColumn(closeButton, 1);
        headerGrid.Children.Add(closeButton);

        Grid.SetRow(headerGrid, 0);
        grid.Children.Add(headerGrid);

        // WebView
        Grid.SetRow(webView, 1);
        grid.Children.Add(webView);

        // Loading indicator
        loadingIndicator.VerticalOptions = LayoutOptions.Center;
        loadingIndicator.HorizontalOptions = LayoutOptions.Center;
        Grid.SetRow(loadingIndicator, 1);
        grid.Children.Add(loadingIndicator);

        return grid;
    }
}