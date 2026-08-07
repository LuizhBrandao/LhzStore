CREATE TABLE ProductPriceHistory (
    Id UNIQUEIDENTIFIER PRIMARY KEY,
    ProductId UNIQUEIDENTIFIER NOT NULL,
    AveragePurchasePrice DECIMAL(18,2) NOT NULL,
    LigaPokemonPrice DECIMAL(18,2) NOT NULL,
    ChangeDate DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    
    -- Cria o relacionamento: se o produto for deletado, o histórico dele também é limpo (Cascata)
    CONSTRAINT FK_ProductPriceHistory_Products FOREIGN KEY (ProductId) REFERENCES Products(Id) ON DELETE CASCADE
);