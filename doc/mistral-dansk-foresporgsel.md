# Forespørgsel til Mistral om dansk i Voxtral

## Hvor den skal sendes

| Vej | Hvornår |
|---|---|
| `support@mistral.ai` | Almindelig vej. Skriv på engelsk. |
| Support i konsollen på `console.mistral.ai` | Bedst, hvis du er betalende kunde — sagen knyttes til din konto |
| Mistrals Discord | Hurtigst svar, men uforpligtende. God til at høre, om andre spørger om det samme |

Konsollen er den, der tæller. En henvendelse fra en betalende konto vejer
mere i en produktbeslutning end en mail fra en tilfældig adresse — og de kan
se dit forbrug, hvilket er dét, der gør spørgsmålet konkret for dem.

**Tjek adressen i konsollen, før du sender.** Den er den eneste, der er
autoritativ; alt andet kan være forældet.

## Hvorfor målingerne skal med

Et spørgsmål om en køreplan bliver som regel besvaret med «vi kan ikke
kommentere på fremtidige udgivelser». Et spørgsmål med et tal i bliver
besvaret af et menneske, fordi det ligner en fejlrapport frem for en
ønskeseddel.

Derfor står afvisningen ordret i mailen, og derfor står det, at valget koster
dem noget: dikteringen kører nu lokalt i stedet for på deres API.

---

## Udkast

> **Emne:** Danish (`da`) support in Voxtral transcription — roadmap?
>
> Hi,
>
> We build a Danish dictation and meeting-notes application and use Voxtral
> for transcription through the EU endpoint. We would like to ask about
> Danish language support.
>
> Today the `language` parameter rejects `da`. Verified against
> `api.eu.mistral.ai/v1/audio/transcriptions` on 31 August 2026, with both
> `voxtral-mini-latest` and `voxtral-mini-2602`:
>
> ```
> Got unsupported language `da`, should be one of:
> ['ar', 'en', 'de', 'es', 'fr', 'hi', 'it', 'nl', 'pt', 'zh', 'ru', 'ko', 'ja']
> ```
>
> The model transcribes Danish well when it guesses right. The problem is
> that it cannot be told. Without a language parameter, the same Danish
> dictation came back as French, German and Dutch on three separate
> occasions. When that happens, every single word is wrong — it is not a
> quality issue but a language selection issue.
>
> We tried influencing it through the `prompt` field, which helps but is not
> a guarantee. It also conflicts with the other use of that field: our users'
> glossaries are mostly English technical terms, and forty English words
> outweigh one Danish sentence.
>
> Two questions:
>
> 1. Is Danish on the roadmap for the transcription API, and is there a
>    timeframe we can plan around?
> 2. If not, would you consider accepting `da` and letting the model do its
>    best, rather than rejecting the request? The model clearly handles
>    Danish — it simply will not take it as an instruction.
>
> For context on why this matters to us: we have had to move dictation
> transcription off your API and onto local whisper.cpp, purely because
> `-l da` works there. We would rather use Voxtral, and would move back the
> day Danish is accepted.
>
> Happy to share sample audio and our measurements if that is useful.
>
> Best regards,
> [dit navn]
> [firma, hvis du vil oplyse det]

---

## Hvis der ikke kommer svar

Discord er værd at prøve parallelt. Andre nordiske udviklere står med samme
problem — svensk og norsk er heller ikke på listen — og flere stemmer om det
samme flytter mere end én.
