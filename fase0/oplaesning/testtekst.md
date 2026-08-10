# Fase 0 — oplæsningstekst

**Læs denne tekst højt i normalt taletempo.** Den er på 2349 ord, hvilket giver cirka 20 minutter ved 120 ord i minuttet — læser du hurtigt, bliver det 18, læser du langsomt, bliver det 21. Alle tre dele er fint.

Læs den som du ville tale til en kollega, ikke som en oplæsning. Pauser, vejrtrækning og små tøvelyde er en del af testen; det er dem, der gør optagelsen realistisk.

**Inden du begynder:**

- Læs **ikke** overskrifterne højt. De er kun til dig.
- Tidsmarkørerne i kantede parenteser er tjekpunkter ved 120 ord i minuttet. Er du langt fra dem, så justér tempoet — men lad være med at jage tiden på bekostning af en naturlig oplæsning.
- **Udskift navnene.** Jeg har brugt opdigtede navne, fordi jeg ikke kender dine kollegaer og kunder. Ret dem til rigtige navne før du læser — det er præcis dem, Whisper staver forkert, og det er dét, testen skal afsløre.
- Start optagelsen som **fysisk møde**, sæt dig i normal afstand fra mikrofonen, og lad være med at læne dig ind mod den.

---

## Blok 1 — status og baggrund [0:00 - 3:20]

Godmorgen. Jeg tager lige en status på Nordby-projektet, så vi har det samme billede inden styregruppemødet på torsdag.

Vi gik i luften med den første del af rettighedsstyringen den ellevte juni, og der er nu gået knap otte uger. I den periode har vi kørt fire hundrede og tolv brugere igennem den nye onboarding-proces. Af dem er tre hundrede og otteogfirs gået helt automatisk, mens fireogtyve krævede manuel behandling. Det svarer til en fejlrate på lige under seks procent, og det er faktisk bedre end de ti procent, vi havde regnet med i business casen.

De fireogtyve sager fordeler sig i tre grupper. Den største gruppe er eksterne konsulenter, hvor vi ikke har et CPR-nummer at slå op på. Den næste er medarbejdere, der er genansat efter tidligere at have været offboardet, og hvor deres gamle konto stadig lå i en terminal tilstand. Og den sidste gruppe er dem, hvor HR-systemet havde en startdato, der lå før den dato, hvor lederen faktisk godkendte ansættelsen.

Lad mig tage dem én ad gangen, for de tre grupper kræver hver sin løsning, og kun den ene af dem er noget, vi kan rette i vores egen kode.

Provisioneringen mod Entra ID kører over SCIM, og den del har været stabil hele vejen. Vi har haft to udfald siden juni, begge på grund af rate limiting fra Microsoft Graph, og begge blev afhjulpet af den kø-mekanisme, som Anders byggede i foråret. Der er ingen tabte hændelser, og afstemningen mellem Kernesys og Entra viser fuld overensstemmelse på alle undtagen syv objekter. De syv er alle servicekonti, som blev oprettet manuelt uden om systemet, og de skal ryddes op, men de er ikke kritiske.

Til gengæld har vi et reelt problem med deprovisioneringen, og det er dét, jeg gerne vil bruge mest tid på i dag. Når en medarbejder fratræder, skal adgangen lukkes samme dag. Det gør den også i Entra, i Exchange Online og i de systemer, der får deres adgang via sikkerhedsgrupper. Men de to ERP-integrationer, vi arvede fra den tidligere leverandør, lytter kun på en natlig fil, og den fil bliver først lagt klar klokken tre om natten. Det betyder, at der er et vindue på op til seksten timer, hvor en fratrådt medarbejder stadig har adgang til økonomisystemet.

Det er ikke acceptabelt, og det er heller ikke noget, vi kan forklare os ud af i en revision.

## Blok 2 — teknik og tal [3:20 - 6:50]

Jeg har set på tre mulige løsninger, og de koster meget forskelligt.

