using System.IO;
using System.Windows;
using NoteApp.Core;
using NoteApp.Core.Deling;

namespace NoteApp.Desktop.Transcribe;

/// <summary>
/// «Skriv ud på den anden computer».
/// </summary>
/// <remarks>
/// ============ DET ER DÉT, TO MASKINER ER TIL FOR ============
///
/// Den bærbare optager mødet. Whisper på en processor tager længere tid, end
/// mødet selv varede — og imens er maskinen varm, langsom og på batteri.
/// Knappen her sender lyden over til den kraftige, som har grafikkortet, og
/// henter udskriften hjem igen.
///
/// ============ DER SENDES ALDRIG NOGET AF SIG SELV ============
///
/// Lyden er det mest private, appen har. Den går kun ud, når nogen trykker på
/// knappen, og kun til en computer, der er godkendt med koden på begge skærme.
/// Dialogen siger, hvor meget der sendes, og hvor det lander.
///
/// ============ SPØRGSMÅLET OM SPROG ER DET SAMME ============
///
/// Den kører gennem det samme sprogvindue som en lokal kørsel. Sproget er
/// ikke en detalje: vælges det forkert, er hvert eneste ord i udskriften
/// forkert — og det er lige så sandt, når den anden computer skriver den ud.
/// </remarks>
public partial class TranscribeView
{
    /// <summary>Den computer, der kan tage arbejdet. Null, når der ikke er nogen.</summary>
    /// <remarks>
    /// TRE KRAV, OG DE ER DER ALLE TRE AF EN GRUND: den skal være godkendt
    /// begge veje (ellers bliver arbejdet afvist i den anden ende), den skal
    /// have meldt sig for nylig (ellers ligger lyden og venter på en slukket
    /// maskine), og den skal være den primære (den bærbare skal ikke sende
    /// arbejde til en anden bærbar).
    /// </remarks>
    private static Maskinoplysning? Modtager()
    {
        if (!Delt.Slaaet_til) return null;

        return Delt.Andre()
                   .Where(m => Parring.MaaUdveksle(m)
                               && m.HarGodkendt(Maskinid.Id) != false
                               && m.ILive
                               && m.Rolle == Maskinrolle.Primaer)
                   .OrderByDescending(m => m.SidstSet)
                   .FirstOrDefault();
    }

    /// <summary>Viser eller skjuler knappen. Kaldes hver gang en optagelse vælges.</summary>
    private void VisSendknap()
    {
        if (SendKnap is null) return;

        var modtager = Modtager();

        SendKnap.Visibility = modtager is null ? Visibility.Collapsed : Visibility.Visible;

        if (modtager is null) return;

        SendKnap.Content = Sprog.T("deling.skriv_ud_paa", modtager.Navn);
        SendKnap.ToolTip = Sprog.T("deling.skriv_ud_paa_hjaelp", modtager.Navn);
        SendKnap.IsEnabled = Valgt is not null && Valgt.HarLyd && !Sendt(Valgt);
    }

    /// <summary>Ligger optagelsen allerede i køen?</summary>
    private static bool Sendt(OptagelseVisning valgt)
    {
        var meta = MeetingStore.Load(valgt.Mappe);

        if (meta is null) return false;

        return Arbejdskoe.Mine().Any(o => o.Moede == meta.Id.ToString());
    }

