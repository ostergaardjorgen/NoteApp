using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace NoteApp.Desktop;

/// <summary>
/// Farten, tingene glider med. ÉT sted.
///
/// HVORFOR DEN IKKE ER TO TAL, DER TILFÆLDIGVIS ER ENS
///
/// Sidespalterne i Cockpittet, menuen i venstre side, ruderne der popper op
/// og vinduerne gør det samme: de kommer og går. Bevæger de sig forskelligt,
/// læses de som forskellige slags ting — og man mærker det, længe før man kan
/// sige hvorfor.
///
/// Stod tallet flere steder, ville de være ens den dag, de blev skrevet, og
/// forskellige den dag, det ene blev justeret. Her er det ét tal, og så kan
/// de ikke komme fra hinanden.
/// </summary>
public static class Glid
{
    /// <summary>
    /// Hvor lang tid en foldning tager.
    /// </summary>
    /// <remarks>
    /// Fire tiendedele af et sekund. Kortere føles hastigt — en rude, der
    /// forsvinder med et snup, læses som en fejl. Længere, og man sidder og
    /// venter på at kunne se det, man bad om.
    /// </remarks>
    public static readonly TimeSpan Varighed = TimeSpan.FromMilliseconds(400);

    /// <summary>
    /// Hvor langt en rude rejser, mens den folder sig ud. Pixels.
    /// </summary>
    /// <remarks>
    /// Kort med vilje. En rude, der kommer flyvende langvejsfra, er en
    /// forestilling; en, der stiger de sidste par pixels på plads, er en
    /// bevægelse man knap registrerer — og det er den, der føles dyr.
    /// </remarks>
    public const double Rejse = 8;

    /// <summary>Hvor lille en rude er, lige før den er der. Andel af sig selv.</summary>
    public const double Spire = 0.98;

    /// <summary>
    /// Kurven: blød i begge ender.
    /// </summary>
    /// <remarks>
    /// EaseInOut, fordi bevægelsen både begynder og slutter af sig selv. En
    /// bevægelse, der starter brat, ligner et ryk; en, der stopper brat,
    /// ligner noget, der ramte en væg.
    ///
    /// Der laves en ny hver gang. En delt kurve kan fryses af den første
    /// animation, der bruger den, og så kan den anden ikke ændre den.
    /// </remarks>
    public static IEasingFunction Kurve() => new CubicEase { EasingMode = EasingMode.EaseInOut };

    /// <summary>En animation, der glider til <paramref name="maal"/>.</summary>
    public static DoubleAnimation Til(double maal) => new()
    {
        To = maal,
        Duration = Varighed,
        EasingFunction = Kurve(),
    };

    /// <summary>En animation, der glider fra <paramref name="fra"/> til <paramref name="maal"/>.</summary>
    /// <remarks>
    /// Startpunktet skrives med, når noget skal komme frem. Sætter man i
    /// stedet værdien først og animerer bagefter, gør den ingenting: en
    /// animation, der stadig holder sin slutværdi fra sidste gang, spærrer
    /// for den værdi, man lige har skrevet.
    /// </remarks>
    public static DoubleAnimation Fra(double fra, double maal)
    {
        var a = Til(maal);
        a.From = fra;
        return a;
    }

    // ==================================================================
    //                            RUDER
    // ==================================================================

    /// <summary>Det, en enkelt rude skal huske om sig selv.</summary>
    private sealed class Rudestyring
    {
        /// <summary>Er den i gang med at lukke? Så skal den ikke gøre det igen.</summary>
        public bool Lukker;

        /// <summary>Lukkede den oprindeligt sig selv ved klik udenfor?</summary>
        public bool Selvlukkende;
    }

    /// <remarks>
    /// Svag tabel, så en rude, der ikke bruges mere, kan ryddes op. Et
    /// almindeligt opslagsværk ville holde fast i hver eneste rude, der
    /// nogensinde har været åben.
    /// </remarks>
    private static readonly ConditionalWeakTable<Popup, Rudestyring> Ruder = new();

