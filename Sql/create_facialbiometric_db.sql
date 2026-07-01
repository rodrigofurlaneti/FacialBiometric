-- =====================================================================
-- FacialBiometricDB — banco novo e independente, dedicado ao
-- microsserviço FacialBiometricAPI. Não referencia o banco
-- OpenFinancialExchange (Users aqui é autônomo: só Id + FullName + foto).
-- Padrões: PascalCase, BIGINT IDENTITY(1,1), DATETIME2, soft delete.
-- =====================================================================

IF DB_ID('FacialBiometricDB') IS NULL
BEGIN
    CREATE DATABASE FacialBiometricDB;
END
GO

USE FacialBiometricDB;
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Users')
BEGIN
    CREATE TABLE Users
    (
        Id                  BIGINT IDENTITY(1,1) NOT NULL,
        FullName            NVARCHAR(200)         NOT NULL,

        -- Biometria facial ----------------------------------------------
        -- PhotoPath: caminho relativo do arquivo no storage (sistema de
        --            arquivos do servidor — ver IPhotoStorageService).
        -- FaceEmbedding: vetor de características faciais gerado pelo
        --            provedor de biometria (JSON serializado). Fica NULL
        --            até o modelo real ser plugado em
        --            LocalFacialBiometricProvider (Infrastructure).
        -- PhotoRegisteredAt: quando a foto/embedding foi gravada — usado
        --            para responder "usuário já tem foto cadastrada?"
        PhotoPath           NVARCHAR(500)         NULL,
        FaceEmbedding       NVARCHAR(MAX)         NULL,
        PhotoRegisteredAt   DATETIME2             NULL,

        IsActive            BIT                   NOT NULL CONSTRAINT DF_Users_IsActive DEFAULT (1),
        CreatedAt           DATETIME2             NOT NULL CONSTRAINT DF_Users_CreatedAt DEFAULT (SYSUTCDATETIME()),
        UpdatedAt           DATETIME2             NOT NULL CONSTRAINT DF_Users_UpdatedAt DEFAULT (SYSUTCDATETIME()),

        CONSTRAINT PK_Users PRIMARY KEY CLUSTERED (Id)
    );

    -- Consulta mais comum: "este usuário já tem foto?" — cobre com include
    CREATE INDEX IX_Users_Id_PhotoPath
        ON Users (Id)
        INCLUDE (PhotoPath, PhotoRegisteredAt);
END
GO
