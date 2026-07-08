# Cle de recuperation locale (0.40.0-dev)

Cette passe ajoute la recuperation locale du profil et du coffre dans `PulseBrowser.WinUI`.

## Principe

Pulse Browser ne stocke pas le mot de passe en clair et ne peut pas deviner un mot de passe oublie. La recuperation repose donc sur une cle locale creee a l'avance :

- une cle `PULSE-...` est generee a la creation du profil ;
- l'utilisateur doit la noter ou la copier ;
- la cle peut etre regeneree depuis `Parametres > Profil` ;
- l'ecran de connexion expose `Mot de passe oublie ?` ;
- avec la cle, l'utilisateur definit un nouveau mot de passe.

Sans mot de passe et sans cle de recuperation, les donnees chiffrees restent illisibles.

## Coffre

`VaultStore` maintient une copie de recuperation chiffree du coffre. Elle est protegee par une cle de donnees de secours, elle-meme emballee par la cle de recuperation utilisateur.

Quand le coffre est ouvert, cette copie est tenue a jour sans stocker la cle de recuperation en clair. Lors d'une recuperation, la cle `PULSE-...` ouvre la copie de secours, puis le coffre est rechiffre avec le nouveau mot de passe.

## Profil

`UserProfile` conserve uniquement un hash PBKDF2-SHA256 de la cle de recuperation, avec sel aleatoire. La cle affichee n'est pas persistée en clair.

## Limites

- Les anciens profils doivent creer une cle depuis `Parametres > Profil` avant de pouvoir recuperer un mot de passe oublie.
- Le changement de mot de passe par recuperation desactive le PIN, car le PIN doit ensuite etre recree proprement.
- Cette passe ne traite pas encore le multi-utilisateur ni les moyens de paiement.
