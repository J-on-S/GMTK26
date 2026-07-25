using UnityEngine;
public abstract class State : MonoBehaviour
{
    protected StateManager stateManager;
    [SerializeField] protected string animName = "";
    protected private GameObject bot;
    protected private Animator anim;
    protected virtual void Awake()
    {
        bot = transform.parent.parent.gameObject;
        anim = bot.GetComponent<Animator>();
    }
    public virtual void Initialize(StateManager manager)
    {
        stateManager = manager;
    }

    // Called once when entering
    public virtual void EnterState() { }

    // Called every frame
    public abstract State UpdateState();

    // Called once before leaving
    public virtual void ExitState() { }
}