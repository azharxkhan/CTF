-- Deterministic seed for the SQL playground. All data is FAKE. Explicit INSERTs (no random)
-- so Restore always reproduces exactly this. Practice flags only — not the scored flags.

-- Users: normal users + one admin (Id 3). Password hashes are fake placeholders.
INSERT INTO Users (Id, Email, DisplayName, PasswordHash, IsAdmin) VALUES
 (1, 'user1@corp.local',  'Alex Rivera',   'md5:5f4dcc3b5aa765d61d8327deb882cf99', 0),
 (2, 'user2@corp.local',  'Sam Okafor',    'md5:e10adc3949ba59abbe56e057f20f883e', 0),
 (3, 'admin@corp.local',  'Admin',         'md5:21232f297a57a5a743894a0e4a801fc3', 1),
 (4, 'user4@corp.local',  'Priya Nair',    'md5:25d55ad283aa400af464c76d713c07ad', 0),
 (5, 'user5@corp.local',  'Jordan Blake',  'md5:d8578edf8458ce06fbc5bb76a58c5ca4', 0),
 (6, 'user6@corp.local',  'Wei Zhang',     'md5:0d107d09f5bbe40cade3de5c71e9e9b7', 0),
 (7, 'user7@corp.local',  'Fatima Yusuf',  'md5:5ebe2294ecd0e0f08eab7690d2a6ee69', 0),
 (8, 'user8@corp.local',  'Diego Santos',  'md5:6cb75f652a9b52798eb6cf2201057c73', 0);

-- Products: the search target for SQLi practice.
INSERT INTO Products (Id, Name, Price, Stock) VALUES
 (1, 'Widget',        9.99,  120),
 (2, 'Gadget',        19.50, 80),
 (3, 'Sprocket',      4.25,  500),
 (4, 'Cog',           2.10,  1000),
 (5, 'Flux Capacitor',999.00,3),
 (6, 'Bracket',       6.75,  240),
 (7, 'Grommet',       1.15,  1500),
 (8, 'Widget Pro',    14.99, 60);

-- Invoices: owned by various users. IDOR practice — one holds a practice flag in Notes.
INSERT INTO Invoices (Id, OwnerId, Amount, Notes) VALUES
 (1, 1, 120.00, 'Q1 order'),
 (2, 2, 45.50,  'replacement parts'),
 (3, 3, 8800.00,'admin: flag{practice_idor_read_others_invoice}'),
 (4, 4, 15.00,  'sample'),
 (5, 1, 210.75, 'bulk widgets'),
 (6, 5, 33.20,  'misc'),
 (7, 6, 500.00, 'flux capacitor deposit');

-- Orders: the Ref field is the interpolation target for challenge 4 practice.
INSERT INTO Orders (Id, UserId, ProductId, Qty, Ref) VALUES
 (1, 1, 1, 10, 'ORD-1001'),
 (2, 2, 2, 2,  'ORD-1002'),
 (3, 4, 3, 50, 'ORD-1003'),
 (4, 1, 8, 4,  'ORD-1004'),
 (5, 5, 4, 100,'ORD-1005'),
 (6, 6, 5, 1,  'ORD-1006');

-- Comments: rendered in the web app; XSS practice. Seeded benign.
INSERT INTO Comments (Id, AuthorId, Body) VALUES
 (1, 1, 'Great product, fast shipping.'),
 (2, 2, 'Does this come in blue?'),
 (3, 4, 'Works as described.');

-- Flags table: PRACTICE values for UNION-based and blind-extraction drills.
INSERT INTO Flags (Id, Name, Secret) VALUES
 (1, 'union_practice', 'flag{practice_union_select_works}'),
 (2, 'blind_practice', 'flag{practice_blind_timing}'),
 (3, 'admin_note',     'flag{practice_read_the_admin_row}');
