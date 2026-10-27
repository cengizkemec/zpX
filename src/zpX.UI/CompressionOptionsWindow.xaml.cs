using System.Windows;
using zpX.Core.Archives;

namespace zpX.UI;

public partial class CompressionOptionsWindow : Window
{
    public int CompressionLevel { get; private set; } = 5;
    public string? Password { get; private set; }
    public bool EncryptFileNames { get; private set; }
    public bool CreateSfx { get; private set; }
    public string FormatExtension { get; private set; } = ArchiveCreator.Extension;

    public CompressionOptionsWindow()
    {
        InitializeComponent();
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        if (PasswordBox.Password != PasswordConfirmBox.Password)
        {
            MessageBox.Show(this, "Passwords do not match.", "zpX", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var level = 5;
        if (LevelBox.SelectedItem is System.Windows.Controls.ComboBoxItem item && item.Tag is string tag)
            int.TryParse(tag, out level);

        CompressionLevel = level;
        Password = string.IsNullOrWhiteSpace(PasswordBox.Password) ? null : PasswordBox.Password;
        EncryptFileNames = EncryptNamesCheckBox.IsChecked == true;
        CreateSfx = SfxCheckBox.IsChecked == true;
        FormatExtension = (FormatBox.SelectedIndex == 1) ? ".zip" : ArchiveCreator.Extension;

        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
