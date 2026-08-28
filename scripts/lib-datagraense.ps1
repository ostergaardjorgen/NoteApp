<#
.SYNOPSIS
    Håndhæver grundprincippet: intet forlader den pc, appen kører på.

.DESCRIPTION
    Princippet står i doc\mine-data.md og som afsnit 0 i SPEC_HeyPia_v1.md:
    ingen feature må introducere en risiko for, at data kan forlade maskinen.

    Kan en funktion i sagens natur alligevel flytte data væk — det gælder reelt
    kun brugerens eget valg af en destination på en netværkssti — skal tre krav
    være opfyldt, og de er implementeret her:

      1. Aktiv brugerbeslutning, aldrig standard og aldrig en sideeffekt.
      2. Godkendelse i selve øjeblikket: kørslen stopper og venter på svar.
      3. Informeret godkendelse: risikoen listes konkret, før der spørges.

    Kan kravene ikke opfyldes — en planlagt opgave har ingen til at godkende —
    nægter koden at køre i stedet for at gætte sig til et ja.

    Filen dot-sources af backup-mine-data.ps1 og planlaeg-backup.ps1. Den ligger
    ét sted med vilje: to kopier af en risikoliste driver fra hinanden, og så
    står brugeren med to forskellige beslutningsgrundlag for samme beslutning.
#>

function Get-HeyPiaDataRod {
    <#
    .SYNOPSIS
        Hvor brugerens data ligger — samme rækkefølge som appen bruger.

    .DESCRIPTION
        Appen kan flytte datamappen, og gør den det, skriver den den nye sti i
        %APPDATA%\NoteApp\datasti.txt. Slår scriptet ikke det samme op, tager
        det backup af det forkerte sted uden at opdage det — og en backup, der
        sikrer en tom mappe, er værre end ingen, fordi man tror man er dækket.

        Rækkefølge: NOTEAPP_DATA, pegefilen, standarden C:\AppNoter.
    #>
    if (-not [string]::IsNullOrWhiteSpace($env:NOTEAPP_DATA)) {
        return [IO.Path]::GetFullPath($env:NOTEAPP_DATA)
    }

    $peger = Join-Path $env:APPDATA 'NoteApp\datasti.txt'
    if (Test-Path $peger) {
        $valgt = (Get-Content $peger -Raw -Encoding UTF8).Trim()
        if (-not [string]::IsNullOrWhiteSpace($valgt)) { return [IO.Path]::GetFullPath($valgt) }
    }

    return 'C:\AppNoter'
}

function Test-ForladerMaskinen {
    <#
    .SYNOPSIS
        Sand hvis stien ligger uden for den fysiske maskine.
    #>
    param([Parameter(Mandatory)][string] $Sti)

    if ($Sti.StartsWith('\\')) { return $true }
    try {
        $rod = [IO.Path]::GetPathRoot($Sti)
        if ([string]::IsNullOrWhiteSpace($rod)) { return $false }
        return (New-Object IO.DriveInfo $rod).DriveType -eq 'Network'
    } catch {
        # Kan drevtypen ikke afgoeres, behandles stien som lokal. Et
        # utilgaengeligt drev fejler alligevel hoejlydt, naar der skrives.
        return $false
    }
}

