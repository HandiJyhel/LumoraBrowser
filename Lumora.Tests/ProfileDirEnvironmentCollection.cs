using Xunit;

namespace Lumora.Tests;

// LUMORA_PROFILE_DIR (Environment.SetEnvironmentVariable) est global au process : deux
// tests qui le modifient en même temps, dans des classes différentes exécutées en
// parallèle par xUnit (comportement par défaut), se marchent dessus et échouent de
// façon intermittente selon l'ordonnancement. Bug réel constaté le 2026-08-19
// (dotnet test : 2 échecs sur 821, tous deux liés à cette variable, disparaissant en
// lançant chaque test isolément). Ce marqueur force les classes qui la manipulent à
// tourner en séquence entre elles, sans ralentir le reste de la suite (elles restent
// parallèles à toutes les autres classes de tests).
public sealed class ProfileDirEnvironmentFixture
{
}

[CollectionDefinition(Name)]
public sealed class ProfileDirEnvironmentCollection : ICollectionFixture<ProfileDirEnvironmentFixture>
{
    public const string Name = "LUMORA_PROFILE_DIR (sequentiel)";
}
