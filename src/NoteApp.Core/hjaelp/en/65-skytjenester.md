# Cloud service integration

*OneDrive, Google Drive, iCloud, Dropbox — how a recording from your Apple or Android device reaches HeyPia on its own*

Most meetings do not happen at the desk. A conversation in the car, an
agreement in the corridor, a walkthrough at the customer's site — the phone
is the recorder, and it is already in your pocket.

This section describes how to set the chain up once. After that there are no
manual steps: you record on the phone, and the recording is waiting in HeyPia
the next time you open the app.

## The whole path in four links

1. **You record** on your iPhone, iPad or Android phone with the recorder
   already on the device.
2. **The recording is saved to a cloud folder** — iCloud Drive, OneDrive,
   Google Drive or Dropbox.
3. **The cloud service's program on the PC** syncs that folder to disk.
4. **HeyPia watches the folder** and offers the file on Recordings.

Links 1 and 2 happen on the phone. Links 3 and 4 you set up once on the PC.

## Why nothing is signed in to

HeyPia never asks for access to your Google, Microsoft or Apple account in
order to fetch recordings. There is no password, no consent screen and no
additional supplier that gains access to your data.

The reason is simple: iCloud, OneDrive, Google Drive, Dropbox, Nextcloud and
Synology Drive all have a Windows program that syncs to an ordinary folder on
disk. HeyPia sees a folder. Nothing else.

It also means the chain works with any service that can put a folder on your
PC — including one we have never heard of.

## Step 1 — pick the service you already use

Pick the one you already have. There is no advantage in adding a new one.

| Service | Typically suits | The program on the PC |
| --- | --- | --- |
| iCloud Drive | iPhone and iPad | iCloud for Windows |
| Google Drive | Android | Google Drive for desktop |
| OneDrive | Windows and Microsoft 365 | Ships with Windows |
| Dropbox | Either | Dropbox for Windows |
| Nextcloud, Synology Drive | Own server or NAS | The service's own program |

## Step 2 — install the cloud service's program on the PC

Without it the files stay in the cloud, and then there is no folder to watch.
Get the program from the service itself:

- **Apple:** "iCloud for Windows" from the Microsoft Store.
- **Google:** "Google Drive for desktop" from google.com/drive/download.
- **Microsoft:** OneDrive is already on the machine; you only need to sign in.
- **Dropbox:** the Dropbox program from dropbox.com/install.

Sign in with the **same account as on the phone**. That is the whole point:
the account is what ties the two devices together.

## Step 3 — point HeyPia at the folder

Open **Settings → Files**. Under "Watched folders" the app finds the cloud
services installed on the machine by itself and suggests one folder in each:
`<cloud service>\HeyPia`.

For each row you can:

- **edit the path** — the suggestion is only a suggestion. If you already use
  a different folder for your recordings, write that one instead.
- **Browse** — find the folder instead of typing it. The picker opens where
  the path points.
- **Save** — creates the folder if it is missing and starts watching it. The
  line below confirms what was saved.
- **remove the row** — if you do not use the service, it should not be there.

Tick **"Import automatically"** if recordings should come in without you
deciding each time. Without the tick every file is offered at the top of
Recordings, and you say yes or no. The default is off, because a shared
folder can receive an audio file that is not a meeting.

See **Audio files from elsewhere** for the details of how folders are
watched.

## Step 4 — record on the phone

### iPhone and iPad

1. Open **Voice Memos** and record.
2. Tap the recording, choose **Share → Save to Files**.
3. Choose **iCloud Drive → HeyPia**.

If you use OneDrive, Google Drive or Dropbox on your iPhone instead, they
appear in the same share menu. Pick the HeyPia folder in the service you set
up in step 3.

If you record often, a shortcut in the Shortcuts app that saves straight to
the folder is worth the two minutes. Then it is one tap instead of three.

### Android

1. Record with **Recorder** (Google Recorder), **Voice Recorder** or whatever
   recorder came with the phone.
2. Tap **Share** and choose **Google Drive**, **OneDrive** or **Dropbox**.
3. Choose the HeyPia folder.

On many Android phones the recorder can be set to save straight into a synced
folder. If that option is there, use it — then steps 2 and 3 disappear
entirely.

## What happens next

HeyPia looks in the folders **every minute** while the app is open, and
immediately when it starts. A file must have been still for **twenty seconds**
before it counts as finished — otherwise it could be read while it is still
being downloaded.

The recording is dated by the **file's own date**. A conversation from
Tuesday sorts as Tuesday, even if it only reaches the PC on Thursday.

Every file is offered **once**. Say no, and it does not come back.

## Files that only exist in the cloud

iCloud and OneDrive often leave the file in the cloud and show only a
placeholder on disk: right name, right size, no content.

HeyPia **never reads inside a file** while scanning — only name, size and
date. Otherwise the app would pull the whole cloud folder down just by
looking. A placeholder is still a find; it is downloaded when you import it,
and you are told so.

## Recordings from a phone have one track

A recording HeyPia makes itself has two sides: your microphone and the people
in the room. A file from elsewhere has one. Everything sits under the same
speaker no matter how many people took part, and it cannot be separated
afterwards.

That does not make the transcript worse — it only means it does not say who
said what.

## What does not happen

- **Your source file is never touched.** It is copied. The original stays in
  the cloud folder, so the phone keeps it.
- **Nothing is sent to the cloud service.** HeyPia reads a folder; it does
  not write to it and does not sign in to anything.
- **The audio does not leave your machine.** The recording is transcribed
  locally. Only the text goes further, and only when you ask for a document.

## When it does not work

**The file does not appear.** Check that the cloud service's program is
running and that the folder really exists on disk — open it in File Explorer.
If there is a cloud icon next to the file it has not been downloaded yet;
that is still a find, but wait for the sync to finish.

**The folder is not in the list.** The app finds cloud services by asking
Windows where they sync to. If the program was just installed, restart
HeyPia. Otherwise you can always add the folder with **Add folder**.

**The file was offered and I said no by mistake.** Use **Forget what has been
seen** under Settings → Files. Every file in the folders is then offered
again.

**Wrong file type.** `.m4a`, `.mp3`, `.wav`, `.aac`, `.mp4`, `.wma` and
`.flac` are read. Both iPhone and Android record in one of them by default.
