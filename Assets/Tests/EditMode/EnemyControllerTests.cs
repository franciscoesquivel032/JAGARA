using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;
using Jagara.Runtime.Data;
using Jagara.Runtime.DungeonGen;
using Jagara.Runtime.Enemies;
using Jagara.Runtime.Gameplay;
using Jagara.Runtime.TurnSystem;

namespace Jagara.Tests.EditMode
{
    /// <summary>
    /// Covers the assembled wake -> chase -> forget -> adjacency -> step state
    /// machine inside EnemyController.TakeTurn() itself. Every ingredient
    /// (EnemyAILogic.ShouldWake/ShouldForget/IsAdjacent/TryChooseStep,
    /// EnemyAIContext's lazy BFS) already has isolated coverage
    /// (EnemyAILogicTests, EnemyAIContextTests) - these tests exist to catch
    /// bugs in how TakeTurn() wires those ingredients together, using real
    /// TurnResolver/OccupancyGrid/DistanceField/EnemyAIContext/EnemyConfigSO
    /// instances (no mocks), following the pattern EnemyAIContextTests
    /// established for MonoBehaviour-adjacent logic in Edit Mode.
    ///
    /// EnemyConfigSO.moveDuration is forced to 0 via SerializedObject so
    /// GridMover's tween coroutine (see GridMover.MoveRoutine) completes
    /// synchronously inside StepTo instead of yielding across frames - this
    /// lets a single test call TakeTurn() more than once without needing to
    /// pump Edit Mode Update frames, and without racing GridMover.StepTo's
    /// "already moving" guard.
    /// </summary>
    public class EnemyControllerTests
    {
        private const int Width = 20;
        private const int Height = 1;

        private GameObject tilemapGO;
        private Tilemap tilemap;
        private FloorData floor;
        private OccupancyGrid occupancy;
        private TurnResolver resolver;

        private GameObject playerGO;
        private GridMover playerMover;

        private GameObject enemyGO;
        private GridMover enemyMover;
        private EnemyController controller;

        private EnemyConfigSO config;
        private EnemyAIContext context;
        private PlayerStatsSO playerStats;

        [SetUp]
        public void SetUp()
        {
            // 20x1 open corridor - wide enough for every distance used below.
            var grid = new TileType[Width, Height];
            for (int x = 0; x < Width; x++)
            {
                grid[x, 0] = TileType.Floor;
            }

            floor = new FloorData(grid, new List<RoomData>());
            occupancy = new OccupancyGrid(Width, Height);
            resolver = new TurnResolver();

            tilemapGO = new GameObject("Tilemap");
            tilemap = tilemapGO.AddComponent<Tilemap>();
        }

        [TearDown]
        public void TearDown()
        {
            if (enemyGO != null)
            {
                Object.DestroyImmediate(enemyGO);
            }

            if (playerGO != null)
            {
                Object.DestroyImmediate(playerGO);
            }

            if (config != null)
            {
                Object.DestroyImmediate(config);
            }

            if (playerStats != null)
            {
                Object.DestroyImmediate(playerStats);
            }

            Object.DestroyImmediate(tilemapGO);
        }

        private void SpawnPlayer(Vector2Int cell)
        {
            playerGO = new GameObject("Player");
            playerMover = playerGO.AddComponent<GridMover>();
            playerMover.Initialize(floor, tilemap, cell, occupancy);
        }

        private void SpawnEnemy(Vector2Int cell, int detectionRange, int forgetRange, int baseMaxHP = 30, int baseAttackDamage = 3)
        {
            config = ScriptableObject.CreateInstance<EnemyConfigSO>();
            var serialized = new SerializedObject(config);
            serialized.FindProperty("detectionRange").intValue = detectionRange;
            serialized.FindProperty("forgetRange").intValue = forgetRange;
            serialized.FindProperty("moveDuration").floatValue = 0f;
            serialized.FindProperty("baseMaxHP").intValue = baseMaxHP;
            serialized.FindProperty("baseAttackDamage").intValue = baseAttackDamage;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            playerStats = ScriptableObject.CreateInstance<PlayerStatsSO>();
            context = new EnemyAIContext(floor, occupancy, resolver, playerMover, playerStats);

            enemyGO = new GameObject("Enemy");
            enemyMover = enemyGO.AddComponent<GridMover>();
            var enemySpriteRenderer = enemyGO.AddComponent<SpriteRenderer>();
            controller = enemyGO.AddComponent<EnemyController>();

            // Outside Play Mode, AddComponent does not automatically invoke
            // Awake()/OnEnable() for a plain MonoBehaviour (no [ExecuteAlways]
            // on EnemyController) - GridMover-only tests (e.g.
            // EnemyAIContextTests) never hit this because GridMover has no
            // Awake() to populate. Forcing the calls via SendMessage is
            // unreliable here (Unity's internal "ShouldRunBehaviour()" engine
            // assertion fires inconsistently for both Awake and OnEnable in
            // this headless/unfocused Edit Mode context), so instead
            // reflection directly injects the private fields Awake() would
            // have set - reproducing Awake's *result* without depending on
            // the engine's message-dispatch machinery.
            SetPrivateField(controller, "mover", enemyMover);
            SetPrivateField(controller, "spriteRenderer", enemySpriteRenderer);

            controller.Initialize(config, floor, tilemap, cell, resolver, context);
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(field, $"Expected private field '{fieldName}' on {target.GetType().Name}.");
            field.SetValue(target, value);
        }

