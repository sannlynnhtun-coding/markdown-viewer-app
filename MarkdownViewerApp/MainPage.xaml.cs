using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage.Pickers;
using Windows.System;
using Windows.UI.Text;
using WinRT.Interop;

namespace MarkdownViewerApp;

public sealed partial class MainPage : Page
{
    private static readonly Regex LinkPattern = new(@"\[([^\]]+)\]\(([^)]+)\)", RegexOptions.Compiled);
    private static readonly FontFamily PreviewFontFamily = new("Myanmar Text, Segoe UI");
    private static readonly FontFamily CodeFontFamily = new("Myanmar Text, Consolas");
    private static readonly SolidColorBrush SearchMatchBrush = new(Colors.OrangeRed);
    private readonly List<MarkdownFile> _allFiles = new();
    private readonly ObservableCollection<MarkdownFile> _files = new();
    private MarkdownFile? _currentFile;
    private bool _hasUnsavedChanges;
    private bool _isEditing;
    private bool _isLoadingFile;
    private bool _isChangingSelection;
    private bool _isPreviewControlKeyDown;
    private bool _isPreviewSelectAllActive;
    private string _searchQuery = "";

    public MainPage()
    {
        InitializeComponent();
#if DEBUG
        VerifyTableParser();
        VerifySearchMatching();
#endif
        FilesList.ItemsSource = _files;
        PreviewScrollViewer.AddHandler(
            PointerPressedEvent,
            new PointerEventHandler(PreviewScrollViewer_PointerPressed),
            handledEventsToo: true);
        PreviewScrollViewer.AddHandler(
            KeyDownEvent,
            new KeyEventHandler(PreviewScrollViewer_KeyDown),
            handledEventsToo: true);
        PreviewScrollViewer.AddHandler(
            KeyUpEvent,
            new KeyEventHandler(PreviewScrollViewer_KeyUp),
            handledEventsToo: true);
        RenderPreview();
        UpdateUiState("Choose a folder to list .md files.");
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        if (e.Parameter is not string filePath || string.IsNullOrWhiteSpace(filePath))
        {
            return;
        }

        try
        {
            await OpenFileFromShellAsync(filePath);
        }
        catch (Exception ex)
        {
            await ShowMessageAsync("Open failed", ex.Message);
        }
    }

    private async void ChooseFolder_Click(object sender, RoutedEventArgs e)
    {
        if (_hasUnsavedChanges)
        {
            await ShowMessageAsync("Unsaved changes", "Save or revert the current file before choosing another folder.");
            return;
        }

        if (App.MainWindow is null)
        {
            await ShowMessageAsync("Window not ready", "Try again after the app finishes opening.");
            return;
        }

        FolderPicker picker;
        try
        {
            picker = new FolderPicker
            {
                SuggestedStartLocation = PickerLocationId.DocumentsLibrary
            };
            picker.FileTypeFilter.Add("*");
            InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(App.MainWindow));
        }
        catch (Exception ex)
        {
            await ShowMessageAsync("Folder picker unavailable", ex.Message);
            return;
        }

        Windows.Storage.StorageFolder? folder;
        try
        {
            folder = await picker.PickSingleFolderAsync();
        }
        catch (Exception ex)
        {
            await ShowMessageAsync("Folder picker unavailable", ex.Message);
            return;
        }

        if (folder is null)
        {
            return;
        }

        try
        {
            await LoadFolderAsync(folder.Path);
        }
        catch (Exception ex)
        {
            await ShowMessageAsync("Folder load failed", ex.Message);
            return;
        }

