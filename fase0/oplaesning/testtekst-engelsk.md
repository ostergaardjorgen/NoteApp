---
sprog: en
---

# Engelsk prøvetekst

**Læs denne højt på engelsk, i dit normale mødetempo.** Cirka 5 minutter. Ret ikke dig selv undervejs — det er din almindelige udtale, målingen skal ramme, ikke din bedste.

Den måler tre ting, den danske tekst ikke kan: om Whisper finder engelsk hos en dansker med accent, hvor godt accenten genkendes, og om referatet kommer ud på dansk alligevel.

Facit står i `facitliste-engelsk.md`. Kig først der bagefter.

---

## Blok 1 — migrering og budget [0:00 - 2:40]

Right, let's get started. Thanks for making the time — I know this was moved twice already.

Three things on the agenda today. The migration status, the budget question that came up on Friday, and then the support handover, which I suspect will take the longest.

Starting with the migration. We are through the first wave. Four hundred and twelve accounts moved over the weekend, and three hundred and eighty-eight of those went through without anyone touching them. That leaves twenty-four that needed manual work, which is a failure rate just under six percent.

That is higher than I would like, but it is not alarming, and every single one of the twenty-four failed for the same reason: the department field was empty in the source system.

So the fix is not technical. Somebody needs to go through the remaining records and fill in that field before the second wave. Priya, can you take that? You have the access already.

The second wave is scheduled for the fourteenth, so anything before the twelfth works. And if you find more than fifty empty records, tell me straight away, because then we have a bigger problem than a data entry job.

Second item, the budget. We have spent three hundred and one hours on this project so far, against a budget of four hundred and fifty. On paper that looks fine.

The problem is the hundred and twenty hours the integration vendor quoted for the ERP connector. That is not in the budget, because we assumed we would build it ourselves.

So we are either a hundred and twenty hours over, or we do not do the connector. Those are the two honest options, and I do not want us to pretend there is a third one.

## Blok 2 — sikkerhedsafvejningen og overdragelsen [2:40 - 5:20]

If we skip the connector, then deprovisioning stays manual, and the window between somebody leaving and their access being removed stays at sixteen hours. With the connector it drops to under a minute. That is the trade, and it is a security trade, not a convenience one.

There is something in between. We can flag the urgent cases in the nightly file and process those separately. That gets us to fifteen minutes instead of sixteen hours for the cases that actually matter.

It is not clean, but it is sixty-four times better, and it costs us about eight hours of work instead of a hundred and twenty. Let's do that.

Marcus, write it up so we have the reasoning on record, because someone will ask about this in six months and none of us will remember why we did it this way.

Third item, the support handover. From the first of next month, first-line support moves to the service desk. What I need to be clear about is what they are allowed to do without asking us.

Password resets, yes. Group membership for the standard groups, yes. Anything touching the administrative roles, no — that comes to us, and it comes to us in writing, not on chat.

Emergency access they can grant, but with a ceiling. Twenty-four hours, and a maximum of three extensions. After that it escalates to a named person, and that person is me until we agree otherwise.

I do not want a situation where somebody has had emergency rights for three weeks because nobody was watching the clock.

One thing we have not settled: what happens when the service desk is closed. That is a real gap and I do not have an answer today. Let's take it offline.

Anything else? No? Good. Thanks everyone.
