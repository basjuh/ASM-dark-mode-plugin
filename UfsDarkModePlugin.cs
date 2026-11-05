using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using ServerManagerTool.Plugin.Common;

namespace Ufs.DarkMode
{
    public sealed class UfsDarkModePlugin : IPlugin
    {
        private const int DwmwaUseImmersiveDarkMode = 20;
        private const int DwmwaUseImmersiveDarkModeBefore20H1 = 19;
        private const string ThemeResourceName = "Ufs.DarkMode.Theme.xaml";

        public static string LastError { get; private set; }

        private static readonly FontFamily UfsFont = new FontFamily("Ebrima");
        private static readonly Brush TransparentBrush = Brushes.Transparent;

        private static Brush BgBrush;
        private static Brush BlockBrush;
        private static Brush BlockAltBrush;
        private static Brush InputBrush;
        private static Brush BorderBrush;
        private static Brush AccentBrush;
        private static Brush AccentDarkBrush;
        private static Brush AccentPressedBrush;
        private static Brush TextBrush;
        private static Brush MutedBrush;
        private static Brush DisabledBrush;
        private static Brush WhiteBrush;
        private static Brush RedBrush;
        private static Brush GreenBrush;
        private static Brush OrangeBrush;
        private static Brush YellowBrush;
        private static Brush PurpleBrush;
        private static Brush TableHeaderBrush;
        private static Brush TableRowBrush;
        private static Brush TableRowAltBrush;
        private static Brush TableLineBrush;
        private static Brush TableSelectionBrush;
        private static Brush TableHighlightBrush;

        private static readonly ConditionalWeakTable<Expander, object> ExpandedExpanders = new ConditionalWeakTable<Expander, object>();

        private static ResourceDictionary _appTheme;
        private static DispatcherTimer _timer;
        private static string _themeXaml;
        private static bool _initialized;
        private static bool _handlerRegistered;

        public bool Enabled => true;

        public string PluginCode => "UFSDARK";

        public string PluginName => "UFS Dark Mode";

        public Version PluginVersion => Assembly.GetExecutingAssembly().GetName().Version;

        public bool HasConfigForm => false;

        public void Initialize()
        {
            try
            {
                Application app = Application.Current;
                if (app == null)
                {
                    return;
                }

                if (!app.Dispatcher.CheckAccess())
                {
                    app.Dispatcher.BeginInvoke(new Action(Initialize));
                    return;
                }

                if (_initialized)
                {
                    return;
                }

                _initialized = true;
                _themeXaml = ReadThemeXaml();
                _appTheme = XamlReader.Parse(_themeXaml) as ResourceDictionary;
                if (_appTheme == null)
                {
                    throw new InvalidOperationException("Theme XAML did not produce a ResourceDictionary.");
                }

                LoadBrushes(_appTheme);
                AddSystemColors(_appTheme);
                EnsureAppThemeLast();
                RegisterLoadHandler();
                ThemeExistingWindows(app);
                app.Activated += App_Activated;
                StartTimer(app);
            }
            catch (Exception ex)
            {
                LastError = ex.ToString();
            }
        }

        public void OpenConfigForm(Window owner)
        {
        }

        [DllImport("dwmapi.dll", PreserveSig = true)]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

        private static string ReadThemeXaml()
        {
            using (Stream stream = typeof(UfsDarkModePlugin).Assembly.GetManifestResourceStream(ThemeResourceName))
            {
                if (stream == null)
                {
                    throw new InvalidOperationException("Embedded theme resource not found: " + ThemeResourceName);
                }

                using (StreamReader reader = new StreamReader(stream))
                {
                    return reader.ReadToEnd();
                }
            }
        }

        private static void LoadBrushes(ResourceDictionary theme)
        {
            BgBrush = GetBrush(theme, "UfsBgBrush");
            BlockBrush = GetBrush(theme, "UfsBlockBrush");
            BlockAltBrush = GetBrush(theme, "UfsBlockAltBrush");
            InputBrush = GetBrush(theme, "UfsInputBrush");
            BorderBrush = GetBrush(theme, "UfsBorderBrush");
            AccentBrush = GetBrush(theme, "UfsAccentBrush");
            AccentDarkBrush = GetBrush(theme, "UfsAccentDarkBrush");
            AccentPressedBrush = GetBrush(theme, "UfsAccentPressedBrush");
            TextBrush = GetBrush(theme, "UfsTextBrush");
            MutedBrush = GetBrush(theme, "UfsMutedBrush");
            DisabledBrush = GetBrush(theme, "UfsDisabledBrush");
            WhiteBrush = GetBrush(theme, "UfsWhiteBrush");
            RedBrush = GetBrush(theme, "UfsRedBrush");
            GreenBrush = GetBrush(theme, "UfsGreenBrush");
            OrangeBrush = GetBrush(theme, "UfsOrangeBrush");
            YellowBrush = GetBrush(theme, "UfsYellowBrush");
            PurpleBrush = GetBrush(theme, "UfsPurpleBrush");
            TableHeaderBrush = GetBrush(theme, "UfsTableHeaderBrush");
            TableRowBrush = GetBrush(theme, "UfsTableRowBrush");
            TableRowAltBrush = GetBrush(theme, "UfsTableRowAltBrush");
            TableLineBrush = GetBrush(theme, "UfsTableLineBrush");
            TableSelectionBrush = GetBrush(theme, "UfsTableSelectionBrush");
            TableHighlightBrush = GetBrush(theme, "UfsTableHighlightBrush");
        }

        private static Brush GetBrush(ResourceDictionary theme, string key)
        {
            Brush brush = theme[key] as Brush;
            if (brush != null && brush.CanFreeze && !brush.IsFrozen)
            {
                brush.Freeze();
            }

            return brush;
        }

        private static ResourceDictionary CreateThemeDictionary()
        {
            ResourceDictionary theme = XamlReader.Parse(_themeXaml) as ResourceDictionary;
            if (theme == null)
            {
                throw new InvalidOperationException("Theme XAML did not produce a ResourceDictionary.");
            }

            AddSystemColors(theme);
            return theme;
        }

        private static void AddSystemColors(ResourceDictionary theme)
        {
            theme[SystemColors.WindowBrushKey] = BlockBrush;
            theme[SystemColors.WindowTextBrushKey] = TextBrush;
            theme[SystemColors.ControlBrushKey] = BlockBrush;
            theme[SystemColors.ControlTextBrushKey] = TextBrush;
            theme[SystemColors.ControlDarkBrushKey] = BorderBrush;
            theme[SystemColors.ControlLightBrushKey] = BlockAltBrush;
            theme[SystemColors.ControlDarkDarkBrushKey] = BorderBrush;
            theme[SystemColors.ControlLightLightBrushKey] = TextBrush;
            theme[SystemColors.GrayTextBrushKey] = MutedBrush;
            theme[SystemColors.HighlightBrushKey] = AccentBrush;
            theme[SystemColors.HighlightTextBrushKey] = WhiteBrush;
            theme[SystemColors.HotTrackBrushKey] = AccentBrush;
            theme[SystemColors.MenuBrushKey] = BlockBrush;
            theme[SystemColors.MenuTextBrushKey] = TextBrush;
            theme[SystemColors.MenuBarBrushKey] = BgBrush;
            theme[SystemColors.ActiveBorderBrushKey] = BorderBrush;
            theme[SystemColors.InactiveBorderBrushKey] = BorderBrush;
            theme[SystemColors.ScrollBarBrushKey] = BlockAltBrush;
            theme[SystemColors.DesktopBrushKey] = BgBrush;
        }

