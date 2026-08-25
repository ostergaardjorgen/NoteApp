// PRØVERNE KØRER ÉN AD GANGEN.
//
// De saetter NOTEAPP_DATA, og den er global for processen. Koerer to klasser
// samtidig, peger den ene proeves datamappe ind i den andens - og saa fejler
// de paa skift, uden at der er noget galt med koden.
//
// Det koster tid og er den rigtige pris: en proeve, der fejler tilfaeldigt,
// er vaerre end ingen proeve, fordi man holder op med at tro paa den.

[assembly: Xunit.CollectionBehavior(DisableTestParallelization = true)]
