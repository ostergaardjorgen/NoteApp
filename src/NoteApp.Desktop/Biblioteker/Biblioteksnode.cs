using System.Collections.ObjectModel;
using System.ComponentModel;

namespace NoteApp.Desktop.Biblioteker;

/// <summary>
/// Én knude i bibliotekstræet — et bibliotek («Møder», «Arkiv»), en mappe
/// under det, eller en optagelse.
///
/// HVORFOR OPTAGELSERNE LIGGER I SELVE TRÆET
///
/// Første udgave havde tre spalter: træ, liste, indhold. Det er én spalte for
/// meget. Stifinder har to — beholdere til venstre, indhold til højre — og
/// når man folder en mappe ud i Stifinder, står filerne dér i træet.
///
/// Det er samme opbygning her. Optagelser uden mappe ligger direkte under
/// biblioteket; optagelser i en mappe ligger under mappen. Så er der ét sted
/// at lede, og den plads, listen tog, er givet til teksten.
///
/// HVORFOR DER IKKE ER EN «UDEN MAPPE»-KNUDE
///
/// Der var en. Den var forkert: en mappe, appen selv fandt på, ved siden af
/// dem, brugeren havde lavet. Det, der ikke ligger i en mappe, ligger i roden
/// — som i ethvert andet filtræ. Vil man tage noget UD af en mappe igen,
/// trækker man det op på biblioteket.
/// </summary>
public sealed class Biblioteksnode : INotifyPropertyChanged
{
    public enum Slags { Bibliotek, Mappe, Optagelse }

    private Biblioteksnode(Slags slags, string navn, string glyf,
                           Transcribe.Gruppe gruppe, string? mappe)
    {
        Art = slags;
        Navn = navn;
        Glyf = glyf;
        Gruppe = gruppe;
        Mappe = mappe;
    }

    /// <summary>Et bibliotek: «Møder» eller «Arkiv». Roden i træet.</summary>
    public static Biblioteksnode Bibliotek(string navn, string glyf, Transcribe.Gruppe gruppe) =>
        new(Slags.Bibliotek, navn, glyf, gruppe, null);

    /// <summary>En af brugerens egne mapper under et bibliotek.</summary>
    public static Biblioteksnode Mappenode(string navn, Transcribe.Gruppe gruppe) =>
        new(Slags.Mappe, navn, "\uE8B7", gruppe, navn);

    /// <summary>En optagelse. Kan trækkes, men ikke slippes noget i.</summary>
    public static Biblioteksnode Optagelsesnode(Transcribe.OptagelseVisning o) =>
        new(Slags.Optagelse, o.Titel, o.ErSkrevetUd ? "\uE8A5" : "\uE720", o.Gruppe, o.Emnemappe)
        {
            Optagelse = o
        };

    public Slags Art { get; }
    public string Navn { get; }

    /// <summary>Tegnet fra Segoe MDL2 Assets. Efterprøvet mod skrifttypens cmap.</summary>
    public string Glyf { get; }

    public Transcribe.Gruppe Gruppe { get; }

    /// <summary>
    /// Hvilken mappe knuden står for eller ligger i. <c>null</c> = roden.
    /// </summary>
    public string? Mappe { get; }

    /// <summary>Sat på optagelsesknuder, null på alt andet.</summary>
    public Transcribe.OptagelseVisning? Optagelse { get; private init; }

    public bool ErBeholder => Art != Slags.Optagelse;

    public ObservableCollection<Biblioteksnode> Boern { get; } = new();

    private int _antal;

    /// <summary>Antal optagelser. Kun beholdere tæller.</summary>
    public int Antal
    {
        get => _antal;
        set { _antal = value; Ret(nameof(Antal)); Ret(nameof(Visning)); }
    }

    /// <summary>
    /// Teksten på knuden. Beholdere har et tal med, optagelser har deres
    /// dato og længde — samme oplysning, listen viste før.
    /// </summary>
    public string Visning => Art == Slags.Optagelse ? Navn : $"{Navn} ({Antal})";

    public string? Under => Optagelse?.Detaljer;

    private bool _erUdfoldet;

    /// <summary>
    /// Foldet ud eller ej.
    ///
    /// Alt starter foldet SAMMEN. Et træ, der åbner sig selv, skjuler sin egen
    /// form: man kan ikke se, hvor mange biblioteker der er, når det første
    /// fylder skærmen.
    /// </summary>
    public bool ErUdfoldet
    {
        get => _erUdfoldet;
        set { if (_erUdfoldet == value) return; _erUdfoldet = value; Ret(nameof(ErUdfoldet)); }
    }

    private bool _erValgt;
    public bool ErValgt
    {
        get => _erValgt;
        set { if (_erValgt == value) return; _erValgt = value; Ret(nameof(ErValgt)); }
    }

    private bool _erDropmaal;

    /// <summary>Musen holder noget hen over knuden lige nu.</summary>
    public bool ErDropmaal
    {
        get => _erDropmaal;
        set { if (_erDropmaal == value) return; _erDropmaal = value; Ret(nameof(ErDropmaal)); }
    }

    /// <summary>Alle knuder i træet, knuden selv først.</summary>
    public IEnumerable<Biblioteksnode> MedBoern()
    {
        yield return this;
        foreach (var b in Boern)
        foreach (var x in b.MedBoern())
            yield return x;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void Ret(string navn) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(navn));
}
