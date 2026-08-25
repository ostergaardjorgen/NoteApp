# When something goes wrong

*The questions that come up most often*

## The guests cannot be heard on the recording

The speaker track was empty. That happens if the meeting's audio went to a
different device from the one the app was listening on.

Choose the right speaker under **Settings → Audio** and press **Test
microphone and speaker** before the next meeting.

Do not mute inside the meeting program. Then no sound is played, and there is
nothing to record. If you would rather not listen along, turn down the Windows
volume by the clock — it still records.

## The transcript is in the wrong language

The guess was wrong. Run it again and choose the language yourself — you are
asked before it starts.

The result looks like finished text even when the language is wrong. That is
why it is often only spotted in the minutes.

## A name is misheard every time

Correct it in the line, and use **Learn the correction**. Then it becomes a
rule: the next time the same thing is misheard, the app fixes it itself — in
this transcript and in every future one.

## The recording did not start, even though the event was marked

The app was not running. A closed program cannot start a recording.

Turn on **Start NoteApp when I log in to Windows** under Settings. It takes up
nothing when it is not recording.

## The event from Google does not appear

It fetches every fifteen minutes and at startup. If you do not want to wait,
press the sync icon in the calendar's header.

If an error is shown under the icon, the connection has stopped working. Go to
**Settings → Integrations** and connect again.

## Documents cannot be made

The API key for the language model is not set up. Without it the app can
record and transcribe, but not make documents.

Go to **AI models**. It says there how the five minutes are spent.

## The transcription takes a very long time

There is no NVIDIA graphics card, so it runs on the processor. It works, but
an hour-long meeting takes longer than the meeting lasted.

Under **Settings → Machine requirements** you can see what is met on this
particular machine.

## The disk is filling up

The audio is the only thing that takes up space. See **Space and audio files**
— it can be cleared both automatically after a deadline and on a single
recording.

## The help itself

The texts here are ordinary files in the data folder under `hjaelp`. They can
be edited if something is imprecise — the app does not overwrite them.
