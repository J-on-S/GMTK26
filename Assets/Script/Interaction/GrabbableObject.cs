using UnityEngine;
using System.Collections;

public class GrabbableObject : MonoBehaviour, IInteractable, IHoverable{
  public Item item;
  public float respawnTime = 3f;

  [Tooltip("Channel and clips this object plays on grab and drop. When set, it wins over the four fields below; they stay as the fallback so objects placed before presets existed keep sounding the same.")]
  public AudioGrappablePreset audioPreset;

  public AudioEventChannel channel;
  public Audio metalPickupAudio;
  public Audio clothPickupAudio;
  public Audio metalDropAudio;
  public Audio clothDropAudio;

  [Header("Held pose")]
  [Tooltip("Offset from the hand, in the hold point's own space. Lets one hold point carry objects whose pivot is not where the hand should grip them.")]
  public Vector3 holdOffset = Vector3.zero;

  [Tooltip("Rotation applied on top of the hand's, in degrees. For an object modelled facing the wrong way.")]
  public Vector3 holdRotationOffset = Vector3.zero;

  [Tooltip("Per-axis size the object is held at, as a multiple of the size it has in the room. One = held at its real size. Applies only while it is in the hand; dropping, shelving or respawning puts the real size back.")]
  public Vector3 holdScaleMultiplier = Vector3.one;

  [Header("Drop toss")]
  [Tooltip("Push away from the player when dropped, along the camera's forward flattened to the ground. 0 = it drops straight down.")]
  public float dropForwardImpulse = 3.5f;

  [Tooltip("Upward part of that push, so the object arcs above the horizon and lands clear instead of scraping the floor at the player's feet.")]
  public float dropUpImpulse = 0.8f;

  private Vector3 initialPosition;
  private Quaternion initialRotation;

  /// <summary>The size this object is meant to be seen at, in world units.</summary>
  /// <remarks>Kept in world space, not as a localScale: the object changes parent every time it is
  /// picked up, dropped or shelved, and only the world size is supposed to stay the same across that.</remarks>
  private Vector3 startWorldScale = Vector3.one;
  private bool worldScaleCaptured;

  /// <summary>The hand this object was last posed into, so <see cref="RestoreWorldScale"/> knows whether the held size or the room size applies.</summary>
  /// <remarks>Compared against the current parent rather than trusted on its own: the fridge and the
  /// delivery reparent the object without going through <see cref="DetachToWorld"/>, and a part shelved
  /// straight out of the hand must go back to its room size, not keep the held one.</remarks>
  private Transform heldParent;

  private Rigidbody rb;
  private Interactor holder;
  private Coroutine respawnRoutine;

  /// <summary>The rigidbody's authored <c>isKinematic</c>, restored on respawn so a delivery never turns a kinematic object dynamic.</summary>
  /// <remarks>Snapshotted before the first grab flips it kinematic; without it <c>GoDynamic</c> would hardcode every respawned object dynamic, whatever it was authored as.</remarks>
  private bool startKinematic;

  public Vector3 StartWorldScale => startWorldScale;

  /// <summary>What the description HUD calls this item: its authored <see cref="itemName"/>, or the GameObject's name when that is blank.</summary>
  public string DisplayName
  {
      get
      {
          if (item == null)
        {
          Debug.LogError("item is null: current gameObject: "+gameObject.name);
          return gameObject.name;
        }
              

          return string.IsNullOrWhiteSpace(item.Name)
              ? gameObject.name
              : item.Name;
      }
  }
  /// <summary>Set the frame the player is looking at this, cleared in LateUpdate: the interactor calls HoverOver every such frame, so a frame with no call means the aim left.</summary>
  private bool hovering;

  void Awake(){
    rb = GetComponent<Rigidbody>();
    // no Rigidbody at all is the most static an object gets, so it counts as kinematic here:
    // EnsureRigidbody adds a dynamic one on the first grab, and reading that back as the authored
    // state would respawn a shelf prop as a falling body inside the shelf it was sitting on.
    startKinematic = rb == null || rb.isKinematic;
    CaptureWorldScale();
    SetStartPose(transform.position, transform.rotation);
  }

  /// <summary>Takes the size the object is standing at as the one it should keep.</summary>
  /// <remarks>Called on the first reparent as well as in Awake: the held-pose preview runs in edit mode,
  /// where Awake never fired, and a scale of one there would resize the object the moment it was picked up.</remarks>
  private void CaptureWorldScale(){
    startWorldScale = transform.lossyScale;
    worldScaleCaptured = true;
  }

  public void SetStartPose(Vector3 position, Quaternion rotation){
    initialPosition = position;
    initialRotation = rotation;
  }