Den første er at få leverandøren til at eksponere et rigtigt API. Det er den rene løsning, og det ville give os deprovisionering på under et minut. Prisen er, at de har estimeret det til hundrede og tyve timer, og de kan tidligst starte i november. Med deres timepris på ni hundrede og halvtreds kroner lander det på lidt over hundrede og fjorten tusind kroner, og så er der ikke taget højde for test.

Den anden mulighed er, at vi selv skriver direkte i deres database. Det kan lade sig gøre rent teknisk, og jeg har fået bekræftet, at vi har adgangen. Men jeg vil ikke anbefale det. Vi overtager hele ansvaret for deres datamodel i det øjeblik, vi gør det, og de vil med rette afvise enhver supportsag bagefter.

Den tredje mulighed er den, jeg hælder til. Vi skriver filen oftere. I stedet for én gang i døgnet lægger vi den hvert kvarter, og vi tilføjer en markering på de rækker, der er hastesager. Det giver os et vindue på maksimalt femten minutter i stedet for seksten timer. Det er ikke perfekt, men det er en faktor fireogtres bedre, og det kan vi have klar inden udgangen af september uden at involvere leverandøren overhovedet.

Jeg har regnet lidt på, hvad det betyder for driften. Filen fylder i dag omkring fire komma to megabyte, og den bliver læst på under tolv sekunder. Kører vi hvert kvarter, taler vi om seksoghalvfems kørsler i døgnet mod én i dag. Det er ikke noget, der belaster noget som helst, hverken hos os eller hos dem.

Der er en ting mere, jeg vil nævne, mens vi er ved provisionering. Vi har fået spørgsmålet fra Malene, om vi kan understøtte MitID Erhverv i den næste fase. Det korte svar er ja, men det kræver, at vi indfører en CVR-dimension på funktionsrollerne, og den ændring rører ved noget, der er ret centralt. Jeg vil ikke love en dato på det, før vi har haft mulighed for at teste det på en enkelt kunde.

Med hensyn til de privilegerede roller er situationen bedre, end jeg havde frygtet. Vi har nu et-til-et mellem person og privilegeret rolle, hvilket vil sige, at ingen roller er tildelt via grupper. Attesteringen af dem kører kvartalsvis og er adskilt fra den almindelige attestering. Sidste runde blev afsluttet den treogtyvende juli med en svarprocent på syvoghalvfems. De sidste tre procent er tre ledere, der har været på ferie, og de er eskaleret opad i ledelseskæden som designet.

## Blok 3 — hvad der ikke virker [6:50 - 10:10]

Nu til det, der ikke fungerer, og hvor jeg gerne vil have jeres holdning.

Vi skal ikke lave om på attesteringsintervallet. Det får jeg spørgsmålet om hver gang, og svaret er stadig det samme: kvartalsvis er det, revisionen forventer, og det er det, standarden lægger op til. Vi skal derimod lave om på, hvad der sker, når en attestering ikke bliver besvaret. I dag sker der ingenting, og det er den forkerte default.

Adgangen må ikke bare fortsætte, fordi ingen har svaret. Den må gerne fortsætte, hvis nogen aktivt tager stilling til det, men tavshed skal ikke være det samme som en godkendelse. Jeg foreslår, at vi vender den om: reagerer ingen inden fjorten dage, bliver adgangen suspenderet, ikke fjernet. Suspenderet betyder, at den kan genåbnes med ét klik, hvis det viser sig at være en fejl.

Der er noget lignende med nødadgangen. Nødadgang skal kunne gives, og den skal kunne gives hurtigt, ellers bliver den ikke brugt, når der er brug for den. Men den skal ikke kunne gives ubegrænset, og den skal ikke kunne forlænges i det uendelige. Jeg vil have et loft på fireogtyve timer og maksimalt tre forlængelser, og derefter skal det eskaleres til en anden godkender end den, der gav den oprindeligt.

Jeg vil også gerne have ryddet op i et begreb, som vi bruger forkert internt. Vi siger nedstrøms-systemer, når vi taler om dem, der modtager data fra os. Det skal vi holde op med. De hedder modtagersystemer, og det er den betegnelse, der står i dokumentationen og i kontrakten. Det lyder som en detalje, men jeg har set det skabe forvirring i to kundemøder allerede.

