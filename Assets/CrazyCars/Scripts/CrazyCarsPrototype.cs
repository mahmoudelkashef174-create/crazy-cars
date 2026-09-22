using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CrazyCars
{
    public static class MobileInputState
    {
        public static bool Left, Right, Accelerate, Brake, Drift, ItemPressed;
        public static float Steering => (Left ? -1f : 0f) + (Right ? 1f : 0f);
        public static bool ConsumeItem() { if (!ItemPressed) return false; ItemPressed = false; return true; }
    }

    public enum MobileButtonAction { Left, Right, Accelerate, Brake, Drift, Item }

    public sealed class MobileHoldButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        public MobileButtonAction action;
        public void OnPointerDown(PointerEventData e) => Set(true);
        public void OnPointerUp(PointerEventData e) => Set(false);
        void OnDisable() => Set(false);
        void Set(bool v)
        {
            switch (action)
            {
                case MobileButtonAction.Left: MobileInputState.Left=v; break;
                case MobileButtonAction.Right: MobileInputState.Right=v; break;
                case MobileButtonAction.Accelerate: MobileInputState.Accelerate=v; break;
                case MobileButtonAction.Brake: MobileInputState.Brake=v; break;
                case MobileButtonAction.Drift: MobileInputState.Drift=v; break;
                case MobileButtonAction.Item: if(v) MobileInputState.ItemPressed=true; break;
            }
        }
    }

    [RequireComponent(typeof(Rigidbody))]
    public sealed class ArcadeKartController : MonoBehaviour
    {
        public bool playerControlled = true;
        public float acceleration=26f, reverseAcceleration=15f, maxSpeed=24f, steerStrength=92f;
        public float normalGrip=.90f, driftGrip=.69f, driftSteerMultiplier=1.28f, downforce=15f;
        public Transform visualRoot;
        public float CurrentSpeedKph => Vector3.Dot(rb.linearVelocity, transform.forward) * 3.6f;

        Rigidbody rb;
        float throttle, steer, boostTimer, speedMultiplier=1f;
        bool drift;

        void Awake()
        {
            rb=GetComponent<Rigidbody>();
            rb.mass=280f;
            rb.centerOfMass=new Vector3(0,-.32f,0);
            rb.interpolation=RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode=CollisionDetectionMode.ContinuousDynamic;
        }

        public void SetAIInput(float t,float s,bool d){ if(playerControlled)return; throttle=t; steer=s; drift=d; }
        public void AddBoost(float seconds=2f){ boostTimer=Mathf.Max(boostTimer,seconds); }
        public void SetSpeedMultiplier(float value,float seconds){ speedMultiplier=Mathf.Clamp(value,.2f,2f); CancelInvoke(nameof(ResetSpeed)); Invoke(nameof(ResetSpeed),seconds); }
        void ResetSpeed()=>speedMultiplier=1f;

        void Update()
        {
            if(playerControlled)
            {
                float t=0,s=0;
                if(Input.GetKey(KeyCode.W)||Input.GetKey(KeyCode.UpArrow)) t+=1;
                if(Input.GetKey(KeyCode.S)||Input.GetKey(KeyCode.DownArrow)) t-=1;
                if(Input.GetKey(KeyCode.A)||Input.GetKey(KeyCode.LeftArrow)) s-=1;
                if(Input.GetKey(KeyCode.D)||Input.GetKey(KeyCode.RightArrow)) s+=1;
                throttle=Mathf.Abs(t)>.01f?t:(MobileInputState.Accelerate?1:0)+(MobileInputState.Brake?-1:0);
                steer=Mathf.Abs(s)>.01f?s:MobileInputState.Steering;
                drift=Input.GetKey(KeyCode.LeftShift)||MobileInputState.Drift;
            }
            if(visualRoot) visualRoot.localRotation=Quaternion.Slerp(visualRoot.localRotation,Quaternion.Euler(0,0,-steer*7f),Time.deltaTime*8f);
        }

        void FixedUpdate()
        {
            bool grounded=Physics.Raycast(transform.position+Vector3.up*.25f,Vector3.down,1f);
            float forward=Vector3.Dot(rb.linearVelocity,transform.forward);
            float max=maxSpeed*(boostTimer>0?1.42f:1f)*speedMultiplier;

            if(throttle>0 && forward<max) rb.AddForce(transform.forward*acceleration*throttle*rb.mass);
            if(throttle<0 && forward>-8f) rb.AddForce(transform.forward*reverseAcceleration*throttle*rb.mass);

            if(grounded)
            {
                float speed01=Mathf.Clamp01(Mathf.Abs(forward)/Mathf.Max(1,maxSpeed));
                if(Mathf.Abs(forward)>.7f)
                    rb.MoveRotation(rb.rotation*Quaternion.Euler(0,steer*steerStrength*(.25f+.75f*speed01)*(drift?driftSteerMultiplier:1f)*Mathf.Sign(forward)*Time.fixedDeltaTime,0));

                Vector3 f=Vector3.Project(rb.linearVelocity,transform.forward);
                Vector3 l=Vector3.Project(rb.linearVelocity,transform.right);
                rb.linearVelocity=f+l*(drift?driftGrip:normalGrip)+Vector3.up*rb.linearVelocity.y;
                rb.AddForce(-transform.up*downforce*rb.mass);
            }
            if(boostTimer>0) boostTimer-=Time.fixedDeltaTime;
        }
    }

    public sealed class KartCamera : MonoBehaviour
    {
        public Transform target;
        public Rigidbody targetBody;
        public Vector3 offset=new Vector3(0,4.4f,-7.6f);
        Camera cam;
        void Awake()=>cam=GetComponent<Camera>();
        void LateUpdate()
        {
            if(!target)return;
            Vector3 wanted=target.TransformPoint(offset);
            transform.position=Vector3.Lerp(transform.position,wanted,1-Mathf.Exp(-8f*Time.deltaTime));
            Vector3 vel=targetBody?targetBody.linearVelocity:Vector3.zero;
            Vector3 look=target.position+Vector3.up*1.1f+Vector3.ProjectOnPlane(vel,Vector3.up).normalized*2f;
            transform.rotation=Quaternion.Slerp(transform.rotation,Quaternion.LookRotation(look-transform.position),1-Mathf.Exp(-8f*Time.deltaTime));
            if(cam) cam.fieldOfView=Mathf.Lerp(cam.fieldOfView,Mathf.Lerp(58,72,Mathf.InverseLerp(3,28,vel.magnitude)),Time.deltaTime*4);
        }
    }

    public sealed class RaceProgress : MonoBehaviour
    {
        public int totalLaps=3, checkpointCount=4;
        public int CurrentLap{get;private set;}=1;
        public int NextCheckpoint{get;private set;}
        public bool Finished{get;private set;}
        public void Pass(int index)
        {
            if(Finished||index!=NextCheckpoint)return;
            NextCheckpoint++;
            if(NextCheckpoint>=checkpointCount)
            {
                NextCheckpoint=0; CurrentLap++;
                if(CurrentLap>totalLaps){ Finished=true; RaceManager.Instance?.Finish(this); }
            }
        }
    }

    public sealed class Checkpoint : MonoBehaviour
    {
        public int index;
        void OnTriggerEnter(Collider c){ var p=c.GetComponentInParent<RaceProgress>(); if(p)p.Pass(index); }
    }

    public enum CrazyItem { None, Turbo, Freeze, Bomb, Shield }

    [RequireComponent(typeof(ArcadeKartController))]
    public sealed class KartItemSystem : MonoBehaviour
    {
        public CrazyItem HeldItem{get;private set;}
        ArcadeKartController kart;
        float shieldTimer;
        void Awake()=>kart=GetComponent<ArcadeKartController>();
        void Update()
        {
            if(shieldTimer>0)shieldTimer-=Time.deltaTime;
            if((kart.playerControlled && (Input.GetKeyDown(KeyCode.Space)||MobileInputState.ConsumeItem()))) Use();
        }
        public void GiveRandom(){ if(HeldItem==CrazyItem.None) HeldItem=(CrazyItem)Random.Range(1,5); }
        public void Use()
        {
            if(HeldItem==CrazyItem.None)return;
            var item=HeldItem; HeldItem=CrazyItem.None;
            if(item==CrazyItem.Turbo) kart.AddBoost(2.2f);
            else if(item==CrazyItem.Shield) shieldTimer=5f;
            else if(item==CrazyItem.Freeze)
            {
                ArcadeKartController best=null; float dBest=999;
                foreach(var other in FindObjectsByType<ArcadeKartController>(FindObjectsSortMode.None))
                {
                    if(other==kart)continue;
                    float d=Vector3.Distance(transform.position,other.transform.position);
                    if(d<dBest&&d<28){best=other;dBest=d;}
                }
                if(best)best.SetSpeedMultiplier(.35f,2.2f);
            }
            else if(item==CrazyItem.Bomb)
            {
                var b=GameObject.CreatePrimitive(PrimitiveType.Sphere);
                b.name="CrazyBomb"; b.transform.position=transform.position+transform.forward*1.7f+Vector3.up*.6f; b.transform.localScale=Vector3.one*.55f;
                var r=b.AddComponent<Rigidbody>(); r.mass=.6f; r.linearVelocity=GetComponent<Rigidbody>().linearVelocity+transform.forward*17f+Vector3.up*2f;
                b.AddComponent<BombProjectile>();
            }
        }
    }

    public sealed class BombProjectile : MonoBehaviour
    {
        void Start()=>Destroy(gameObject,4f);
        void OnCollisionEnter(Collision c)
        {
            foreach(var hit in Physics.OverlapSphere(transform.position,4f))
            {
                var k=hit.GetComponentInParent<ArcadeKartController>();
                if(k) k.GetComponent<Rigidbody>().AddForce(((k.transform.position-transform.position).normalized+Vector3.up*.45f)*1200f,ForceMode.Impulse);
            }
            Destroy(gameObject);
        }
    }

    public sealed class ItemPickup : MonoBehaviour
    {
        Collider col; Renderer ren;
        void Awake(){col=GetComponent<Collider>();ren=GetComponent<Renderer>();}
        void Update()=>transform.Rotate(0,100f*Time.deltaTime,0,Space.World);
        void OnTriggerEnter(Collider c)
        {
            var i=c.GetComponentInParent<KartItemSystem>(); if(!i)return;
            i.GiveRandom(); col.enabled=false; if(ren)ren.enabled=false; Invoke(nameof(Respawn),4f);
        }
        void Respawn(){col.enabled=true;if(ren)ren.enabled=true;}
    }

    [RequireComponent(typeof(ArcadeKartController))]
    public sealed class SimpleAIBot : MonoBehaviour
    {
        public Transform[] waypoints;
        int index;
        ArcadeKartController kart;
        void Awake(){kart=GetComponent<ArcadeKartController>();kart.playerControlled=false;}
        void FixedUpdate()
        {
            if(waypoints==null||waypoints.Length==0)return;
            Vector3 local=transform.InverseTransformPoint(waypoints[index].position);
            float steer=Mathf.Clamp(local.x/Mathf.Max(1,local.magnitude)*2f,-1,1);
            float angle=Mathf.Abs(Mathf.Atan2(local.x,local.z)*Mathf.Rad2Deg);
            kart.SetAIInput(angle>50?.55f:1f,steer,angle>30);
            if(local.magnitude<5f)index=(index+1)%waypoints.Length;
        }
    }

    public sealed class RaceManager : MonoBehaviour
    {
        public static RaceManager Instance{get;private set;}
        public RaceProgress player;
        public Text countdownText, lapText;
        float countdown=3.5f;
        void Awake(){Instance=this;Application.targetFrameRate=60;}
        void Update()
        {
            if(countdown>0){countdown-=Time.deltaTime;if(countdownText)countdownText.text=countdown>1?Mathf.CeilToInt(countdown).ToString():countdown>0?"GO!":"";}
            if(lapText&&player)lapText.text=$"LAP {Mathf.Min(player.CurrentLap,player.totalLaps)} / {player.totalLaps}";
        }
        public void Finish(RaceProgress r){if(r==player&&countdownText)countdownText.text="FINISH!";}
    }

    public sealed class KartHUD : MonoBehaviour
    {
        public ArcadeKartController kart; public KartItemSystem items; public Text speedText,itemText;
        void Update()
        {
            if(kart&&speedText)speedText.text=$"{Mathf.Abs(Mathf.RoundToInt(kart.CurrentSpeedKph))} km/h";
            if(items&&itemText)itemText.text=items.HeldItem==CrazyItem.None?"STAR":items.HeldItem.ToString().ToUpperInvariant();
        }
    }
}
