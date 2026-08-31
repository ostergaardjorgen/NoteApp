using NoteApp.Core;
using Xunit;

namespace NoteApp.Tests;

/// <summary>
/// Tallene om grafikkortet, laest af motorens egen udskrift.
/// </summary>
/// <remarks>
/// LINJERNE ER KOPIERET FRA EN RIGTIG KOERSEL 31-08-2026 og ikke skrevet
/// efter hukommelsen. Et format, der er gaettet, bestaar sin egen proeve og
/// fejler i virkeligheden - det er praecis dét, der skete med feltnavnet
/// «audio_seconds» i dikteringen.
/// </remarks>
public class GrafikmaalTest
{
    private const string VramLinje =
        "ggml_cuda_init: found 1 CUDA devices (Total VRAM: 6143 MiB):";

    private const string ModelLinje =
        "whisper_model_load:        CUDA0 total size =   487.01 MB";

    [Fact]
    public void Kortets_hukommelse_laeses()
    {
        Assert.Equal(6143, Grafikmaal.Kort(VramLinje));
    }

    [Fact]
    public void Modellens_stoerrelse_laeses()
    {
        Assert.Equal(487.01, Grafikmaal.Model(ModelLinje));
    }

    [Fact]
    public void Procenten_er_modellens_andel_af_kortet()
    {
        var m = new Grafikmaal(487.01, 6143);

        // 487 af 6143 er knap otte procent.
        Assert.InRange(m.Procent, 7.8, 8.0);
    }

    [Fact]
    public void Uden_grafikkort_er_der_ingenting_at_laese()
    {
        // Paa CPU staar der ikke CUDA i linjen, og saa er svaret ingenting -
        // ikke nul. Nul ville paastaa, at modellen fylder intet paa et kort,
        // den slet ikke ligger paa.
        Assert.Null(Grafikmaal.Model("whisper_model_load:  CPU total size = 487.01 MB"));
        Assert.Null(Grafikmaal.Kort("noget helt andet"));
        Assert.Null(Grafikmaal.Model(null));
        Assert.Null(Grafikmaal.Kort(null));
    }

    [Fact]
    public void Et_kort_paa_nul_giver_ikke_en_division_med_nul()
    {
        Assert.Equal(0, new Grafikmaal(487, 0).Procent);
    }
}
