// Services/GrandLivreService.cs
using System.Text.Json;
using dadaApp.Data;
using dadaApp.Models;
using Microsoft.EntityFrameworkCore;

namespace dadaApp.Services
{
    public class GrandLivreService
    {
        private readonly AppDbContext _context;

        public GrandLivreService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<List<Ecriture>> GetEcrituresAvecCompteAsync()
        {
            return await _context.Ecritures
                .Include(e => e.Compte)
                .OrderBy(e => e.DateComptable)
                .ToListAsync();
        }

        public async Task<(bool success, string message, string? numeroLettrage)> CreerLettrageManuelAsync(
            List<int> ecritureIds)
        {
            var ecritures = await _context.Ecritures
                .Include(e => e.Compte)
                .Where(e => ecritureIds.Contains(e.EcritureId))
                .ToListAsync();

            // Validation 1: Au moins 2 écritures
            if (ecritures.Count < 2)
                return (false, "Vous devez sélectionner au moins 2 écritures", null);

            // Validation 2: Toutes les écritures doivent être du même client
            var clients = ecritures.Select(e => e.Compte?.CodeClient).Distinct().ToList();
            if (clients.Count > 1 || clients.Any(c => string.IsNullOrEmpty(c)))
                return (false, "Toutes les écritures doivent appartenir au même client", null);

            // Validation 3: Total Débit - Total Crédit = 0
            var totalDebit = ecritures.Sum(e => e.Debit);
            var totalCredit = ecritures.Sum(e => e.Credit);
            if (Math.Abs(totalDebit - totalCredit) > 0.01m)
                return (false, $"Le solde doit être nul (Débit: {totalDebit:N2}, Crédit: {totalCredit:N2})", null);

            // Validation 4: Aucune écriture ne doit déjà être lettrée
            if (ecritures.Any(e => !string.IsNullOrWhiteSpace(e.NumeroLettrage)))
                return (false, "Une ou plusieurs écritures sont déjà lettrées", null);

            // Générer le numéro de lettrage
            var dernierLettrage = await _context.LettragesManuels
                .OrderByDescending(l => l.NumeroLettrage)
                .FirstOrDefaultAsync();

            int prochainNumero = 1;
            if (dernierLettrage != null && dernierLettrage.NumeroLettrage.StartsWith("LM"))
            {
                var numStr = dernierLettrage.NumeroLettrage.Substring(2);
                if (int.TryParse(numStr, out int num))
                    prochainNumero = num + 1;
            }

            string numeroLettrage = $"LM{prochainNumero:D4}";

            // Sauvegarder les détails complets pour réintégration
            var details = ecritures.Select(e => new EcritureLettrageDetail
            {
                EcritureId = e.EcritureId,
                DateComptable = e.DateComptable,
                CodeJournal = e.CodeJournal,
                NumeroPiece = e.NumeroPiece,
                Libelle = e.Libelle,
                Debit = e.Debit,
                Credit = e.Credit,
                DateEcheance = e.DateEcheance,
                NumeroCompte = e.Compte?.NumeroCompte ?? ""
            }).ToList();

            var lettrageManuel = new LettrageManuel
            {
                NumeroLettrage = numeroLettrage,
                DateCreation = DateTime.UtcNow,
                CodeClient = ecritures.First().Compte!.CodeClient!,
                NomClient = ecritures.First().Compte!.NomClient ?? "",
                TotalDebit = totalDebit,
                TotalCredit = totalCredit,
                EcrituresJson = JsonSerializer.Serialize(details)
            };

            _context.LettragesManuels.Add(lettrageManuel);

            // Mettre à jour les écritures
            foreach (var ecriture in ecritures)
            {
                ecriture.NumeroLettrage = numeroLettrage;
            }

            await _context.SaveChangesAsync();

            return (true, $"Lettrage {numeroLettrage} créé avec succès", numeroLettrage);
        }

