using System.Windows;

namespace NoteApp.Desktop.Transcribe;

/// <summary>Et navn til en optagelse. Intet andet.</summary>
public partial class RenameWindow : Window
{
    public string NytNavn => Felt.Text.Trim();

    public RenameWindow(string nuværende)
    {
        InitializeComponent();

        Felt.Text = nuværende;
        Loaded += (_, _) => { Felt.Focus(); Felt.SelectAll(); };
    }

    private void Gem_Click(object sender, RoutedEventArgs e)
    {
        if (NytNavn.Length == 0)
        {
            MessageBox.Show("Navnet må ikke være tomt.", "Mangler navn",
                MessageBoxButton.OK, MessageBoxImage.Information);
            Felt.Focus();
            return;
        }

        DialogResult = true;
    }

    private void Annuller_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
