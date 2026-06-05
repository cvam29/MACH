/*
 * Idempotent fulfillment seed for the local SQL Server `fulfillment` schema.
 *
 * Targets tables OWNED BY ANOTHER TRACK (EF Core migrations create them):
 *   fulfillment.Stores          (Id, Name, Email, ReceptionEmail, Lat, Lng)
 *   fulfillment.Suppliers       (Id, Name, Email)
 *   fulfillment.ProductSuppliers(Sku, SupplierId)
 *
 * MERGE-based upserts keyed on natural keys (store Name, supplier Name, SKU) so
 * re-running never duplicates rows. Deterministic GUIDs keep FK references and
 * re-runs stable. Stores are spread across a region (lat/lng) so distance-based
 * delivery quoting produces varied results.
 *
 * NOTE: this script assumes the `fulfillment` schema + tables already exist.
 * It does NOT create them (the Persistence/EF track owns the schema).
 */

SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRANSACTION;

/* ---------- Stores ---------- */
DECLARE @Stores TABLE (
    Id              UNIQUEIDENTIFIER,
    Name            NVARCHAR(200),
    Email           NVARCHAR(320),
    ReceptionEmail  NVARCHAR(320),
    Lat             FLOAT,
    Lng             FLOAT
);

INSERT INTO @Stores (Id, Name, Email, ReceptionEmail, Lat, Lng) VALUES
    ('1f1e1d1c-0001-4a00-9000-000000000001', N'Berlin Mitte Flagship',   N'berlin.store@mach-demo.example',    N'berlin.reception@mach-demo.example',    52.5200, 13.4050),
    ('1f1e1d1c-0001-4a00-9000-000000000002', N'Munich Marienplatz',      N'munich.store@mach-demo.example',    N'munich.reception@mach-demo.example',    48.1351, 11.5820),
    ('1f1e1d1c-0001-4a00-9000-000000000003', N'Hamburg HafenCity',       N'hamburg.store@mach-demo.example',   N'hamburg.reception@mach-demo.example',   53.5411, 9.9940),
    ('1f1e1d1c-0001-4a00-9000-000000000004', N'Cologne Innenstadt',      N'cologne.store@mach-demo.example',   N'cologne.reception@mach-demo.example',   50.9375, 6.9603),
    ('1f1e1d1c-0001-4a00-9000-000000000005', N'Frankfurt Zeil',          N'frankfurt.store@mach-demo.example', N'frankfurt.reception@mach-demo.example', 50.1109, 8.6821);

MERGE fulfillment.Stores AS target
USING @Stores AS source
    ON target.Name = source.Name
WHEN MATCHED THEN
    UPDATE SET
        target.Email          = source.Email,
        target.ReceptionEmail = source.ReceptionEmail,
        target.Lat            = source.Lat,
        target.Lng            = source.Lng
WHEN NOT MATCHED BY TARGET THEN
    INSERT (Id, Name, Email, ReceptionEmail, Lat, Lng)
    VALUES (source.Id, source.Name, source.Email, source.ReceptionEmail, source.Lat, source.Lng);

/* ---------- Suppliers ---------- */
DECLARE @Suppliers TABLE (
    Id    UNIQUEIDENTIFIER,
    Name  NVARCHAR(200),
    Email NVARCHAR(320)
);

INSERT INTO @Suppliers (Id, Name, Email) VALUES
    ('2a2b2c2d-0002-4b00-9000-000000000001', N'Northwind Apparel Co.',  N'orders@northwind-apparel.example'),
    ('2a2b2c2d-0002-4b00-9000-000000000002', N'Fabrikam Textiles',      N'supply@fabrikam-textiles.example'),
    ('2a2b2c2d-0002-4b00-9000-000000000003', N'Contoso Outfitters',     N'replenishment@contoso-outfitters.example');

MERGE fulfillment.Suppliers AS target
USING @Suppliers AS source
    ON target.Name = source.Name
WHEN MATCHED THEN
    UPDATE SET target.Email = source.Email
WHEN NOT MATCHED BY TARGET THEN
    INSERT (Id, Name, Email)
    VALUES (source.Id, source.Name, source.Email);