Og så en ting om roller, som jeg vil sige tydeligt, fordi der har været tvivl om det. Administratorer skal kunne alt. Hvis vi bygger et endepunkt, der er begrænset af scopes, så skal administratoren passere det, uden at vi skal huske at tilføje en undtagelse hver gang. Det er ikke en bekvemmelighed, det er en fejlkilde vi fjerner. Vi har haft mindst fire fejl, der udelukkende skyldtes, at nogen glemte det.

De indbyggede roller skal i øvrigt kunne redigeres. Vi har haft en antagelse om, at standardrollerne var låste, og det er ikke rigtigt. Kunden skal kunne ændre dem. Det, vi skal sikre, er, at synkroniseringen af dem kun kører én gang, ved oprettelse, og at den ikke nulstiller kundens ændringer, hver gang systemet genstarter.

## Blok 4 — drift, support og økonomi [10:10 - 16:00]

Inden jeg samler op, vil jeg lige runde driften og økonomien, for der er et par ting, der hænger sammen med det, jeg lige har sagt.

På supportsiden har vi haft treogtredive sager siden juni. Det lyder af meget, men fireogtyve af dem er den samme sag: brugere, der ikke kan finde ud af, hvor de skal godkende en anmodning, fordi notifikationsmailen lander i deres uønsket post. Rasmus har set på det, og løsningen er kedelig men effektiv — vi skal have kunden til at whitelist afsenderadressen. Jeg har bedt ham skrive en kort vejledning, som vi kan sende med, når vi onboarder næste kunde, i stedet for at opdage det bagefter hver gang.

De resterende ni sager er rigtige fejl. Fire af dem handlede om, at brugere med JIT-adgang ikke kunne se, hvornår deres adgang udløb. Det er rettet. To handlede om tidszoner, hvor et tidspunkt blev vist en time forkert, fordi containeren kører i UTC, og vi et sted glemte at konvertere før visning. Også rettet, men det er en fejlklasse, jeg gerne vil have os til at være mere systematiske omkring — hver gang der indtastes et klokkeslæt, skal det gå gennem den samme konvertering, og det gør det ikke alle steder endnu.

De sidste tre sager er den slags, der bekymrer mig mest, fordi de handler om data, der ikke stemmer. To brugere så et navn i en attestering, der ikke var deres eget. Det viste sig at være demodata, der ikke var ryddet ordentligt, da kunden blev flyttet fra demo til produktion. Det gjorde ingen skade, men forestil jer, hvordan det ser ud, hvis det sker foran en revisor.

Jeg har derfor bedt Camilla om at bygge en fast kontrol, der kører hver gang vi udgiver: den gennemgår alle visninger med demodata og fejler bygget, hvis der dukker et navn op, som ikke findes i den pågældende kundes egne data. Det er billigere at fange automatisk end at forklare bagefter.

Så til økonomien. Vi har brugt tre hundrede og enogfirs timer på projektet indtil nu, mod et budget på fire hundrede og halvtreds. Det ser fint ud, men der er en hage: de hundrede og tyve timer, jeg nævnte til ERP-integrationen, ligger ikke i budgettet, fordi vi antog, at leverandøren havde et API. Vælger vi kvartersløsningen, koster den os anslået femogtredive timer internt, og det kan vi holde inden for rammen. Vælger vi API-vejen til foråret, skal der lægges et tillæg ind i næste års budget.

Der er også opstartsgebyret, som vi skal have afklaret. Standarden er ni tusind ni hundrede og femoghalvfems kroner, og det er dét, der står i tilbuddet. Men kunden har spurgt, om det kan indregnes i den løbende betaling i stedet, og det er ikke noget, jeg vil svare på uden at have vendt det med bogholderiet først.

