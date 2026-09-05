# Where your data goes

*What stays on the machine, what is sent — and what you can say about it*

## Stays on this PC

The audio, the transcripts, your notes and the documents.

The recording is made locally, and Whisper transcribes it locally. The app has
**no code** that could send an audio file anywhere.

It is a design choice, not a limitation waiting to be lifted. Speech
recognition in the cloud would be faster, and it has been rejected.

## The line runs at the transcript

The recording and the transcription happen here on the machine. They work
without a key and without a connection. Everything after that happens at the
supplier: the documents, the short summary and the dictation.

What is sent is the transcript's **text** — to Mistral AI, a French company,
subject to the GDPR directly. The app can only call the European endpoint;
that is locked in the code, not a setting.

Under **Compliance** every single transmission is listed: time, address,
model, number of characters, price and a checksum. The text itself is not
stored — it already sits with the meeting, and one more copy would be one more
place it could slip out from.

The line costs two things, and they should be said plainly. Without a key the
app can only record and transcribe. And without a connection — on a train, in
a meeting room with no network — you get the text and nothing else.

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

> «The meeting recording never leaves my PC; it is transcribed locally. What
> is sent onward is the text alone. The processing takes place
> at Mistral AI, a French company subject to the GDPR and covered by a data
> processing agreement, and the app calls only their European endpoint.»

## Dictation is the one exception

When you dictate — hold the shortcut key down and speak — **that one clip** is
sent to Mistral to be written out. It is your own voice, your own seconds, and
it happens only while you hold the key yourself.

Nothing changes for the people in the meeting. Their audio stays on the
machine, and from a meeting only the transcript is ever sent. The difference is
who chose: you pressed, they did not.

The same goes for the wake word. If you say «Hey Pia» and carry on speaking,
it is your own voice and your own seconds that are sent to be written out —
not anyone else's.

Dictation can be switched off under **Dictation → Settings**. With it off, no
audio leaves the machine at all.

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
