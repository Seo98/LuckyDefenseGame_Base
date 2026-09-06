using System;

/// <summary>
/// Enemy 처치 결과를 미네랄 보상으로 변환합니다.
/// </summary>
public sealed class EnemyRewardService : IDisposable
{
    private readonly EnemySpawner enemySpawner;
    private readonly MineralWallet mineralWallet;
    private bool isDisposed;

    /// <summary>
    /// 처치 보상이 지급된 직후 지급량과 현재 잔액을 전달합니다.
    /// </summary>
    public event Action<int, int> RewardGranted;

    /// <summary>
    /// EnemySpawner와 미네랄 지갑을 연결하고 제거 이벤트 구독을 시작합니다.
    /// </summary>
    public EnemyRewardService(EnemySpawner enemySpawner, MineralWallet mineralWallet)
    {
        this.enemySpawner = enemySpawner ??
            throw new ArgumentNullException(nameof(enemySpawner));
        this.mineralWallet = mineralWallet ??
            throw new ArgumentNullException(nameof(mineralWallet));

        this.enemySpawner.EnemyRemoved += HandleEnemyRemoved;
    }

    /// <summary>
    /// Enemy 제거 이벤트 구독을 해제합니다.
    /// </summary>
    public void Dispose()
    {
        if (isDisposed)
        {
            return;
        }

        enemySpawner.EnemyRemoved -= HandleEnemyRemoved;
        RewardGranted = null;
        isDisposed = true;
    }

    private void HandleEnemyRemoved(EnemyData enemyData, EnemyRemovalReason reason)
    {
        if (reason != EnemyRemovalReason.Killed || enemyData == null)
        {
            return;
        }

        int reward = enemyData.MineralReward;
        if (reward <= 0)
        {
            return;
        }

        mineralWallet.Add(reward);
        RewardGranted?.Invoke(reward, mineralWallet.Balance);
    }
}