function Show-DatagraenseRisiko {
    <#
    .SYNOPSIS
        Krav 3: lister hvad beslutningen konkret indebærer, før der spørges.
    #>
    param([Parameter(Mandatory)][string] $Sti)

    Write-Host ""
    Write-Host "  STOP — destinationen ligger uden for denne maskine" -ForegroundColor Red
    Write-Host ""
    Write-Host "  $Sti"
    Write-Host ""
    Write-Host "  Hvad du er ved at beslutte:" -ForegroundColor Yellow
    Write-Host "    * Arkivet indeholder ALT: mødeoptagelser som lyd, transskriptioner,"
    Write-Host "      dine noter, ordlisten og learning.db med indlærte rettelser."
    Write-Host "      Det er råt indhold fra rigtige møder med rigtige mennesker."
    Write-Host "    * Zip-filen er IKKE krypteret — hverken undervejs eller når den ligger der."
    Write-Host "    * Alle med adgang til destinationen kan åbne den. Det gælder også"
    Write-Host "      administratorer, snapshots og backup af det system, den lander på."
    Write-Host "    * Mødedeltagerne har ikke sagt ja til dette. Du optog dem på din egen pc."
    Write-Host "    * Det kan ikke fortrydes. En kopi, der først er ude, er ude."
    Write-Host ""
    Write-Host "  Alternativer, der holder alt lokalt:" -ForegroundColor Green
    Write-Host "    * Standarden: $(Join-Path $env:USERPROFILE 'HeyPia-backup')"

    # Drevbogstaver siger intet om, hvor et drev fysisk ligger: paa denne
    # maskine er baade E: og H: mappede shares. Derfor gaettes der ikke paa et
    # bogstav i teksten — der spoerges paa maskinen, hvad der faktisk er lokalt.
    $lokale = [IO.DriveInfo]::GetDrives() |
        Where-Object { $_.IsReady -and $_.DriveType -in 'Fixed', 'Removable' } |
        ForEach-Object { $_.Name.TrimEnd('\') }
    if ($lokale) {
        Write-Host "    * Et lokalt drev, fx -Destination '$($lokale[-1])\NoteApp-backup'"
        Write-Host "      Lokale drev lige nu: $($lokale -join ', ')" -ForegroundColor DarkGray
    }
    Write-Host ""
}

function Confirm-ForladerMaskinen {
    <#
    .SYNOPSIS
        Krav 1 og 2. Returnerer $true hvis handlingen må fortsætte, $false hvis
        brugeren afbrød. Kaster, hvis der ikke er nogen til at godkende.

    .PARAMETER Forhaandsgodkendt
        Sat når valget allerede er truffet foran skærmen i en tidligere kørsel
        — det er sådan en planlagt opgave kan arve en godkendelse. Springer
        spørgsmålet over, men ikke visningen af risikoen.

    .PARAMETER Uinteraktiv
        Sat når kørslen ikke har nogen at spørge (fx -Stille fra en opgave).
    #>
    param(
        [Parameter(Mandatory)][string] $Sti,
        [switch] $Forhaandsgodkendt,
        [switch] $Uinteraktiv
    )

    Show-DatagraenseRisiko -Sti $Sti

    if ($Forhaandsgodkendt) {
        Write-Host "  Fortsætter — valget er godkendt på forhånd." -ForegroundColor Yellow
        Write-Host ""
        return $true
    }

    if ($Uinteraktiv -or -not [Environment]::UserInteractive) {
        throw ("Destinationen ligger uden for maskinen, og der er ingen til at godkende " +
               "det i denne kørsel.`n" +
               "Der er IKKE gjort noget, og intet har forladt maskinen.`n" +
               "Vælg en lokal destination, eller kør kommandoen manuelt én gang og " +
               "godkend valget der.")
    }

    $svar = Read-Host "  Skriv JA for at lade data forlade maskinen (alt andet afbryder)"
    Write-Host ""
    return ($svar -ceq 'JA')
}

function Write-DatagraenseLog {
    <#
    .SYNOPSIS
        Skriver beslutningen til backup-loggen, så den kan genfindes bagefter.
        Fejler aldrig kørslen: en manglende logline må ikke koste en backup.
    #>
    param(
        [Parameter(Mandatory)][string] $DataMappe,
        [Parameter(Mandatory)][string] $Tekst
    )

    try {
        $logMappe = Join-Path $DataMappe 'log'
        New-Item -ItemType Directory -Force $logMappe | Out-Null
        Add-Content -Path (Join-Path $logMappe 'backup.log') `
                    -Value ('{0}  {1}' -f (Get-Date -Format 's'), $Tekst) -Encoding UTF8
    } catch { }
}