    private void Send_Click(object sender, RoutedEventArgs e)
    {
        if (Valgt is not { } valgt) return;

        if (Modtager() is not { } modtager)
        {
            Dialogs.AppDialog.Vis(Window.GetWindow(this),
                Sprog.T("deling.ingen_modtager_titel"),
                Sprog.T("deling.ingen_modtager"), Dialogs.Slags.Pas_paa);

            return;
        }

        var meta = MeetingStore.Load(valgt.Mappe);

        if (meta is null)
        {
            Dialogs.AppDialog.Vis(Window.GetWindow(this),
                Sprog.T("deling.kan_ikke_sende"),
                Sprog.T("deling.moedet_mangler"), Dialogs.Slags.Pas_paa);

            return;
        }

        // ---------- hvilke spor er der? ----------
        var wav = OptagelseVisning.Lydfilen(valgt.Mappe);
        var loop = Path.Combine(valgt.Mappe, "loopback.wav");

        var kunLoop = string.Equals(wav, loop, StringComparison.OrdinalIgnoreCase);
        var toSpor = !kunLoop && File.Exists(loop);

        if (!File.Exists(wav))
        {
            Dialogs.AppDialog.Vis(Window.GetWindow(this),
                Sprog.T("deling.kan_ikke_sende"),
                Sprog.T("deling.ingen_lyd"), Dialogs.Slags.Pas_paa);

            return;
        }

        // ---------- sproget ----------
        var sprogvalg = new SprogvalgWindow(toSpor, meta.ValgtSprogMik, meta.ValgtSprogLoop,
                                            valgt.Titel, kunLoop)
        { Owner = Window.GetWindow(this) };

        sprogvalg.ShowDialog();

        if (!sprogvalg.Godkendt) return;

        var mit = sprogvalg.MitSprog;
        var deres = sprogvalg.DeresSprog ?? mit;

        // ---------- sig hvad der sendes ----------
        var fylder = Fylder(wav) + (toSpor ? Fylder(loop) : 0);

        var ja = Dialogs.AppDialog.Spoerg(Window.GetWindow(this),
            Sprog.T("deling.send_titel", valgt.Titel, modtager.Navn),
            Sprog.T("deling.send_tekst", valgt.Titel, Megabyte(fylder), modtager.Navn),
            godkend: Sprog.T("deling.send_ja"),
            annuller: Sprog.T("faelles.annuller"),
            slags: Dialogs.Slags.Valg);

        if (!ja) return;

        // ---------- af sted ----------
        var model = AppSettings.Current.PreferredModel ?? WhisperInstall.Standard.Id;
        var moede = meta.Id.ToString();

        try
        {
            Status.Text = Sprog.T("deling.sender", modtager.Navn);

            if (kunLoop)
            {
                Arbejdskoe.Laeg(modtager, moede, "loopback", loop, mit, model, "",
                                Transcriber.WavSeconds(loop));
            }
            else
            {
                Arbejdskoe.Laeg(modtager, moede, "mikrofon", wav, mit, model, "",
                                Transcriber.WavSeconds(wav));

                if (toSpor)
                    Arbejdskoe.Laeg(modtager, moede, "loopback", loop, deres, model, "",
                                    Transcriber.WavSeconds(loop));
            }

            // SPROGVALGET HUSKES PAA MOEDET, praecis som ved en lokal
            // koersel. Skrives det om igen, skal der ikke spoerges forfra.
            meta.ValgtSprogMik = mit;
            if (toSpor) meta.ValgtSprogLoop = deres;
            MeetingStore.Save(valgt.Mappe, meta);

            Historik.Skriv(HaendelseType.Transskription,
                Sprog.T("deling.historik_sendt", modtager.Navn),
                Sprog.T("deling.historik_sendt_detalje", valgt.Titel, Megabyte(fylder)),
                Udfald.Fuldført);

            Status.Text = Sprog.T("deling.sendt", modtager.Navn);
        }
        catch (Exception ex)
        {
            Status.Text = "";

            Dialogs.AppDialog.Vis(Window.GetWindow(this),
                Sprog.T("deling.kan_ikke_sende"), ex.Message, Dialogs.Slags.Fejl);
        }

        VisSendknap();
    }

    private static long Fylder(string sti)
    {
        try { return new FileInfo(sti).Length; }
        catch (Exception) { return 0; }
    }

    private static string Megabyte(long byte_) => $"{byte_ / 1024.0 / 1024.0:0} MB";
}
