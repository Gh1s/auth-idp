# Identity Provider - Database

Cette page décrit la base de données de l'Identity Provider.

1. [Accueil](README.md)
2. [Spécifications fonctionnelles](functional_spec.md)
3. [Spécifications techniques](technical_spec.md)
4. [Architecture](architecture.md)
5. [Database](database.md)

## Base données

Le serveur de base de données est dédié à l'IDP et la base de données s'appelle `idp`.

## Schémas

Il y a 2 schémas dans la base `idp` :
- `data_protection`
- `hydra`

### `data_protection`

Le schéma `data_protection` stocke les clés de protection pour l'API [Data Protection](https://docs.microsoft.com/en-us/aspnet/core/security/data-protection/configuration/overview?view=aspnetcore-6.0) d'ASP.NET Core.

La structure du schéma est maintenue avec des migrations. Elles sont générées automatiquement avec le contexte de données [DataProtectionKeyContext](../src/Csb.Auth.Idp/DataProtectionKeyContext.cs) qui implémente [IDataProtectionKeyContext](https://docs.microsoft.com/en-us/aspnet/core/security/data-protection/configuration/overview?view=aspnetcore-6.0#persistkeystodbcontext). C'est le service [DataProtectionKeyContextMigrationService](../src/Csb.Auth.Idp/DataProtectionKeyContextMigrationService.cs) qui s'occupe de les exécuter au démarrage de l'IDP.

### `hydra`

Le schéma `hydra` stocke les données de configuration d'Hydra.

La structure du schéma est maintenue par le job `hydra-migrate`. Il s'exécute avec l'image Docker fournie directement par [Ory](https://www.ory.sh/hydra/docs/configure-deploy/).