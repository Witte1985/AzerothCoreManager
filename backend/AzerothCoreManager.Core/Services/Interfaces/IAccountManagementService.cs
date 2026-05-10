using AzerothCoreManager.Core.Contracts;

namespace AzerothCoreManager.Core.Services.Interfaces;

/// <summary>
/// Service for managing AzerothCore accounts and characters
/// </summary>
public interface IAccountManagementService
{
    // Account queries (MySQL)
    Task<List<AccountDto>> GetAccountsAsync(string stackId, CancellationToken cancellationToken = default);
    Task<List<CharacterDto>> GetCharactersAsync(string stackId, int accountId, CancellationToken cancellationToken = default);
    Task<List<CharacterDto>> GetAllCharactersAsync(string stackId, CancellationToken cancellationToken = default);

    // AH Bot setup (direct DB injection)
    Task<AhBotSetupResultDto> CreateAhBotCharactersAsync(string stackId, CancellationToken cancellationToken = default);
    
    // Account actions (SOAP)
    Task<bool> CreateAccountAsync(string stackId, string username, string password, CancellationToken cancellationToken = default);
    Task<bool> DeleteAccountAsync(string stackId, string username, CancellationToken cancellationToken = default);
    Task<bool> SetPasswordAsync(string stackId, string username, string password, CancellationToken cancellationToken = default);
    Task<bool> SetGmLevelAsync(string stackId, string username, int level, int realmId = -1, CancellationToken cancellationToken = default);
    Task<bool> BanAccountAsync(string stackId, string username, string duration, string reason, CancellationToken cancellationToken = default);
    Task<bool> UnbanAccountAsync(string stackId, string username, CancellationToken cancellationToken = default);
    Task<bool> BanIpAsync(string stackId, string ip, string duration, string reason, CancellationToken cancellationToken = default);
    Task<bool> UnbanIpAsync(string stackId, string ip, CancellationToken cancellationToken = default);
    
    // Character actions (SOAP)
    Task<bool> SendMessageAsync(string stackId, string characterName, string message, CancellationToken cancellationToken = default);
    Task<bool> SendItemsAsync(string stackId, string characterName, int itemId, int count, CancellationToken cancellationToken = default);
    Task<bool> SendMoneyAsync(string stackId, string characterName, long copperAmount, CancellationToken cancellationToken = default);
    Task<bool> KickPlayerAsync(string stackId, string characterName, string reason = "", CancellationToken cancellationToken = default);
    Task<bool> RenameCharacterAsync(string stackId, string characterName, CancellationToken cancellationToken = default);
    Task<bool> CustomizeCharacterAsync(string stackId, string characterName, CancellationToken cancellationToken = default);
    Task<bool> SetCharacterLevelAsync(string stackId, string characterName, int level, CancellationToken cancellationToken = default);
}
