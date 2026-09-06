using System;
using R3;

/// <summary>
/// 전투 중 미네랄의 지급, 소비 및 초기화를 관리합니다.
/// </summary>
public sealed class MineralWallet : IDisposable
{
    /// <summary>
    /// R3의 ReactiveProperty를 사용해 현재 미네랄을 저장하고 변경을 발행합니다.
    /// </summary>
    private readonly ReactiveProperty<int> balance;

    private readonly int startingBalance;
    private bool isDisposed;

    /// <summary>
    /// 현재 보유한 미네랄입니다.
    /// </summary>
    public int Balance => balance.Value;

    /// <summary>
    /// R3의 Observable을 사용해 미네랄 변경을 발행합니다.
    /// </summary>
    public Observable<int> BalanceChanged => balance;

    /// <summary>
    /// BattleRulesData에 설정된 시작 미네랄로 지갑을 생성합니다.
    /// </summary>
    public MineralWallet(BattleRulesData battleRules)
    {
        if (battleRules == null)
        {
            throw new ArgumentNullException(nameof(battleRules));
        }

        startingBalance = Math.Max(0, battleRules.StartingMinerals);
        balance = new ReactiveProperty<int>(startingBalance);
    }

    /// <summary>
    /// 지정한 미네랄을 보유하고 있는지 확인합니다.
    /// </summary>
    public bool CanSpend(int amount)
    {
        ThrowIfDisposed();
        return amount >= 0 && Balance >= amount;
    }

    /// <summary>
    /// 잔액이 충분하면 미네랄을 소비하고 true를 반환합니다.
    /// </summary>
    public bool TrySpend(int amount)
    {
        ThrowIfDisposed();

        if (!CanSpend(amount))
        {
            return false;
        }

        balance.Value -= amount;
        return true;
    }

    /// <summary>
    /// 양수 미네랄을 지급합니다.
    /// </summary>
    public void Add(int amount)
    {
        ThrowIfDisposed();

        if (amount <= 0)
        {
            return;
        }

        long result = (long)Balance + amount;
        balance.Value = result >= int.MaxValue ? int.MaxValue : (int)result;
    }

    /// <summary>
    /// 잔액을 BattleRulesData의 시작 미네랄로 초기화합니다.
    /// </summary>
    public void Reset()
    {
        ThrowIfDisposed();
        balance.Value = startingBalance;
    }

    /// <summary>
    /// R3 상태 스트림을 정리합니다.
    /// </summary>
    public void Dispose()
    {
        if (isDisposed)
        {
            return;
        }

        balance.Dispose();
        isDisposed = true;
    }

    private void ThrowIfDisposed()
    {
        if (isDisposed)
        {
            throw new ObjectDisposedException(nameof(MineralWallet));
        }
    }
}