        if (_files.Count > 0)
        {
            _isChangingSelection = true;
            FilesList.SelectedItem = _files[0];
            _isChangingSelection = false;
            await LoadFileAsync(_files[0]);
        }
    }

    private async void FilesList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isChangingSelection || FilesList.SelectedItem is not MarkdownFile selectedFile)
        {
            return;
        }

        if (_hasUnsavedChanges && !Equals(selectedFile, _currentFile))
        {
            _isChangingSelection = true;
            FilesList.SelectedItem = _currentFile;
            _isChangingSelection = false;
            await ShowMessageAsync("Unsaved changes", "Save or revert the current file before opening another file.");
            return;
        }

        if (!Equals(selectedFile, _currentFile))
        {
            await LoadFileAsync(selectedFile);
        }
    }

    private void Edit_Click(object sender, RoutedEventArgs e)
    {
        if (_currentFile is null)
        {
            return;
        }

        _isEditing = true;
        ContentTabs.SelectedIndex = 1;
        EditorBox.Focus(FocusState.Programmatic);
        UpdateUiState();
    }

    private void EditorBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isLoadingFile || !_isEditing || _currentFile is null)
        {
            return;
        }

        _hasUnsavedChanges = true;
        _currentFile.Content = EditorBox.Text;
        ApplySearch();
        RenderPreview(EditorBox.Text);
        UpdateUiState();
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        _searchQuery = SearchBox.Text.Trim();
        ApplySearch();
        RenderPreview();
        UpdateUiState(EmptyFilesMessage());
    }

    private void ToggleSidebar_Click(object sender, RoutedEventArgs e)
    {
        FilesSplitView.IsPaneOpen = !FilesSplitView.IsPaneOpen;
    }

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        if (_currentFile is null || !_isEditing)
        {
            return;
        }

        if (!_hasUnsavedChanges)
        {
            _isEditing = false;
            ContentTabs.SelectedIndex = 0;
            UpdateUiState();
            return;
        }

        var result = await new ContentDialog
        {
            Title = "Save changes?",
            Content = $"Overwrite {_currentFile.Name}?",
            PrimaryButtonText = "Save",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot
        }.ShowAsync();

        if (result != ContentDialogResult.Primary)
        {
            return;
        }

        try
        {
            await File.WriteAllTextAsync(_currentFile.FullPath, EditorBox.Text, Encoding.UTF8);
            _currentFile.Content = EditorBox.Text;
            _hasUnsavedChanges = false;
            _isEditing = false;
            ContentTabs.SelectedIndex = 0;
            ApplySearch();
            RenderPreview(EditorBox.Text);
            UpdateUiState();
        }
        catch (Exception ex)
        {
            await ShowMessageAsync("Save failed", ex.Message);
        }
    }

    private async void Revert_Click(object sender, RoutedEventArgs e)
    {
        if (_currentFile is not null)
        {
            _isEditing = false;
            ContentTabs.SelectedIndex = 0;
            await LoadFileAsync(_currentFile);
        }
    }

    private async Task LoadFolderAsync(string folderPath)
    {
        FolderPathText.Text = folderPath;
        _currentFile = null;
        _hasUnsavedChanges = false;
        _isEditing = false;
        _searchQuery = "";
        _isLoadingFile = true;
        EditorBox.Text = "";
        SearchBox.Text = "";
        _isLoadingFile = false;

        _allFiles.Clear();
        _files.Clear();
        foreach (var file in Directory.EnumerateFiles(folderPath, "*.md", SearchOption.AllDirectories)
                     .OrderBy(file => Path.GetRelativePath(folderPath, file), StringComparer.CurrentCultureIgnoreCase))
        {
            _allFiles.Add(new MarkdownFile(
                Path.GetFileName(file),
                Path.GetRelativePath(folderPath, file),
                file,
                await File.ReadAllTextAsync(file, Encoding.UTF8)));
        }

        ApplySearch();
        ContentTabs.SelectedIndex = 0;
        RenderPreview();
        UpdateUiState(EmptyFilesMessage());
    }

    private async Task OpenFileFromShellAsync(string filePath)
    {
        if (!string.Equals(Path.GetExtension(filePath), ".md", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Markdown Viewer can only open .md files.");
        }

        var fullPath = Path.GetFullPath(filePath);
        var folderPath = Path.GetDirectoryName(fullPath);
        if (string.IsNullOrWhiteSpace(folderPath))
        {
            throw new InvalidOperationException("The file's containing folder could not be found.");
        }

        await LoadFolderAsync(folderPath);

        var file = _allFiles.FirstOrDefault(candidate =>
            string.Equals(candidate.FullPath, fullPath, StringComparison.OrdinalIgnoreCase));
        if (file is null)
        {
            throw new FileNotFoundException("The selected Markdown file could not be found.", fullPath);
        }

        _isChangingSelection = true;
        FilesList.SelectedItem = file;
        _isChangingSelection = false;
        await LoadFileAsync(file);
    }

    private async Task LoadFileAsync(MarkdownFile file)
    {
        string markdown;
        try
        {
            markdown = await File.ReadAllTextAsync(file.FullPath, Encoding.UTF8);
        }
        catch (Exception ex)
        {
            await ShowMessageAsync("Open failed", ex.Message);
            return;
        }

        _currentFile = file;
        _currentFile.Content = markdown;
        _hasUnsavedChanges = false;
        _isEditing = false;
        _isLoadingFile = true;
        EditorBox.Text = markdown;
        _isLoadingFile = false;
        ContentTabs.SelectedIndex = 0;
        RenderPreview(markdown);
        UpdateUiState();
    }

    private void ApplySearch()
    {
        var visibleFiles = string.IsNullOrWhiteSpace(_searchQuery)
            ? _allFiles
            : _allFiles.Where(file => MatchesSearch(file, _searchQuery)).ToList();

        _isChangingSelection = true;
        _files.Clear();
        foreach (var file in visibleFiles)
        {
            _files.Add(file);
        }

        FilesList.SelectedItem = _currentFile is not null && _files.Contains(_currentFile)
            ? _currentFile
            : null;
        _isChangingSelection = false;
    }

    private string EmptyFilesMessage()
    {
        if (_allFiles.Count == 0)
        {
            return "No .md files in this folder.";
        }

        return string.IsNullOrWhiteSpace(_searchQuery)
            ? ""
            : "No Markdown files match this search.";
    }

    private static bool MatchesSearch(MarkdownFile file, string query)
    {
        return file.Name.Contains(query, StringComparison.CurrentCultureIgnoreCase) ||
               file.RelativePath.Contains(query, StringComparison.CurrentCultureIgnoreCase) ||
               file.Content.Contains(query, StringComparison.CurrentCultureIgnoreCase);
    }

    private void RenderPreview(string? markdown = null)
    {
        _isPreviewControlKeyDown = false;
        _isPreviewSelectAllActive = false;
        PreviewPanel.Children.Clear();

        if (_currentFile is null)
        {
            PreviewPanel.Children.Add(MutedText("Choose a folder and select a Markdown file."));
            return;
        }

        var text = markdown ?? EditorBox.Text;
        if (string.IsNullOrWhiteSpace(text))
        {
            PreviewPanel.Children.Add(MutedText("This Markdown file is empty."));
            return;
        }

        var paragraph = new StringBuilder();
        var code = new StringBuilder();
        var inCodeBlock = false;

        var lines = text.Replace("\r\n", "\n").Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            var rawLine = lines[i];
            var line = rawLine.TrimEnd('\r');
            var trimmed = line.Trim();

            if (trimmed.StartsWith("```", StringComparison.Ordinal))
            {
                FlushParagraph(paragraph);
                if (inCodeBlock)
                {
                    AddCodeBlock(code.ToString().TrimEnd());
                    code.Clear();
                }
                inCodeBlock = !inCodeBlock;
                continue;
            }

            if (inCodeBlock)
            {
                code.AppendLine(line);
                continue;
            }

            if (string.IsNullOrWhiteSpace(line))
            {
                FlushParagraph(paragraph);
                continue;
            }

            if (TryReadTable(lines, i, out var table, out var tableEndIndex))
            {
                FlushParagraph(paragraph);
                AddTable(table);
                i = tableEndIndex;
                continue;
            }

            if (TryAddHeading(line) || TryAddRule(trimmed) || TryAddListItem(trimmed) || TryAddQuote(trimmed))
            {
                FlushParagraph(paragraph);
                continue;
            }

            if (paragraph.Length > 0)
            {
                paragraph.Append(' ');
            }
            paragraph.Append(CleanInline(line.Trim()));
        }

        FlushParagraph(paragraph);
        if (inCodeBlock && code.Length > 0)
        {
            AddCodeBlock(code.ToString().TrimEnd());
        }
    }

    private bool TryAddHeading(string line)
    {
        var trimmedStart = line.TrimStart();
        var level = trimmedStart.TakeWhile(c => c == '#').Count();
        if (level is < 1 or > 6 || trimmedStart.Length <= level || trimmedStart[level] != ' ')
        {
            return false;
        }

        AddText(CleanInline(trimmedStart[(level + 1)..]), level switch
        {
            1 => 28,
            2 => 24,
            3 => 20,
            4 => 18,
            _ => 16
        }, Weight(600), new Thickness(0, level == 1 ? 0 : 8, 0, 2));
        return true;
    }

    private bool TryAddRule(string trimmed)
    {
        if (trimmed.Length < 3 || trimmed.Any(c => c is not '-' and not '*' and not '_'))
        {
            return false;
        }

        PreviewPanel.Children.Add(new Border
        {
            Height = 1,
            Background = new SolidColorBrush(Colors.Gray),
            Opacity = 0.4,
            Margin = new Thickness(0, 8, 0, 8)
        });
        return true;
    }

    private bool TryAddListItem(string trimmed)
    {
        var markerLength = 0;
        if (trimmed.StartsWith("- ", StringComparison.Ordinal) ||
            trimmed.StartsWith("* ", StringComparison.Ordinal) ||
            trimmed.StartsWith("+ ", StringComparison.Ordinal))
        {
            markerLength = 2;
        }
        else
        {
            var dot = trimmed.IndexOf(". ", StringComparison.Ordinal);
            if (dot > 0 && trimmed[..dot].All(char.IsDigit))
            {
                markerLength = dot + 2;
            }
        }

        if (markerLength == 0)
        {
            return false;
        }

        AddText($"- {CleanInline(trimmed[markerLength..])}", 14, Weight(400), new Thickness(12, 0, 0, 0));
        return true;
    }

    private bool TryAddQuote(string trimmed)
    {
        if (!trimmed.StartsWith("> ", StringComparison.Ordinal))
        {
            return false;
        }

        PreviewPanel.Children.Add(new Border
        {
            BorderBrush = new SolidColorBrush(Colors.Gray),
            BorderThickness = new Thickness(4, 0, 0, 0),
            Margin = new Thickness(0, 4, 0, 4),
            Padding = new Thickness(10, 0, 0, 0),
            Child = CreateTextBlock(CleanInline(trimmed[2..]), PreviewFontFamily, 14, Weight(400), new Thickness(0))
        });
        return true;
    }

    private void FlushParagraph(StringBuilder paragraph)
    {
        if (paragraph.Length == 0)
        {
            return;
        }

        AddText(paragraph.ToString(), 14, Weight(400), new Thickness(0, 0, 0, 4));
        paragraph.Clear();
    }

    private void AddCodeBlock(string code)
    {
        PreviewPanel.Children.Add(new Border
        {
            BorderBrush = new SolidColorBrush(Colors.Gray),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(12),
            Margin = new Thickness(0, 4, 0, 4),
            Child = CreateTextBlock(code, CodeFontFamily, 14, Weight(400), new Thickness(0))
        });
    }

    private void AddTable(MarkdownTable table)
    {
        var grid = new Grid
        {
            HorizontalAlignment = HorizontalAlignment.Stretch
        };

        for (var column = 0; column < table.Headers.Count; column++)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition
            {
                MinWidth = column == 0 ? 120 : 180,
                Width = column == 0 ? GridLength.Auto : new GridLength(1, GridUnitType.Star)
            });
        }

        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        for (var row = 0; row < table.Rows.Count; row++)
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        }

        for (var column = 0; column < table.Headers.Count; column++)
        {
            AddTableCell(grid, table.Headers[column], 0, column, isHeader: true);
        }

        for (var row = 0; row < table.Rows.Count; row++)
        {
            for (var column = 0; column < table.Headers.Count; column++)
            {
                AddTableCell(grid, table.Rows[row][column], row + 1, column, isHeader: false);
            }
        }

        PreviewPanel.Children.Add(new Border
        {
            BorderBrush = new SolidColorBrush(Colors.Gray),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Margin = new Thickness(0, 4, 0, 8),
            Child = grid
        });
    }

    private void AddTableCell(Grid grid, string text, int row, int column, bool isHeader)
    {
        var cell = new Border
        {
            Background = isHeader ? new SolidColorBrush(Colors.Gray) { Opacity = 0.12 } : null,
            BorderBrush = new SolidColorBrush(Colors.Gray),
            BorderThickness = new Thickness(0, 0, 1, 1),
            Padding = new Thickness(10, 6, 10, 6),
            Child = CreateTextBlock(text, PreviewFontFamily, 14, isHeader ? Weight(600) : Weight(400), new Thickness(0))
        };

        Grid.SetRow(cell, row);
        Grid.SetColumn(cell, column);
        grid.Children.Add(cell);
    }

    private void AddText(string text, double fontSize, FontWeight weight, Thickness margin)
    {
        PreviewPanel.Children.Add(CreateTextBlock(text, PreviewFontFamily, fontSize, weight, margin));
    }

    private void PreviewScrollViewer_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Escape && _isPreviewSelectAllActive)
        {
            ClearPreviewSelectAll();
            e.Handled = true;
            return;
        }

        if (e.Key == VirtualKey.Control)
        {
            _isPreviewControlKeyDown = true;
            return;
        }

        if (!_isPreviewControlKeyDown)
        {
            return;
        }

        if (e.Key == VirtualKey.A)
        {
            SelectAllPreviewText();
            e.Handled = _isPreviewSelectAllActive;
            return;
        }

        if (e.Key == VirtualKey.C && _isPreviewSelectAllActive)
        {
            CopyAllPreviewText();
            e.Handled = true;
        }
    }

    private void PreviewScrollViewer_KeyUp(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Control)
        {
            _isPreviewControlKeyDown = false;
        }
    }

    private void SelectAllPreviewText()
    {
        ClearPreviewSelectAll();
        foreach (var textBlock in DescendantTextBlocks(PreviewPanel))
        {
            var text = textBlock.Tag as string ?? textBlock.Text;
            if (text.Length == 0)
            {
                continue;
            }

            var highlighter = new TextHighlighter
            {
                Background = new SolidColorBrush(Colors.DodgerBlue),
                Foreground = new SolidColorBrush(Colors.White)
            };
            highlighter.Ranges.Add(new Microsoft.UI.Xaml.Documents.TextRange
            {
                StartIndex = 0,
                Length = text.Length
            });
            textBlock.TextHighlighters.Add(highlighter);
            _isPreviewSelectAllActive = true;
        }
    }

    private void PreviewScrollViewer_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        _isPreviewControlKeyDown = false;
        ClearPreviewSelectAll();
    }

    private void ClearPreviewSelectAll()
    {
        foreach (var textBlock in DescendantTextBlocks(PreviewPanel))
        {
            textBlock.TextHighlighters.Clear();
        }

        _isPreviewSelectAllActive = false;
    }

    private void CopyAllPreviewText()
    {
        var text = GetPreviewText();
        if (text.Length == 0)
        {
            return;
        }

        var dataPackage = new DataPackage
        {
            RequestedOperation = DataPackageOperation.Copy
        };
        dataPackage.SetText(text);
        Clipboard.SetContent(dataPackage);
        Clipboard.Flush();
    }

    private string GetPreviewText()
    {
        var blocks = PreviewPanel.Children
            .Select(GetPreviewElementText)
            .Where(text => !string.IsNullOrWhiteSpace(text));

        return string.Join(Environment.NewLine + Environment.NewLine, blocks);
    }

    private static string GetPreviewElementText(DependencyObject element)
    {
        if (element is TextBlock textBlock)
        {
            return textBlock.Tag as string ?? textBlock.Text;
        }

        if (element is Border { Child: Grid table } &&
            table.RowDefinitions.Count > 0 &&
            table.ColumnDefinitions.Count > 0)
        {
            return GetPreviewTableText(table);
        }

        if (element is Border { Child: DependencyObject child })
        {
            return GetPreviewElementText(child);
        }

        return string.Join(
            Environment.NewLine,
            DescendantTextBlocks(element)
                .Select(block => block.Tag as string ?? block.Text)
                .Where(text => !string.IsNullOrEmpty(text)));
    }

    private static string GetPreviewTableText(Grid table)
    {
        var rows = new string[table.RowDefinitions.Count][];
        for (var row = 0; row < rows.Length; row++)
        {
            rows[row] = new string[table.ColumnDefinitions.Count];
        }

        foreach (var child in table.Children.OfType<FrameworkElement>())
        {
            var row = Grid.GetRow(child);
            var column = Grid.GetColumn(child);
            if (row < rows.Length && column < rows[row].Length)
            {
                rows[row][column] = GetPreviewElementText(child);
            }
        }

        return string.Join(
            Environment.NewLine,
            rows.Select(row => string.Join('\t', row)));
    }

    private static IEnumerable<TextBlock> DescendantTextBlocks(DependencyObject root)
    {
        var childCount = VisualTreeHelper.GetChildrenCount(root);
        for (var index = 0; index < childCount; index++)
        {
            var child = VisualTreeHelper.GetChild(root, index);
            if (child is TextBlock textBlock)
            {
                yield return textBlock;
            }

            foreach (var descendant in DescendantTextBlocks(child))
            {
                yield return descendant;
            }
        }
    }

    private TextBlock CreateTextBlock(string text, FontFamily fontFamily, double fontSize, FontWeight weight, Thickness margin)
    {
        var textBlock = new TextBlock
        {
            FontFamily = fontFamily,
            FontSize = fontSize,
            FontWeight = weight,
            IsTextSelectionEnabled = true,
            LineHeight = LineHeight(fontSize),
            Margin = margin,
            Tag = text,
            TextWrapping = TextWrapping.Wrap
        };

        SetHighlightedText(textBlock, text);
        return textBlock;
    }

    private void SetHighlightedText(TextBlock textBlock, string text)
    {
        if (string.IsNullOrWhiteSpace(_searchQuery))
        {
            textBlock.Text = text;
            return;
        }

        var start = 0;
        while (start < text.Length)
        {
            var index = text.IndexOf(_searchQuery, start, StringComparison.CurrentCultureIgnoreCase);
            if (index < 0)
            {
                break;
            }

            if (index > start)
            {
                textBlock.Inlines.Add(new Run { Text = text[start..index] });
            }

            textBlock.Inlines.Add(new Run
            {
                Text = text[index..(index + _searchQuery.Length)],
                Foreground = SearchMatchBrush,
                FontWeight = Weight(700)
            });
            start = index + _searchQuery.Length;
        }

        if (start == 0)
        {
            textBlock.Text = text;
            return;
        }

        if (start < text.Length)
        {
            textBlock.Inlines.Add(new Run { Text = text[start..] });
        }
    }

    private static FontWeight Weight(ushort value)
    {
        return new FontWeight { Weight = value };
    }

    private static double LineHeight(double fontSize)
    {
        return Math.Ceiling(fontSize * 1.75);
    }

    private static TextBlock MutedText(string text)
    {
        return new TextBlock
        {
            Text = text,
            FontFamily = PreviewFontFamily,
            IsTextSelectionEnabled = true,
            LineHeight = LineHeight(14),
            Foreground = new SolidColorBrush(Colors.Gray),
            Tag = text,
            TextWrapping = TextWrapping.Wrap
        };
    }

    private static bool TryReadTable(string[] lines, int startIndex, out MarkdownTable table, out int endIndex)
    {
        table = new MarkdownTable(Array.Empty<string>(), Array.Empty<IReadOnlyList<string>>());
        endIndex = startIndex;

        if (startIndex + 1 >= lines.Length ||
            ParseTableRow(lines[startIndex]) is not { Count: > 0 } header ||
            ParseTableRow(lines[startIndex + 1]) is not { Count: > 0 } separator ||
            separator.Count != header.Count ||
            !separator.All(IsTableSeparatorCell))
        {
            return false;
        }

        var rows = new List<IReadOnlyList<string>>();
        for (var i = startIndex + 2; i < lines.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i]) ||
                ParseTableRow(lines[i]) is not { Count: > 0 } row ||
                row.Count != header.Count)
            {
                break;
            }

            rows.Add(row);
            endIndex = i;
        }

        table = new MarkdownTable(header, rows);
        endIndex = Math.Max(endIndex, startIndex + 1);
        return true;
    }

    private static List<string>? ParseTableRow(string line)
    {
        var trimmed = line.Trim();
        if (!trimmed.Contains('|'))
        {
            return null;
        }

        if (trimmed.StartsWith("|", StringComparison.Ordinal))
        {
            trimmed = trimmed[1..];
        }

        if (trimmed.EndsWith("|", StringComparison.Ordinal))
        {
            trimmed = trimmed[..^1];
        }

        return trimmed.Split('|').Select(cell => CleanInline(cell.Trim())).ToList();
    }

    private static bool IsTableSeparatorCell(string cell)
    {
        var trimmed = cell.Trim();
        if (trimmed.StartsWith(":", StringComparison.Ordinal))
        {
            trimmed = trimmed[1..];
        }

        if (trimmed.EndsWith(":", StringComparison.Ordinal))
        {
            trimmed = trimmed[..^1];
        }

        return trimmed.Length >= 3 && trimmed.All(c => c == '-');
    }

