using UnityEngine;
using VContainer;
using VContainer.Unity;

/// <summary>
/// VContainer를 사용해 한 판에 필요한 적 스폰 서비스를 조립합니다.
/// </summary>
public sealed class BattleRoundScope : LifetimeScope
{
    [SerializeField] private BattleRulesData battleRules;
    [SerializeField] private LoopPath loopPath;
    [SerializeField] private Transform enemyPoolRoot;
    [SerializeField] private BattleGameController battleController;

    /// <summary>
    /// VContainer에 데이터, 씬 컴포넌트와 런타임 서비스를 등록합니다.
    /// </summary>
    protected override void Configure(IContainerBuilder builder)
    {
        // 이미 존재하는 SO를 전달합니다.
        builder.RegisterInstance(battleRules);

        // 씬에 배치된 컴포넌트를 연결합니다.
        builder.RegisterComponent(loopPath);

        // 한 Scope 안에서는 같은 인스턴스를 사용합니다.
        builder.Register<EnemyPopulationTracker>(Lifetime.Scoped);

        // 어떤 부모 Transform을 사용할지는 조립 장소에서 정합니다.
        builder.Register<EnemyPoolService>(
            _ => new EnemyPoolService(enemyPoolRoot),
            Lifetime.Scoped);

        // 생성자에 필요한 위의 세 의존성을 자동으로 전달합니다.
        builder.Register<EnemySpawner>(Lifetime.Scoped);

        // 컨트롤러의 [Inject] 메서드에 서비스를 전달합니다.
        builder.RegisterComponent(battleController);
    }
}