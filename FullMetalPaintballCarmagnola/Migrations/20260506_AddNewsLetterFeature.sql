INSERT INTO "RolePermissions" ("RoleName", "FeatureName", "IsAllowed")
SELECT 'Admin', 'NewsLetter', TRUE
WHERE NOT EXISTS (
    SELECT 1
    FROM "RolePermissions"
    WHERE "RoleName" = 'Admin'
      AND "FeatureName" = 'NewsLetter'
);