        private static void EnsureAppThemeLast()
        {
            Application app = Application.Current;
            if (app == null || _appTheme == null)
            {
                return;
            }

            var merged = app.Resources.MergedDictionaries;
            if (!merged.Contains(_appTheme))
            {
                merged.Add(_appTheme);
                return;
            }

            if (merged.Count > 0 && merged[merged.Count - 1] != _appTheme)
            {
                merged.Remove(_appTheme);
                merged.Add(_appTheme);
            }
        }

        private static void EnsureThemeInScope(FrameworkElement element)
        {
            if (element == null)
            {
                return;
            }

            var merged = element.Resources.MergedDictionaries;
            bool hasDefault = false;
            ResourceDictionary existing = null;

            foreach (ResourceDictionary dictionary in merged)
            {
                if (IsThemeDictionary(dictionary))
                {
                    existing = dictionary;
                }

                if (IsDefaultDictionary(dictionary))
                {
                    hasDefault = true;
                }
            }

            bool shouldAdd = hasDefault || element is Window || element is UserControl || element is Page;
            if (!shouldAdd)
            {
                return;
            }

            if (existing != null)
            {
                if (merged.Count > 0 && merged[merged.Count - 1] != existing)
                {
                    merged.Remove(existing);
                    merged.Add(existing);
                }

                return;
            }

            merged.Add(CreateThemeDictionary());
        }

        private static bool IsThemeDictionary(ResourceDictionary dictionary)
        {
            return dictionary != null && dictionary.Contains("UfsThemeMarker");
        }

