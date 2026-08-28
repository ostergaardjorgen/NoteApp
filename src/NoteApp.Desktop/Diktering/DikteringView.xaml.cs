using System.Windows.Controls;

namespace NoteApp.Desktop.Diktering;

/// <summary>
/// Alt om diktering på ét sted: prøverummet, teksttyperne og ordbogen.
///
/// De tre faner er de tre ting, man gør, når dictationen ikke rammer: man
/// prøver den af, man retter instruktionen, og man lærer den et ord. Lå de på
/// hver sin skærm, ville det være tre opgaver i stedet for én.
/// </summary>
public partial class DikteringView : UserControl
{
    public DikteringView() => InitializeComponent();
}
