using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Tilemaps;
using Jagara.Runtime.DungeonGen;
using Jagara.Runtime.TurnSystem;
using Jagara.Runtime.UI;

namespace Jagara.Runtime.Gameplay
{
    /// <summary>
    /// Reads grid-movement input and drives GridMover. Input is resolved
    /// fresh every Update, so holding a direction produces continuous stepping
    /// once the mover's current tween finishes - no explicit input queue needed.
    /// </summary>
    [RequireComponent(typeof(GridMover))]
    public class PlayerController : MonoBehaviour
    {
        [SerializeField] private GridMover mover;
        [SerializeField] private InputActionAsset controlsAsset;

        public GridMover Mover => mover;

        private InputAction moveAction;
        private TurnResolver turnResolver;
        private ActionMenuController actionMenu;

        private void Awake()
        {
            var map = controlsAsset.FindActionMap("Gameplay", throwIfNotFound: true);
            moveAction = map.FindAction("Move", throwIfNotFound: true);
        }

        private void OnEnable()
        {
            moveAction.Enable();
            mover.OnMoveCompleted += HandleMoveCompleted;
        }

        private void OnDisable()
        {
            moveAction.Disable();
            mover.OnMoveCompleted -= HandleMoveCompleted;
        }

        public void Initialize(FloorData floor, Tilemap tilemap, Vector2Int startCell, TurnResolver resolver, OccupancyGrid occupancy, ActionMenuController menu)
        {
            turnResolver = resolver;
            actionMenu = menu;
            mover.Initialize(floor, tilemap, startCell, occupancy);
        }

        private void Update()
        {
            if (mover.IsMoving || (turnResolver != null && turnResolver.IsResolving) || (actionMenu != null && actionMenu.IsOpen))
            {
                return;
            }

            Vector2Int direction = CardinalDirectionResolver.Resolve(moveAction.ReadValue<Vector2>());
            if (direction != Vector2Int.zero)
            {
                mover.TryMove(direction);
            }
        }

        private void HandleMoveCompleted() => turnResolver?.EndPlayerTurn();
    }
}
