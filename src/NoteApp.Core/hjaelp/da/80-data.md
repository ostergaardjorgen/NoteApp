# Hvor dine data går hen

*Hvad der bliver på maskinen, hvad der sendes — og hvad du kan sige om det*

## Bliver på denne pc

Lyden, transkriptionerne, dine noter og dokumenterne.

Optagelsen laves lokalt, og Whisper skriver den ud lokalt. Appen har **ingen
kode**, der kan sende en lydfil nogen steder hen.

Det er et designvalg, ikke en begrænsning, der venter på at blive ophævet.
Talegenkendelse i skyen ville være hurtigere, og det er fravalgt.

## Det ene, der sendes

Skal der laves et dokument, sendes transkriptionens **tekst** til Mistral AI —
et fransk selskab, underlagt GDPR direkte. Appen kan kun kalde det europæiske
endepunkt; det er spærret i koden, ikke en indstilling.

Under **Compliance** står hver eneste afsendelse: tidspunkt, adresse, model,
antal tegn, pris og en kontrolsum. Selve teksten gemmes ikke — den ligger
allerede ved mødet, og en kopi mere ville være endnu et sted, den kunne slippe
ud fra.

## Derfor betyder det noget, at lyden bliver hos dig

Forskellen på en lydfil og en transkription er ikke en gradsforskel.

Lyden indeholder mere end ordene: hvem der talte kan høres, også når navnet
aldrig bliver sagt. Dertil kommer tonefald, tøven, accent og ting om helbred
og sindstilstand, som ingen har sagt højt. Intet af det følger med en
transkription.

Og en stemmeoptagelse kan blive til **biometriske data**, hvis den behandles
med det formål at genkende, hvem der taler. Tekst kan ikke laves om til et
stemmeaftryk.

## Det, du kan sige — og som holder hele vejen

> «Optagelse og transskription forlader aldrig min pc. Bearbejdningen sker hos
> Mistral AI, et fransk selskab underlagt GDPR og omfattet af
> databehandleraftale, og appen kalder alene deres europæiske endepunkt.»

## Det, du ikke skal sige

«Ingen amerikansk virksomhed er involveret» holder **ikke**. Mistral bruger
underleverandører, og flere af dem er amerikansk ejede, også når serverne står
i Europa.

«100 % europæisk» er et absolut ord om noget, hvor leverandørens egne
dokumenter tager forbehold.

En påstand, der ikke kan holdes hele vejen, er dyrere end en, der siger lidt
mindre. Hele gennemgangen står under **Compliance**.

## Dine filer

Alt dit ligger i datamappen — som standard `C:\AppNoter`. Den ligger uden for
programmet, så en afinstallation ikke rører den, og en sikkerhedskopi af den
ene mappe tager det hele med.

Du kan flytte den under **Indstillinger → Filer**.
