using System.Windows;

namespace NoteApp.Desktop.Transcribe;

/// <summary>
/// «Det her ord blev hørt forkert — det skulle have været det her.»
///
/// Det er hele læringssløjfen på én skærm. Whisper kan ikke trænes, og
/// ordlisten viste sig at gøre ingen forskel; det eneste, der målbart virker,
/// er at rette bagefter. Men en rettelse, man ikke kan lave nogen steder, er
/// ikke en funktion — og det var præcis, hvad appen påstod, at den kunne.
/// </summary>
public partial class CorrectionWindow : Window
{
    public string Hørt => FeltHoert.Text.Trim();
    public string Rigtigt => FeltRigtigt.Text.Trim();

    public CorrectionWindow(string markeret, int antalIUdskriften)
    {
        InitializeComponent();

        FeltHoert.Text = markeret;

        Konsekvens.Text = antalIUdskriften switch
        {
            0 => "Ordet står ikke i denne udskrift, men reglen gælder fremover.",
            1 => "Ordet står ét sted i denne udskrift og bliver rettet med det samme.",
            _ => $"Ordet står {antalIUdskriften} steder i denne udskrift og bliver rettet alle steder med det samme."
        };

        Loaded += (_, _) => FeltRigtigt.Focus();
    }

    private void Gem_Click(object sender, RoutedEventArgs e)
    {
        if (Hørt.Length == 0 || Rigtigt.Length == 0)
        {
            MessageBox.Show("Begge felter skal udfyldes.", "Mangler noget",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (string.Equals(Hørt, Rigtigt, StringComparison.Ordinal))
        {
            MessageBox.Show("De to felter er ens — så er der ikke noget at rette.",
                "Ingen forskel", MessageBoxButton.OK, MessageBoxImage.Information);
            FeltRigtigt.Focus();
            return;
        }

        DialogResult = true;
    }

    private void Annuller_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
