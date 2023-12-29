# Identity Provider - Spécifications techniques

Cette page décrit les spécifications techniques de l'Identity Provider.

1. [Accueil](README.md)
2. [Spécifications fonctionnelles](functional_spec.md)
3. [Spécifications techniques](technical_spec.md)
4. [Architecture](architecture.md)
5. [Database](database.md)

## Protocole

La solution choisi pour implémenter le protocole OpenID Connect est [Ory/Hydra](https://www.ory.sh/hydra). Comme décrit dans [l'introduction de leur documentation](https://www.ory.sh/hydra/docs/), Hydra implémente correctement le framework OAuth 2.0 et OpenID Connect Core 1.0.

## Référentiels utilisateurs

Les référentiels utilisateurs sont implémentés sous forme de [gRPC](https://grpc.io). Le prototype est disponible dans le fichier [users.proto](../protos/users.proto) :

```protobuf
syntax = "proto3";

package auth;

message AuthRequest {
    string Username = 1;
    string Password = 2;
}

message AuthResponse {
    bool Succeeded = 1;
    int32 Error = 2;
    string Subject = 3;
}

enum IdentifierType {
    SUBJECT = 0;
    USER_NAME = 1;
}

message ClaimsRequest {
    string Identifier = 1;
    IdentifierType IdentifierType = 2;
    repeated string Claims = 3;
}

message ClaimsResponse {
    bool Succeeded = 1;
    int32 Error = 2;
    map<string, string> Claims = 3;
}

message SearchRequest {
    string Search = 1;
    repeated string Claims = 2;
}

message SearchResponse {
    bool Succeeded = 1;
    int32 Error = 2;
    repeated SearchResponseResult Results = 3;
}

message SearchResponseResult {
    map<string, string> Properties = 1;
}

service User {
    rpc Authenticate (AuthRequest) returns (AuthResponse) {}
    rpc FindClaims (ClaimsRequest) returns (ClaimsResponse) {}
    rpc SearchClaims (SearchRequest) returns (SearchResponse) {}
}
```

Le service `User` fournit 3 méthodes :
- `Authenticate` : Elle permet d'authentifier un utilisateur avec le référentiel
- `FindClaims` : Elle permet de récupérer les claims d'un utilisateur depuis son identifiant unique (subject id) ou depuis son nom d'utilisateur
- `SearchClaims` : Elle permet de rechercher des utilsiateurs dans le référentiel et de récupérer leurs informations sous forme de claims

Les champs du message `AuthRequest` sont les suivants :

| Champ      | Description                                                      |
| ---------- | ---------------------------------------------------------------- |
| `Username` | Le nom d'utilisateur pour l'authentification.                    |
| `Password` | Le mot de passe pour l'authentification.                         |

Les champs du message `AuthResponse` sont les suivants :

| Champ       | Description                                                                                               |
| ----------- | --------------------------------------------------------------------------------------------------------- |
| `Succeeded` | Définit si l'authentication a réussie.                                                                    |
| `Error`     | Le code d'erreur si l'authentification a échouée. Voir l'implémentation du gRPC pour le détail des codes. |
| `Subject`   | L'identifiant unique de l'utilisateur si l'authentication a réussie.                                      |

Les champs du message `ClaimsRequest` sont les suivants :

| Champ            | Description                                                      |
| ---------------- | ---------------------------------------------------------------- |
| `Identifier`     | L'identifiant pour la recherche des claims.                      |
| `IdentifierType` | Le type d'identifiant pour la recherche des claims.              |
| `Claims`         | La liste des claims demandés.                                    |

Les types d'identifiants sont décris selon la structure `IdentifierType` :

| Valeur           | Description                                                                                                    |
| ---------------- | -------------------------------------------------------------------------------------------------------------- |
| `SUBJECT`        | L'identifiant unique de l'utilisateur dans le référentiel. Le champ dépend de l'implémentation du référentiel. |
| `USER_NAME`      | Le nom d'utilisateur de l'utlisateur.                                                                          |

Les champs du message `ClaimsResponse` sont les suivants :

| Champ       | Description                                                                                               |
| ----------- | --------------------------------------------------------------------------------------------------------- |
| `Succeeded` | Définit si la recherche a réussie.                                                                        |
| `Error`     | Le code d'erreur si la recherche a échouée. Voir l'implémentation du gRPC pour le détail des codes.       |
| `Claims`    | La liste des claims trouvés.                                                                              |

Les champs du message `SearchRequest` sont les suivants :

| Champ        | Description                                                                                    |
| ------------ | ---------------------------------------------------------------------------------------------- |
| `Search`     | Les paramètres de recherche. Leur interprétation est propre à l'implémentation du référentiel. |
| `Claims`     | La liste des claims demandés.                                                                  |

Les champs du message `SearchResponse` sont les suivants :

| Champ       | Description                                                                                               |
| ----------- | --------------------------------------------------------------------------------------------------------- |
| `Succeeded` | Définit si la recherche a réussie.                                                                        |
| `Error`     | Le code d'erreur si la recherche a échouée. Voir l'implémentation du gRPC pour le détail des codes.       |
| `Claims`    | La liste des claims trouvés.                                                                              |

Les champs du message `SearchResponseResult` sont les suivants :

| Champ        | Description                                              |
| ------------ | -------------------------------------------------------- |
| `Properties` | Un dictionaire clé-valeur ou la clé est le nom du claim. |

Les deux implémentations du service `User` sont disponibles ici :
- `grpc-ldap` : https://github.com/csbiti/auth-stores/grpc/ldap/README.md
- `grpc-accounts` : https://github.com/csbiti/auth-stores/grpc/accounts/README.md

Les gRPC ne fonctionnent qu'en HTTP2, par conséquent, la terminaison TLS au niveau du socket est obligatoire. Il est possible d'indiquer à l'IDP quels sont les certificats utilisés par les gRPC à l'aide des paramètres suivants :
- `grpc-ldap` : `Users:Clients:ldap:CertificatePath`
- `grpc-accounts` : `Users:Clients:accounts:CertificatePath`

> ⚠️ Les certificats sont attendus au format `pem`.

## Protection des données

La protection des données est assurée par l'API [Data Protection](https://docs.microsoft.com/en-us/aspnet/core/security/data-protection/configuration/overview?view=aspnetcore-6.0) d'ASP.NET Core. Elle permet entre autre de chiffrer les cookies d'authentification qui contiennent des [PII](https://en.wikipedia.org/wiki/Personal_data).

Pour garantir l'aspect stateless et la scalabilité de l'IDP, la clé de chiffrement est stockée sur un support partagé. Selon la valeur du paramètre `DataProtection:StorageMode`, la clé est stockée différement :
- `FileSystem` : La clé est stockée sur le système de fichier, à l'emplacement renseigné par `DataProtection:StoragePath`.
- `DbContext` : La clé est  [stockée en base de données](database.md#data_protection).

Que le mode de stockage soit `FileSystem` ou `DbContext`, la clé est stockée de façon chiffrée à l'aide d'un certificat X509. Le certificat est renseigné avec les paramètres suivant :

| Paramètre                            | Description                                      |
| ------------------------------------ | ------------------------------------------------ |
| `Dataprotection:CertificatePath`     | Le chemin vers le certificat. |
| `Dataprotection:CertificatePassword` | Le mot de passe du certificat.                   |

> ⚠️ Les certificats sont attendus au format `pkcs12`.