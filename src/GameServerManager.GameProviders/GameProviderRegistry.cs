namespace GameServerManager.GameProviders;

public class GameProviderRegistry
{
    private readonly Dictionary<string, IGameServerProvider> _providers;

    public GameProviderRegistry(IEnumerable<IGameServerProvider> providers)
    {
        _providers = providers.ToDictionary(
            provider => provider.GameId,
            provider => provider,
            StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyCollection<IGameServerProvider> Providers => _providers.Values.ToArray();

    public static GameProviderRegistry CreateDefault()
    {
        return new GameProviderRegistry(new IGameServerProvider[]
        {
            new ArkSurvivalAscendedProvider(),
            new ArkSurvivalAscendedLegacyProvider(),
            new ArkSurvivalEvolvedProvider(),
            new MinecraftJavaProvider(),
            new MinecraftBedrockProvider(),
            new SevenDaysToDieProvider(),
            new PalworldProvider(),
            new RustProvider(),
            new ValheimProvider(),
            new ConanExilesProvider(),
            new ProjectZomboidProvider(),
            new SatisfactoryProvider(),
            new FactorioProvider(),
            new GenericServerProvider()
        });
    }

    public bool TryGetProvider(string gameId, out IGameServerProvider provider)
    {
        return _providers.TryGetValue(gameId, out provider!);
    }

    public IGameServerProvider GetProvider(string gameId)
    {
        if (TryGetProvider(gameId, out var provider))
        {
            return provider;
        }

        throw new KeyNotFoundException($"No game server provider is registered for game ID '{gameId}'.");
    }
}