#if DEBUG
    private static void VerifyTableParser()
    {
        var lines = new[]
        {
            "| Term | Meaning |",
            "| --- | --- |",
            "| FX | Currency conversion rate |"
        };

        if (!TryReadTable(lines, 0, out var table, out var endIndex) ||
            endIndex != 2 ||
            table.Headers.Count != 2 ||
            table.Rows.Count != 1 ||
            table.Rows[0][0] != "FX")
        {
            throw new InvalidOperationException("Markdown table parser self-check failed.");
        }
    }

    private static void VerifySearchMatching()
    {
        var file = new MarkdownFile("business-vocabulary-burmese.md", @"split-markdown\business-vocabulary-burmese.md", "C:\\docs\\business-vocabulary-burmese.md", "FX rate");
        if (!MatchesSearch(file, "fx") ||
            !MatchesSearch(file, "split-markdown") ||
            !MatchesSearch(file, "business-vocabulary") ||
            MatchesSearch(file, "does-not-exist"))
        {
            throw new InvalidOperationException("Markdown search self-check failed.");
        }
    }
#endif

    private static string CleanInline(string text)
    {
        text = LinkPattern.Replace(text, "$1 ($2)");
        return text
            .Replace("**", "")
            .Replace("__", "")
            .Replace("`", "");
    }

    private void UpdateUiState(string? emptyFilesText = null)
    {
        var hasFile = _currentFile is not null;
        EditorBox.IsEnabled = hasFile;
        EditorBox.IsReadOnly = !_isEditing;
        EditButton.IsEnabled = hasFile;
        EditButton.Visibility = hasFile && !_isEditing ? Visibility.Visible : Visibility.Collapsed;
        SaveButton.Visibility = _isEditing ? Visibility.Visible : Visibility.Collapsed;
        SaveButton.IsEnabled = hasFile && _hasUnsavedChanges;
        RevertButton.Visibility = _isEditing ? Visibility.Visible : Visibility.Collapsed;
        RevertButton.IsEnabled = hasFile;
        DirtyText.Visibility = _hasUnsavedChanges ? Visibility.Visible : Visibility.Collapsed;
        SearchBox.IsEnabled = _allFiles.Count > 0;
        FileCountText.Text = string.IsNullOrWhiteSpace(_searchQuery)
            ? (_files.Count == 1 ? "1 file" : $"{_files.Count} files")
            : $"{_files.Count} / {_allFiles.Count} files";
        EmptyFilesText.Text = emptyFilesText ?? EmptyFilesText.Text;
        EmptyFilesText.Visibility = _files.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        FilesList.Visibility = _files.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
    }

    private async Task ShowMessageAsync(string title, string message)
    {
        await new ContentDialog
        {
            Title = title,
            Content = message,
            CloseButtonText = "OK",
            XamlRoot = XamlRoot
        }.ShowAsync();
    }

    private sealed class MarkdownFile
    {
        public MarkdownFile(string name, string relativePath, string fullPath, string content)
        {
            Name = name;
            RelativePath = relativePath;
            FullPath = fullPath;
            Content = content;
        }

        public string Name { get; }

        public string RelativePath { get; }

        public string FullPath { get; }

        public string Content { get; set; }
    }

    private sealed record MarkdownTable(IReadOnlyList<string> Headers, IReadOnlyList<IReadOnlyList<string>> Rows);
}