/* ---------- ProductSuppliers (Sku -> SupplierId) ---------- */
/* SKUs mirror seed/catalog.json; supplier chosen by the product brand.
   Northwind -> supplier 1, Fabrikam -> supplier 2, Contoso -> supplier 3. */
DECLARE @ProductSuppliers TABLE (
    Sku        NVARCHAR(100),
    SupplierId UNIQUEIDENTIFIER
);

INSERT INTO @ProductSuppliers (Sku, SupplierId) VALUES
    -- Northwind (supplier 1)
    (N'TOP-TEE-WHT-M', '2a2b2c2d-0002-4b00-9000-000000000001'),
    (N'TOP-TEE-BLK-L', '2a2b2c2d-0002-4b00-9000-000000000001'),
    (N'TOP-POL-NVY-M', '2a2b2c2d-0002-4b00-9000-000000000001'),
    (N'BOT-CHN-KHK-34','2a2b2c2d-0002-4b00-9000-000000000001'),
    (N'BOT-JOG-BLK-L', '2a2b2c2d-0002-4b00-9000-000000000001'),
    (N'OUT-PRK-OLV-M', '2a2b2c2d-0002-4b00-9000-000000000001'),
    (N'FTW-RUN-WHT-42','2a2b2c2d-0002-4b00-9000-000000000001'),
    (N'FTW-HIK-GRY-42','2a2b2c2d-0002-4b00-9000-000000000001'),
    (N'FTW-TRN-GRY-43','2a2b2c2d-0002-4b00-9000-000000000001'),
    (N'TOP-FLN-GRN-L', '2a2b2c2d-0002-4b00-9000-000000000001'),
    -- Fabrikam (supplier 2)
    (N'TOP-OXF-BLU-M', '2a2b2c2d-0002-4b00-9000-000000000002'),
    (N'TOP-HEN-OLV-S', '2a2b2c2d-0002-4b00-9000-000000000002'),
    (N'BOT-TRS-CHR-32','2a2b2c2d-0002-4b00-9000-000000000002'),
    (N'OUT-BMB-BLK-M', '2a2b2c2d-0002-4b00-9000-000000000002'),
    (N'OUT-RAN-YEL-S', '2a2b2c2d-0002-4b00-9000-000000000002'),
    (N'FTW-CRT-BLK-43','2a2b2c2d-0002-4b00-9000-000000000002'),
    (N'FTW-SLP-NVY-41','2a2b2c2d-0002-4b00-9000-000000000002'),
    (N'BOT-JNS-BLK-34','2a2b2c2d-0002-4b00-9000-000000000002'),
    -- Contoso (supplier 3)
    (N'TOP-KNT-GRY-L', '2a2b2c2d-0002-4b00-9000-000000000003'),
    (N'BOT-JNS-IND-32','2a2b2c2d-0002-4b00-9000-000000000003'),
    (N'BOT-SHT-SND-M', '2a2b2c2d-0002-4b00-9000-000000000003'),
    (N'OUT-DNM-BLU-L', '2a2b2c2d-0002-4b00-9000-000000000003'),
    (N'OUT-BLZ-NVY-L', '2a2b2c2d-0002-4b00-9000-000000000003'),
    (N'FTW-CHL-BRN-44','2a2b2c2d-0002-4b00-9000-000000000003'),
    (N'TOP-TEE-RED-S', '2a2b2c2d-0002-4b00-9000-000000000003'),
    (N'OUT-VST-NVY-M', '2a2b2c2d-0002-4b00-9000-000000000003');

MERGE fulfillment.ProductSuppliers AS target
USING @ProductSuppliers AS source
    ON target.Sku = source.Sku
WHEN MATCHED THEN
    UPDATE SET target.SupplierId = source.SupplierId
WHEN NOT MATCHED BY TARGET THEN
    INSERT (Sku, SupplierId)
    VALUES (source.Sku, source.SupplierId);

COMMIT TRANSACTION;

PRINT 'Fulfillment seed applied (Stores, Suppliers, ProductSuppliers).';
