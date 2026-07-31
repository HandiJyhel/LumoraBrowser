# Exception additionnelle à la GNU General Public License v3.0

Cette exception est accordée par les détenteurs des droits d'auteur de
Lumora, en vertu de la Section 7 de la GNU General Public License
version 3 ("Termes additionnels"). Elle complète le fichier `LICENSE`
(texte intégral et non modifié de la GPLv3) sans en altérer les termes.

## Contexte

Lumora dépend, pour fonctionner sous Windows, de composants système
propriétaires non couverts par une licence libre :

- Le runtime et le SDK **Microsoft Edge WebView2** (moteur de rendu web
  intégré), distribués par Microsoft sous leurs propres conditions de
  licence.
- Le **Windows App SDK** et ses composants runtime associés, dans leurs
  parties non couvertes par une licence libre (le SDK de développement
  est sous licence MIT, mais son exécution repose sur des composants
  runtime Windows propriétaires).

Sans aménagement explicite, la portée copyleft de la GPLv3 pourrait
laisser planer un doute sur la possibilité de distribuer Lumora lié à
ces composants. Cette exception lève ce doute.

## Permission accordée

En plus des droits déjà accordés par la GPLv3, vous êtes autorisé(e) à :

1. lier ou combiner Lumora (ou toute œuvre dérivée couverte par cette
   licence) avec le runtime et le SDK Microsoft Edge WebView2, le
   Windows App SDK et ses composants runtime associés ;
2. distribuer l'œuvre combinée résultante, y compris sous forme binaire
   embarquant ces composants propriétaires,

sans que cela ne soumette ces composants propriétaires eux-mêmes aux
termes de la GPLv3, et sans que cela ne soit considéré comme une
violation de la Section 7 de la GPLv3.

## Portée de l'exception

Cette permission ne s'étend qu'aux composants système cités ci-dessus,
strictement nécessaires au fonctionnement de Lumora sous Windows. Elle
ne s'applique à aucune autre bibliothèque ou dépendance tierce intégrée
au projet, qui reste soumise aux termes normaux de la GPLv3 (ou à sa
propre licence compatible, le cas échéant).

Cette exception n'accorde aucun droit sur les composants propriétaires
de Microsoft eux-mêmes : leur utilisation reste régie par les
conditions de licence propres à Microsoft (Microsoft Software License
Terms applicables au WebView2 Runtime/SDK et au Windows App SDK).