        private static bool IsDefaultDictionary(ResourceDictionary dictionary)
        {
            if (dictionary == null || dictionary.Source == null)
            {
                return false;
            }

            return dictionary.Source.OriginalString.IndexOf("Default.xaml", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static void RegisterLoadHandler()
        {
            if (_handlerRegistered)
            {
                return;
            }

            _handlerRegistered = true;
            EventManager.RegisterClassHandler(
                typeof(FrameworkElement),
                FrameworkElement.LoadedEvent,
                new RoutedEventHandler(OnElementLoaded),
                true);
        }

        private static void StartTimer(Application app)
        {
            if (_timer != null)
            {
                return;
            }

            _timer = new DispatcherTimer(DispatcherPriority.Background, app.Dispatcher);
            _timer.Interval = TimeSpan.FromMilliseconds(1000);
            _timer.Tick += Timer_Tick;
            _timer.Start();
        }

        private static void Timer_Tick(object sender, EventArgs e)
        {
            try
            {
                EnsureAppThemeLast();
                ThemeExistingWindows(Application.Current);
            }
            catch (Exception ex)
            {
                LastError = ex.ToString();
            }
        }

        private static void App_Activated(object sender, EventArgs e)
        {
            try
            {
                EnsureAppThemeLast();
                ThemeExistingWindows(Application.Current);
            }
            catch (Exception ex)
            {
                LastError = ex.ToString();
            }
        }

        private static void OnElementLoaded(object sender, RoutedEventArgs e)
        {
            try
            {
                FrameworkElement element = sender as FrameworkElement;
                if (element == null)
                {
                    return;
                }

                EnsureThemeInScope(element);
                Apply(element);

                if (element is Window || element is UserControl || element is Page)
                {
                    Walk(element);
                }
            }
            catch (Exception ex)
            {
                LastError = ex.ToString();
            }
        }

        private static void ThemeExistingWindows(Application app)
        {
            if (app == null)
            {
                return;
            }

            foreach (Window window in app.Windows)
            {
                try
                {
                    EnsureThemeInScope(window);
                    Walk(window);
                }
                catch (Exception ex)
                {
                    LastError = ex.ToString();
                }
            }
        }

        private static void Walk(DependencyObject parent)
        {
            if (parent == null)
            {
                return;
            }

            int children;
            try
            {
                children = VisualTreeHelper.GetChildrenCount(parent);
            }
            catch
            {
                return;
            }

            for (int i = 0; i < children; i++)
            {
                DependencyObject child = null;
                try
                {
                    child = VisualTreeHelper.GetChild(parent, i);
                }
                catch
                {
                }

                if (child == null)
                {
                    continue;
                }

                FrameworkElement element = child as FrameworkElement;
                if (element != null)
                {
                    try
                    {
                        if (element is Window || element is UserControl || element is Page)
                        {
                            EnsureThemeInScope(element);
                        }

                        Apply(element);
                    }
                    catch (Exception ex)
                    {
                        LastError = ex.ToString();
                    }
                }

                Walk(child);
            }
        }

        private static void Apply(FrameworkElement element)
        {
            if (element == null)
            {
                return;
            }

            Window window = element as Window;
            if (window != null)
            {
                ThemeWindow(window);
                return;
            }

            TextBlock textBlock = element as TextBlock;
            if (textBlock != null)
            {
                ApplyTextBlock(textBlock);
                return;
            }

            Label label = element as Label;
            if (label != null)
            {
                ApplyLabel(label);
                return;
            }

            Border border = element as Border;
            if (border != null)
            {
                ApplyBorder(border);
                return;
            }

            Panel panel = element as Panel;
            if (panel != null)
            {
                ApplyPanel(panel);
                return;
            }

            Shape shape = element as Shape;
            if (shape != null)
            {
                ApplyShape(shape);
                return;
            }

            CheckBox checkBox = element as CheckBox;
            if (checkBox != null)
            {
                ApplyCheckBox(checkBox);
                return;
            }

            RadioButton radioButton = element as RadioButton;
            if (radioButton != null)
            {
                ApplyRadioButton(radioButton);
                return;
            }

            Button button = element as Button;
            if (button != null)
            {
                ApplyButton(button);
                return;
            }

            TextBoxBase textBoxBase = element as TextBoxBase;
            if (textBoxBase != null)
            {
                ApplyTextBox(textBoxBase);
                return;
            }

            PasswordBox passwordBox = element as PasswordBox;
            if (passwordBox != null)
            {
                ApplyPasswordBox(passwordBox);
                return;
            }

            ComboBox comboBox = element as ComboBox;
            if (comboBox != null)
            {
                ApplyComboBox(comboBox);
                return;
            }

            DataGrid dataGrid = element as DataGrid;
            if (dataGrid != null)
            {
                ApplyDataGrid(dataGrid);
                return;
            }

            ListView listView = element as ListView;
            if (listView != null)
            {
                ApplyListView(listView);
                return;
            }

            TabControl tabControl = element as TabControl;
            if (tabControl != null)
            {
                ApplyTabControl(tabControl);
                return;
            }

            TabItem tabItem = element as TabItem;
            if (tabItem != null)
            {
                ApplyTabItem(tabItem);
                return;
            }

            GroupBox groupBox = element as GroupBox;
            if (groupBox != null)
            {
                ApplyGroupBox(groupBox);
                return;
            }

            Expander expander = element as Expander;
            if (expander != null)
            {
                ApplyExpander(expander);
                return;
            }

            ToolTip toolTip = element as ToolTip;
            if (toolTip != null)
            {
                ApplyToolTip(toolTip);
                return;
            }

            StatusBar statusBar = element as StatusBar;
            if (statusBar != null)
            {
                ApplyStatusBar(statusBar);
                return;
            }

            ProgressBar progressBar = element as ProgressBar;
            if (progressBar != null)
            {
                ApplyProgressBar(progressBar);
                return;
            }

            Slider slider = element as Slider;
            if (slider != null)
            {
                ApplySlider(slider);
                return;
            }

            ScrollBar scrollBar = element as ScrollBar;
            if (scrollBar != null)
            {
                ApplyScrollBar(scrollBar);
                return;
            }

            Separator separator = element as Separator;
            if (separator != null)
            {
                separator.Background = BorderBrush;
                separator.BorderBrush = BorderBrush;
                return;
            }

            Thumb thumb = element as Thumb;
            if (thumb != null)
            {
                ApplyThumb(thumb);
                return;
            }

            RepeatButton repeatButton = element as RepeatButton;
            if (repeatButton != null)
            {
                ApplyRepeatButton(repeatButton);
                return;
            }

            MenuItem menuItem = element as MenuItem;
            if (menuItem != null)
            {
                ApplyMenuItem(menuItem);
                return;
            }

            DataGridRow dataGridRow = element as DataGridRow;
            if (dataGridRow != null)
            {
                ApplyDataGridRow(dataGridRow);
                return;
            }

            DataGridCell dataGridCell = element as DataGridCell;
            if (dataGridCell != null)
            {
                ApplyDataGridCell(dataGridCell);
                return;
            }

            DataGridColumnHeader dataGridHeader = element as DataGridColumnHeader;
            if (dataGridHeader != null)
            {
                ApplyDataGridColumnHeader(dataGridHeader);
                return;
            }

            GridViewColumnHeader gridViewHeader = element as GridViewColumnHeader;
            if (gridViewHeader != null)
            {
                ApplyGridViewColumnHeader(gridViewHeader);
                return;
            }

            ListViewItem listViewItem = element as ListViewItem;
            if (listViewItem != null)
            {
                ApplyListViewItem(listViewItem);
                return;
            }

            TreeViewItem treeViewItem = element as TreeViewItem;
            if (treeViewItem != null)
            {
                ApplyTreeViewItem(treeViewItem);
                return;
            }

            ListBoxItem listBoxItem = element as ListBoxItem;
            if (listBoxItem != null)
            {
                ApplyListBoxItem(listBoxItem);
                return;
            }

            ToggleButton toggleButton = element as ToggleButton;
            if (toggleButton != null)
            {
                ApplyToggleButton(toggleButton);
                return;
            }

            ScrollViewer scrollViewer = element as ScrollViewer;
            if (scrollViewer != null
                && (FindAncestor<ListView>(scrollViewer) != null || FindAncestor<DataGrid>(scrollViewer) != null))
            {
                ApplyScrollViewer(scrollViewer);
                return;
            }

            Control control = element as Control;
            if (control != null)
            {
                ApplyControl(control);
            }
        }

        private static void ThemeWindow(Window window)
        {
            if (!BrushEquals(window.Background, BgBrush))
            {
                window.Background = BgBrush;
            }

            if (!BrushEquals(window.Foreground, TextBrush))
            {
                window.Foreground = TextBrush;
            }

            if (window.FontFamily == null || window.FontFamily.Source != "Ebrima")
            {
                window.FontFamily = UfsFont;
            }

            ApplyDarkTitleBar(window);
        }

        private static void ApplyTextBlock(TextBlock textBlock)
        {
            Brush foreground = NormalizeForeground(textBlock.Foreground, textBlock.IsEnabled);
            if (foreground != null && !BrushEquals(textBlock.Foreground, foreground))
            {
                textBlock.Foreground = foreground;
            }

            Brush background = NormalizeTextBackground(textBlock.Background);
            if (background != null && !BrushEquals(textBlock.Background, background))
            {
                textBlock.Background = background;
            }

            if (textBlock.FontFamily == null || textBlock.FontFamily.Source != "Ebrima")
            {
                textBlock.FontFamily = UfsFont;
            }
        }

        private static void ApplyLabel(Label label)
        {
            Brush foreground = NormalizeForeground(label.Foreground, label.IsEnabled);
            if (foreground != null && !BrushEquals(label.Foreground, foreground))
            {
                label.Foreground = foreground;
            }

            Brush background = NormalizeTextBackground(label.Background);
            if (background != null && !BrushEquals(label.Background, background))
            {
                label.Background = background;
            }

            if (label.FontFamily == null || label.FontFamily.Source != "Ebrima")
            {
                label.FontFamily = UfsFont;
            }
        }

        private static void ApplyBorder(Border border)
        {
            Thumb thumb = FindAncestor<Thumb>(border);
            Track track = FindAncestor<Track>(border);
            Brush background = null;

            if (thumb != null)
            {
                if (IsHeaderGripper(thumb))
                {
                    if (!BrushEquals(border.Background, TransparentBrush))
                    {
                        border.Background = TransparentBrush;
                    }

                    if (!BrushEquals(border.BorderBrush, TransparentBrush))
                    {
                        border.BorderBrush = TransparentBrush;
                    }

                    return;
                }

                if (IsLightOrBlue(border.Background))
                {
                    background = AccentBrush;
                }
            }
            else if (track != null)
            {
                if (IsLightOrBlue(border.Background))
                {
                    background = BlockBrush;
                }
            }
            else
            {
                background = NormalizeBackground(border.Background, border, false);
            }

            if (background != null && !BrushEquals(border.Background, background))
            {
                border.Background = background;
            }

            Brush borderBrush = NormalizeBorderBrush(border.BorderBrush);
            if (borderBrush != null && !BrushEquals(border.BorderBrush, borderBrush))
            {
                border.BorderBrush = borderBrush;
            }
        }

        private static void ApplyPanel(Panel panel)
        {
            Brush background = NormalizeBackground(panel.Background, panel, true);
            if (background != null && !BrushEquals(panel.Background, background))
            {
                panel.Background = background;
            }
        }

        private static void ApplyShape(Shape shape)
        {
            if (FindAncestor<Slider>(shape) != null)
            {
                return;
            }

            Thumb thumb = FindAncestor<Thumb>(shape);
            if (thumb != null)
            {
                if (IsHeaderGripper(thumb))
                {
                    if (shape.Fill != null)
                    {
                        shape.Fill = TransparentBrush;
                    }

                    if (shape.Stroke != null)
                    {
                        shape.Stroke = TransparentBrush;
                    }

                    return;
                }

                if (shape.Fill != null && !IsSemanticBrush(shape.Fill))
                {
                    shape.Fill = AccentBrush;
                }

                if (shape.Stroke != null && !IsSemanticBrush(shape.Stroke))
                {
                    shape.Stroke = AccentDarkBrush;
                }

                return;
            }

            ToggleButton toggleButton = FindAncestor<ToggleButton>(shape);
            if (toggleButton != null && !(toggleButton is CheckBox) && !(toggleButton is RadioButton))
            {
                if (FindAncestor<Expander>(shape) != null)
                {
                    if (shape is Ellipse)
                    {
                        if (shape.Fill != null && !IsSemanticBrush(shape.Fill))
                        {
                            shape.Fill = BlockBrush;
                        }

                        if (shape.Stroke != null && !IsSemanticBrush(shape.Stroke))
                        {
                            shape.Stroke = BorderBrush;
                        }

                        return;
                    }

                    if (shape.Stroke != null && !IsSemanticBrush(shape.Stroke))
                    {
                        shape.Stroke = AccentBrush;
                    }

                    return;
                }

                NormalizeShape(shape, TextBrush);
                return;
            }

            Button button = FindAncestor<Button>(shape);
            if (button != null)
            {
                NormalizeShape(shape, TextBrush);
            }
        }

        private static void NormalizeShape(Shape shape, Brush brush)
        {
            if (shape.Fill != null && !IsSemanticBrush(shape.Fill) && IsDarkOrBlue(shape.Fill))
            {
                shape.Fill = brush;
            }

            if (shape.Stroke != null && !IsSemanticBrush(shape.Stroke) && IsDarkOrBlue(shape.Stroke))
            {
                shape.Stroke = brush;
            }
        }

        private static void ApplyButton(Button button)
        {
            bool hasText = HasTextContent(button.Content);
            ControlTemplate template = GetTemplate(button, hasText ? "UfsActionButtonTemplate" : "UfsIconButtonTemplate");
            if (template != null && !HasTemplateSetter(button.Style) && button.Template != template)
            {
                button.Template = template;
            }

            if (hasText)
            {
                Brush background;
                Brush foreground;
                Brush borderBrush;

                if (!button.IsEnabled)
                {
                    background = BlockAltBrush;
                    foreground = DisabledBrush;
                    borderBrush = BorderBrush;
                }
                else if (IsSemanticBrush(button.Background))
                {
                    background = button.Background;
                    foreground = TextBrush;
                    borderBrush = BorderBrush;
                }
                else
                {
                    background = AccentBrush;
                    foreground = TextBrush;
                    borderBrush = AccentDarkBrush;
                }

                if (!BrushEquals(button.Background, background))
                {
                    button.Background = background;
                }

                if (!BrushEquals(button.Foreground, foreground))
                {
                    button.Foreground = foreground;
                }

                if (!BrushEquals(button.BorderBrush, borderBrush))
                {
                    button.BorderBrush = borderBrush;
                }

                if (button.BorderThickness.Left != 1 || button.BorderThickness.Top != 1)
                {
                    button.BorderThickness = new Thickness(1);
                }

                if (button.FontWeight != FontWeights.SemiBold)
                {
                    button.FontWeight = FontWeights.SemiBold;
                }
            }
            else
            {
                if (!BrushEquals(button.Background, TransparentBrush))
                {
                    button.Background = TransparentBrush;
                }

                if (!BrushEquals(button.BorderBrush, TransparentBrush))
                {
                    button.BorderBrush = TransparentBrush;
                }

                if (button.BorderThickness.Left != 0 || button.BorderThickness.Top != 0)
                {
                    button.BorderThickness = new Thickness(0);
                }

                if (!BrushEquals(button.Foreground, TextBrush))
                {
                    button.Foreground = TextBrush;
                }
            }

            if (button.FontFamily == null || button.FontFamily.Source != "Ebrima")
            {
                button.FontFamily = UfsFont;
            }
        }

        private static void ApplyCheckBox(CheckBox checkBox)
        {
            ControlTemplate template = GetTemplate(checkBox, "UfsCheckBoxTemplate");
            if (template != null && checkBox.Template != template)
            {
                checkBox.Template = template;
            }

            if (!BrushEquals(checkBox.Foreground, TextBrush))
            {
                checkBox.Foreground = TextBrush;
            }

            if (!BrushEquals(checkBox.Background, TransparentBrush))
            {
                checkBox.Background = TransparentBrush;
            }

            if (!BrushEquals(checkBox.BorderBrush, BorderBrush))
            {
                checkBox.BorderBrush = BorderBrush;
            }

            if (checkBox.FontFamily == null || checkBox.FontFamily.Source != "Ebrima")
            {
                checkBox.FontFamily = UfsFont;
            }
        }

        private static void ApplyRadioButton(RadioButton radioButton)
        {
            ControlTemplate template = GetTemplate(radioButton, "UfsRadioButtonTemplate");
            if (template != null && radioButton.Template != template)
            {
                radioButton.Template = template;
            }

            if (!BrushEquals(radioButton.Foreground, TextBrush))
            {
                radioButton.Foreground = TextBrush;
            }

            if (!BrushEquals(radioButton.Background, TransparentBrush))
            {
                radioButton.Background = TransparentBrush;
            }

            if (!BrushEquals(radioButton.BorderBrush, BorderBrush))
            {
                radioButton.BorderBrush = BorderBrush;
            }

            if (radioButton.FontFamily == null || radioButton.FontFamily.Source != "Ebrima")
            {
                radioButton.FontFamily = UfsFont;
            }
        }

        private static void ApplyTextBox(TextBoxBase textBox)
        {
            Brush background = InputBrush;
            Brush foreground = textBox.IsEnabled ? TextBrush : MutedBrush;

            if (!BrushEquals(textBox.Background, background))
            {
                textBox.Background = background;
            }

            if (!BrushEquals(textBox.Foreground, foreground))
            {
                textBox.Foreground = foreground;
            }

            if (!BrushEquals(textBox.BorderBrush, BorderBrush))
            {
                textBox.BorderBrush = BorderBrush;
            }

            if (!BrushEquals(textBox.CaretBrush, TextBrush))
            {
                textBox.CaretBrush = TextBrush;
            }

            if (!BrushEquals(textBox.SelectionBrush, AccentBrush))
            {
                textBox.SelectionBrush = AccentBrush;
            }

            if (textBox.FontFamily == null || textBox.FontFamily.Source != "Ebrima")
            {
                textBox.FontFamily = UfsFont;
            }
        }

        private static void ApplyPasswordBox(PasswordBox passwordBox)
        {
            Brush background = InputBrush;
            Brush foreground = passwordBox.IsEnabled ? TextBrush : MutedBrush;

            if (!BrushEquals(passwordBox.Background, background))
            {
                passwordBox.Background = background;
            }

            if (!BrushEquals(passwordBox.Foreground, foreground))
            {
                passwordBox.Foreground = foreground;
            }

            if (!BrushEquals(passwordBox.BorderBrush, BorderBrush))
            {
                passwordBox.BorderBrush = BorderBrush;
            }

            if (!BrushEquals(passwordBox.CaretBrush, TextBrush))
            {
                passwordBox.CaretBrush = TextBrush;
            }

            if (!BrushEquals(passwordBox.SelectionBrush, AccentBrush))
            {
                passwordBox.SelectionBrush = AccentBrush;
            }

            if (passwordBox.FontFamily == null || passwordBox.FontFamily.Source != "Ebrima")
            {
                passwordBox.FontFamily = UfsFont;
            }
        }

        private static void ApplyComboBox(ComboBox comboBox)
        {
            Brush background = InputBrush;
            Brush foreground = comboBox.IsEnabled ? TextBrush : MutedBrush;

            if (!BrushEquals(comboBox.Background, background))
            {
                comboBox.Background = background;
            }

            if (!BrushEquals(comboBox.Foreground, foreground))
            {
                comboBox.Foreground = foreground;
            }

            if (!BrushEquals(comboBox.BorderBrush, BorderBrush))
            {
                comboBox.BorderBrush = BorderBrush;
            }

            if (comboBox.FontFamily == null || comboBox.FontFamily.Source != "Ebrima")
            {
                comboBox.FontFamily = UfsFont;
            }
        }

        private static void ApplyDataGrid(DataGrid dataGrid)
        {
            if (!BrushEquals(dataGrid.Background, BgBrush))
            {
                dataGrid.Background = BgBrush;
            }

            if (!BrushEquals(dataGrid.Foreground, TextBrush))
            {
                dataGrid.Foreground = TextBrush;
            }

            if (!BrushEquals(dataGrid.BorderBrush, TableLineBrush))
            {
                dataGrid.BorderBrush = TableLineBrush;
            }

            if (!BrushEquals(dataGrid.RowBackground, TableRowBrush))
            {
                dataGrid.RowBackground = TableRowBrush;
            }

            if (!BrushEquals(dataGrid.AlternatingRowBackground, TableRowAltBrush))
            {
                dataGrid.AlternatingRowBackground = TableRowAltBrush;
            }

            if (!BrushEquals(dataGrid.HorizontalGridLinesBrush, TableLineBrush))
            {
                dataGrid.HorizontalGridLinesBrush = TableLineBrush;
            }

            if (!BrushEquals(dataGrid.VerticalGridLinesBrush, TableLineBrush))
            {
                dataGrid.VerticalGridLinesBrush = TableLineBrush;
            }

            if (dataGrid.FontFamily == null || dataGrid.FontFamily.Source != "Ebrima")
            {
                dataGrid.FontFamily = UfsFont;
            }
        }

        private static void ApplyListView(ListView listView)
        {
            if (!BrushEquals(listView.Background, BlockBrush))
            {
                listView.Background = BlockBrush;
            }

            if (!BrushEquals(listView.Foreground, TextBrush))
            {
                listView.Foreground = TextBrush;
            }

            if (!BrushEquals(listView.BorderBrush, BorderBrush))
            {
                listView.BorderBrush = BorderBrush;
            }

            if (listView.FontFamily == null || listView.FontFamily.Source != "Ebrima")
            {
                listView.FontFamily = UfsFont;
            }
        }

        private static void ApplyScrollViewer(ScrollViewer scrollViewer)
        {
            Brush target = FindAncestor<DataGrid>(scrollViewer) != null ? BgBrush : BlockBrush;
            if (scrollViewer.Background == null || IsLightOrBlue(scrollViewer.Background))
            {
                scrollViewer.Background = target;
            }
        }

        private static void ApplyTabControl(TabControl tabControl)
        {
            if (!BrushEquals(tabControl.Background, BgBrush))
            {
                tabControl.Background = BgBrush;
            }

            if (!BrushEquals(tabControl.Foreground, TextBrush))
            {
                tabControl.Foreground = TextBrush;
            }

            if (!BrushEquals(tabControl.BorderBrush, BorderBrush))
            {
                tabControl.BorderBrush = BorderBrush;
            }

            if (tabControl.FontFamily == null || tabControl.FontFamily.Source != "Ebrima")
            {
                tabControl.FontFamily = UfsFont;
            }
        }

        private static void ApplyTabItem(TabItem tabItem)
        {
            Brush background = tabItem.IsSelected ? BlockAltBrush : BlockBrush;
            Brush borderBrush = tabItem.IsSelected ? AccentBrush : BorderBrush;

            if (!BrushEquals(tabItem.Background, background))
            {
                tabItem.Background = background;
            }

            if (!BrushEquals(tabItem.Foreground, TextBrush))
            {
                tabItem.Foreground = TextBrush;
            }

            if (!BrushEquals(tabItem.BorderBrush, borderBrush))
            {
                tabItem.BorderBrush = borderBrush;
            }

            if (tabItem.FontFamily == null || tabItem.FontFamily.Source != "Ebrima")
            {
                tabItem.FontFamily = UfsFont;
            }
        }

        private static void ApplyGroupBox(GroupBox groupBox)
        {
            if (!BrushEquals(groupBox.Background, BlockBrush))
            {
                groupBox.Background = BlockBrush;
            }

            if (!BrushEquals(groupBox.Foreground, TextBrush))
            {
                groupBox.Foreground = TextBrush;
            }

            if (!BrushEquals(groupBox.BorderBrush, BorderBrush))
            {
                groupBox.BorderBrush = BorderBrush;
            }

            if (groupBox.BorderThickness.Left != 1 || groupBox.BorderThickness.Top != 1)
            {
                groupBox.BorderThickness = new Thickness(1);
            }

            if (groupBox.FontFamily == null || groupBox.FontFamily.Source != "Ebrima")
            {
                groupBox.FontFamily = UfsFont;
            }
        }

        private static void ApplyExpander(Expander expander)
        {
            if (!BrushEquals(expander.Foreground, TextBrush))
            {
                expander.Foreground = TextBrush;
            }

            if (!BrushEquals(expander.Background, TransparentBrush))
            {
                expander.Background = TransparentBrush;
            }

            if (!BrushEquals(expander.BorderBrush, TransparentBrush))
            {
                expander.BorderBrush = TransparentBrush;
            }

            if (expander.FontFamily == null || expander.FontFamily.Source != "Ebrima")
            {
                expander.FontFamily = UfsFont;
            }

            if (!IsInsideServerSettings(expander))
            {
                return;
            }

            object marker;
            if (ExpandedExpanders.TryGetValue(expander, out marker))
            {
                return;
            }

            ExpandedExpanders.Add(expander, null);

            if (expander.IsExpanded)
            {
                return;
            }

            expander.SetCurrentValue(Expander.IsExpandedProperty, true);
            expander.UpdateLayout();
            Walk(expander);
        }

        private static bool IsInsideServerSettings(DependencyObject element)
        {
            DependencyObject current = element;

            while (current != null)
            {
                FrameworkElement frameworkElement = current as FrameworkElement;
                if (frameworkElement != null && frameworkElement.GetType().Name == "ServerSettingsControl")
                {
                    return true;
                }

                try
                {
                    current = VisualTreeHelper.GetParent(current);
                }
                catch
                {
                    return false;
                }
            }

            return false;
        }

        private static void ApplyToolTip(ToolTip toolTip)
        {
            if (!BrushEquals(toolTip.Background, BlockBrush))
            {
                toolTip.Background = BlockBrush;
            }

            if (!BrushEquals(toolTip.Foreground, TextBrush))
            {
                toolTip.Foreground = TextBrush;
            }

            if (!BrushEquals(toolTip.BorderBrush, AccentBrush))
            {
                toolTip.BorderBrush = AccentBrush;
            }

            if (toolTip.FontFamily == null || toolTip.FontFamily.Source != "Ebrima")
            {
                toolTip.FontFamily = UfsFont;
            }
        }

        private static void ApplyStatusBar(StatusBar statusBar)
        {
            if (!BrushEquals(statusBar.Background, BlockAltBrush))
            {
                statusBar.Background = BlockAltBrush;
            }

            if (!BrushEquals(statusBar.Foreground, TextBrush))
            {
                statusBar.Foreground = TextBrush;
            }

            if (statusBar.FontFamily == null || statusBar.FontFamily.Source != "Ebrima")
            {
                statusBar.FontFamily = UfsFont;
            }
        }

        private static void ApplyProgressBar(ProgressBar progressBar)
        {
            if (!BrushEquals(progressBar.Background, BlockBrush))
            {
                progressBar.Background = BlockBrush;
            }

            if (!BrushEquals(progressBar.Foreground, AccentBrush))
            {
                progressBar.Foreground = AccentBrush;
            }

            if (!BrushEquals(progressBar.BorderBrush, BorderBrush))
            {
                progressBar.BorderBrush = BorderBrush;
            }
        }

        private static void ApplySlider(Slider slider)
        {
            if (!BrushEquals(slider.Foreground, AccentBrush))
            {
                slider.Foreground = AccentBrush;
            }

            if (!BrushEquals(slider.Background, BlockBrush))
            {
                slider.Background = BlockBrush;
            }

            if (!BrushEquals(slider.BorderBrush, BorderBrush))
            {
                slider.BorderBrush = BorderBrush;
            }

            if (slider.Orientation == Orientation.Horizontal)
            {
                ControlTemplate template = GetTemplate(slider, "UfsSliderTemplate");
                if (template != null && slider.Template != template)
                {
                    slider.Template = template;
                }
            }
        }

        private static void ApplyScrollBar(ScrollBar scrollBar)
        {
            if (!BrushEquals(scrollBar.Background, BlockAltBrush))
            {
                scrollBar.Background = BlockAltBrush;
            }

            if (!BrushEquals(scrollBar.Foreground, AccentBrush))
            {
                scrollBar.Foreground = AccentBrush;
            }

            if (!BrushEquals(scrollBar.BorderBrush, BorderBrush))
            {
                scrollBar.BorderBrush = BorderBrush;
            }
        }

        private static bool IsHeaderGripper(Thumb thumb)
        {
            if (thumb == null)
            {
                return false;
            }

            if (thumb.Name == "PART_LeftHeaderGripper" || thumb.Name == "PART_RightHeaderGripper")
            {
                return true;
            }

            return thumb.TemplatedParent is DataGridColumnHeader || thumb.TemplatedParent is GridViewColumnHeader;
        }

        private static void ApplyThumb(Thumb thumb)
        {
            if (IsHeaderGripper(thumb))
            {
                if (!BrushEquals(thumb.Background, TransparentBrush))
                {
                    thumb.Background = TransparentBrush;
                }

                if (!BrushEquals(thumb.BorderBrush, TransparentBrush))
                {
                    thumb.BorderBrush = TransparentBrush;
                }

                return;
            }

            if (!BrushEquals(thumb.Background, AccentBrush))
            {
                thumb.Background = AccentBrush;
            }

            if (!BrushEquals(thumb.BorderBrush, AccentDarkBrush))
            {
                thumb.BorderBrush = AccentDarkBrush;
            }
        }

        private static void ApplyRepeatButton(RepeatButton repeatButton)
        {
            if (IsLightOrBlue(repeatButton.Background))
            {
                if (!BrushEquals(repeatButton.Background, BlockAltBrush))
                {
                    repeatButton.Background = BlockAltBrush;
                }
            }

            if (NormalizeBorderBrush(repeatButton.BorderBrush) != null
                && !BrushEquals(repeatButton.BorderBrush, BorderBrush))
            {
                repeatButton.BorderBrush = BorderBrush;
            }
        }

        private static void ApplyMenuItem(MenuItem menuItem)
        {
            if (IsLightOrBlue(menuItem.Background))
            {
                if (!BrushEquals(menuItem.Background, BlockBrush))
                {
                    menuItem.Background = BlockBrush;
                }
            }

            Brush foreground = NormalizeForeground(menuItem.Foreground, menuItem.IsEnabled);
            if (foreground != null && !BrushEquals(menuItem.Foreground, foreground))
            {
                menuItem.Foreground = foreground;
            }
        }

        private static void ApplyListBoxItem(ListBoxItem item)
        {
            if (!(item is ComboBoxItem))
            {
                ControlTemplate template = GetTemplate(item, "UfsListItemTemplate");
                if (template != null && item.Template != template)
                {
                    item.Template = template;
                }
            }

            Brush foreground = NormalizeForeground(item.Foreground, item.IsEnabled);
            if (foreground != null && !BrushEquals(item.Foreground, foreground))
            {
                item.Foreground = foreground;
            }

            if (!item.IsSelected && IsLightOrBlue(item.Background))
            {
                if (!BrushEquals(item.Background, TransparentBrush))
                {
                    item.Background = TransparentBrush;
                }
            }
        }

        private static void ApplyListViewItem(ListViewItem item)
        {
            ListView listView = ItemsControl.ItemsControlFromItemContainer(item) as ListView;
            if (listView == null || !(listView.View is GridView))
            {
                ApplyListBoxItem(item);
                return;
            }

            ControlTemplate template = GetTemplate(item, "UfsGridViewItemTemplate");
            if (template != null && item.Template != template)
            {
                item.Template = template;
            }

            if (item.HorizontalContentAlignment != HorizontalAlignment.Stretch)
            {
                item.HorizontalContentAlignment = HorizontalAlignment.Stretch;
            }

            Brush foreground = NormalizeForeground(item.Foreground, item.IsEnabled);
            if (foreground != null && !BrushEquals(item.Foreground, foreground))
            {
                item.Foreground = foreground;
            }

            Brush background = item.Background;
            if (!BrushEquals(background, TableHighlightBrush))
            {
                Brush target;
                if (IsBeigeTone(background))
                {
                    target = TableHighlightBrush;
                }
                else
                {
                    int index = -1;
                    try
                    {
                        index = listView.ItemContainerGenerator.IndexFromContainer(item);
                    }
                    catch
                    {
                    }

                    target = index >= 0 && index % 2 == 1 ? TableRowAltBrush : TableRowBrush;
                }

                if (!BrushEquals(background, target))
                {
                    item.Background = target;
                }
            }
        }

        private static void ApplyTreeViewItem(TreeViewItem item)
        {
            Brush foreground = NormalizeForeground(item.Foreground, item.IsEnabled);
            if (foreground != null && !BrushEquals(item.Foreground, foreground))
            {
                item.Foreground = foreground;
            }

            if (!item.IsSelected && IsLightOrBlue(item.Background))
            {
                if (!BrushEquals(item.Background, TransparentBrush))
                {
                    item.Background = TransparentBrush;
                }
            }
        }

        private static void ApplyDataGridColumnHeader(DataGridColumnHeader header)
        {
            if (!BrushEquals(header.Background, TableHeaderBrush))
            {
                header.Background = TableHeaderBrush;
            }

            if (!BrushEquals(header.Foreground, AccentBrush))
            {
                header.Foreground = AccentBrush;
            }

            if (!BrushEquals(header.BorderBrush, TableLineBrush))
            {
                header.BorderBrush = TableLineBrush;
            }

            if (header.BorderThickness != new Thickness(0))
            {
                header.BorderThickness = new Thickness(0);
            }

            Thickness padding = new Thickness(8, 6, 8, 6);
            if (header.Padding != padding)
            {
                header.Padding = padding;
            }

            if (header.FontWeight != FontWeights.SemiBold)
            {
                header.FontWeight = FontWeights.SemiBold;
            }

            if (header.FontFamily == null || header.FontFamily.Source != "Ebrima")
            {
                header.FontFamily = UfsFont;
            }

            ControlTemplate template = GetTemplate(header, "UfsDataGridColumnHeaderTemplate");
            if (template != null && header.Template != template)
            {
                header.Template = template;
            }

            TextBlock content = header.Content as TextBlock;
            if (content != null)
            {
                if (!BrushEquals(content.Foreground, AccentBrush))
                {
                    content.Foreground = AccentBrush;
                }

                if (content.FontWeight != FontWeights.SemiBold)
                {
                    content.FontWeight = FontWeights.SemiBold;
                }
            }
        }

        private static void ApplyGridViewColumnHeader(GridViewColumnHeader header)
        {
            if (!BrushEquals(header.Background, TableHeaderBrush))
            {
                header.Background = TableHeaderBrush;
            }

            if (!BrushEquals(header.Foreground, AccentBrush))
            {
                header.Foreground = AccentBrush;
            }

            if (!BrushEquals(header.BorderBrush, AccentBrush))
            {
                header.BorderBrush = AccentBrush;
            }

            if (header.BorderThickness != new Thickness(0, 0, 0, 2))
            {
                header.BorderThickness = new Thickness(0, 0, 0, 2);
            }

            Thickness padding = new Thickness(8, 6, 8, 6);
            if (header.Padding != padding)
            {
                header.Padding = padding;
            }

            if (header.FontWeight != FontWeights.SemiBold)
            {
                header.FontWeight = FontWeights.SemiBold;
            }

            if (header.FontFamily == null || header.FontFamily.Source != "Ebrima")
            {
                header.FontFamily = UfsFont;
            }

            TextBlock content = header.Content as TextBlock;
            if (content != null)
            {
                if (!BrushEquals(content.Foreground, AccentBrush))
                {
                    content.Foreground = AccentBrush;
                }

                if (content.FontWeight != FontWeights.SemiBold)
                {
                    content.FontWeight = FontWeights.SemiBold;
                }
            }
        }

        private static void ApplyDataGridRow(DataGridRow row)
        {
            Brush foreground = NormalizeForeground(row.Foreground, row.IsEnabled);
            if (foreground != null && !BrushEquals(row.Foreground, foreground))
            {
                row.Foreground = foreground;
            }

            row.Selected -= OnDataGridRowSelected;
            row.Selected += OnDataGridRowSelected;
            row.Unselected -= OnDataGridRowUnselected;
            row.Unselected += OnDataGridRowUnselected;

            if (row.IsSelected)
            {
                InvalidateDataGridRowSelection(row);
            }
            else
            {
                Brush background = DataGridRowBackground(row);
                if (!BrushEquals(row.Background, background))
                {
                    row.Background = background;
                }
            }
        }

        private static Brush DataGridRowBackground(DataGridRow row)
        {
            var alternation = 0;
            object index = row.GetValue(ItemsControl.AlternationIndexProperty);
            if (index is int)
            {
                alternation = (int)index;
            }

            if (alternation % 2 == 1 && TableRowAltBrush != null)
            {
                return TableRowAltBrush;
            }

            return TableRowBrush;
        }

        private static void OnDataGridRowSelected(object sender, RoutedEventArgs e)
        {
            InvalidateDataGridRowSelection(sender as DataGridRow);
        }

        private static void OnDataGridRowUnselected(object sender, RoutedEventArgs e)
        {
            DataGridRow row = sender as DataGridRow;
            if (row == null)
            {
                return;
            }

            row.Background = DataGridRowBackground(row);
            row.BorderBrush = TableLineBrush ?? BorderBrush;
            row.BorderThickness = new Thickness(0, 0, 0, 1);
        }

        private static void InvalidateDataGridRowSelection(DataGridRow row)
        {
            if (row == null)
            {
                return;
            }

            if (TableSelectionBrush != null)
            {
                row.Background = TableSelectionBrush;
            }

            if (WhiteBrush != null)
            {
                row.Foreground = WhiteBrush;
            }

            if (AccentBrush != null)
            {
                row.BorderBrush = AccentBrush;
            }

            row.BorderThickness = new Thickness(2, 0, 0, 1);
        }

        private static void ApplyDataGridCell(DataGridCell cell)
        {
            Brush foreground = NormalizeForeground(cell.Foreground, cell.IsEnabled);
            if (foreground != null && !BrushEquals(cell.Foreground, foreground))
            {
                cell.Foreground = foreground;
            }

            if (cell.IsSelected)
            {
                if (TableSelectionBrush != null && !BrushEquals(cell.Background, TableSelectionBrush))
                {
                    cell.Background = TableSelectionBrush;
                }

                if (WhiteBrush != null && !BrushEquals(cell.Foreground, WhiteBrush))
                {
                    cell.Foreground = WhiteBrush;
                }
            }
            else
            {
                if (BrushEquals(cell.Background, TableSelectionBrush)
                    || IsLightOrBlue(cell.Background)
                    || IsAccentTone(cell.Background))
                {
                    cell.Background = TransparentBrush;
                }
            }
        }

        private static bool IsAccentTone(Brush brush)
        {
            SolidColorBrush solid = brush as SolidColorBrush;
            if (solid == null)
            {
                return false;
            }

            Color color = solid.Color;
            return color.A >= 32
                && color.R > 200
                && color.G >= 90
                && color.G <= 150
                && color.B < 50;
        }

        private static bool IsBeigeTone(Brush brush)
        {
            SolidColorBrush solid = brush as SolidColorBrush;
            if (solid == null)
            {
                return false;
            }

            Color color = solid.Color;
            return color.A >= 32
                && color.R > 225
                && color.G > 225
                && color.B >= 180
                && color.B < 235;
        }

        private static void ApplyToggleButton(ToggleButton toggleButton)
        {
            if (IsLightOrBlue(toggleButton.Background))
            {
                if (!BrushEquals(toggleButton.Background, BlockBrush))
                {
                    toggleButton.Background = BlockBrush;
                }
            }

            Brush foreground = NormalizeForeground(toggleButton.Foreground, toggleButton.IsEnabled);
            if (foreground != null && !BrushEquals(toggleButton.Foreground, foreground))
            {
                toggleButton.Foreground = foreground;
            }

            if (NormalizeBorderBrush(toggleButton.BorderBrush) != null
                && !BrushEquals(toggleButton.BorderBrush, BorderBrush))
            {
                toggleButton.BorderBrush = BorderBrush;
            }

            if (toggleButton.FontFamily == null || toggleButton.FontFamily.Source != "Ebrima")
            {
                toggleButton.FontFamily = UfsFont;
            }
        }

        private static void ApplyControl(Control control)
        {
            if (IsLightOrBlue(control.Background))
            {
                if (!BrushEquals(control.Background, BlockBrush))
                {
                    control.Background = BlockBrush;
                }
            }

            Brush foreground = NormalizeForeground(control.Foreground, control.IsEnabled);
            if (foreground != null && !BrushEquals(control.Foreground, foreground))
            {
                control.Foreground = foreground;
            }

            if (NormalizeBorderBrush(control.BorderBrush) != null
                && !BrushEquals(control.BorderBrush, BorderBrush))
            {
                control.BorderBrush = BorderBrush;
            }

            if (control.FontFamily == null || control.FontFamily.Source != "Ebrima")
            {
                control.FontFamily = UfsFont;
            }
        }

        private static Brush NormalizeForeground(Brush brush, bool enabled)
        {
            if (!enabled)
            {
                return MutedBrush;
            }

            SolidColorBrush solid = brush as SolidColorBrush;
            if (solid == null)
            {
                return TextBrush;
            }

            Color color = solid.Color;
            if (color.A < 32)
            {
                return TextBrush;
            }

            if (IsBlue(color))
            {
                return TextBrush;
            }

            if (IsSemanticColor(color))
            {
                if (color.R > 180 && color.G < 140 && color.B < 140)
                {
                    return RedBrush;
                }

                if (color.G > 160 && color.R < 180 && color.B < 180)
                {
                    return GreenBrush;
                }

                if (color.R > 180 && color.G > 100 && color.B < 120)
                {
                    return OrangeBrush;
                }

                if (color.R > 140 && color.B > 180)
                {
                    return PurpleBrush;
                }

                if (color.R > 180 && color.G > 180 && color.B < 150)
                {
                    return YellowBrush;
                }

                return Luminance(color) < 0.55 ? TextBrush : null;
            }

            if (Luminance(color) < 0.75)
            {
                return TextBrush;
            }

            return null;
        }

        private static Brush NormalizeTextBackground(Brush brush)
        {
            if (!IsLightOrBlue(brush))
            {
                return null;
            }

            return TransparentBrush;
        }

        private static Brush NormalizeBackground(Brush brush, FrameworkElement element, bool panel)
        {
            if (brush == null)
            {
                return null;
            }

            SolidColorBrush solid = brush as SolidColorBrush;
            if (solid != null)
            {
                Color color = solid.Color;
                if (color.A < 32)
                {
                    return null;
                }

                if (IsBlue(color))
                {
                    return panel ? PanelBrush(element) : BlockBrush;
                }

                if (IsSemanticColor(color))
                {
                    return null;
                }

                if (Luminance(color) > 0.55)
                {
                    return panel ? PanelBrush(element) : BlockBrush;
                }

                return null;
            }

            GradientBrush gradient = brush as GradientBrush;
            if (gradient != null && gradient.GradientStops.Count > 0)
            {
                double total = 0;
                foreach (GradientStop stop in gradient.GradientStops)
                {
                    total += Luminance(stop.Color);
                }

                if (total / gradient.GradientStops.Count > 0.55)
                {
                    return panel ? PanelBrush(element) : BlockBrush;
                }
            }

            return null;
        }

        private static Brush PanelBrush(FrameworkElement element)
        {
            if (FindAncestor<GroupBox>(element) != null || FindAncestor<Expander>(element) != null)
            {
                return BlockBrush;
            }

            return BgBrush;
        }

        private static Brush NormalizeBorderBrush(Brush brush)
        {
            SolidColorBrush solid = brush as SolidColorBrush;
            if (solid == null)
            {
                return null;
            }

            Color color = solid.Color;
            if (color.A < 32)
            {
                return null;
            }

            if (IsSemanticColor(color))
            {
                return null;
            }

            if (IsBlue(color))
            {
                return BorderBrush;
            }

            double luminance = Luminance(color);
            if (luminance > 0.55 || luminance < 0.12)
            {
                return BorderBrush;
            }

            return null;
        }

        private static bool IsLightOrBlue(Brush brush)
        {
            SolidColorBrush solid = brush as SolidColorBrush;
            if (solid != null)
            {
                Color color = solid.Color;
                if (color.A < 32)
                {
                    return false;
                }

                if (IsBlue(color))
                {
                    return true;
                }

                if (IsSemanticColor(color))
                {
                    return false;
                }

                return Luminance(color) > 0.55;
            }

            GradientBrush gradient = brush as GradientBrush;
            if (gradient != null && gradient.GradientStops.Count > 0)
            {
                double total = 0;
                foreach (GradientStop stop in gradient.GradientStops)
                {
                    total += Luminance(stop.Color);
                }

                return total / gradient.GradientStops.Count > 0.55;
            }

            return false;
        }

        private static bool IsSemanticBrush(Brush brush)
        {
            SolidColorBrush solid = brush as SolidColorBrush;
            return solid != null && IsSemanticColor(solid.Color);
        }

        private static bool IsDarkOrBlue(Brush brush)
        {
            SolidColorBrush solid = brush as SolidColorBrush;
            if (solid == null)
            {
                return false;
            }

            Color color = solid.Color;
            if (color.A < 32)
            {
                return false;
            }

            return IsBlue(color) || Luminance(color) < 0.35;
        }

        private static bool IsSemanticColor(Color color)
        {
            if (color.A < 32)
            {
                return false;
            }

            double saturation = Saturation(color);
            if (saturation < 0.45)
            {
                return false;
            }

            if (IsBlue(color))
            {
                return false;
            }

            return true;
        }

        private static bool IsBlue(Color color)
        {
            if (color.A < 32)
            {
                return false;
            }

            int max = Math.Max(color.R, Math.Max(color.G, color.B));
            int min = Math.Min(color.R, Math.Min(color.G, color.B));
            if (max == 0 || (double)(max - min) / max < 0.20)
            {
                return false;
            }

            return color.B > color.R + 20 && color.B > color.G + 10;
        }

        private static bool HasTextContent(object content)
        {
            if (content == null)
            {
                return false;
            }

            string text = content as string;
            if (text != null)
            {
                return !string.IsNullOrWhiteSpace(text);
            }

            TextBlock textBlock = content as TextBlock;
            if (textBlock != null)
            {
                return !string.IsNullOrWhiteSpace(textBlock.Text);
            }

            Label label = content as Label;
            if (label != null)
            {
                return HasTextContent(label.Content);
            }

            ContentControl contentControl = content as ContentControl;
            if (contentControl != null)
            {
                return HasTextContent(contentControl.Content);
            }

            Panel panel = content as Panel;
            if (panel != null)
            {
                foreach (UIElement child in panel.Children)
                {
                    if (child != null && HasTextContent(child))
                    {
                        return true;
                    }
                }

                return false;
            }

            FrameworkElement element = content as FrameworkElement;
            if (element != null)
            {
                foreach (object child in LogicalTreeHelper.GetChildren(element))
                {
                    if (HasTextContent(child))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static ControlTemplate GetTemplate(FrameworkElement element, string key)
        {
            ControlTemplate template = element.TryFindResource(key) as ControlTemplate;
            if (template == null && _appTheme != null)
            {
                template = _appTheme[key] as ControlTemplate;
            }

            return template;
        }

        private static bool HasTemplateSetter(Style style)
        {
            if (style == null)
            {
                return false;
            }

            foreach (SetterBase setterBase in style.Setters)
            {
                Setter setter = setterBase as Setter;
                if (setter != null && setter.Property == Control.TemplateProperty && setter.Value is ControlTemplate)
                {
                    return true;
                }
            }

            return false;
        }

        private static T FindAncestor<T>(DependencyObject child) where T : DependencyObject
        {
            DependencyObject current = child;
            while (current != null)
            {
                T typed = current as T;
                if (typed != null)
                {
                    return typed;
                }

                current = VisualTreeHelper.GetParent(current);
            }

            return null;
        }

        private static void ApplyDarkTitleBar(Window window)
        {
            try
            {
                WindowInteropHelper helper = new WindowInteropHelper(window);
                IntPtr handle = helper.Handle;
                if (handle == IntPtr.Zero)
                {
                    handle = helper.EnsureHandle();
                }

                if (handle == IntPtr.Zero)
                {
                    return;
                }

                int enabled = 1;
                if (DwmSetWindowAttribute(handle, DwmwaUseImmersiveDarkMode, ref enabled, sizeof(int)) != 0)
                {
                    DwmSetWindowAttribute(handle, DwmwaUseImmersiveDarkModeBefore20H1, ref enabled, sizeof(int));
                }
            }
            catch
            {
            }
        }

        private static bool BrushEquals(Brush first, Brush second)
        {
            if (ReferenceEquals(first, second))
            {
                return true;
            }

            if (first == null || second == null)
            {
                return false;
            }

            SolidColorBrush firstSolid = first as SolidColorBrush;
            SolidColorBrush secondSolid = second as SolidColorBrush;
            if (firstSolid != null && secondSolid != null)
            {
                return firstSolid.Color == secondSolid.Color;
            }

            return false;
        }

        private static double Luminance(Color color)
        {
            return (0.299 * color.R + 0.587 * color.G + 0.114 * color.B) / 255.0;
        }

        private static double Saturation(Color color)
        {
            int max = Math.Max(color.R, Math.Max(color.G, color.B));
            int min = Math.Min(color.R, Math.Min(color.G, color.B));
            if (max == 0)
            {
                return 0;
            }

            return (double)(max - min) / max;
        }
    }
}
