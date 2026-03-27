using dadaApp.Data;
using dadaApp.Models;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace dadaApp.Services
{
    public class ImportComptabiliteService
    {
        private readonly AppDbContext _context;
        private readonly ILogger<ImportComptabiliteService> _logger;

        // ─── Positions des colonnes (1-based, longueur fixe) ───────────────────
        // Format du grand-livre auxiliaire TXT :
        //
        // [  1..  8]  Date comptable   (dd/MM/yy)
        // [  9..  9]  espace
        // [ 10.. 16]  Numéro pièce
        // [ 17.. 17]  espace
        // [ 18.. 19]  Code journal
        // [ 20.. 20]  espace
        // [ 21.. 49]  Libellé          (29 chars)
        // [ 50.. 51]  espaces
        // [ 52.. 59]  Date facture     (dd/MM/yy)
        // [ 60.. 60]  espace
        // [ 61.. 67]  Référence / numéro facture (7 chars)
        // [ 68.. 69]  espaces
        // [ 70.. 77]  Date échéance    (dd/MM/yy)
        // [ 78.. 78]  !
        // [ 79.. 92]  Débit            (14 chars, aligné à droite)
        // [ 93..105]  Crédit           (13 chars, aligné à droite)
        // [106..106]  !
        // [107..132]  Numéro lettrage  (aligné à droite)
        // ────────────────────────────────────────────────────────────────────────

        private const int POS_DATE_COMP   = 1;
        private const int LEN_DATE_COMP   = 8;

        private const int POS_PIECE       = 10;
        private const int LEN_PIECE       = 7;

        private const int POS_JOURNAL     = 18;
        private const int LEN_JOURNAL     = 2;

        private const int POS_LIBELLE     = 21;
        private const int LEN_LIBELLE     = 29;

        private const int POS_DATE_FACT   = 52;
        private const int LEN_DATE_FACT   = 8;

        private const int POS_REFERENCE   = 61;
        private const int LEN_REFERENCE   = 7;

        private const int POS_DATE_ECH    = 70;
        private const int LEN_DATE_ECH    = 8;

        // Position du premier "!" => 78 (peut varier de ±1 selon la ligne)
        // On repère les "!" dynamiquement pour être robuste.

        // Dans la zone entre les deux "!" : 27 chars
        // Débit  : 14 premiers chars
        // Crédit : 13 chars suivants
        private const int LEN_DEBIT       = 14;
        private const int LEN_CREDIT      = 13;

        public ImportComptabiliteService(
            AppDbContext context,
            ILogger<ImportComptabiliteService> logger)
        {
            _context = context;
            _logger  = logger;
        }

        // ═══════════════════════════════════════════════════════════════════════
        //  Détection automatique du format et import
        // ═══════════════════════════════════════════════════════════════════════
        public async Task<ResultatImport> ImporterDepuisCsv(Stream fileStream, string nomFichier)
        {
            // Détection automatique du format basée sur l'extension et le contenu
            var extension = Path.GetExtension(nomFichier).ToLowerInvariant();
            
            // Si extension .txt ou contenu ressemble à un grand livre TXT
            if (extension == ".txt" || await EstFormatTxt(fileStream))
            {
                return await ImporterDepuisTxt(fileStream, nomFichier);
            }
            
            // Par défaut, traiter comme CSV (pour compatibilité future)
            throw new NotSupportedException(
                "Format CSV non encore implémenté. Veuillez utiliser un fichier TXT (grand livre auxiliaire).");
        }

        /// <summary>
        /// Détecte si le fichier est un format TXT (grand livre auxiliaire)
        /// en analysant les premières lignes
        /// </summary>
        private async Task<bool> EstFormatTxt(Stream fileStream)
        {
            var position = fileStream.Position;
            try
            {
                fileStream.Position = 0;
                using var reader = new StreamReader(fileStream, Encoding.GetEncoding("ISO-8859-1"), leaveOpen: true);
                
                // Lire les 10 premières lignes
                for (int i = 0; i < 10; i++)
                {
                    var ligne = await reader.ReadLineAsync();
                    if (ligne == null) break;
                    
                    // Indicateurs d'un fichier TXT grand livre
                    if (ligne.Contains("GRAND") && ligne.Contains("LIVRE") ||
                        ligne.Contains("ET468") ||
                        ligne.StartsWith("Compte :") ||
                        (ligne.Contains("!") && RegexDate.IsMatch(ligne)))
                    {
                        return true;
                    }
                }
            }
            finally
            {
                fileStream.Position = position;
            }
            
            return false;
        }

        // ═══════════════════════════════════════════════════════════════════════
        //  Point d'entrée principal pour TXT
        // ═══════════════════════════════════════════════════════════════════════
        public async Task<ResultatImport> ImporterDepuisTxt(Stream fileStream, string nomFichier)
        {
            Console.WriteLine("=== ImporterDepuisTxt appelée ===");
            var resultat = new ResultatImport();

            try
            {
                // Le fichier utilise l'encodage ISO-8859-1 (Latin-1)
                using var reader = new StreamReader(fileStream, Encoding.GetEncoding("ISO-8859-1"));

                string? compteActuel    = null;
                string? codeClientActuel = null;
                string? nomClientActuel  = null;
                int     numeroLigne     = 0;
                string? ligne;

                while ((ligne = await reader.ReadLineAsync()) != null)
                {
                    numeroLigne++;

                    // Supprimer le \r éventuel (fichier Windows CRLF)
                    ligne = ligne.TrimEnd('\r');

                    // ── Lignes à ignorer ────────────────────────────────────
                    if (string.IsNullOrWhiteSpace(ligne))               continue;
                    if (EstLigneEntete(ligne))                          continue;
                    if (ligne.StartsWith("====="))                      continue;
                    if (ligne.StartsWith("------"))                     continue;
                    if (ligne.Contains("Report au"))                    continue;
                    if (ligne.Contains("Solde") && ligne.Contains("fin")) continue;

                    // ── Ligne "Compte :" ─────────────────────────────────────
                    if (ligne.StartsWith("Compte :"))
                    {
                        ParseLigneCompte(ligne,
                            out compteActuel,
                            out codeClientActuel,
                            out nomClientActuel);

                        Console.WriteLine(
                            $"✨ Compte: {compteActuel} / {codeClientActuel} - {nomClientActuel}");
                        continue;
                    }

                    // ── Ligne d'écriture (commence par une date dd/MM/yy) ───
                    if (EstLigneEcriture(ligne))
                    {
                        try
                        {
                            await TraiterLigneEcriture(
                                ligne,
                                compteActuel,
                                codeClientActuel,
                                nomClientActuel);

                            resultat.NombreLignesImportees++;

                            if (resultat.NombreLignesImportees % 10 == 0)
                                Console.WriteLine(
                                    $"📊 {resultat.NombreLignesImportees} écritures traitées...");
                        }
                        catch (Exception ex)
                        {
                            var erreur = $"Ligne {numeroLigne}: {ex.Message}";
                            resultat.Erreurs.Add(erreur);
                            _logger.LogWarning(ex,
                                "Erreur ligne {N}: {Ligne}",
                                numeroLigne,
                                ligne.Substring(0, Math.Min(60, ligne.Length)));
                        }
                    }
                }

                Console.WriteLine("💾 Sauvegarde dans la BD...");
                Console.WriteLine($"   Comptes   : {_context.ChangeTracker.Entries<Compte>().Count()}");
                Console.WriteLine($"   Écritures : {_context.ChangeTracker.Entries<Ecriture>().Count()}");

                await _context.SaveChangesAsync();

                Console.WriteLine("✅ Sauvegarde terminée !");
                resultat.Succes = true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ ERREUR GÉNÉRALE: {ex.Message}");
                resultat.Succes = false;
                resultat.Erreurs.Add($"Erreur générale: {ex.Message}");
                _logger.LogError(ex, "Erreur import TXT");
            }

            return resultat;
        }

        // ═══════════════════════════════════════════════════════════════════════
        //  Parser la ligne "Compte :"
        //  Exemple : "Compte : 411000     00066       CBL REPRO MADA   ..."
        // ═══════════════════════════════════════════════════════════════════════
        private static void ParseLigneCompte(
            string ligne,
            out string? numeroCompte,
            out string? codeClient,
            out string? nomClient)
        {
            // Retirer le préfixe "Compte : "
            var contenu = ligne.Substring("Compte :".Length).Trim();

            // Couper sur le premier "!" s'il est présent
            var idxBang = contenu.IndexOf('!');
            if (idxBang >= 0)
                contenu = contenu.Substring(0, idxBang).Trim();

            // Le premier token est le numéro de compte (ex: "411000")
            var tokens = contenu.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            if (tokens.Length == 0)
            {
                numeroCompte = codeClient = nomClient = null;
                return;
            }

            numeroCompte = tokens[0];

            if (tokens.Length == 1)
            {
                // Juste "411000" → pas de code client (compte collectif)
                codeClient = null;
                nomClient  = numeroCompte;
                return;
            }

            // Détecter si tokens[1] ressemble à un code client
            // Ex: "00066", "00360", "00360C" → code client ; "COLLECTIF" → début du nom
            // Règle: chaîne compacte alphanumérique (sans espace), contenant au moins un chiffre.
            var secondToken = tokens[1].Trim();
            var ressembleCodeClient =
                secondToken.Length <= 12 &&
                secondToken.Any(char.IsDigit) &&
                secondToken.All(char.IsLetterOrDigit);

            if (ressembleCodeClient)
            {
                codeClient = secondToken;
                nomClient  = tokens.Length > 2
                    ? string.Join(" ", tokens.Skip(2))
                    : codeClient;
            }
            else
            {
                codeClient = null;
                nomClient  = string.Join(" ", tokens.Skip(1));
            }
        }

        // ═══════════════════════════════════════════════════════════════════════
        //  Déterminer si une ligne est une ligne d'écriture
        // ═══════════════════════════════════════════════════════════════════════
        private static readonly Regex RegexDate =
            new(@"^\d{2}/\d{2}/\d{2}", RegexOptions.Compiled);

        private static bool EstLigneEcriture(string ligne) =>
            ligne.Length >= 78 && RegexDate.IsMatch(ligne);

        // ═══════════════════════════════════════════════════════════════════════
        //  Déterminer si une ligne est une ligne d'en-tête à ignorer
        // ═══════════════════════════════════════════════════════════════════════
        private static bool EstLigneEntete(string ligne) =>
            ligne.StartsWith("ET468")    ||
            ligne.StartsWith("TRANSIT")  ||
            ligne.StartsWith("PERIODE")  ||
            ligne.StartsWith("Date")     ||
            ligne.StartsWith("Comptable");

        // ═══════════════════════════════════════════════════════════════════════
        //  Traiter une ligne d'écriture (parsing dynamique amélioré)
        // ═══════════════════════════════════════════════════════════════════════
        private async Task TraiterLigneEcriture(
            string  ligne,
            string? numeroCompte,
            string? codeClient,
            string? nomClient)
        {
            if (string.IsNullOrWhiteSpace(numeroCompte))
                throw new InvalidOperationException("Numéro de compte manquant.");

            // ── Repérer les deux "!" pour délimiter les zones (dynamique) ───
            int bang1 = ligne.IndexOf('!');
            int bang2 = bang1 >= 0 ? ligne.IndexOf('!', bang1 + 1) : -1;

            if (bang1 < 0)
                throw new FormatException("Séparateur '!' introuvable.");

            // ── Parsing dynamique : extraire la date en début de ligne ──────
            string dateCompStr = "";
            string pieceStr = "";
            string journalStr = "";
            string libelleStr = "";
            string dateFactStr = "";
            string referenceStr = "";
            string dateEchStr = "";

            // Méthode 1: Parsing par positions fixes (si la ligne est assez longue)
            if (ligne.Length >= 78)
            {
                dateCompStr  = Extraire(ligne, POS_DATE_COMP,  LEN_DATE_COMP);
                pieceStr     = Extraire(ligne, POS_PIECE,      LEN_PIECE);
                journalStr   = Extraire(ligne, POS_JOURNAL,    LEN_JOURNAL);
                libelleStr   = Extraire(ligne, POS_LIBELLE,    LEN_LIBELLE);
                dateFactStr  = Extraire(ligne, POS_DATE_FACT,  LEN_DATE_FACT);
                referenceStr = Extraire(ligne, POS_REFERENCE,  LEN_REFERENCE);
                dateEchStr   = Extraire(ligne, POS_DATE_ECH,   LEN_DATE_ECH);
            }
            else
            {
                // Méthode 2: Parsing flexible basé sur les espaces et patterns
                var avantBang = ligne.Substring(0, Math.Min(bang1, ligne.Length));
                var parts = avantBang.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                
                if (parts.Length > 0 && RegexDate.IsMatch(parts[0]))
                {
                    dateCompStr = parts[0];
                }
                if (parts.Length > 1)
                {
                    pieceStr = parts[1];
                }
                if (parts.Length > 2)
                {
                    journalStr = parts[2];
                }
                
                // Le libellé : tout ce qui reste avant le "!" après journal
                if (parts.Length > 2)
                {
                    var idxJournal = avantBang.IndexOf(journalStr);
                    if (idxJournal >= 0)
                    {
                        var libelleStart = idxJournal + journalStr.Length;
                        if (libelleStart < avantBang.Length)
                        {
                            libelleStr = avantBang.Substring(libelleStart).Trim();
                        }
                    }
                }
                
                // Chercher les dates facture et échéance dans la partie avant "!"
                // Format typique : dateFact ref dateEch
                var datePattern = new Regex(@"\d{2}/\d{2}/\d{2,4}");
                var dates = datePattern.Matches(avantBang).Cast<Match>().Select(m => m.Value).ToList();
                
                if (dates.Count >= 2)
                {
                    // Première date après date comptable = date facture
                    dateFactStr = dates[1];
                    if (dates.Count >= 3)
                    {
                        dateEchStr = dates[2];
                    }
                }
                
                // Référence : chercher pattern /XX ou alphanumérique après date facture
                var refPattern = new Regex(@"/\w+|\d{4,}");
                var refMatch = refPattern.Match(avantBang);
                if (refMatch.Success)
                {
                    referenceStr = refMatch.Value;
                }
            }

            // ── Zone entre les deux "!" : débit / crédit (parsing dynamique amélioré) ─
            string debitStr  = "";
            string creditStr = "";

            if (bang1 >= 0)
            {
                int longueurZone = (bang2 > bang1 ? bang2 : ligne.Length) - bang1 - 1;
                if (longueurZone > 0)
                {
                    string zoneMonnaie = ligne.Substring(bang1 + 1, Math.Min(longueurZone, ligne.Length - bang1 - 1));

                    // Méthode hybride : combiner positions fixes et détection dynamique
                    // Le format standard a ~27 caractères entre les "!" : 14 débit + 13 crédit
                    if (zoneMonnaie.Length >= LEN_DEBIT + LEN_CREDIT)
                    {
                        // Format standard : utiliser les positions fixes
                        debitStr  = zoneMonnaie.Substring(0, LEN_DEBIT).Trim();
                        creditStr = zoneMonnaie.Substring(LEN_DEBIT, Math.Min(LEN_CREDIT, zoneMonnaie.Length - LEN_DEBIT)).Trim();
                    }
                    else
                    {
                        // Format variable : détection dynamique
                        // Chercher les montants (nombres avec virgules/points)
                        var montantPattern = new Regex(@"\d[\d\s,\.]*\d|\d+[,\.]\d+");
                        var matches = montantPattern.Matches(zoneMonnaie)
                            .Cast<Match>()
                            .Where(m => m.Value.Trim().Length > 0)
                            .Select(m => m.Value.Trim())
                            .ToList();

                        if (matches.Count >= 2)
                        {
                            // Deux montants : le premier est débit, le second crédit
                            debitStr = matches[0];
                            creditStr = matches[1];
                        }
                        else if (matches.Count == 1)
                        {
                            // Un seul montant : déterminer par position relative
                            var posMontant = zoneMonnaie.IndexOf(matches[0]);
                            var milieu = zoneMonnaie.Length / 2;
                            
                            if (posMontant < milieu)
                                debitStr = matches[0];
                            else
                                creditStr = matches[0];
                        }
                    }
                    
                    // Nettoyer les chaînes finales
                    debitStr = debitStr.Trim();
                    creditStr = creditStr.Trim();
                }
            }

            // ── Numéro de lettrage (après le 2e "!") ───────────────────────
            string? numeroLettrage = null;

            if (bang2 > 0 && bang2 + 1 < ligne.Length)
            {
                var candidat = ligne.Substring(bang2 + 1).Trim();

                // Le lettrage est un nombre ou un code alphanumérique court
                if (!string.IsNullOrWhiteSpace(candidat))
                {
                    // Nettoyer le candidat (enlever espaces, garder alphanumérique)
                    var nettoye = new StringBuilder();
                    foreach (char c in candidat)
                    {
                        if (char.IsLetterOrDigit(c) || c == '-' || c == '_')
                            nettoye.Append(c);
                        else if (char.IsWhiteSpace(c) && nettoye.Length > 0)
                            break; // Arrêter au premier espace après le début
                    }
                    
                    candidat = nettoye.ToString();
                    
                    if (candidat.Length > 0 && candidat.Length <= 20)
                {
                    numeroLettrage = candidat;
                    }
                }
            }

            // ── Debug ──────────────────────────────────────────────────────
            Console.WriteLine(
                $"📝 {dateCompStr} | Pce:{pieceStr.Trim()} Jrn:{journalStr.Trim()} " +
                $"| D:{debitStr.Trim()} C:{creditStr.Trim()} | Ltr:{numeroLettrage}");

            // ── Chercher ou créer le compte ────────────────────────────────
            var compte = _context.ChangeTracker
                .Entries<Compte>()
                .Select(e => e.Entity)
                .FirstOrDefault(c =>
                    c.NumeroCompte == numeroCompte &&
                    c.CodeClient   == codeClient);

            if (compte == null)
            {
                compte = await _context.Comptes
                    .FirstOrDefaultAsync(c =>
                        c.NumeroCompte == numeroCompte &&
                        c.CodeClient   == codeClient);
            }

            if (compte == null)
            {
                compte = new Compte
                {
                    NumeroCompte = numeroCompte,
                    CodeClient   = codeClient,
                    NomClient    = nomClient ?? codeClient ?? numeroCompte,
                    DateCreation = DateTime.UtcNow,
                    Ecritures    = new List<Ecriture>()
                };
                _context.Comptes.Add(compte);
                Console.WriteLine($"   ✨ Nouveau compte: {numeroCompte} ({codeClient})");
            }

            // ── Créer l'écriture ───────────────────────────────────────────
            var ecriture = new Ecriture
            {
                Compte         = compte,
                DateComptable  = ParseDate(dateCompStr),
                NumeroPiece    = Nettoyer(pieceStr),
                CodeJournal    = Nettoyer(journalStr),
                Libelle        = Nettoyer(libelleStr),
                DateFacture    = ParseDateOpt(dateFactStr),
                Reference      = Nettoyer(referenceStr),
                DateEcheance   = ParseDateOpt(dateEchStr),
                Debit          = ParseMontant(debitStr),
                Credit         = ParseMontant(creditStr),
                NumeroLettrage = numeroLettrage,
                DateSaisie     = DateTime.UtcNow
            };

            _context.Ecritures.Add(ecriture);
        }

        // ═══════════════════════════════════════════════════════════════════════
        //  Helpers
        // ═══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Extrait une sous-chaîne à position fixe (1-based).
        /// Retourne une chaîne vide si la ligne est trop courte.
        /// </summary>
        private static string Extraire(string ligne, int posDebut, int longueur)
        {
            int idx = posDebut - 1; // convertir en 0-based
            if (idx >= ligne.Length) return "";
            int len = Math.Min(longueur, ligne.Length - idx);
            return ligne.Substring(idx, len);
        }

        private static readonly string[] DateFormats =
        {
            "dd/MM/yy", "d/M/yy", "dd/M/yy", "d/MM/yy",
            "dd/MM/yyyy", "d/M/yyyy"
        };

        private static DateTime ParseDate(string s)
        {
            s = s.Trim();
            if (string.IsNullOrWhiteSpace(s))
                throw new ArgumentException("Date comptable vide.");

            var dt = DateTime.ParseExact(s, DateFormats,
                CultureInfo.InvariantCulture, DateTimeStyles.None);

            return DateTime.SpecifyKind(dt, DateTimeKind.Utc);
        }

        private static DateTime? ParseDateOpt(string? s)
        {
            if (string.IsNullOrWhiteSpace(s)) return null;
            s = s.Trim();

            if (DateTime.TryParseExact(s, DateFormats,
                CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
                return DateTime.SpecifyKind(dt, DateTimeKind.Utc);

            return null;
        }

        private static decimal ParseMontant(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return 0m;

            // Garder uniquement chiffres, virgule et point
            var sb = new StringBuilder();
            foreach (char c in s)
                if (char.IsDigit(c) || c == ',' || c == '.')
                    sb.Append(c);

            // La virgule est le séparateur décimal en France
            var valeur = sb.ToString().Replace(",", ".");

            return decimal.TryParse(valeur, NumberStyles.Any,
                CultureInfo.InvariantCulture, out var montant)
                ? montant
                : 0m;
        }

        private static string? Nettoyer(string? s)
        {
            if (string.IsNullOrWhiteSpace(s)) return null;
            return s.Trim();
        }
    }

    public class ResultatImport
    {
        public bool         Succes                { get; set; }
        public int          NombreLignesImportees { get; set; }
        public List<string> Erreurs               { get; set; } = new();
    }
}