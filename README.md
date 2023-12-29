# Identity Provider

Ce projet contient l'Identity Provider de la CSB.
Il utilise [Ory/Hydra](https://www.ory.sh/hydra) pour implémenter le protocole OpenID Connect.

## 📁 Structure du projet

### `/docker`

Les fichiers de configuration pour les services dans Docker.

| Dossier                             | Description                                                                                                    |
| ----------------------------------- | -------------------------------------------------------------------------------------------------------------- |
| `/docker/certs`                     | Les certificats utilisés pour les terminaison TLS et le chiffrement des données.                               |
| `/docker/certs/data-protection.*`   | Les fichiers du certificat X509 pour le chiffrement des données de l'IDP.                                      |
| `/docker/certs/tls-grpc-accounts.*` | Les fichiers du certificat X509 pour la terminaison TLS du gRPC `accounts` sur l'environnement `development`.  |
| `/docker/certs/tls-grpc-ldap.*`     | Les fichiers du certificat X509 pour la terminaison TLS du gRPC `ldap` sur l'environnement `development`.      |
| `/docker/certs/tls-localhost.*`     | Les fichiers du certificat X509 pour la terminaison TLS des services qui répondent sur `localhost`.            |
| `/docker/grpc`                      | Les fichiers de configuration pour les gRPCs des user stores.                                                  |
| `/docker/hydra`                     | Les fichiers de configuration d'Hydra.                                                                         |
| `/docker/idp`                       | Les fichiers de configuration de l'IDP.                                                                        |
| `/docker/nginx`                     | Les fichiers de configuration du reverse proxy.                                                                |
| `/docker/samples`                   | Les fichiers de configuration des exemples.                                                                    |

Le fichier `docker-compose-build.yaml` permet générer les images Docker de la stack d'authentification.<br />
Le fichier `docker-compose-dev.yaml` permet de lancer les services requis pour l'environnement de développement et le tests.<br />
Le fichier `build.sh` permet de générer les images Docker pour tous les services de stack d'authentification.

### `/protos`

Les fichiers [protobuf](https://developers.google.com/protocol-buffers).

### `/src`

Le code source.

### `/test`

Les tests unitaires, d'intégration et systèmes.

### `/samples`

Les exemples d'intégration de l'IDP.

## 🧰 Tooling

### .NET

Le code source du projet est écris en [.NET 5](https://dot.net).

Pour le compiler il vous faudra le [SDK .NET 5](https://dotnet.microsoft.com/download).

### IDE

Comme IDE vous pouvez utiliser [Visual Studio](https://visualstudio.microsoft.com/en/downloads) ou [Rider](https://www.jetbrains.com/rider).

## 🔑 Keymaterials

### TLS

Les terminaisons TLS des services sont assurées par des certificats x509.

> ℹ️ Le mot de passes des certificats X509 de développement au format PKCS12 est `Pass@word1`.

#### Génération

Pour générer la clé privée :

```bash
openssl genrsa -out $key_file_name 4096
```

Pour générer le certificat : 

```bash
openssl req -new -x509 -sha256 -key $key_file_name -out $cert_file_name -days 9999 -subj "/CN=$host_name/C=NC/L=Nouméa/OU=APP-DEV" -addext "subjectAltName = DNS:$host_name"
```

#### Installation

Pour importer un certificat dans le store « Autorités de certification racines de confiance », depuis un invite de commande `Powershell` en mode admin :

```powershell
Import-Certificate -FilePath $cert_file_name -CertStoreLocation cert:\CurrentUser\Root
```

## 👨‍🍳 Samples

Les exemples permettent d'illustrer l'intégration de l'IDP avec différents [grant types](https://oauth.net/2/grant-types).

| Grant type           | Projet                                                                                 |
| -------------------- | -------------------------------------------------------------------------------------- |
| `implicit`           | TODO: 🚧 Add MVC implicit grant type sample.                                          |
| `implicit`           | TODO: 🚧 Add Angular implicit grant type sample.                                      |
| `authorization_code` | [Csb.Auth.Samples.AuthorizationCodeMvc](samples/Csb.Auth.Samples.AuthorizationCodeMvc) |
| `authorization_code` | TODO: 🚧 Add Angular authorization_code flow sample.                                  |