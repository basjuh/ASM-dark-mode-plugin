using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Ufs.DarkMode;

internal static class UfsBrandingInjector
{
    private const string Url = "https://unofficial-servers.com/servers/ark-survival-evolved";
    private const string ButtonName = "UfsBrandingButton";
    private const string ResourceName = "Ufs.DarkMode.Assets.ufs-logo.png";

    private static BitmapImage logo;

    public static void EnsureInjected()
    {
        try
        {
            Application app = Application.Current;
            if (app == null)
            {
                return;
            }

            foreach (Window window in app.Windows)
            {
                if (window == null || window.GetType().FullName != "ServerManagerTool.MainWindow")
                {
                    continue;
                }

                Inject(window);
            }
        }
        catch
        {
        }
    }

    private static void Inject(Window window)
    {
        Button discord = FindDiscordButton(window);
        if (discord == null)
        {
            return;
        }

        if (LogicalTreeHelper.GetParent(discord) is not Grid grid)
        {
            return;
        }

        foreach (UIElement existing in grid.Children)
        {
            if (existing is Button named && named.Name == ButtonName)
            {
                return;
            }
        }

        const int insertAt = 2;
        if (grid.ColumnDefinitions.Count > insertAt)
        {
            grid.ColumnDefinitions.Insert(insertAt, new ColumnDefinition { Width = GridLength.Auto });

            foreach (UIElement child in grid.Children)
            {
                int column = Grid.GetColumn(child);
                int span = Grid.GetColumnSpan(child);

                if (column >= insertAt)
                {
                    Grid.SetColumn(child, column + 1);
                }
                else if (column + span > insertAt)
                {
                    Grid.SetColumnSpan(child, span + 1);
                }
            }
        }

        var image = new Image
        {
            Source = Logo,
            Width = 40,
            Height = 40,
            Stretch = Stretch.Uniform
        };
        RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.HighQuality);

        var button = new Button
        {
            Name = ButtonName,
            ToolTip = "Unofficial-Servers.com ARK server list",
            Style = window.TryFindResource("ButtonStyle4") as Style,
            Width = 50,
            Height = 50,
            Margin = new Thickness(5, 0, 0, 0),
            Padding = new Thickness(0),
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Content = image
        };
        button.Click += OnUfsClick;

        Grid.SetRow(button, 1);
        Grid.SetRowSpan(button, 2);
        Grid.SetColumn(button, insertAt);

        grid.Children.Add(button);
    }

    private static Button FindDiscordButton(DependencyObject root)
    {
        foreach (Button button in EnumerateButtons(root))
        {
            if (Grid.GetRow(button) == 1
                && Grid.GetColumn(button) == 0
                && Grid.GetColumnSpan(button) == 2
                && Grid.GetRowSpan(button) == 2
                && button.Content is Image)
            {
                return button;
            }

            if (button.ToolTip is string tip && tip.IndexOf("discord", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return button;
            }
        }

        return null;
    }

    private static IEnumerable<Button> EnumerateButtons(DependencyObject root)
    {
        if (root is Button button)
        {
            yield return button;
        }

        foreach (object child in LogicalTreeHelper.GetChildren(root))
        {
            if (child is DependencyObject dependencyObject)
            {
                foreach (Button item in EnumerateButtons(dependencyObject))
                {
                    yield return item;
                }
            }
        }
    }

    private static BitmapImage Logo
    {
        get
        {
            if (logo != null)
            {
                return logo;
            }

            var assembly = typeof(UfsBrandingInjector).Assembly;
            using (var stream = assembly.GetManifestResourceStream(ResourceName))
            {
                if (stream == null)
                {
                    return null;
                }

                var image = new BitmapImage();
                image.BeginInit();
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.StreamSource = stream;
                image.EndInit();
                image.Freeze();
                logo = image;
                return logo;
            }
        }
    }

    private static void OnUfsClick(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo(Url) { UseShellExecute = true });
        }
        catch
        {
        }
    }
}