        public async Task<(bool success, string message)> SupprimerLettrageManuelAsync(string numeroLettrage)
        {
            var lettrage = await _context.LettragesManuels
                .FirstOrDefaultAsync(l => l.NumeroLettrage == numeroLettrage);

            if (lettrage == null)
                return (false, "Lettrage non trouvé");

            // Délettrer les écritures existantes
            var ecritures = await _context.Ecritures
                .Where(e => e.NumeroLettrage == numeroLettrage)
                .ToListAsync();

            foreach (var ecriture in ecritures)
            {
                ecriture.NumeroLettrage = null;
            }

            // On garde l'enregistrement LettrageManuel pour l'historique
            // Si vous voulez le supprimer aussi, décommentez :
            _context.LettragesManuels.Remove(lettrage);

            await _context.SaveChangesAsync();

            return (true, "Lettrage supprimé avec succès");
        }

        public async Task<(bool success, string message)> SupprimerTousLesLettragesAsync()
        {
            var ecritures = await _context.Ecritures
                .Where(e => !string.IsNullOrWhiteSpace(e.NumeroLettrage))
                .ToListAsync();

            foreach (var ecriture in ecritures)
            {
                ecriture.NumeroLettrage = null;
            }

            _context.LettragesManuels.RemoveRange(_context.LettragesManuels);

            await _context.SaveChangesAsync();
            return (true, "Tous les lettrages ont été supprimés");
        }

        public async Task<List<LettrageManuel>> GetLettrageManuelsAsync()
        {
            return await _context.LettragesManuels
                .OrderByDescending(l => l.DateCreation)
                .ToListAsync();
        }

        /// <summary>
        /// Propose automatiquement des lettrages pour les écritures non lettrées
        /// où la somme des débits - somme des crédits = 0, pour le même nom client
        /// </summary>
        public async Task<List<PropositionLettrage>> ProposerLettragesAutomatiquesAsync(string? nomClientFiltre = null)
        {
            Console.WriteLine($"🔍 [C#] Début de ProposerLettragesAutomatiquesAsync (client: {nomClientFiltre ?? "tous"})");
            
            // Récupérer toutes les écritures non lettrées avec leur compte
            Console.WriteLine("🔍 [C#] Récupération des écritures non lettrées...");
            var query = _context.Ecritures
                .Include(e => e.Compte)
                .Where(e => string.IsNullOrWhiteSpace(e.NumeroLettrage) && 
                           e.Compte != null && 
                           !string.IsNullOrWhiteSpace(e.Compte.NomClient));

            // Filtrer par client si spécifié
            if (!string.IsNullOrWhiteSpace(nomClientFiltre))
            {
                query = query.Where(e => e.Compte!.NomClient.Contains(nomClientFiltre));
                Console.WriteLine($"🔍 [C#] Filtrage par client: {nomClientFiltre}");
            }

            var ecrituresNonLettre = await query
                .OrderBy(e => e.Compte!.NomClient)
                .ThenBy(e => e.DateComptable)
                .ToListAsync();

            Console.WriteLine($"🔍 [C#] {ecrituresNonLettre.Count} écritures non lettrées trouvées");

            var propositions = new List<PropositionLettrage>();

            // Grouper par nom client
            Console.WriteLine("🔍 [C#] Groupement par nom client...");
            var groupesParClient = ecrituresNonLettre
                .GroupBy(e => e.Compte!.NomClient)
                .ToList();

            Console.WriteLine($"🔍 [C#] {groupesParClient.Count} groupes de clients trouvés");

            int groupeIndex = 0;
            foreach (var groupe in groupesParClient)
            {
                groupeIndex++;
                var nomClient = groupe.Key;
                var codeClient = groupe.First().Compte?.CodeClient;
                var ecrituresClient = groupe.ToList();

                // Limiter le nombre d'écritures par client pour optimiser (max 50)
                if (ecrituresClient.Count > 50)
                {
                    Console.WriteLine($"🔍 [C#] Client {nomClient} a {ecrituresClient.Count} écritures, limitation à 50 pour optimisation");
                    ecrituresClient = ecrituresClient.Take(50).ToList();
                }

                Console.WriteLine($"🔍 [C#] Traitement du groupe {groupeIndex}/{groupesParClient.Count}: {nomClient} ({ecrituresClient.Count} écritures)");

                // Trouver les combinaisons qui s'équilibrent
                Console.WriteLine($"🔍 [C#] Recherche de combinaisons équilibrées pour {nomClient}...");
                var combinaisons = TrouverCombinaisonsEquilibrees(ecrituresClient);
                Console.WriteLine($"🔍 [C#] {combinaisons.Count} combinaisons trouvées pour {nomClient}");

                foreach (var combinaison in combinaisons)
                {
                    var totalDebit = combinaison.Sum(e => e.Debit);
                    var totalCredit = combinaison.Sum(e => e.Credit);

                    propositions.Add(new PropositionLettrage
                    {
                        NomClient = nomClient,
                        CodeClient = codeClient,
                        EcritureIds = combinaison.Select(e => e.EcritureId).ToList(),
                        TotalDebit = totalDebit,
                        TotalCredit = totalCredit,
                        NombreEcritures = combinaison.Count,
                        Ecritures = combinaison.Select(e => new EcritureInfo
                        {
                            EcritureId = e.EcritureId,
                            DateComptable = e.DateComptable,
                            NumeroPiece = e.NumeroPiece,
                            Libelle = e.Libelle,
                            Debit = e.Debit,
                            Credit = e.Credit
                        }).ToList()
                    });
                }
            }

            Console.WriteLine($"🔍 [C#] Total de {propositions.Count} propositions trouvées");

            // Trier par nombre d'écritures (du plus simple au plus complexe)
            var resultat = propositions.OrderBy(p => p.NombreEcritures).ToList();
            Console.WriteLine($"🔍 [C#] Retour de {resultat.Count} propositions triées");
            
            return resultat;
        }