Med hensyn til efterlevelse ligger vi bedre, end vi gjorde ved sidste gennemgang. Vi kan nu dokumentere, hvem der har haft hvilken adgang på et vilkårligt tidspunkt tilbage til den ellevte juni, og det er præcis dét, ISO 27001 forlanger på det punkt. Attesteringshistorikken overlever nu også, når en rolle slettes, hvilket den ikke gjorde før — der forsvandt beviset sammen med rollen, og det er ikke godt nok, når man skal dokumentere noget bagud i tid.

Det, vi mangler, er to ting. Vi kan endnu ikke levere en fuld udtræksrapport til en borger, der beder om indsigt i, hvilke oplysninger vi har om vedkommende, og vi kan ikke slette på anmodning uden at ødelægge revisionssporet. De to krav trækker i hver sin retning, og jeg har ikke en færdig løsning på det. Det er noget, vi skal have set på inden årsskiftet, ikke fordi nogen har klaget, men fordi vi ikke skal stå og opfinde det, første gang nogen spørger.

Endelig en bemærkning om PIM. Vi har vores egen erstatning for det nu, med tildeling per bruger og tidsbegrænset forhøjelse. Den har kørt siden foråret uden problemer. Men jeg vil gerne have målt, hvor mange gange den faktisk bliver brugt, for hvis svaret er to gange om måneden, så har vi bygget noget dyrt til noget, der kunne klares med en telefonopringning. Og hvis svaret er halvtreds gange om ugen, så skal vi kigge på, om folk har for lidt adgang i det daglige.

## Blok 5 — beslutninger, opgaver og åbne spørgsmål [16:00 - 19:40]

Lad mig samle op på det, jeg mener, vi skal beslutte i dag.

For det første: vi går med kvartersløsningen på ERP-filen og involverer ikke leverandøren i denne omgang. Det skal med i referatet, at vi har fravalgt API-løsningen af hensyn til tid, ikke af hensyn til pris, og at vi tager den op igen til foråret.

For det andet: attestering, der ikke besvares inden fjorten dage, fører til suspendering, ikke til fjernelse. Vi bygger det med mulighed for at genåbne.

For det tredje: nødadgang får et loft på fireogtyve timer og maksimalt tre forlængelser med skiftende godkender.

For det fjerde: vi udskyder MitID Erhverv til efter årsskiftet, og vi melder det til Malene i denne uge, så hun ikke går og regner med noget andet.

For det femte: de syv forældreløse servicekonti ryddes op inden udgangen af august.

Og for det sjette: vi retter sprogbrugen, så vi konsekvent siger modtagersystemer.

Så til opgaverne. Anders tager kvartersløsningen på ERP-filen og har et estimat klar på fredag. Malene skriver til kunden om MitID Erhverv inden onsdag. Thomas rydder op i de syv servicekonti og dokumenterer, hvor de kom fra, så vi kan lukke hullet. Sofie opdaterer manualen med den nye attesteringsregel, både på dansk og på engelsk, og det skal være i samme udgivelse. Jeg tager selv fat i revisoren og får bekræftet, at suspendering opfylder kravet på samme måde som fjernelse gør.

Til sidst det, jeg ikke har svar på, og som jeg gerne vil have hjælp til.

Jeg ved ikke, hvordan vi skal håndtere de eksterne konsulenter uden CPR-nummer på længere sigt. Vi har en midlertidig løsning, men den skalerer ikke, hvis vi får en kunde med flere hundrede eksterne.

Jeg ved heller ikke, om vi skal tilbyde kvartersløsningen til de andre kunder, der har den samme ERP-integration, eller om vi skal lade dem blive på den natlige fil, indtil de selv beder om andet.

Jeg er i tvivl om, hvorvidt vi overhovedet skal blive ved med at understøtte den gamle importformat med semikolon-separerede filer. Der er to kunder tilbage på den, og den koster os tid hver gang vi rører ved importen.

Og så er der spørgsmålet om, hvorvidt vores nuværende logning er tilstrækkelig til NIS2. Jeg tror det, men jeg vil ikke sige det højt til en kunde, før nogen med forstand på det har set på det.

Det var, hvad jeg havde. Sig til, hvis I mener, jeg har misforstået noget, eller hvis der er noget, der skal med på listen inden torsdag.
