namespace Aster.Core;
/// <summary>Let the compact island be seen after launch before applying normal foreground auto-stow.</summary>
public sealed class StartupReveal(IClock clock)
{
    private readonly DateTimeOffset until=clock.UtcNow+TimeSpan.FromSeconds(5);
    public bool AllowStow(bool requested)=>requested&&clock.UtcNow>=until;
}
