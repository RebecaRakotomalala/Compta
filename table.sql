-- Table principale : Comptes
CREATE TABLE Comptes (
    CompteId SERIAL PRIMARY KEY,
    NumeroCompte VARCHAR(20) NOT NULL UNIQUE,
    CodeClient VARCHAR(20),
    NomClient VARCHAR(200) NOT NULL,
    DateCreation TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- Table : Écritures comptables
CREATE TABLE Ecritures (
    EcritureId SERIAL PRIMARY KEY,
    CompteId INT NOT NULL,
    DateComptable DATE NOT NULL,
    NumeroPiece VARCHAR(20),
    CodeJournal VARCHAR(10),
    Libelle VARCHAR(200),
    DateFacture DATE,
    Reference VARCHAR(50),
    DateEcheance DATE,
    Debit DECIMAL(15,2) DEFAULT 0,
    Credit DECIMAL(15,2) DEFAULT 0,
    NumeroLettrage VARCHAR(20),
    DateSaisie TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    
    CONSTRAINT fk_compte FOREIGN KEY (CompteId) 
        REFERENCES Comptes(CompteId) ON DELETE CASCADE,
    CONSTRAINT chk_montant CHECK (Debit >= 0 AND Credit >= 0)
);

-- Index pour performances
CREATE INDEX idx_ecritures_date ON Ecritures(DateComptable);
CREATE INDEX idx_ecritures_compte ON Ecritures(CompteId);
CREATE INDEX idx_ecritures_reference ON Ecritures(Reference);
CREATE INDEX idx_ecritures_lettrage ON Ecritures(NumeroLettrage);

-- Vue pour soldes
CREATE VIEW VueSoldesComptes AS
SELECT 
    c.CompteId,
    c.NumeroCompte,
    c.NomClient,
    SUM(e.Debit) as TotalDebit,
    SUM(e.Credit) as TotalCredit,
    SUM(e.Debit - e.Credit) as Solde
FROM Comptes c
LEFT JOIN Ecritures e ON c.CompteId = e.CompteId
GROUP BY c.CompteId, c.NumeroCompte, c.NomClient;