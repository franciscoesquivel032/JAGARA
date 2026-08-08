using UnityEngine;
using UnityEngine.Tilemaps;
using Jagara.Runtime.Combat;
using Jagara.Runtime.Data;
using Jagara.Runtime.DungeonGen;
using Jagara.Runtime.Gameplay;
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
        private GridMover mover;
        private SpriteRenderer spriteRenderer;
        private GridVisualAnimator visualAnimator;

        private EnemyConfigSO config;
        private EnemyAIContext context;
        private TurnResolver resolver;
        private HealthState health;

        private EnemyAIState state = EnemyAIState.Dormant;

        /// <summary>This enemy instance's runtime HP, seeded from config.BaseMaxHP at Initialize.</summary>
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
            mover.OnMoveCompleted += HandleAnimationCompleted;
        }

        private void OnDisable()
        {
            mover.OnMoveCompleted -= HandleAnimationCompleted;

            if (health != null)
            {
                health.OnHPChanged -= HandleHPChanged;
            }

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
            health = new HealthState(config.BaseMaxHP);
            health.OnHPChanged += HandleHPChanged;

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
                PerformAttack(playerCell);
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

        private void HandleAnimationCompleted()
        {
            if (!animationPending)
            {
                return;
            }

            resolver.EndActorAnimation();
            animationPending = false;
        }

        /// <summary>
        /// Resolves a basic bump-attack against the player's HealthState and
        /// records it against PlayerStatsSO's turn accumulator rather than
        /// posting a message directly - multiple enemies can be adjacent and
        /// attack in the same player turn, and PlayerController posts one
        /// combined message once the turn is fully resolved (see
        /// PlayerController.HandleTurnEnded). The player's own death handling
        /// (message, input freeze) is owned by PlayerController via its
        /// HealthState.OnDeath subscription, not here. Also kicks off this
        /// enemy's attack-lunge animation (if a GridVisualAnimator is present),
        /// gating turn resolution on it exactly like a move step does via
        /// BeginActorAnimation/EndActorAnimation - if no visual animator is
        /// present (as in EnemyControllerTests), no gating happens at all and
        /// TakeTurn returns exactly as it does today.
        /// </summary>
        private void PerformAttack(Vector2Int playerCell)
        {
            CombatResolver.AttackResult result = CombatResolver.ResolveBumpAttack(config.BaseAttackDamage, context.PlayerStats.Health);
            context.PlayerStats.RecordIncomingAttack(config.DisplayName, result.Damage);

            if (visualAnimator != null)
            {
                Vector2Int direction = playerCell - mover.CurrentCell;
                resolver.BeginActorAnimation();
                animationPending = true;
                visualAnimator.PlayAttack(direction, HandleAnimationCompleted);
            }
        }

        /// <summary>
        /// Plays this enemy's hit-reaction flash+shake whenever the player
        /// damages it (HealthState.OnHPChanged only fires on a genuine hit -
        /// see HealthState.TakeDamage). Purely cosmetic. On a killing blow,
        /// this coroutine starts but is immediately cut short by Die()
        /// destroying the GameObject in the same frame - a known, accepted
        /// gap (no death animation; see the design spec).
        /// </summary>
        private void HandleHPChanged(int current, int max)
        {
            visualAnimator?.PlayHitReaction();
        }

        /// <summary>
        /// Called by an attacker (e.g. PlayerController after a killing bump-attack)
        /// once this enemy's HP reaches 0. Unregisters from the turn resolver and
        /// frees its tile immediately - a dying enemy must stop acting and stop
        /// blocking movement/attacks right away, exactly as before - but the
        /// GameObject's destruction is now deferred until its death-blink animation
        /// finishes (if a GridVisualAnimator is present), instead of destroying it
        /// the same frame. Falls back to immediate Destroy when no visual animator
        /// is present, matching every other animation fallback in this class.
        /// </summary>
        public void Die()
        {
            resolver.UnregisterActor(this);
            mover.ReleaseCell();

            if (visualAnimator != null)
            {
                visualAnimator.PlayDeath(() => Destroy(gameObject));
            }
            else
            {
                Destroy(gameObject);
            }
        }
    }
}
