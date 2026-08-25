# Where your data goes

*What stays on the machine, what is sent — and what you can say about it*

## Stays on this PC

The audio, the transcripts, your notes and the documents.

The recording is made locally, and Whisper transcribes it locally. The app has
**no code** that could send an audio file anywhere.

It is a design choice, not a limitation waiting to be lifted. Speech
recognition in the cloud would be faster, and it has been rejected.

## The one thing that is sent

If a document is to be made, the transcript's **text** is sent to Mistral AI —
a French company, subject to the GDPR directly. The app can only call the
European endpoint; that is locked in the code, not a setting.

Under **Compliance** every single transmission is listed: time, address,
model, number of characters, price and a checksum. The text itself is not
stored — it already sits with the meeting, and one more copy would be one more
place it could slip out from.

## Why it matters that the audio stays with you

The difference between an audio file and a transcript is not a difference of
degree.

The audio holds more than the words: who spoke can be heard, even when the
name is never said. On top of that come tone of voice, hesitation, accent and
things about health and state of mind that nobody said out loud. None of that
travels with a transcript.

And a voice recording can become **biometric data** if it is processed for the
purpose of recognising who is speaking. Text cannot be turned back into a
voiceprint.

## What you can say — and what holds all the way

> «Recording and transcription never leave my PC. The processing takes place
> at Mistral AI, a French company subject to the GDPR and covered by a data
> processing agreement, and the app calls only their European endpoint.»

## What you should not say

«No American company is involved» does **not** hold. Mistral uses
sub-processors, and several of them are American-owned, even when the servers
stand in Europe.

«100 % European» is an absolute word about something where the supplier's own
documents make reservations.

A claim that cannot be held all the way is more expensive than one that says a
little less. The full review is under **Compliance**.

## Your files

Everything of yours lives in the data folder — by default `C:\AppNoter`. It
sits outside the program, so an uninstall does not touch it, and a backup of
that one folder takes everything with it.

You can move it under **Settings → Files**.
