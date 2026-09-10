# Recording

*Two tracks, four ways to start — and what happens if something goes wrong*

## The four ways to start

- **The button** at the top left. It always works.
- **The shortcut key**, anywhere in Windows. It sits next to the button, so
  you can see what it is set to.
- **The wake word**, followed by the command for recording a meeting. Nothing
  has to be touched. See below.
- **An event in the calendar** marked for automatic recording. Then it starts
  two minutes before the meeting — if the app is running.

## Meeting or webinar

A **meeting** records two tracks: your microphone and what the computer plays.
Both are transcribed, so it takes about twice as long — in return both sides
are there, and each line shows which side was speaking.

A **webinar** records only what the computer plays. You listen; you do not
speak. It takes half the space, is transcribed in half the time, and comes out
better: there is no echo from your own microphone and no overlapping speech.

If the meeting was in person and no sound arrived on the speaker track, the
empty track is deleted by itself when you stop. You do not have to choose
between «in person» and «online» in advance.

## Phone calls from your mobile

With **Phone Link** you can take calls on the computer — and HeyPia can record
the conversation like any other meeting.

### What Phone Link is — and what HeyPia does with it

Phone Link is Microsoft's own app. It ships with Windows 11; if it is missing,
get it from the Microsoft Store. It connects your phone to the computer so
calls, messages and notifications appear on screen.

**HeyPia does not touch Phone Link.** The two do not talk to each other, and
there is nothing to configure between them. When a call starts, Windows
connects the phone's audio to the computer over Bluetooth — and that is what
HeyPia notices. From there it is a recording like any other: the audio stays on
the machine, it is transcribed here, and you can make a document out of it.

That also means the order does not matter. If Phone Link is already running, it
works the moment you install HeyPia — and the other way round.

### Set up Phone Link — Android

You need a PC running Windows 10 (October 2022 update or later) or Windows 11,
and a phone running **Android 10 or later**. Phone and PC must be on the **same
Wi-Fi**.

1. Install **Link to Windows** on the phone — from Google Play or the Galaxy
   Store. On many Samsung phones it is already there.
2. Open **Phone Link** on the computer and choose **Android**.
3. Sign in to the app on the phone with **the same Microsoft account** as on
   the PC.
4. Scan the QR code on the PC screen with the phone, and grant the permissions
   it asks for.
5. Open **Calls** in Phone Link and press **Get started**. A prompt appears on
   the phone — tap **Allow**.

Calls need **Bluetooth** between phone and PC. Wi-Fi alone is not enough:
messages and photos travel over the network, but the audio goes over Bluetooth.

### Set up Phone Link — iPhone

You need a PC with **Bluetooth Low Energy (BLE)** and an iPhone running **iOS
16 or later**. You need a **personal** Microsoft account — a work or school
account will not do.

1. Turn on **Bluetooth** on both PC and iPhone.
2. Open **Phone Link** on the computer and choose **iPhone**.
3. Scan the QR code with the iPhone **camera** — no app to install first. *Link
   to Windows* exists in the App Store but is not needed for pairing.
4. Approve on the phone. Then turn on the permissions under **Settings →
   Bluetooth → ⓘ next to your PC**: system notifications, messages and
   contacts.

An iPhone gives you less than an Android — Apple opens up less. Calls, messages
and notifications work; apps and photos do not.

### Microsoft's own guides

The details change, and Microsoft has them first:

