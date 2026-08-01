using System.Collections.Generic;
using UnityEngine;

namespace Jagara.Runtime.Narrative.Dialogue
{
    /// <summary>
    /// An ordered conversation, played start to finish by DialoguePanelController.
    /// Branching is deliberately absent: the GDD does not define dialogue choices,
    /// so adding them here would be inventing design.
    /// </summary>
    [CreateAssetMenu(fileName = "New Dialogue", menuName = "Jagara/Dialogue")]
    public class DialogueSO : ScriptableObject
    {
        [SerializeField] private List<DialogueLine> lines = new();

        public IReadOnlyList<DialogueLine> Lines => lines;

        public int LineCount => lines != null ? lines.Count : 0;
    }
}
