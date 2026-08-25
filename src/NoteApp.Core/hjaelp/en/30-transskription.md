# From audio to text

*What happens, how long it takes, and what you can correct*

Select a recording under **Recordings** and press **Create transcript**.

It all happens on your machine. No audio file leaves it — the app has no code
that could send one.

## How long it takes

With an NVIDIA graphics card it runs faster than the audio lasts. An
hour-long meeting takes about fifteen minutes.

Without a graphics card it runs on the processor. It works, but an hour-long
meeting takes longer than the meeting itself lasted.

The first run after a start spends half a minute loading the model. Nothing
happens on screen meanwhile — that is normal.

## Choose the right language

You are asked which language was spoken — one for your track and one for the
guests'. They do not have to be the same.

**Choose «detect it» only if you genuinely do not know.** If the guess is
wrong, the whole transcript comes out in the wrong language, and the result
looks like finished text. Then the mistake is only spotted in the minutes.

## How good it is

Measured against known answers: 92.3 % in Danish, 90.5 % in English, 83.1 %
when switching between the languages mid-sentence.

Names and technical terms are what most often come out wrong.

## Correct the text

Click into a line and type. It saves itself shortly after the last keystroke.

The corrections live in their own file. The machine's own version stays
untouched alongside and can be seen under the **Raw text** tab — so there is
always a way back.

It is the **corrected** text that documents and search use.

## Names of the speakers

Click the speaker next to a line and type the name. The name is stored as a
link, not inside the text, so it survives the transcript being run again.

The app can separate the two **sides** of an online meeting — your microphone
and what the computer plays. If three people sat at the other end, speaker
recognition can tell the voices apart, but it does not know their names. The
name is your judgement, not a measurement.

## Run it again

You can transcribe the same recording again with a different model or a
different language, as long as the audio is there. Your corrections and
speaker names are not overwritten — you are asked.
