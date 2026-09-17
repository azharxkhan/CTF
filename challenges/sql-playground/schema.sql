-- SQL playground schema. Matches docs/player-guide/sql-playground.md.
-- Written in a portable subset that works for SQLite (per-player sandbox) and, with the
-- noted tweaks, SQL Server (Playground_<name>). Small, readable, e-commerce shaped.
--
-- SQL Server notes:
--   * INTEGER PRIMARY KEY  -> INT IDENTITY(1,1) PRIMARY KEY
--   * TEXT                 -> NVARCHAR(200)
--   * REAL                 -> DECIMAL(10,2)
-- The seed.sql INSERTs are dialect-neutral and work as-is on both.

DROP TABLE IF EXISTS Orders;
DROP TABLE IF EXISTS Invoices;
DROP TABLE IF EXISTS Comments;
DROP TABLE IF EXISTS Products;
DROP TABLE IF EXISTS Flags;
DROP TABLE IF EXISTS Users;

CREATE TABLE Users (
    Id           INTEGER PRIMARY KEY,
    Email        TEXT NOT NULL,
    DisplayName  TEXT NOT NULL,
    PasswordHash TEXT NOT NULL,   -- fake hashes, not real credentials
    IsAdmin      INTEGER NOT NULL DEFAULT 0
);

CREATE TABLE Products (
    Id     INTEGER PRIMARY KEY,
    Name   TEXT NOT NULL,
    Price  REAL NOT NULL,
    Stock  INTEGER NOT NULL
);

CREATE TABLE Invoices (
    Id       INTEGER PRIMARY KEY,
    OwnerId  INTEGER NOT NULL,     -- FK -> Users.Id (IDOR practice)
    Amount   REAL NOT NULL,
    Notes    TEXT
);

CREATE TABLE Orders (
    Id         INTEGER PRIMARY KEY,
    UserId     INTEGER NOT NULL,   -- FK -> Users.Id
    ProductId  INTEGER NOT NULL,   -- FK -> Products.Id
    Qty        INTEGER NOT NULL,
    Ref        TEXT NOT NULL       -- the FromSqlRaw target (challenge 4 practice)
);

CREATE TABLE Comments (
    Id        INTEGER PRIMARY KEY,
    AuthorId  INTEGER NOT NULL,    -- FK -> Users.Id
    Body      TEXT NOT NULL        -- rendered in the web app (XSS practice)
);

CREATE TABLE Flags (
    Id      INTEGER PRIMARY KEY,
    Name    TEXT NOT NULL,
    Secret  TEXT NOT NULL          -- PRACTICE flags only (UNION / blind extraction drills)
);