  /// <summary>Sizes the object to its authored world scale under whatever parent it currently has, times <see cref="holdScaleMultiplier"/> while it is in the hand.</summary>
  /// <remarks>
  /// Invariant: scale is set here and nowhere else. Reparenting with <c>worldPositionStays</c> true lets
  /// Unity rewrite localScale to chase the world size, and when a parent is both rotated and non-uniformly
  /// scaled the result has shear that no TRS can hold -- Unity approximates it, so every grab/drop cycle
  /// leaves the object a little off, and the error compounds. Recomputing from one stored world size
  /// instead means a hundred grabs land on the same number as the first.
  /// <para>The held multiplier is applied on top of that stored size rather than to the object's current
  /// one, for the same reason: it is re-derived every time, so it never compounds and dropping is just
  /// the same sum without it.</para>
  /// </remarks>
  public void RestoreWorldScale(){
    if (!worldScaleCaptured) CaptureWorldScale();

    Vector3 target = HeldPoseActive
      ? Vector3.Scale(startWorldScale, holdScaleMultiplier)
      : startWorldScale;

    Transform parent = transform.parent;
    transform.localScale = parent == null
      ? target
      : DivideScale(target, parent.lossyScale);
  }

  /// <summary>True while the object is sitting in the hand it was posed into -- the only time the held size applies.</summary>
  private bool HeldPoseActive => heldParent != null && transform.parent == heldParent;

  /// <summary>Unparents the object, leaving it exactly where it stood and at its authored size.</summary>
  /// <remarks>The world pose is read then written back rather than handed to <c>worldPositionStays</c>,
  /// which would also recompute localScale -- the drift this class exists to avoid.</remarks>
  public void DetachToWorld(){
    Vector3 worldPosition = transform.position;
    Quaternion worldRotation = transform.rotation;

    transform.SetParent(null, false);
    transform.SetPositionAndRotation(worldPosition, worldRotation);
    heldParent = null; // out of the hand: back to the size it has in the room
    RestoreWorldScale();
  }

  private static Vector3 DivideScale(Vector3 world, Vector3 parent){
    return new Vector3(
      SafeDivide(world.x, parent.x),
      SafeDivide(world.y, parent.y),
      SafeDivide(world.z, parent.z));
  }

  // a zero-scaled parent has no size to divide out; keeping the world value beats an infinity
  private static float SafeDivide(float value, float divisor){
    return Mathf.Approximately(divisor, 0f) ? value : value / divisor;
  }

  /// <summary>Parents this object to the hand and sits it there at its own offsets.</summary>
  /// <remarks>Pose only -- no physics, no sound, no claim on the player. The grab and the edit-mode
  /// preview both go through here, so what an author lines up is what the player will be handed.</remarks>
  public void ApplyHeldPose(Transform holdPoint){
    if (holdPoint == null) return;

    if (!worldScaleCaptured) CaptureWorldScale(); // before the hand's scale is in the chain

    // false, not true: every one of the three values is written below, so keeping the world pose
    // would buy nothing and cost the localScale rewrite that used to shrink objects a little per grab.
    transform.SetParent(holdPoint, false);
    heldParent = holdPoint; // before the scale is written: it is what turns the held multiplier on

    // local means relative to the parent
    transform.localPosition = holdOffset; // centre of holdPoint, plus this object's own offset
    transform.localRotation = Quaternion.Euler(holdRotationOffset); // the hand's rotation, turned by this object's offset
    RestoreWorldScale(); // its room size times the held multiplier, whatever the hand is scaled to
  }

  //GRAB
  public virtual void Interact(Interactor player){
    if (player.heldObject == null){
      // make an object as a child for holdPoint
      Debug.Log("player holdp"+player.holdPoint);
      PlayPickupSound();
      ApplyHeldPose(player.holdPoint);
      EnsureRigidbody().isKinematic = true; // no physics for object;
      SetCollidersEnabled(false);
      player.heldObject = this;
      player.heldObject.item = item;
      holder = player;
    }
  }

  private void PlayPickupSound(){
    if (audioPreset != null){
      audioPreset.PlayPickup(item.Type);
      return;
    }
    if (channel == null) return;
    channel.Play(item.Type == ItemType.Tool ? metalPickupAudio : clothPickupAudio);
  }

  private void PlayDropSound(){
    if (audioPreset != null){
      audioPreset.PlayDrop(item.Type);
      return;
    }
    if (channel == null) return;
    channel.Play(item.Type == ItemType.Tool ? metalDropAudio : clothDropAudio);
  }

  private Rigidbody EnsureRigidbody(){
    if (rb == null && !TryGetComponent(out rb)){
      rb = gameObject.AddComponent<Rigidbody>();
    }
    return rb;
  }

  private void GoDynamic(){
    Rigidbody body = EnsureRigidbody();
    body.isKinematic = false;
    body.linearVelocity = Vector3.zero;
    body.angularVelocity = Vector3.zero;
  }

  public void SetCollidersEnabled(bool enabled){
    Collider[] colliders = GetComponentsInChildren<Collider>(true);
    for (int i = 0; i < colliders.Length; i++){
      colliders[i].enabled = enabled;
    }
  }

  private void SetRenderersEnabled(bool enabled){
    Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
    for (int i = 0; i < renderers.Length; i++){
      renderers[i].enabled = enabled;
    }
  }

  public void ReleaseFromHolder(){
    if (holder != null && holder.heldObject == this){
      holder.heldObject = null;
    }
    holder = null;
  }

