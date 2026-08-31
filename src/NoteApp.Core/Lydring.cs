namespace NoteApp.Core;

/// <summary>
/// En ring af lydprøver, der hele tiden overskriver sig selv.
///
/// HVORFOR DEN FINDES
///
/// Vågeordet høres af én motor, og først NÅR den har meldt det, blev
/// mikrofonen åbnet til selve optagelsen. Der gik derfor tid — motoren skal
/// høre ordet færdigt, afgøre det, og lydenheden skal åbnes — og i den tid
/// talte man ud i ingenting.
///
/// Målt 30-08-2026: motoren brugte mellem 60 og 555 millisekunder på at
/// afgøre ordet, og oven i lå åbningen af enheden. De første ord efter «Hej
/// Pia» blev klippet, og man skulle vente på boblen for at være sikker.
///
/// Ringen løser det ved at vende det om: lyden er der allerede. Mikrofonen er
/// åben, mens der lyttes, og de sidste sekunder ligger klar i hukommelsen. Når
/// ordet høres, tages de med tilbage i tiden.
///
/// DEN SKRIVER ALDRIG NOGET NED. Ringen er hukommelse og kun hukommelse: den
/// overskriver sig selv i ring, så det, der er faldet ud, ikke findes nogen
/// steder. Først når vågeordet ER hørt, bliver noget til en fil. Det er den
/// samme aftale som før — se compliance-skærmen.
/// </summary>
public sealed class Lydring
{
    private readonly short[] _proever;
    private int _skriv;
    private bool _fyldt;

    /// <summary>Hvor mange prøver ringen kan holde.</summary>
    public int Plads => _proever.Length;

    /// <summary>Hvor mange prøver der ligger i den lige nu.</summary>
    public int Antal => _fyldt ? _proever.Length : _skriv;

    public Lydring(int proever)
    {
        if (proever <= 0) throw new ArgumentOutOfRangeException(nameof(proever));
        _proever = new short[proever];
    }

    /// <summary>Ringen, der rummer <paramref name="sekunder"/> ved den givne frekvens.</summary>
    public static Lydring Til(double sekunder, int frekvens) =>
        new(Math.Max(1, (int)(sekunder * frekvens)));

    /// <summary>
    /// Lægger prøver i ringen. Er den fuld, falder de ældste ud.
    /// </summary>
    public void Skriv(ReadOnlySpan<short> nye)
    {
        // Er der flere prøver end hele ringen, er kun halen interessant. Uden
        // det ville en stor blok koere hele ringen igennem flere gange.
        if (nye.Length >= _proever.Length)
        {
            nye[^_proever.Length..].CopyTo(_proever);
            _skriv = 0;
            _fyldt = true;
            return;
        }

        foreach (var p in nye)
        {
            _proever[_skriv] = p;
            _skriv = (_skriv + 1) % _proever.Length;
            if (_skriv == 0) _fyldt = true;
        }
    }

    /// <summary>
    /// Henter indholdet i den rækkefølge, det blev sagt — ældst først.
    /// </summary>
    public short[] Laes()
    {
        var antal = Antal;
        var ud = new short[antal];

        if (!_fyldt)
        {
            Array.Copy(_proever, 0, ud, 0, antal);
            return ud;
        }

        // Fra skrivepunktet og rundt: dét er den aeldste proeve.
        var foerste = _proever.Length - _skriv;
        Array.Copy(_proever, _skriv, ud, 0, foerste);
        Array.Copy(_proever, 0, ud, foerste, _skriv);

        return ud;
    }

    /// <summary>Tømmer ringen.</summary>
    public void Ryd()
    {
        Array.Clear(_proever);
        _skriv = 0;
        _fyldt = false;
    }
}
