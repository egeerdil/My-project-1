using System.Collections.Generic;

namespace LingoRun
{
    /// <summary>
    /// Port of VERB_FORMS from lingorun_v48.jsx: maps a conjugated surface
    /// form as it appears in a sentence ("bebo") to its infinitive lemma
    /// ("beber") and English key ("to drink"), per language — the bridge that
    /// lets a correct Sentences answer advance the underlying word's mastery
    /// in the dictionary, and a missed one push the verb into the word
    /// review pool. Resolution order: direct article-stripped vocab match
    /// (nouns like "café"), then the table's en key, then the lemma matched
    /// against vocab targets (catches entries filed under a different
    /// English gloss, e.g. "veo"→"ver"→"to watch"). Only keys that exist in
    /// the loaded vocab are returned.
    /// </summary>
    public static class VerbForms
    {
        struct FormInfo { public string lemma; public string en; }

        static Dictionary<string, Dictionary<string, FormInfo>> _tables;

        static void Add(Dictionary<string, FormInfo> d, string lemma, string enKey, params string[] forms)
        {
            var info = new FormInfo { lemma = lemma, en = enKey };
            foreach (var f in forms) if (!d.ContainsKey(f)) d[f] = info;
        }

        static void EnsureBuilt()
        {
            if (_tables != null) return;
            _tables = new Dictionary<string, Dictionary<string, FormInfo>>();

            var es = new Dictionary<string, FormInfo>();
            Add(es, "ser", "to be", "soy", "es");
            Add(es, "estar", "to be (state)", "estoy", "estás", "está");
            Add(es, "tener", "to have", "tengo", "tienes", "tiene", "tenemos");
            Add(es, "hablar", "to speak", "hablo", "hablas", "habla", "hablamos", "hablan");
            Add(es, "beber", "to drink", "bebo", "bebes", "bebe", "bebemos", "beben");
            Add(es, "comer", "to eat", "como", "comes", "come", "comemos", "comen");
            Add(es, "ir", "to go", "voy", "vas", "va", "vamos", "van", "irme");
            Add(es, "querer", "to want", "quiero", "quieres", "quiere", "queremos", "quieren");
            Add(es, "poder", "to be able to", "puedo", "puedes", "puede", "podemos", "pueden");
            Add(es, "saber", "to know", "sabes", "sabe", "sabemos", "saben");
            Add(es, "ver", "to see", "veo", "ves", "ve", "vemos", "ven", "vi");
            Add(es, "dar", "to give", "doy", "das", "da", "damos", "dan");
            Add(es, "venir", "to come", "vengo", "vienes", "viene", "venimos", "vienen");
            Add(es, "hacer", "to do", "hago", "haces", "hace", "hacemos", "hacen", "hice");
            Add(es, "vivir", "to live", "vivo", "vives", "vive", "vivimos", "viven");
            Add(es, "trabajar", "to work", "trabajo", "trabajas", "trabaja", "trabajamos");
            Add(es, "estudiar", "to study", "estudio", "estudias", "estudia");
            Add(es, "comprar", "to buy", "compro", "compras", "compra");
            Add(es, "amar", "to love", "amo", "amas", "ama");
            Add(es, "odiar", "to hate", "odio");
            Add(es, "decir", "to say", "digo", "dices", "dice");
            Add(es, "escribir", "to write", "escribo", "escribes", "escribe");
            Add(es, "cantar", "to sing", "canto");
            Add(es, "tocar", "to play (instrument)", "tocas");
            Add(es, "gustar", "to please / like", "gusta");
            Add(es, "levantarse", "to get up", "levanto");
            Add(es, "lavarse", "to wash", "lavo");
            Add(es, "llover", "to rain", "lloviendo");
            _tables["es"] = es;

            var fr = new Dictionary<string, FormInfo>();
            Add(fr, "être", "to be", "suis", "est");
            Add(fr, "avoir", "to have", "ai", "as", "a", "avons", "ont");
            Add(fr, "aller", "to go", "vais", "vas", "va", "allons", "vont");
            Add(fr, "parler", "to speak", "parle", "parles", "parlons", "parlent");
            Add(fr, "boire", "to drink", "bois", "boit", "buvons", "boivent", "bu");
            Add(fr, "manger", "to eat", "mange", "manges", "mangeons", "mangent");
            Add(fr, "vouloir", "to want", "veux", "veut", "voulons", "veulent");
            Add(fr, "pouvoir", "to be able to", "peux", "peut", "pouvons", "peuvent");
            Add(fr, "savoir", "to know", "sais", "sait", "savons");
            Add(fr, "voir", "to see", "vois", "voit", "voyons");
            Add(fr, "faire", "to do", "fais", "fait", "faisons");
            Add(fr, "habiter", "to live in", "habite", "habites");
            Add(fr, "vivre", "to live", "vis", "vit");
            Add(fr, "travailler", "to work", "travaille", "travailles", "travaillent");
            Add(fr, "aimer", "to love", "aime", "aimes", "aiment");
            Add(fr, "dire", "to say", "dis", "dit", "disons");
            Add(fr, "chanter", "to sing", "chante", "chantes");
            Add(fr, "jouer", "to play", "joue", "joues");
            Add(fr, "pleuvoir", "to rain", "pleut");
            Add(fr, "lire", "to read", "lu");
            _tables["fr"] = fr;

            var it = new Dictionary<string, FormInfo>();
            Add(it, "essere", "to be", "sono", "è");
            Add(it, "stare", "to be (state)", "sto", "stai", "sta");
            Add(it, "avere", "to have", "ho", "hai", "ha", "abbiamo", "hanno");
            Add(it, "andare", "to go", "vado", "vai", "va", "andiamo", "vanno");
            Add(it, "parlare", "to speak", "parlo", "parli", "parla", "parliamo", "parlano");
            Add(it, "bere", "to drink", "bevo", "bevi", "beve", "beviamo", "bevono", "bevuto");
            Add(it, "mangiare", "to eat", "mangio", "mangi", "mangia", "mangiamo", "mangiano", "mangiato");
            Add(it, "volere", "to want", "voglio", "vuoi", "vuole", "vogliamo", "vogliono");
            Add(it, "potere", "to be able to", "posso", "puoi", "possiamo", "possono");
            Add(it, "sapere", "to know", "so", "sai", "sa", "sappiamo");
            Add(it, "vedere", "to see", "vedo", "vedi", "vede", "vediamo", "vedono");
            Add(it, "fare", "to do", "faccio", "fai", "fa", "facciamo");
            Add(it, "vivere", "to live", "vivo", "vivi", "vive", "viviamo");
            Add(it, "lavorare", "to work", "lavoro", "lavori", "lavora", "lavoriamo");
            Add(it, "studiare", "to study", "studio", "studi", "studia");
            Add(it, "comprare", "to buy", "compro", "compri", "compra");
            Add(it, "amare", "to love", "amo", "ami", "ama", "amiamo");
            Add(it, "dire", "to say", "dico", "dici", "dice", "dicono");
            Add(it, "scrivere", "to write", "scrivo", "scrivi", "scrive");
            Add(it, "cantare", "to sing", "canto", "canti");
            Add(it, "suonare", "to play (instrument)", "suono", "suoni");
            Add(it, "piacere", "to please / like", "piace");
            Add(it, "alzarsi", "to get up", "alzo");
            Add(it, "leggere", "to read", "letto");
            Add(it, "piovere", "to rain", "piovendo");
            _tables["it"] = it;

            var de = new Dictionary<string, FormInfo>();
            Add(de, "sein", "to be", "bin", "ist");
            Add(de, "haben", "to have", "habe", "hast", "hat", "haben");
            Add(de, "gehen", "to go", "gehe", "gehst", "geht", "gehen");
            Add(de, "sprechen", "to speak", "spreche", "sprichst", "spricht", "sprechen");
            Add(de, "trinken", "to drink", "trinke", "trinkst", "trinkt", "trinken", "getrunken");
            Add(de, "essen", "to eat", "esse", "isst", "essen", "gegessen");
            Add(de, "wollen", "to want", "will", "willst", "wollen");
            Add(de, "können", "to be able to", "kann", "kannst");
            Add(de, "mögen", "to like", "mag", "magst");
            Add(de, "machen", "to do", "mache", "machst", "macht", "machen");
            Add(de, "kommen", "to come", "komme", "kommst", "kommt");
            Add(de, "arbeiten", "to work", "arbeite", "arbeitest", "arbeitet", "arbeiten");
            Add(de, "studieren", "to study", "studiere", "studierst", "studiert");
            Add(de, "wohnen", "to live in", "wohne", "wohnst", "wohnt");
            Add(de, "kaufen", "to buy", "kaufe", "kaufst");
            Add(de, "lieben", "to love", "liebe", "liebst", "liebt");
            Add(de, "sagen", "to say", "sage");
            Add(de, "schreiben", "to write", "schreibe", "schreibst", "schreibt");
            Add(de, "singen", "to sing", "singe");
            Add(de, "spielen", "to play", "spielst");
            Add(de, "stehen", "to stand / get up", "stehe");
            Add(de, "regnen", "to rain", "regnet");
            Add(de, "lesen", "to read", "gelesen");
            _tables["de"] = de;
        }

        /// <summary>Resolve a sentence-blank word ("bebo", "café") to the
        /// English key of a vocab entry, or null when it links to nothing.</summary>
        public static string ResolveToVocabKey(string surface)
        {
            if (string.IsNullOrEmpty(surface)) return null;
            string s = surface.Trim().ToLowerInvariant().Trim('.', ',', '!', '?', '¡', '¿');
            if (s.Length == 0) return null;

            // 1) Direct vocab match (nouns / already-infinitive verbs).
            var direct = VocabDatabase.FindByTargetSurface(s);
            if (direct != null) return direct.en;

            // 2) Conjugation table for the active language.
            EnsureBuilt();
            Dictionary<string, FormInfo> table;
            if (!_tables.TryGetValue(LanguageManager.Current, out table)) return null;
            FormInfo info;
            if (!table.TryGetValue(s, out info)) return null;

            // 2a) The table's own English key, when it's a real vocab entry.
            if (VocabDatabase.FindByEn(info.en) != null) return info.en;

            // 2b) Otherwise match the lemma against vocab targets — catches
            // entries filed under a different gloss ("ver" = "to watch").
            var byLemma = VocabDatabase.FindByTargetSurface(info.lemma);
            return byLemma != null ? byLemma.en : null;
        }
    }
}