  // HOVER -- the interactor calls this every frame the player's aim is on this object.
  public virtual void HoverOver(Interactor player){
    ShowHudDescription();
    hovering = true;
  }

  /// <summary>What this item puts on the description HUD while hovered. Base shows just the name, and only for a tool; a body part overrides this to add its decay bar.</summary>
  protected virtual void ShowHudDescription(){
    if (item.Type == ItemType.BodyPart) return; // parts override; a plain grabbable that is not a tool shows nothing
    BodyPartDescriptionHUD hud = BodyPartDescriptionHUD.LastActiveInstance;
    if (hud != null) hud.ShowName(this);
  }

  /// <summary>Clears the HUD once the aim leaves: a frame with no HoverOver call means the player is no longer looking at this.</summary>
  protected virtual void LateUpdate(){
    if (!hovering){
      BodyPartDescriptionHUD hud = BodyPartDescriptionHUD.LastActiveInstance;
      if (hud != null) hud.HideDescription(this);
    }
    hovering = false;
  }

  // DROP
  public void Drop(){
    PlayDropSound();

    // read the view before the holder is let go: ReleaseFromHolder clears it, and the toss is aimed
    // wherever the player was looking at the moment they dropped.
    Transform view = ViewTransform();

    ReleaseFromHolder();
    // remove parenting, keeping the pose and the size it was held at
    DetachToWorld();
    SetCollidersEnabled(true);
    GoDynamic(); // physics back on for object;
    TossOnDrop(view);
  }

  /// <summary>What the player is looking along, for aiming the drop.</summary>
  /// <remarks>The holder's camera first, since that is the view the drop was made from; the hand and
  /// then the object's own facing are fallbacks for a drop with nobody holding it.</remarks>
  private Transform ViewTransform(){
    if (Camera.main != null) return Camera.main.transform;
    if (holder != null && holder.holdPoint != null) return holder.holdPoint;
    return transform;
  }

  /// <summary>Pushes a dropped object away from the player: forward along the ground, plus a little lift.</summary>
  /// <remarks>
  /// The forward part is flattened, so looking at the floor or the ceiling still tosses the object out
  /// in front rather than straight down or up. The lift is what keeps it from scraping along the floor
  /// at the player's feet -- it arcs, lands clear, and stays reachable.
  /// <para>Straight up or down leaves no horizontal direction at all; the object then just gets the
  /// lift, instead of a normalize on a zero vector.</para>
  /// </remarks>
  private void TossOnDrop(Transform view){
    if (dropForwardImpulse == 0f && dropUpImpulse == 0f) return;

    Rigidbody body = EnsureRigidbody();
    if (body == null || body.isKinematic) return;

    Vector3 forward = view != null ? view.forward : transform.forward;
    forward.y = 0f;
    forward = forward.sqrMagnitude > 1e-6f ? forward.normalized : Vector3.zero;

    body.AddForce(forward * dropForwardImpulse + Vector3.up * dropUpImpulse, ForceMode.Impulse);
  }

  public void ReturnToStart(){
    ReleaseFromHolder();
    DetachToWorld();
    transform.SetPositionAndRotation(initialPosition, initialRotation);
    SetCollidersEnabled(true);
    RestoreStartPhysics(); // physics back, in the object's authored kinematic state
  }

  /// <summary>Puts the rigidbody back to how it was authored -- its start <c>isKinematic</c>, at rest.</summary>
  /// <remarks>Not <see cref="GoDynamic"/>: that always turns physics on, which is right for a toss but would
  /// make a respawned kinematic object dynamic. Velocities are cleared either way so a respawn lands still.</remarks>
  private void RestoreStartPhysics(){
    Rigidbody body = EnsureRigidbody();
    body.isKinematic = startKinematic;
    if (!body.isKinematic){
      body.linearVelocity = Vector3.zero;
      body.angularVelocity = Vector3.zero;
    }
  }

  // this is called when the doctor gets the right item delivered
  public void StartRespawnTimer(){
    if (respawnRoutine != null){
      StopCoroutine(respawnRoutine);
    }
    ReleaseFromHolder();
    respawnRoutine = StartCoroutine(RespawnRoutine());
  }

  // respawn items after a certain amount of time
  IEnumerator RespawnRoutine(){
    DetachToWorld();
    SetRenderersEnabled(false); // hide appearence
    SetCollidersEnabled(false); // hide physical
    yield return new WaitForSeconds(respawnTime);
    ReturnToStart();
    SetRenderersEnabled(true);
    respawnRoutine = null;
  }

  /// <summary>Finishes a respawn that was cut short by the object being disabled, so it is not left stranded hidden.</summary>
  /// <remarks>Unity stops coroutines when a component is disabled, so an object switched off mid-wait would
  /// keep its renderers off and never come back. This lands it at its start, visible and collidable, the
  /// moment that happens; a plain destroy runs it too but the transform writes are harmless there.</remarks>
  void OnDisable(){
    if (respawnRoutine == null) return;

    respawnRoutine = null;
    ReturnToStart();
    SetRenderersEnabled(true);
  }
}