- [Requirements and setup](https://support.microsoft.com/en-us/windows/apps/phonelink/phone-link-requirements-and-setup)
- [Setting up calls](https://support.microsoft.com/en-us/windows/apps/phonelink/setting-up-calls-in-the-phone-link)
- [Frequently asked questions](https://support.microsoft.com/en-us/windows/apps/phonelink/frequently-asked-questions-about-the-phone-link)

### Then HeyPia needs two things

Once the phone is connected, two Windows settings remain. Both are about sound,
and both get missed.

1. **Let Phone Link use the microphone:** **Settings → Privacy → Microphone →
   Phone Link**.

   Without it Phone Link can *play* the call but not *capture* you. It sounds
   like a broken headset and is not: you can hear the other person, they
   cannot hear you.

2. **Make your headset the default communication device.** This is the decisive
   step, and it is not the same as setting the default device.

### The two defaults

Windows has **two** default speakers and **two** default microphones:

| | Used by |
|---|---|
| **Default device** | music, video, YouTube |
| **Default communication device** | calls — Phone Link, Teams, Zoom |

They can point in different directions, and often do: monitor speakers for
music, headset for meetings. Set only one and the call ends up somewhere other
than you think.

To set them:

1. Right-click the speaker icon by the clock → **Sound settings**
2. Scroll to **More sound settings**
3. The **Playback** and **Recording** tabs
4. Right-click your headset in each tab → **Set as default communication device**

If the phone is also paired directly over Bluetooth alongside Phone Link, it
appears in the list as an audio device too — usually *"… Hands-Free"*. If that
one is the communication device, Windows tries to use **the phone** as the
microphone instead of your headset, and the headset's microphone will not work.

### Making sure the whole conversation is captured

A call has two voices arriving two different ways: **yours** through the
microphone, **theirs** out of the speaker. HeyPia records both — but from the
devices selected in the app, under **Settings → Sound**.

**If those two do not point at the same place, the other person is missing from
the recording.** Your own voice is there, the transcript looks complete, and you
only find out when you go looking for something they said.

So: pick **the same headset** in three places — as the default communication
device in Windows (both tabs), and as microphone and speaker in HeyPia.

If you don't, the app says so. The bubble asking whether to record gets a yellow
line: *"Calls run on ‹device›, but HeyPia records from a different speaker."*
Fix it before you press record — afterwards is too late.

### The app asks when you call or get called

As soon as the conversation is running, HeyPia asks down by the clock whether it
should be recorded. That goes for calls you make and calls you take.

You pick the language in the same bubble: **Danish** is selected in advance, and
one click switches to **English**. Nothing else is asked — no folder and no
name, neither before nor after the call.

**Nothing is ever recorded on its own.** The app can only ask. And tell the
person you are talking to — the rules for recording a phone call are not the
same everywhere.

The recording lands in the **Opkald** (Calls) folder in the recordings list,
newest at the top. Only calls through Phone Link go there on their own — a Teams
meeting is a meeting and stays where meetings are.

It is named after when you spoke: **08-09-2026_22:45**. You do not name a call
while the phone is ringing, and without a name they would all read "Untitled".
If the folder is wrong, drag the recording to another one.

## The wake word

If you want neither to press anything nor to reach for a key, you can say it
instead: say the wake word and then what should happen.

The commands are your own list under **Dictation → Commands**, and you can
write more of them yourself. Nothing can run that is not on the list.

The word is **chosen between two** — «Hej Pia» and «Hey Pia». They are two
buttons under **Dictation → Commands**, and there is no field to type in. The
reason has been measured: the engine spreads its confidence across everything
it listens for, so two spellings of the same word compete with each other and
each look weak on their own.

Below the buttons you teach the app your pronunciation by saying **the chosen
word three times**. Then it looks for what your voice and your microphone
actually sound like, and not only for the letters. If you change the word
afterwards, you have to train again — otherwise the old word steals confidence
from the one you now say.

**The wake word works during a webinar.** A webinar records the speaker and
not your microphone, so what you say disturbs nothing — and that is exactly
where you sit listening to something else and think of something. During an
ordinary meeting it is switched off: there the microphone is in use for
something more important.

## Notes as you go

Write a note and press Enter. It takes the time it was written, so it can be
merged into the transcript in the right place afterwards.

**Ctrl+B** sets a bookmark without text — for when there is no time to write.

## Pause

The clock stops along with the audio, so the notes' timestamps keep matching.
Nothing is recorded meanwhile; the microphone is released.

## How the recordings stand in the tree

On the left under **Recordings** stand your own folders — the one for meetings,
the one for webinars, and any others you have made. At the bottom stand two the
app fills by itself: **Opkald** (Calls) and the archive. There is no longer a «Folders» root above them all. It answered a
question nobody asks, and it cost a whole level of indentation in a narrow
column.

Each recording takes **one line**. The date and the length are in the bubble
when the mouse rests on it — they are right enough, but they are rarely what
you are looking for, and they doubled the height of every single row.

To take a recording out of a folder or back out of the archive, drop it beside
the tree.

## What has happened to this recording

When you select a recording, three tabs appear above the content:
**Transcript**, **Documents** and **History**. They only appear then. Without
a selected recording they would be three buttons switching between three empty
panes.

**History** is the recording's own: what it used to be called, when the audio
was transcribed, which documents came out of it, and when it was moved or put
in the archive. Newest at the top.

It is not the same as **History** in the menu. That one is an operations log
of what the app has done, how long it took, and whether it went wrong. This
one is a case file for one meeting.

## If the machine goes down

The audio is written in thirty-second pieces as it goes. If the power fails,
or the app closes unexpectedly, you lose at most the last piece — not the
whole meeting.

The next time the app starts, it assembles the abandoned pieces into a
recording.

## How much space it takes

An hour-long meeting with two tracks is about 220 MB, because the audio is
stored uncompressed. That gives the best transcript. The text, by comparison,
takes 56 KB.

The audio can be deleted once the meeting has been transcribed — see **Space
and audio files**.