    /// <summary>
    /// Giver en rude den samme glidning ud og ind som resten af appen.
    /// </summary>
    /// <remarks>
    /// HVORFOR RUDEN SELV SKAL LUKKE SIG, OG IKKE WPF
    ///
    /// En Popup med StaysOpen=false lukker sig selv, så snart der klikkes
    /// udenfor. Den siger det ikke først — der findes intet «jeg er ved at
    /// lukke». Når man opdager det, er ruden væk, og så er der ikke noget
    /// tilbage at vise en bevægelse på.
    ///
    /// Derfor bliver StaysOpen sat til sandt, og ruden holder selv øje med
    /// musen — præcis som WPF ellers gør det indeni. Så kan lukningen tage
    /// den tid, den skal, og et klik, der lukker en boks, føles som en
    /// beslutning frem for et snup.
    ///
    /// Skal ruden lukkes fra koden, gøres det med <see cref="LukRude"/>.
    /// Sættes IsOpen til falsk direkte, forsvinder den med det samme — og så
    /// er vi tilbage ved snuppet.
    /// </remarks>
    public static void Rude(Popup pop)
    {
        if (pop.Child is not FrameworkElement barn) return;

        var s = new Rudestyring { Selvlukkende = !pop.StaysOpen };
        Ruder.Remove(pop);
        Ruder.Add(pop, s);

        // Uden gennemsigtighed kan der ikke tones. WPF's egen animation
        // slås fra: to bevægelser oven i hinanden er værre end ingen.
        pop.AllowsTransparency = true;
        pop.PopupAnimation = PopupAnimation.None;
        pop.StaysOpen = true;

        var skala = new ScaleTransform(Spire, Spire);
        var flyt = new TranslateTransform(0, -Rejse);
        var gruppe = new TransformGroup();
        gruppe.Children.Add(skala);
        gruppe.Children.Add(flyt);

        barn.RenderTransform = gruppe;
        barn.RenderTransformOrigin = new Point(0.5, 0);

        pop.Opened += (_, _) =>
        {
            s.Lukker = false;

            barn.BeginAnimation(UIElement.OpacityProperty, Fra(0, 1));
            flyt.BeginAnimation(TranslateTransform.YProperty, Fra(-Rejse, 0));
            skala.BeginAnimation(ScaleTransform.ScaleXProperty, Fra(Spire, 1));
            skala.BeginAnimation(ScaleTransform.ScaleYProperty, Fra(Spire, 1));

            if (s.Selvlukkende) Mouse.Capture(barn, CaptureMode.SubTree);
        };

        pop.Closed += (_, _) =>
        {
            s.Lukker = false;
            if (ReferenceEquals(Mouse.Captured, barn)) Mouse.Capture(null);
        };

        if (!s.Selvlukkende) return;

        Mouse.AddPreviewMouseDownOutsideCapturedElementHandler(barn, (_, _) => LukRude(pop));

        // Musen kan blive taget fra ruden af noget inde i den selv. Sker det,
        // skal den tages tilbage - ellers lukker naeste klik udenfor ikke.
        barn.LostMouseCapture += (_, _) =>
        {
            if (pop.IsOpen && !s.Lukker && Mouse.Captured is null)
                Mouse.Capture(barn, CaptureMode.SubTree);
        };

        barn.PreviewKeyDown += (_, e) =>
        {
            if (e.Key != Key.Escape) return;
            LukRude(pop);
            e.Handled = true;
        };
    }

    /// <summary>
    /// Lukker en rude, der er sat op med <see cref="Rude"/> — med bevægelse.
    /// </summary>
    /// <remarks>
    /// Kald den frit. Er ruden lukket eller allerede på vej ud, sker der
    /// ingenting; det er med vilje, for det samme klik kan nå at bede om
    /// lukningen to gange.
    /// </remarks>
    public static void LukRude(Popup pop)
    {
        if (!pop.IsOpen || pop.Child is not FrameworkElement barn) return;

        if (!Ruder.TryGetValue(pop, out var s))
        {
            // Ikke sat op her. Så lukker den, som den plejer.
            pop.IsOpen = false;
            return;
        }

        if (s.Lukker) return;
        s.Lukker = true;

        if (ReferenceEquals(Mouse.Captured, barn)) Mouse.Capture(null);

        var gruppe = barn.RenderTransform as TransformGroup;
        var skala = gruppe?.Children.Count > 0 ? gruppe.Children[0] as ScaleTransform : null;
        var flyt = gruppe?.Children.Count > 1 ? gruppe.Children[1] as TranslateTransform : null;

        var ud = Til(0);
        ud.Completed += (_, _) =>
        {
            if (!s.Lukker) return;
            s.Lukker = false;
            pop.IsOpen = false;
        };

        flyt?.BeginAnimation(TranslateTransform.YProperty, Til(-Rejse));
        skala?.BeginAnimation(ScaleTransform.ScaleXProperty, Til(Spire));
        skala?.BeginAnimation(ScaleTransform.ScaleYProperty, Til(Spire));
        barn.BeginAnimation(UIElement.OpacityProperty, ud);
    }