        /// <summary>
        /// Trouve les combinaisons d'écritures qui s'équilibrent (somme débit - somme crédit = 0)
        /// Utilise un algorithme de recherche de combinaisons avec limite de taille
        /// </summary>
        private List<List<Ecriture>> TrouverCombinaisonsEquilibrees(List<Ecriture> ecritures)
        {
            Console.WriteLine($"🔍 [C#] TrouverCombinaisonsEquilibrees: {ecritures.Count} écritures");
            var resultats = new List<List<Ecriture>>();
            var ecrituresUtilisees = new HashSet<int>();

            // Limite de taille réduite pour optimisation (max 6 écritures au lieu de 10)
            const int maxTaille = 6;
            // Limiter le nombre d'écritures à traiter (max 30)
            const int maxEcritures = 30;

            // Trier les écritures par solde (débit - crédit)
            var ecrituresAvecSolde = ecritures
                .Select(e => (Ecriture: e, Solde: e.Debit - e.Credit))
                .OrderByDescending(x => Math.Abs(x.Solde))
                .Take(maxEcritures) // Limiter à 30 écritures max
                .ToList();

            Console.WriteLine($"🔍 [C#] Écritures triées par solde ({ecrituresAvecSolde.Count} traitées), recherche de combinaisons de taille 2 à {Math.Min(maxTaille, ecrituresAvecSolde.Count)}");

            // Chercher d'abord les combinaisons simples (2 écritures), puis 3, puis 4...
            for (int taille = 2; taille <= Math.Min(maxTaille, ecrituresAvecSolde.Count); taille++)
            {
                Console.WriteLine($"🔍 [C#] Recherche de combinaisons de taille {taille}...");
                var combinaisons = GenererCombinaisons(ecrituresAvecSolde, taille, ecrituresUtilisees);
                Console.WriteLine($"🔍 [C#] {combinaisons.Count} combinaisons de taille {taille} trouvées");
                resultats.AddRange(combinaisons);
                
                // Arrêter si on a trouvé assez de combinaisons (max 20 par client)
                if (resultats.Count >= 20)
                {
                    Console.WriteLine($"🔍 [C#] Limite de 20 combinaisons atteinte, arrêt de la recherche");
                    break;
                }
            }

            Console.WriteLine($"🔍 [C#] Total: {resultats.Count} combinaisons équilibrées trouvées");
            return resultats;
        }