        [Test]
        public void TakeTurn_PlayerFarAway_StaysDormantAndDoesNotMove()
        {
            SpawnPlayer(new Vector2Int(0, 0));
            SpawnEnemy(new Vector2Int(15, 0), detectionRange: 3, forgetRange: 5);

            controller.TakeTurn();

            Assert.AreEqual(new Vector2Int(15, 0), enemyMover.CurrentCell, "A Dormant enemy outside detection range must not move.");
            Assert.IsFalse(enemyMover.IsMoving);
            Assert.IsTrue(occupancy.IsOccupied(new Vector2Int(15, 0)));
        }

        [Test]
        public void TakeTurn_PlayerWithinDetectionRange_WakesAndStepsToward()
        {
            SpawnPlayer(new Vector2Int(0, 0));
            SpawnEnemy(new Vector2Int(3, 0), detectionRange: 6, forgetRange: 8);

            controller.TakeTurn();

            // BFS distance from (3,0) to the player's origin along the open
            // corridor is 3; the only strictly-closer, enterable neighbor is
            // West, (2,0) (North/East/South are out of bounds or farther).
            Assert.AreEqual(new Vector2Int(2, 0), enemyMover.CurrentCell, "A woken enemy must take a single step toward the player.");
            Assert.IsFalse(occupancy.IsOccupied(new Vector2Int(3, 0)));
            Assert.IsTrue(occupancy.IsOccupied(new Vector2Int(2, 0)));
        }

        [Test]
        public void TakeTurn_AdjacentToPlayer_DoesNotMoveEvenThoughChasing()
        {
            SpawnPlayer(new Vector2Int(0, 0));
            SpawnEnemy(new Vector2Int(2, 0), detectionRange: 6, forgetRange: 8);

            // Turn 1: distance 2 <= detectionRange(6) -> wakes and steps to
            // (1,0), landing Chasing and adjacent to the player.
            controller.TakeTurn();
            Assert.AreEqual(new Vector2Int(1, 0), enemyMover.CurrentCell, "Precondition: enemy should have closed to adjacency after turn 1.");

            // Turn 2: now adjacent (distance 1) - must hold position despite
            // still being Chasing (it bump-attacks the player instead of moving).
            int hpBeforeAttack = playerStats.Health.Current;
            controller.TakeTurn();

            Assert.AreEqual(new Vector2Int(1, 0), enemyMover.CurrentCell, "An adjacent Chasing enemy must not move.");
            Assert.IsFalse(enemyMover.IsMoving);
            Assert.AreEqual(config.BaseAttackDamage, hpBeforeAttack - playerStats.Health.Current,
                "An adjacent Chasing enemy must bump-attack the player instead of moving, for exactly its authored BaseAttackDamage.");
        }

        [Test]
        public void TakeTurn_ChasingEnemyLosesPlayerBeyondForgetRange_ReturnsToDormant()
        {
            SpawnPlayer(new Vector2Int(0, 0));
            SpawnEnemy(new Vector2Int(3, 0), detectionRange: 6, forgetRange: 6);

            // Turn 1: distance 3 <= detectionRange(6) -> wakes, steps to (2,0).
            controller.TakeTurn();
            Assert.AreEqual(new Vector2Int(2, 0), enemyMover.CurrentCell, "Precondition: enemy should be Chasing after turn 1.");

            // Move the player far beyond forgetRange, bypassing the normal
            // turn flow (same direct-state-mutation approach
            // EnemyAIContextTests uses) so the next TakeTurn() sees a player
            // that has moved away without the enemy having acted in between.
            playerMover.StepTo(new Vector2Int(19, 0));

            // Turn 2: distance from (2,0) to (19,0) is 17 > forgetRange(6) ->
            // forgets and goes Dormant, returning before any movement check.
            controller.TakeTurn();

            Assert.AreEqual(new Vector2Int(2, 0), enemyMover.CurrentCell, "A forgotten (Dormant) enemy must not move.");
            Assert.IsFalse(enemyMover.IsMoving);
        }

        [Test]
        public void Initialize_SeedsHealthFromConfigBaseMaxHP()
        {
            SpawnPlayer(new Vector2Int(0, 0));
            SpawnEnemy(new Vector2Int(15, 0), detectionRange: 3, forgetRange: 5, baseMaxHP: 42, baseAttackDamage: 3);

            // The authored number is the number in play: no formula sits between
            // the asset and the spawned enemy's HP.
            Assert.AreEqual(42, controller.Health.Max);
            Assert.AreEqual(42, controller.Health.Current, "A freshly spawned enemy must start at full HP.");
        }
    }
}
