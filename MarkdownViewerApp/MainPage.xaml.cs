using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.Storage.Pickers;
using Windows.UI.Text;
using WinRT.Interop;

namespace MarkdownViewerApp;

public sealed partial class MainPage : Page
{
    private static readonly Regex LinkPattern = new(@"\[([^\]]+)\]\(([^)]+)\)", RegexOptions.Compiled);
    private static readonly FontFamily PreviewFontFamily = new("Myanmar Text, Segoe UI");
    private static readonly FontFamily CodeFontFamily = new("Myanmar Text, Consolas");
    private readonly ObservableCollection<MarkdownFile> _files = new();
    private MarkdownFile? _currentFile;
    private bool _hasUnsavedChanges;
    private bool _isEditing;
    private bool _isLoadingFile;
    private bool _isChangingSelection;

    public MainPage()
    {
        InitializeComponent();
        FilesList.ItemsSource = _files;
        RenderPreview();
        UpdateUiState("Choose a folder to list .md files.");
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

        LoadFolder(folder.Path);
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
        RenderPreview(EditorBox.Text);
        UpdateUiState();
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
            _hasUnsavedChanges = false;
            _isEditing = false;
            ContentTabs.SelectedIndex = 0;
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

    private void LoadFolder(string folderPath)
    {
        FolderPathText.Text = folderPath;
        _currentFile = null;
        _hasUnsavedChanges = false;
        _isEditing = false;
        _isLoadingFile = true;
        EditorBox.Text = "";
        _isLoadingFile = false;

        _files.Clear();
        foreach (var file in Directory.EnumerateFiles(folderPath, "*.md", SearchOption.TopDirectoryOnly)
                     .OrderBy(Path.GetFileName, StringComparer.CurrentCultureIgnoreCase))
        {
            _files.Add(new MarkdownFile(Path.GetFileName(file), file));
        }

        ContentTabs.SelectedIndex = 0;
        RenderPreview();
        UpdateUiState(_files.Count == 0 ? "No .md files in this folder." : "");
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
        _hasUnsavedChanges = false;
        _isEditing = false;
        _isLoadingFile = true;
        EditorBox.Text = markdown;
        _isLoadingFile = false;
        ContentTabs.SelectedIndex = 0;
        RenderPreview(markdown);
        UpdateUiState();
    }

    private void RenderPreview(string? markdown = null)
    {
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

        foreach (var rawLine in text.Replace("\r\n", "\n").Split('\n'))
        {
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
            Child = new TextBlock
            {
                FontFamily = PreviewFontFamily,
                LineHeight = LineHeight(14),
                Text = CleanInline(trimmed[2..]),
                TextWrapping = TextWrapping.Wrap
            }
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
            Child = new TextBlock
            {
                FontFamily = CodeFontFamily,
                LineHeight = LineHeight(14),
                Text = code,
                TextWrapping = TextWrapping.Wrap
            }
        });
    }

    private void AddText(string text, double fontSize, FontWeight weight, Thickness margin)
    {
        PreviewPanel.Children.Add(new TextBlock
        {
            Text = text,
            FontFamily = PreviewFontFamily,
            FontSize = fontSize,
            FontWeight = weight,
            LineHeight = LineHeight(fontSize),
            Margin = margin,
            TextWrapping = TextWrapping.Wrap
        });
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
            LineHeight = LineHeight(14),
            Foreground = new SolidColorBrush(Colors.Gray),
            TextWrapping = TextWrapping.Wrap
        };
    }

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
        FileCountText.Text = _files.Count == 1 ? "1 file" : $"{_files.Count} files";
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

    private sealed record MarkdownFile(string Name, string FullPath);
}
