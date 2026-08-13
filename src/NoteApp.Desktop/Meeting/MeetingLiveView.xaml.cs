using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace NoteApp.Desktop.Meeting;

/// <summary>
/// Appen, mens et møde optages.
///
/// HVORFOR HELE SKÆRMEN
///
/// Under et møde er alt andet i appen ligegyldigt. Man skal kunne skrive
/// noter, se at der bliver optaget, og stoppe igen — og der skal være plads
/// til at skrive, ikke et felt i en bjælke.
///
/// Skærmen ejer ikke optagelsen. Den bor i <see cref="MeetingView"/>, bjælken
/// øverst, som bliver siddende hele vejen igennem. Det er med vilje: går man
/// alligevel hen på et andet menupunkt midt i mødet, dør optagelsen ikke med
/// skærmen.
/// </summary>
public partial class MeetingLiveView : UserControl
{
    private readonly MeetingView _bjælke;

    public MeetingLiveView(MeetingView bjælke)
    {
        InitializeComponent();
        _bjælke = bjælke;

        _bjælke.Opdateret += Opdater;
        Unloaded += (_, _) => _bjælke.Opdateret -= Opdater;

        Noter.ItemsSource = _bjælke.Noter;

        Titel.Text = _bjælke.Titel.Length > 0 ? _bjælke.Titel : "Mødet optages";
        Undertitel.Text = "Alt bliver på denne pc. Ingen lyd forlader maskinen.";
        Status.Text = "Optagelsen kører videre, også hvis du går et andet sted hen i appen.";

        Loaded += (_, _) => Felt.Focus();
        Opdater();
    }

    private void Opdater()
    {
        Dispatcher.Invoke(() =>
        {
            Ur.Text = _bjælke.Elapsed.ToString(@"hh\:mm\:ss");

            var pause = _bjælke.IsPaused;
            UrUnder.Text = pause ? "på pause — der optages intet" : "optager";
            Prik.Fill = (Brush)FindResource(pause ? "Advarsel" : "Optager");
            PauseKnap.Content = pause ? "● Fortsæt" : "❚❚ Pause";

            var antal = _bjælke.Noter.Count;
            NoteAntal.Text = antal switch { 0 => "", 1 => "1 note", _ => $"{antal} noter" };
            TomTekst.Visibility = antal == 0 ? Visibility.Visible : Visibility.Collapsed;

            // ItemsControl opdager ikke selv, at listen har faaet et element —
            // den er bundet til en almindelig liste, ikke en observerbar.
            Noter.Items.Refresh();
        });
    }

    private void Felt_Changed(object sender, TextChangedEventArgs e) =>
        Pladsholder.Visibility = Felt.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;

    private void Felt_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.B && Keyboard.Modifiers == ModifierKeys.Control)
        {
            _bjælke.TilføjNoteUdefra("(bogmærke)");
            e.Handled = true;
            return;
        }

        if (e.Key != Key.Enter || Felt.Text.Trim().Length == 0) return;

        _bjælke.TilføjNoteUdefra(Felt.Text.Trim());
        Felt.Clear();
        e.Handled = true;
    }

    private void Pause_Click(object sender, RoutedEventArgs e) => _bjælke.SkiftPause();

    private void Stop_Click(object sender, RoutedEventArgs e) => _bjælke.StopUdefra();
}
