using dadaApp.Data;
using dadaApp.Models;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Text;

namespace dadaApp.Services
{
    public class ImportComptabiliteService
    {
        private readonly AppDbContext _context;
        private readonly ILogger<ImportComptabiliteService> _logger;

        public ImportComptabiliteService(
            AppDbContext context,
            ILogger<ImportComptabiliteService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<ResultatImport> ImporterDepuisCsv(Stream fileStream, string nomFichier)
        {
            Console.WriteLine("=== ImporterDepuisCsv appelée ===");
            var resultat = new ResultatImport();

            try
            {
                using var reader = new StreamReader(fileStream, Encoding.GetEncoding("ISO-8859-1"));
                string? ligne;

                string? compteActuel = null;
                string? codeClientActuel = null;
                string? nomClientActuel = null;

                while ((ligne = await reader.ReadLineAsync()) != null)
                {
                    // Ignorer les lignes vides, d'en-tête ou de séparation
                    if (string.IsNullOrWhiteSpace(ligne) ||
                        ligne.StartsWith("ET468") ||
                        ligne.StartsWith("TRANSIT") ||
                        ligne.StartsWith("PERIODE") ||
                        ligne.StartsWith("=====") ||
                        ligne.StartsWith("~)====") ||
                        ligne.StartsWith("Date,") ||
                        ligne.Contains("Report au") ||
                        ligne.Contains("Solde … fin"))
                    {
                        continue;
                    }

                    // Parser CSV en tenant compte des guillemets
                    var colonnes = ParseCsvLine(ligne);

                    // Ligne de compte : "Compte :,411000,,00066       CBL REPRO MADA"
                    if (colonnes.Count > 0 && colonnes[0] == "Compte :" && colonnes.Count >= 4)
                    {
                        compteActuel = colonnes[1].Trim();
                        
                        // Le nom du client peut être dans colonnes[3] ou après
                        var infoClient = colonnes[3].Trim();
                        
                        // Séparer le code client du nom (ex: "00066       CBL REPRO MADA")
                        var parts = infoClient.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length > 0)
                        {
                            codeClientActuel = parts[0];
                            nomClientActuel = parts.Length > 1 ? string.Join(" ", parts.Skip(1)) : parts[0];
                        }
                        else
                        {
                            codeClientActuel = null;
                            nomClientActuel = infoClient;
                        }
                        
                        Console.WriteLine($"✨ Compte: {compteActuel} / {codeClientActuel} - {nomClientActuel}");
                        continue;
                    }

                    // Ligne séparateur
                    if (colonnes.Count > 0 && colonnes[0].StartsWith("--------"))
                    {
                        continue;
                    }

                    // Ligne d'écriture : commence par une date
                    if (colonnes.Count >= 10 && !string.IsNullOrWhiteSpace(colonnes[0]))
                    {
                        var dateStr = colonnes[0].Trim();
                        
                        // Vérifier si c'est une date valide
                        var formats = new[] { 
                            "d/M/yyyy", "dd/MM/yyyy", "dd/M/yyyy", "d/MM/yyyy"
                        };

                        if (DateTime.TryParseExact(dateStr, formats, 
                            CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
                        {
                            try
                            {
                                await TraiterLigneEcriture(colonnes, compteActuel, codeClientActuel, nomClientActuel);
                                resultat.NombreLignesImportees++;
                                
                                if (resultat.NombreLignesImportees % 10 == 0)
                                {
                                    Console.WriteLine($"📊 {resultat.NombreLignesImportees} écritures traitées...");
                                }
                            }
                            catch (Exception ex)
                            {
                                var erreur = $"Ligne {resultat.NombreLignesImportees + 1}: {ex.Message}";
                                resultat.Erreurs.Add(erreur);
                                _logger.LogWarning(ex, "Erreur ligne: {Ligne}", ligne.Substring(0, Math.Min(50, ligne.Length)));
                            }
                        }
                    }
                }

                Console.WriteLine($"💾 Sauvegarde dans la BD...");
                Console.WriteLine($"   Comptes: {_context.ChangeTracker.Entries<Compte>().Count()}");
                Console.WriteLine($"   Écritures: {_context.ChangeTracker.Entries<Ecriture>().Count()}");

                await _context.SaveChangesAsync();
                
                Console.WriteLine("✅ Sauvegarde terminée !");
                resultat.Succes = true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ ERREUR: {ex.Message}");
                resultat.Succes = false;
                resultat.Erreurs.Add($"Erreur générale: {ex.Message}");
                _logger.LogError(ex, "Erreur import");
            }

            return resultat;
        }

        private async Task TraiterLigneEcriture(List<string> colonnes, string? numeroCompte, string? codeClient, string? nomClient)
        {
            if (string.IsNullOrWhiteSpace(numeroCompte))
                throw new InvalidOperationException("Numéro de compte manquant");

            // Format CSV:
            // [0] Date comptable
            // [1] Numéro pièce
            // [2] Code journal
            // [3] Libellé
            // [4] Date facture
            // [5] Référence
            // [6] Date échéance
            // [7] ! (séparateur visuel)
            // [8] Débit
            // [9] Crédit
            // [10+] Autres colonnes

            var dateComptable = colonnes[0].Trim();
            var numeroPiece = colonnes.Count > 1 ? colonnes[1].Trim() : null;
            var codeJournal = colonnes.Count > 2 ? colonnes[2].Trim() : null;
            var libelle = colonnes.Count > 3 ? colonnes[3].Trim() : null;
            var dateFacture = colonnes.Count > 4 ? colonnes[4].Trim() : null;
            var reference = colonnes.Count > 5 ? colonnes[5].Trim() : null;
            var dateEcheance = colonnes.Count > 6 ? colonnes[6].Trim() : null;
            var debitStr = colonnes.Count > 8 ? colonnes[8].Trim() : "";
            var creditStr = colonnes.Count > 9 ? colonnes[9].Trim() : "";
            
            string? numeroLettrage = null;

            // on cherche l'index du 2e "!" (séparateur après crédit)
            var indicesSeparateurs = colonnes
                .Select((val, idx) => new { val, idx })
                .Where(x => x.val.Trim() == "!")
                .Select(x => x.idx)
                .ToList();

            Console.WriteLine($"   🔍 Séparateurs '!' trouvés: {string.Join(", ", indicesSeparateurs)}");

            if (indicesSeparateurs.Count >= 2)
            {
                var indexLettrage = indicesSeparateurs[1] + 2;

                if (indexLettrage < colonnes.Count)
                {
                    var candidat = NettoyerTexte(colonnes[indexLettrage]);
                    Console.WriteLine($"   🔍 Candidat lettrage à l'index {indexLettrage}: [{candidat}]");

                    // Accepter lettres et chiffres (ex: "LM00001", "12345", "A123", etc.)
                    if (!string.IsNullOrWhiteSpace(candidat))
                    {
                        // Vérifier que c'est un identifiant valide (lettres, chiffres, tirets, underscores)
                        // Exclure les dates, montants, et autres formats
                        var estLettrageValide = candidat.Length > 0 && candidat.Length <= 20 &&
                            (candidat.All(c => char.IsLetterOrDigit(c) || c == '-' || c == '_') ||
                             candidat.All(char.IsDigit));

                        if (estLettrageValide)
                        {
                            numeroLettrage = candidat;
                            Console.WriteLine($"   ✅ Lettrage accepté: [{numeroLettrage}]");
                        }
                        else
                        {
                            Console.WriteLine($"   ⚠️ Candidat rejeté (format invalide): [{candidat}]");
                        }
                    }
                }
                else
                {
                    Console.WriteLine($"   ⚠️ Index lettrage {indexLettrage} hors limites (max: {colonnes.Count - 1})");
                }
            }
            else
            {
                // Si pas de séparateurs "!", chercher dans les colonnes après le crédit
                // Le lettrage peut être dans les colonnes 10, 11, 12, etc.
                Console.WriteLine($"   🔍 Pas assez de séparateurs '!', recherche alternative...");
                for (int i = 10; i < colonnes.Count && i < 15; i++)
                {
                    var candidat = NettoyerTexte(colonnes[i]);
                    if (!string.IsNullOrWhiteSpace(candidat))
                    {
                        // Vérifier si ça ressemble à un lettrage (pas une date, pas un montant)
                        var estLettrageValide = candidat.Length > 0 && candidat.Length <= 20 &&
                            !candidat.Contains("/") && // Exclure les dates
                            !candidat.Contains(",") && !candidat.Contains(".") && // Exclure les montants
                            (candidat.All(c => char.IsLetterOrDigit(c) || c == '-' || c == '_') ||
                             candidat.All(char.IsDigit));

                        if (estLettrageValide)
                        {
                            numeroLettrage = candidat;
                            Console.WriteLine($"   ✅ Lettrage trouvé dans colonne {i}: [{numeroLettrage}]");
                            break;
                        }
                    }
                }
            }


            Console.WriteLine($"📝 {dateComptable} | Colonnes totales: {colonnes.Count}");
            Console.WriteLine($"   Colonne[8] Débit RAW: [{colonnes[8]}]");
            Console.WriteLine($"   Colonne[9] Crédit RAW: [{colonnes[9]}]");
            Console.WriteLine($"   Débit nettoyé: [{debitStr}]");
            Console.WriteLine($"   Crédit nettoyé: [{creditStr}]");
            Console.WriteLine($"🔗 Lettrage détecté: [{numeroLettrage}]");

            Console.WriteLine(
                $"📝 {dateComptable} - {(libelle == null ? "" : libelle.Substring(0, Math.Min(30, libelle.Length)))} | D:{debitStr} C:{creditStr}"
            );
            // Chercher ou créer le compte
            var compte = _context.ChangeTracker
                .Entries<Compte>()
                .Select(e => e.Entity)
                .FirstOrDefault(c => c.NumeroCompte == numeroCompte && c.CodeClient == codeClient);

            if (compte == null)
            {
                compte = await _context.Comptes
                    .FirstOrDefaultAsync(c => c.NumeroCompte == numeroCompte && c.CodeClient == codeClient);
            }

            if (compte == null)
            {
                compte = new Compte
                {
                    NumeroCompte = numeroCompte,
                    CodeClient = codeClient,
                    NomClient = nomClient ?? codeClient ?? numeroCompte,
                    DateCreation = DateTime.UtcNow,
                    Ecritures = new List<Ecriture>()
                };
                _context.Comptes.Add(compte);
                Console.WriteLine($"   ✨ Nouveau compte: {numeroCompte} ({codeClient})");
            }

            var ecriture = new Ecriture
            {
                Compte = compte,
                DateComptable = ParseDate(dateComptable),
                NumeroPiece = NettoyerTexte(numeroPiece),
                CodeJournal = NettoyerTexte(codeJournal),
                Libelle = NettoyerTexte(libelle),
                DateFacture = ParseDateOptionnelle(dateFacture),
                Reference = NettoyerTexte(reference),
                DateEcheance = ParseDateOptionnelle(dateEcheance),
                Debit = ParseMontant(debitStr),
                Credit = ParseMontant(creditStr),
                NumeroLettrage = numeroLettrage,
                DateSaisie = DateTime.UtcNow
            };

            _context.Ecritures.Add(ecriture);
        }

        private DateTime ParseDate(string dateStr)
        {
            if (string.IsNullOrWhiteSpace(dateStr))
                throw new ArgumentException("Date vide");

            var formats = new[] { 
                "d/M/yyyy", "dd/MM/yyyy", "dd/M/yyyy", "d/MM/yyyy"
            };

            var dt = DateTime.ParseExact(dateStr.Trim(), formats,
                CultureInfo.InvariantCulture, DateTimeStyles.None);
            
            return DateTime.SpecifyKind(dt, DateTimeKind.Utc);
        }

        private DateTime? ParseDateOptionnelle(string? dateStr)
        {
            if (string.IsNullOrWhiteSpace(dateStr))
                return null;

            var formats = new[] { 
                "d/M/yyyy", "dd/MM/yyyy", "dd/M/yyyy", "d/MM/yyyy"
            };

            if (DateTime.TryParseExact(dateStr.Trim(), formats,
                CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime date))
                return DateTime.SpecifyKind(date, DateTimeKind.Utc);

            return null;
        }

        private decimal ParseMontant(string montantStr)
        {
            if (string.IsNullOrWhiteSpace(montantStr))
                return 0;

            // Nettoyer le montant : enlever TOUS les caractères non-numériques sauf virgule et point
            var nettoye = new StringBuilder();
            foreach (char c in montantStr)
            {
                if (char.IsDigit(c) || c == ',' || c == '.')
                {
                    nettoye.Append(c);
                }
            }

            // Remplacer virgule par point pour le parsing
            var montantFinal = nettoye.ToString().Replace(",", ".");

            Console.WriteLine($"      💰 Parsing montant: '{montantStr}' → '{montantFinal}'");

            if (decimal.TryParse(montantFinal, NumberStyles.Any, CultureInfo.InvariantCulture, out var montant))
            {
                Console.WriteLine($"      ✅ Montant parsé: {montant}");
                return montant;
            }

            Console.WriteLine($"      ❌ Échec parsing montant");
            return 0;
        }

        private string? NettoyerTexte(string? texte)
        {
            if (string.IsNullOrWhiteSpace(texte))
                return null;

            return texte.Trim().Replace("\"", "");
        }

        // Parser CSV qui gère correctement les champs entre guillemets
        private List<string> ParseCsvLine(string ligne)
        {
            var colonnes = new List<string>();
            var champActuel = new StringBuilder();
            var dansGuillemets = false;

            for (int i = 0; i < ligne.Length; i++)
            {
                char c = ligne[i];

                if (c == '"')
                {
                    dansGuillemets = !dansGuillemets;
                }
                else if (c == ',' && !dansGuillemets)
                {
                    colonnes.Add(champActuel.ToString());
                    champActuel.Clear();
                }
                else
                {
                    champActuel.Append(c);
                }
            }

            // Ajouter le dernier champ
            colonnes.Add(champActuel.ToString());

            return colonnes;
        }
    }

    public class ResultatImport
    {
        public bool Succes { get; set; }
        public int NombreLignesImportees { get; set; }
        public List<string> Erreurs { get; set; } = new();
    }
}