dotnet ef migrations add InitialCreate
dotnet ef database update


psql -U postgres -d login_db

SELECT * FROM "Comptes";
SELECT date_comptable,numero_piece,code_journal,debit,credit FROM "Ecritures" order by date_comptable asc limit 10;

SELECT
    e.date_comptable,
    e.numero_piece,
    e.code_journal,
    e.numero_lettrage,
    e.reference,
    e.date_facture,
    e.date_echeance,
    e.debit,
    e.credit,
    c.numero_compte,
    c.code_client,
    c.nom_client
FROM "Ecritures" e
JOIN "Comptes" c ON c.compte_id = e.compte_id
WHERE c.numero_compte = '411000'
  AND c.nom_client = 'S.F.O.I'
  AND e.date_comptable BETWEEN '2025-01-01' AND '2025-06-30'
ORDER BY e.date_comptable, e.ecriture_id;


SELECT
    e.date_comptable,
    e.numero_piece,
    e.code_journal,
    e.libelle,
    e.numero_lettrage,
    c.code_client,
    c.nom_client
FROM "Ecritures" e
JOIN "Comptes" c ON c.compte_id = e.compte_id
WHERE c.nom_client = 'S.F.O.I'
ORDER BY e.date_comptable, e.ecriture_id limit 10;

DELETE FROM "Comptes";
DELETE FROM "Ecritures";


SELECT COUNT(*) FROM "Comptes";
SELECT COUNT(*) FROM "Ecritures";