using UnityEngine;
using UnityEngine.InputSystem; // NEW input system

public class ShowReactionBar_InputSystem : MonoBehaviour
{
    [SerializeField] private GameObject reactionBarRoot;  // assign ReactionBar
    [SerializeField] private CanvasGroup reactionBarCG;    // assign its CanvasGroup
    [SerializeField] private bool toggleInsteadOfShow = false; // optional

    private InputAction _startAction;

    private void Awake()
    {
        _startAction = new InputAction("StartReaction");
        _startAction.AddBinding("<Mouse>/leftButton");
        _startAction.AddBinding("<Keyboard>/space");

        if (reactionBarRoot) reactionBarRoot.SetActive(false); // hidden on start
        if (reactionBarCG) reactionBarCG.alpha = 0f;
    }

    private void OnEnable()  { _startAction.Enable(); }
    private void OnDisable() { _startAction.Disable(); }

    private void Update()
    {
        if (_startAction.triggered)
        {
            if (toggleInsteadOfShow)
            {
                bool next = !reactionBarRoot.activeSelf;
                reactionBarRoot.SetActive(next);
                if (reactionBarCG) reactionBarCG.alpha = next ? 1f : 0f;
            }
            else
            {
                reactionBarRoot.SetActive(true);
                if (reactionBarCG) reactionBarCG.alpha = 1f;
            }
        }
    }
}
