using UnityEngine;
using UnityEngine.Tilemaps;
using Jagara.Runtime.Combat;
using Jagara.Runtime.Data;
using Jagara.Runtime.DungeonGen;
using Jagara.Runtime.Gameplay;
using Jagara.Runtime.Narrative;
using Jagara.Runtime.Resources;
using Jagara.Runtime.TurnSystem;

namespace Jagara.Runtime.Enemies
{
    /// <summary>
    /// Drives a single enemy's turn-based AI: wake/forget state, then either
    /// waiting (Dormant), bump-attacking the player (adjacent to it), or a
    /// single chase step via EnemyAILogic/EnemyAIContext. Mirrors
    /// PlayerController's GridMover wiring pattern, but acts automatically as
    /// an ITurnActor instead of reading input.
    /// </summary>
    [RequireComponent(typeof(GridMover))]
    public class EnemyController : MonoBehaviour, ITurnActor
    {
        [Header("Log messages")]
        [SerializeField] private MessageLogSO messageLog;
        [SerializeField] private MessageTemplateSO attackMessage;

        private GridMover mover;
        private SpriteRenderer spriteRenderer;
        private GridVisualAnimator visualAnimator;

        private EnemyConfigSO config;
        private EnemyAIContext context;
        private TurnResolver resolver;
        private HealthState health;

        private EnemyAIState state = EnemyAIState.Dormant;

        /// <summary>This enemy instance's runtime HP, seeded from config.Vigor at Initialize.</summary>
        public HealthState Health => health;

        /// <summary>The enemy type's display name, for attacker-side messaging (e.g. PlayerController's bump-attack log lines).</summary>
        public string DisplayName => config.DisplayName;

        // True from the moment this component calls BeginActorAnimation()
        // until the paired OnMoveCompleted (EndActorAnimation) fires. Lets
        // OnDisable release a pending animation if the object is
        // destroyed/disabled mid-tween, so TurnResolver.IsResolving never
        // gets stuck true.
        private bool animationPending;

        private void Awake()
        {
            mover = GetComponent<GridMover>();
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            visualAnimator = GetComponentInChildren<GridVisualAnimator>();

            if (spriteRenderer == null)
            {
                Debug.LogError($"EnemyController on {name}: no SpriteRenderer found in children; enemy will render without a sprite.");
            }
        }

        private void OnEnable()
        {
            mover.OnMoveCompleted += HandleMoveCompleted;
        }

        private void OnDisable()
        {
            mover.OnMoveCompleted -= HandleMoveCompleted;

            if (resolver == null)
            {
                // Initialize was never called (e.g. disabled before wiring completed).
                return;
            }

            if (animationPending)
            {
                resolver.EndActorAnimation();
                animationPending = false;
            }

            resolver.UnregisterActor(this);
        }

        /// <summary>
        /// Applies config to the visual/mover, occupies startCell via
        /// GridMover.Initialize, and registers this enemy with the turn
        /// resolver. Mirrors PlayerController.Initialize's wiring shape.
        /// </summary>
        public void Initialize(EnemyConfigSO config, FloorData floor, Tilemap tilemap, Vector2Int startCell, TurnResolver resolver, EnemyAIContext context)
        {
            this.config = config;
            this.context = context;
            this.resolver = resolver;
            state = EnemyAIState.Dormant;
            health = new HealthState(StatFormulas.ComputeMaxHP(config.Vigor));

            if (spriteRenderer != null)
            {
                spriteRenderer.sprite = config.Sprite;
                visualAnimator?.RefreshSpriteMetrics();
            }

            mover.MoveDuration = config.MoveDuration;
            mover.Initialize(floor, tilemap, startCell, context.Occupancy);

            resolver.RegisterActor(this);
        }

        public void TakeTurn()
        {
            Vector2Int enemyCell = mover.CurrentCell;
            Vector2Int playerCell = context.PlayerCell;

            // An enemy is in exactly one state at the start of its turn, so
            // only the transition matching its current state can fire.
            if (state == EnemyAIState.Dormant)
            {
                if (EnemyAILogic.ShouldWake(enemyCell, playerCell, context.Rooms, config.DetectionRange))
                {
                    state = EnemyAIState.Chasing;
                }
            }
            else if (EnemyAILogic.ShouldForget(enemyCell, playerCell, config.ForgetRange))
            {
                state = EnemyAIState.Dormant;
            }

            if (state == EnemyAIState.Dormant)
            {
                return;
            }

            // Adjacent to the player: bump-attack instead of moving.
            // Deliberately does not touch context.Distances - an adjacent
            // enemy never needs pathfinding this turn.
            if (EnemyAILogic.IsAdjacent(enemyCell, playerCell))
            {
                PerformAttack();
                return;
            }

            bool hasStep = EnemyAILogic.TryChooseStep(context.Distances, context.Occupancy, context.Terrain, enemyCell, out Vector2Int step);
            if (hasStep)
            {
                resolver.BeginActorAnimation();
                animationPending = true;
                mover.StepTo(step);
            }
            // else: blocked on all sides - wait, without touching the
            // animation counter at all.
        }

        private void HandleMoveCompleted()
        {
            if (!animationPending)
            {
                return;
            }

            resolver.EndActorAnimation();
            animationPending = false;
        }

        /// <summary>
        /// Resolves a basic bump-attack against the player's HealthState. The
        /// player's own death handling (message, input freeze) is owned by
        /// PlayerController via its HealthState.OnDeath subscription, not here.
        /// </summary>
        private void PerformAttack()
        {
            CombatResolver.AttackResult result = CombatResolver.ResolveBumpAttack(config.Poder, context.PlayerStats.Health);

            if (messageLog != null && attackMessage != null)
            {
                messageLog.Post(attackMessage, StyledName.Enemy(config.DisplayName), result.Damage);
            }
        }

        /// <summary>
        /// Called by an attacker (e.g. PlayerController after a killing bump-attack)
        /// once this enemy's HP reaches 0. Unregisters from the turn resolver and
        /// destroys the GameObject - GridMover.OnDestroy already vacates occupancy.
        /// </summary>
        public void Die()
        {
            resolver.UnregisterActor(this);
            Destroy(gameObject);
        }
    }
}
