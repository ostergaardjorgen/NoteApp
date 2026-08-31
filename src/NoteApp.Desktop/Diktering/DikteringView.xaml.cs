using System.Windows.Controls;

namespace NoteApp.Desktop.Diktering;

/// <summary>
/// Alt om diktering på ét sted: noterne, prøverummet, teksttyperne, ordbogen
/// og kommandoerne.
///
/// NOTERNE STÅR FØRST, fordi det er dem, man kommer for. De andre er
/// opsætning: noget man gør én gang og sjældent igen.
/// </summary>
public partial class DikteringView : UserControl
{
    /// <summary>
    /// Fanen, skærmen skal åbne på. Sættes, FØR skærmen bygges.
    /// </summary>
    /// <remarks>
    /// Bruges af knappen «Vis noten» på bjælken: den skal føre hen til noten,
    /// ikke bare til dikteringsskærmen. En knap, der åbner det rigtige
    /// program på den forkerte side, er halvt om ved at hjælpe.
    /// </remarks>
    public static int StartFane { get; set; }

    public DikteringView()
    {
        InitializeComponent();

        Loaded += (_, _) =>
        {
            if (StartFane <= 0 || StartFane >= Faner.Items.Count) return;

            Faner.SelectedIndex = StartFane;
            StartFane = 0;
        };
    }
}
