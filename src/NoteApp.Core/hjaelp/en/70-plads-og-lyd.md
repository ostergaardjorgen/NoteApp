# Space and audio files

*The audio is the only thing that takes up space — and it can be cleared*

An hour-long meeting with two tracks is about **220 MB**. The text from the
same meeting takes **56 KB**.

Everything else in the app — the notes, the tasks, the documents — together
takes less space than a single meeting does as audio.

## Why the audio is not compressed

It has been measured. A single language tolerates compression without loss,
but mixed Danish and English goes from 20.08 % to 40.57 % word errors at 24
and 32 kbit/s.

The app cannot know in advance what kind of meeting it will be. So the audio
is stored uncompressed — that is what gives the best transcript.

## Clear the audio after a while

Under **Settings → Files** you set how long the audio stays. The default is
**365 days**.

Three rules that cannot be broken:

- **Never without a transcript.** Without the text there is nothing left
  afterwards. Then it is not tidying up — then it is deleting.
- **Never at zero days or fewer.** A setting that accidentally becomes zero
  must not empty the archive.
- **Never anything but audio.** Notes, documents and the transcript stay
  untouched.

The age is measured on the **transcript's** date, not the audio's.

## Clear a single recording

Select the recording under Recordings and press the **Clear the audio** icon.
It only appears when there is both a transcript and an audio file — otherwise
there is nothing to clear.

## After a long meeting

If the meeting was over two hours, the app asks right after the transcript
whether the audio should be cleared straight away — and says how much space it
takes.

You are asked only **once**, at the point where you have just seen the text
and can judge whether it is good enough. A question a week later cannot be
answered.

The threshold can be changed under Settings.

## What you cannot do afterwards

Once the audio is gone, you can no longer transcribe the recording again with
a better model, run speaker recognition over, or listen back to check whether
the machine heard it right.

The last of those is what is most often needed when a name or a technical term
looks wrong. That is why the default is a whole year and not a month.
