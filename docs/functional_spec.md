# Identity Provider - Spécifications fonctionnelles

Cette page décrit les spécifications fonctionnelles de l'Identity Provider.

1. [Accueil](README.md)
2. [Spécifications fonctionnelles](functional_spec.md)
3. [Spécifications techniques](technical_spec.md)
4. [Architecture](architecture.md)
5. [Database](database.md)

## Protocole

Le protocole choisi pour implémenter l'Identity Provider est OpenID Connect. C'est une surcouche du framework OAuth 2.0.

Tandis qu'OAuth 2.0 permet de gérer l'accès à des ressources protégées (API, informations utilisateur), OpenID Connect permet de gérer l'identification des utilisateurs.

Pour vous documenter sur ces protocoles :
- OAuth 2.0 :
  - https://auth0.com/docs/authorization/protocols/protocol-oauth2
- OpenID Connect :
  - https://auth0.com/docs/authorization/protocols/openid-connect-protocol
  - https://openid.net/connect

## Référentiels utilisateurs

Dans le fonctionnement de l'Identity Provider, le rôle des référentiels utilisateurs est des permettre l'accès aux informations des utilisateurs, dans un référentiel particulier, afin de :
- Valider les informations de connexion d'un utilisateur
- Récupérer les informations d'un utilisateur sous forme de claims
- Rechercher des utilisateurs dans le référentiel et récupérer leurs informations sous forme de claims

> ℹ️ Les claims sont définis par le standard [JWT (JSON Web Token)](https://www.iana.org/assignments/jwt/jwt.xhtml).

Il y deux référentiels utilisateurs :
- ldap
- accounts

Le référentiel `ldap` se base sur les informations des personnels de la CSB, stockées dans l'annuaire LDAP.
> ⚠️ Actuellement, ce référentiel est implémenté complétement dans le repo Git suivant : https://github.com/csbiti/auth-stores

Le référentiel `accounts` se base sur les informations des clients CSB, stockés dans le système de gestion de compte unique pour les clients de la CSB.
> ⚠️ Actuellement, ce référentiel n'est pas implémenté car ses spécifications sont encore incomplètes. Cependant, un implémentation subsidière utilisant un fichier plat est disponible dans le repo Git suivant : https://github.com/csbiti/auth-stores

