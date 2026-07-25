using UnityEngine;
public class Doctor : MonoBehaviour
{   

//     [SerializeField] private 
//     public void Start()
//     {
//         StartCoroutine()
//     } 


//     [Header("POV")]
//     [SerializeField] private Transform player;
//     [SerializeField] private float viewRange = 10f;        // How far enemy can see
//     [SerializeField] private float viewAngle = 90f;        // Cone angle (90 = 45° each side) 
    
//     private bool _canSee;//_hasTreasure;
//     public bool CanSee {
//         get => _canSee;
//         set {
//             if (_canSee != value) {
//                 _canSee = value;
//                 OnSeeChanged();
//             }
//         }
//     }
//     [SerializeField] private float hungryMaxState = 10;
//     [SerializeField] private float _hungryState;
//     public float HungryState {
//         get => _hungryState;
//         set {
//             if (_hungryState != value) {
//                 _hungryState = value;
//                 OnHungryChanged();
//             }
//         }
//     }
//     [SerializeField] private float stressMaxState = 35;
//     [SerializeField] private float _stressState;
//     public float StressState {
//         get => _stressState;
//         set {
//             if (_stressState != value) {
//                 _stressState = value;
//                 OnStressChanged();
//             }
//         }
//     }
//     [SerializeField] private float _distancePlayer;
//     public float DistancePlayer {
//         get => _distancePlayer;
//         set {
//             if (_distancePlayer != value) {
//                 _distancePlayer = value;
//                 OnDistanceChanged();
//             }
//         }
//     }
    
    

//     // Start is called once before the first execution of Update after the MonoBehaviour is created
//     void Start()
//     {
//         agent = GetComponent<NavMeshAgent>();
//         ResetHungry();
//         ResetStress();
//     }
//     void Update()
//     {

//         Hungry();
//         Stress();
//         DistanceToPlayer();
//         GetTimeSeen();
//     }
    
//    //--------------------------------------- ATTACK --------------------------
    
//     public void Attack(GameObject target)
//     {
//         GameObject boulder = Instantiate(boulderPrefab, boulderSpawnPoint.position, Quaternion.identity);
//         Rigidbody rb = boulder.GetComponent<Rigidbody>();

//         Vector3 targetPos = target.transform.position;
//         Vector3 dir = targetPos - boulderSpawnPoint.position;

//         float dist = new Vector2(dir.x, dir.z).magnitude;   
//         float heightDiff = dir.y;

//         // -arc setting
//         float arcHeight = Mathf.Clamp(dist * 0.15f, 2f, 12f);   
//         // grav
//         float g = Mathf.Abs(Physics.gravity.y);

//         // time -> reach peak -> fall to target
//         float tUp = Mathf.Sqrt(2 * arcHeight / g);
//         float tDown = Mathf.Sqrt(2 * Mathf.Abs(heightDiff - arcHeight) / g);
//         float time = tUp + tDown;

//         // Horizontal, vertical velocity
//         Vector3 dirXZ = new Vector3(dir.x, 0, dir.z).normalized;
//         Vector3 horizontalVel = dirXZ * (dist / time);
//         float verticalVel = Mathf.Sqrt(2 * g * arcHeight);
//         Vector3 velocity = horizontalVel + Vector3.up * verticalVel;

//         rb.linearVelocity = velocity;

//         target = null;
//         agent.ResetPath();
//     }
//     public void AttackNear(GameObject target)
//     {
//         Debug.LogError("Attack near");
//         target.GetComponent<Player>().Hurt();
//     }
    
    
//     //----------------- RANDOM POS -------------------------
    

    

//     public Vector3 RandomSpawnPos(){

//         BoxCollider groundCollider = patrolArea.GetComponent<BoxCollider>();
//         Vector3 groundSize = Vector3.Scale(groundCollider.size, patrolArea.transform.localScale);
//         Vector3 groundCenter = groundCollider.transform.position;
    
//         float randomX = Random.Range(
//                 groundCenter.x - groundSize.x / 2f + 2.5f,
//                 groundCenter.x + groundSize.x / 2f - 2.5f
//             );
//             float randomZ = Random.Range(
//                 groundCenter.z - groundSize.z / 2f + 2f,
//                 groundCenter.z + groundSize.z / 2f - 2f
//             );

//         Vector3 spawnPos = new Vector3(randomX, transform.position.y, randomZ);

//         return spawnPos;
//     }
//     //----------------------------- distance_from_player ---------------

//     public void DistanceToPlayer()
//     {
//         DistancePlayer = Vector3.Distance(agent.transform.position, player.transform.position);
//     }
//     void OnDistanceChanged()
//     {
//         planner.Get_world_state().SetValue("distance_from_player", _distancePlayer);
//     }
//     // ------------------------------------------------------------- Can See Player -----------------------------
    
//     public void GetTimeSeen()
//     {
//         bool canSeeCheck = CanSeePlayer();
        
//         if (canSeeCheck)
//         {
//             timeSeen+=Time.deltaTime;
//             if (timeSeen > limitSeen)
//             {
//                 timeSeen = limitSeen;
//             }
//         }
//         else
//         {
//             timeSeen = Mathf.Max(0, timeSeen - 0.2f * Time.deltaTime);
            
//         }

//         if(timeSeen>= seenTrigger)
//         {
//             CanSee = true;
//         }
//         else
//         {
//             CanSee = false;
//         }
//         seen_bar.fillAmount = timeSeen/seenTrigger;

//     }
//     void OnSeeChanged() {
//         //Debug.LogError("Enemy sees player? "+_canSee);
//         if (_canSee)
//         {
//             planner.Get_world_state().SetValue("is_player_seen", _canSee, true);
//             canSeeUI.SetActive(true);
//         }
//         else
//         {
//             planner.Get_world_state().SetValue("is_player_seen", _canSee);
//             canSeeUI.SetActive(false);
//         }
        
        
        
//     }
//     private bool CanSeePlayer()
//     {
//         Vector3 dirToPlayer = (player.position - transform.position).normalized;

//         // Distance check
//         float distanceToPlayer = Vector3.Distance(transform.position, player.position);
//         if (distanceToPlayer > viewRange)
//             return false;

//         // Angle -> check vision cone
//         float angleToPlayer = Vector3.Angle(transform.forward, dirToPlayer);
//         if (angleToPlayer > viewAngle / 2f)
//             return false;

//         // sign check
//         if (Physics.Raycast(transform.position, dirToPlayer, distanceToPlayer, obstacleMask))
//             return false; // something is blocking!

//         return true; // Visible
//     }

//     // ------------------------------------------------------------- HUNGRY -----------------------------
//     private void OnHungryChanged()
//     {
//         planner.Get_world_state().SetValue("hungry_bar", _hungryState);
//     }
//     public void ResetHungry()
//     {
//         HungryState = hungryMaxState;
//     }
//      //private bool isHungry = false;
//     public void Hungry()
//     {
//         if (_hungryState <= 0)
//         {
            
//         }
//         else if(_hungryState > 0)
//         {
//             HungryState-=Time.deltaTime;
//         }
//     }

//     // ------------------------------------------------------------- STRESS -----------------------------
    
//     private void OnStressChanged()
//     {
//         planner.Get_world_state().SetValue("stress_bar", _stressState);
//     }
    
//     public void ResetStress()
//     {
//         Debug.Log("Should Reset Stress");
//         StressState = stressMaxState;
//     }
    
//     public void Stress()
//     {
//         if(_stressState > 0)
//         {
//             StressState-=Time.deltaTime;
//         }
//     }
    
//     void OnDrawGizmos()
//     {
//         Gizmos.color = Color.yellow;

//         // Draw range sphere
//         Gizmos.DrawWireSphere(transform.position, viewRange);

//         // Draw vision cone edges
//         Vector3 leftDir = Quaternion.Euler(0, -viewAngle / 2f, 0) * transform.forward;
//         Vector3 rightDir = Quaternion.Euler(0, viewAngle / 2f, 0) * transform.forward;

//         Gizmos.DrawLine(transform.position, transform.position + leftDir * viewRange);
//         Gizmos.DrawLine(transform.position, transform.position + rightDir * viewRange);
//     }
    
    
    
}