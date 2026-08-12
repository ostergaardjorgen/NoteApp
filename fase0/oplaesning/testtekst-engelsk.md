---
navn: Engelsk prøvetekst
sprog: en
læsetid: ca. 5 minutter
formål: måle sprogdetektering og genkendelse på engelsk med dansk accent
---

# Engelsk prøvetekst

**Læs denne højt på engelsk.** Læs i dit normale mødetempo — ikke langsommere, og ret ikke dig selv undervejs. Det er netop din almindelige udtale, målingen skal ramme, ikke din bedste.

Teksten måler tre ting, som ikke kan måles på den danske tekst:

1. **Om Whisper finder engelsk.** En dansker, der taler engelsk, er det svære tilfælde — accenten trækker mod dansk, og detekteringen kan lande på `da`. Sker det, bliver hele mødet transskriberet som dansk volapyk.
2. **Hvor godt engelsk genkendes med dansk accent.** Fejlraten her er en anden end på dansk, og den skal kendes, før appen bruges til engelske møder.
3. **Om referatet kommer ud på dansk.** Skabelonen skal oversætte. Det er her, oversættelsen sker — ikke i Whisper, som kun kan oversætte *til* engelsk.

Facitlisten står nederst. Kig ikke på den, før du har læst.

---

Right, let's get started. Thanks for making the time — I know this was moved twice already.

Three things on the agenda today. The migration status, the budget question that came up on Friday, and then the support handover, which I suspect will take the longest.

Starting with the migration. We are through the first wave. Four hundred and twelve accounts moved over the weekend, and three hundred and eighty-eight of those went through without anyone touching them. That leaves twenty-four that needed manual work, which is a failure rate just under six percent. That is higher than I would like, but it is not alarming, and every single one of the twenty-four failed for the same reason: the department field was empty in the source system.

So the fix is not technical. Somebody needs to go through the remaining records and fill in that field before the second wave. Priya, can you take that? You have the access already.

Yes, I can do that. How long do I have?

The second wave is scheduled for the fourteenth, so anything before the twelfth works. And if you find more than fifty empty records, tell me straight away, because then we have a bigger problem than a data entry job.

Right. Second item, the budget. We have spent three hundred and one hours on this project so far against a budget of four hundred and fifty. On paper that looks fine. The problem is the hundred and twenty hours the integration vendor quoted for the ERP connector — that is not in the budget, because we assumed we would build it ourselves.

So we are either a hundred and twenty hours over, or we do not do the connector. Those are the two honest options, and I do not want us to pretend there is a third one.

What happens if we skip the connector?

Then deprovisioning stays manual, and the window between somebody leaving and their access being removed stays at sixteen hours. With the connector it drops to under a minute. That is the trade, and it is a security trade, not a convenience one.

Can we do something in between?

We can. We can flag the urgent cases in the nightly file and process those separately. That gets us to fifteen minutes instead of sixteen hours for the cases that actually matter. It is not clean, but it is sixty-four times better, and it costs us about eight hours of work instead of a hundred and twenty.

Let's do that. Marcus, write it up so we have the reasoning on record, because someone will ask about this in six months and none of us will remember.

Third item, the support handover. From the first of next month, first-line support moves to the service desk. What I need to be clear about is what they are allowed to do without asking us.

Password resets, yes. Group membership for the standard groups, yes. Anything touching the administrative roles, no — that comes to us, and it comes to us in writing, not on chat.

And emergency access?

Emergency access they can grant, but with a ceiling. Twenty-four hours, and a maximum of three extensions. After that it escalates to a named person, and that person is me until we agree otherwise. I do not want a situation where somebody has had emergency rights for three weeks because nobody was watching the clock.

One thing we have not settled: what happens when the service desk is closed. That is a real gap and I do not have an answer today. Let's take it offline.

Anything else? No? Good. Thanks everyone.

---

## Facitliste — kig først her efter oplæsningen

**Sproget skal detekteres som `en`.** Bliver det `da`, er den vigtigste måling allerede negativ, og engelske møder skal så låses med et sprogvalg frem for detektering.

**Tal, der skal stå rigtigt i transskriptionen:**

| Tal | Hvad |
|---|---|
| 412 | konti flyttet |
| 388 | gik automatisk igennem |
| 24 | krævede manuel behandling |
| 6 % | fejlrate |
| 50 | grænsen for "sig til med det samme" |
| 301 | timer brugt |
| 450 | timers budget |
| 120 | timer til ERP-connectoren |
| 16 timer | nuværende vindue |
| 15 minutter | vindue med mellemløsningen |
| 64 | faktoren det bliver bedre med |
| 8 | timers arbejde til mellemløsningen |
| 24 timer | loft på nødadgang |
| 3 | maksimale forlængelser |

**Tre beslutninger:** Priya udfylder afdelingsfeltet inden den 12.; mellemløsningen med markering i natfilen vælges frem for connectoren; nødadgang med loft på 24 timer og tre forlængelser.

**To opgaver med ejer:** Priya — udfylde afdelingsfeltet (inden den 12.). Marcus — skrive begrundelsen ned.

**Ét åbent spørgsmål:** hvad der sker, når servicedesken er lukket.

**Referatet skal komme ud på dansk**, selvom mødet var engelsk. Kommer det på engelsk, virker oversættelsen i skabelonen ikke.