        /// <summary>
        /// Génère des combinaisons de taille donnée qui s'équilibrent
        /// </summary>
        private List<List<Ecriture>> GenererCombinaisons(
            List<(Ecriture Ecriture, decimal Solde)> ecrituresAvecSolde, 
            int taille, 
            HashSet<int> ecrituresUtilisees)
        {
            Console.WriteLine($"🔍 [C#] GenererCombinaisons: taille={taille}, {ecrituresAvecSolde.Count} écritures disponibles");
            var resultats = new List<List<Ecriture>>();
            int iterations = 0;
            int combinaisonsTestees = 0;

            // Utiliser une approche récursive pour trouver les combinaisons
            void ChercherCombinaison(List<int> indices, int indexDebut, decimal soldeActuel)
            {
                iterations++;
                if (iterations % 10000 == 0)
                {
                    Console.WriteLine($"🔍 [C#] GenererCombinaisons: {iterations} itérations, {combinaisonsTestees} combinaisons testées, {resultats.Count} résultats trouvés");
                }

                // Si on a atteint la taille désirée
                if (indices.Count == taille)
                {
                    combinaisonsTestees++;
                    // Vérifier si le solde est équilibré (tolérance de 0.01)
                    if (Math.Abs(soldeActuel) <= 0.01m)
                    {
                        var combinaison = indices
                            .Select(i => ecrituresAvecSolde[i].Ecriture)
                            .ToList();

                        // Vérifier qu'aucune écriture n'est déjà utilisée dans une autre proposition
                        if (!indices.Any(i => ecrituresUtilisees.Contains(ecrituresAvecSolde[i].Ecriture.EcritureId)))
                        {
                            resultats.Add(combinaison);
                            // Marquer ces écritures comme utilisées
                            foreach (var idx in indices)
                            {
                                ecrituresUtilisees.Add(ecrituresAvecSolde[idx].Ecriture.EcritureId);
                            }
                            Console.WriteLine($"🔍 [C#] Combinaison équilibrée trouvée: {taille} écritures, solde={soldeActuel:F2}");
                        }
                    }
                    return;
                }

                // Si on dépasse la taille ou qu'on n'a plus assez d'éléments
                if (indices.Count > taille || indexDebut >= ecrituresAvecSolde.Count)
                    return;

                // Limiter la recherche pour éviter les combinaisons trop nombreuses
                // On cherche seulement parmi les premières écritures les plus significatives (réduit à 20)
                var limiteRecherche = Math.Min(indexDebut + 20, ecrituresAvecSolde.Count);

                for (int i = indexDebut; i < limiteRecherche; i++)
                {
                    // Éviter les écritures déjà utilisées
                    if (ecrituresUtilisees.Contains(ecrituresAvecSolde[i].Ecriture.EcritureId))
                        continue;

                    var nouveauSolde = soldeActuel + ecrituresAvecSolde[i].Solde;
                    var nouveauxIndices = new List<int>(indices) { i };

                    ChercherCombinaison(nouveauxIndices, i + 1, nouveauSolde);
                }
            }

            Console.WriteLine($"🔍 [C#] Début de la recherche récursive pour taille {taille}");
            ChercherCombinaison(new List<int>(), 0, 0m);
            Console.WriteLine($"🔍 [C#] Fin de la recherche: {iterations} itérations, {combinaisonsTestees} combinaisons testées, {resultats.Count} résultats");

            return resultats;
        }
    }
}