    // ==================================================================
    //                           VINDUER
    // ==================================================================

    /// <summary>Vinduer, der allerede har fået deres glidning.</summary>
    private static readonly ConditionalWeakTable<Window, object> Vinduer = new();

    /// <summary>Vinduer, der er ved at glide ud, og derfor gerne må lukke nu.</summary>
    private static readonly ConditionalWeakTable<Window, object> Udgaaende = new();

    /// <summary>
    /// Giver et vindue den samme glidning som ruderne — ind når det kommer,
    /// ud når det lukkes.
    /// </summary>
    /// <remarks>
    /// HVORFOR DET ER INDHOLDET, DER TONES, OG IKKE VINDUET
    ///
    /// Et vindue med almindelig ramme kan ikke tones som helhed uden at
    /// blive gennemsigtigt hele vejen igennem, og det er en anden slags
    /// vindue end det, der står i XAML'en. I stedet tones det, vinduet
    /// indeholder. Rammen er der straks, og indholdet falder på plads —
    /// og for de ruder, der ikke HAR nogen ramme, er de to ting det samme.
    ///
    /// Lukningen kræver, at der siges nej én gang: Closing er det eneste
    /// sted, man kan nå at gøre noget, og der er vinduet stadig at se på.
    /// Bevægelsen køres, og bagefter lukkes der for alvor.
    ///
    /// ============ DER STOD, AT DialogResult BLEV STÅENDE. DET GJORDE DEN IKKE.
    ///
    /// WPF nulstiller selv DialogResult, når en lukning bliver annulleret —
    /// `InternalClose` sætter den til null, netop fordi et vindue, der ikke
    /// lukkede, ikke har svaret noget. Et `DialogResult = true` inde i en
    /// OK-knap satte altså flaget, kaldte Close, blev annulleret her af hensyn
    /// til en optoning på 150 ms — og WPF slettede svaret bagefter. `ShowDialog`
    /// returnerede false, og kalderen læste det som «brugeren fortrød».
    ///
    /// Det ramte HVERT modalt vindue i appen: glidningen sættes på alle vinduer
    /// fra App.xaml.cs. Sprogvinduet fik 03-09-2026 sit eget `Godkendt`-flag
    /// som lappeløsning efter en dags fejlsøgning; «Opret dokument» havde den
    /// samme fejl og gjorde 04-09-2026 bogstaveligt talt ingenting, når man
    /// trykkede på knappen.
    ///
    /// Svaret gemmes derfor FØR annulleringen og sættes igen, når bevægelsen
    /// er kørt. At sætte DialogResult lukker selv vinduet, så anden runde går
    /// gennem `Udgaaende` og slipper igennem med svaret i behold.
    /// </remarks>
    public static void Vindue(Window v)
    {
        if (Vinduers(v)) return;
        Vinduer.Add(v, new object());

        if (v.Content is FrameworkElement rod)
            rod.BeginAnimation(UIElement.OpacityProperty, Fra(0, 1));

        v.Closing += (_, e) =>
        {
            if (e.Cancel) return;                       // en anden sagde nej først
            if (Udgaaende.TryGetValue(v, out _)) return; // anden runde: luk endelig
            if (v.Content is not FrameworkElement indhold) return;

            // Læses FØR e.Cancel. Bagefter har WPF slettet det.
            bool? svar = null;
            try { svar = v.DialogResult; } catch (InvalidOperationException) { }

            e.Cancel = true;
            Udgaaende.Add(v, new object());

            var ud = Til(0);
            ud.Completed += (_, _) =>
            {
                if (svar is null) { v.Close(); return; }

                // Sætteren lukker selv vinduet. Kan den ikke bruges — vinduet
                // blev vist med Show og ikke ShowDialog — lukkes der som før,
                // så en fejl her aldrig kan efterlade et vindue, der ikke går væk.
                try { v.DialogResult = svar; }
                catch (InvalidOperationException) { v.Close(); }
            };
            indhold.BeginAnimation(UIElement.OpacityProperty, ud);
        };
    }

    private static bool Vinduers(Window v) => Vinduer.TryGetValue(v, out _);
}
