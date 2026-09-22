#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CrazyCars.Editor
{
    public static class CrazyCarsBootstrap
    {
        public const string ScenePath="Assets/CrazyCars/Scenes/CrazyDesert_Prototype.unity";
        static Shader Lit => Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

        [MenuItem("Crazy Cars/Build Mobile Prototype Scene")]
        public static void BuildScene()=>BuildScene(true);

        public static void BuildScene(bool showDialog)
        {
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            RenderSettings.ambientLight=new Color(.55f,.56f,.62f);
            RenderSettings.fog=true; RenderSettings.fogColor=new Color(.75f,.68f,.55f); RenderSettings.fogDensity=.002f;

            var sun=new GameObject("Sun").AddComponent<Light>(); sun.type=LightType.Directional; sun.intensity=1.25f; sun.shadows=LightShadows.Soft; sun.transform.rotation=Quaternion.Euler(48,-35,0);

            Material road=Mat("Road",new Color(.17f,.18f,.21f),.15f,.55f);
            Material sand=Mat("Sand",new Color(.82f,.57f,.29f),0,.12f);
            Material rail=Mat("Rail",new Color(.92f,.22f,.12f),0,.25f);
            Material pickup=Mat("Pickup",new Color(1f,.78f,.08f),.2f,.5f);

            var ground=GameObject.CreatePrimitive(PrimitiveType.Plane); ground.name="Desert"; ground.transform.localScale=new Vector3(18,1,12); ground.GetComponent<Renderer>().sharedMaterial=sand;

            const int segments=64; const float rx=58, rz=34, width=13;
            var waypoints=new List<Transform>();
            var track=new GameObject("TRACK");
            for(int i=0;i<segments;i++)
            {
                float a=i*Mathf.PI*2/segments, b=(i+1)*Mathf.PI*2/segments;
                Vector3 p=new(Mathf.Cos(a)*rx,.15f,Mathf.Sin(a)*rz), q=new(Mathf.Cos(b)*rx,.15f,Mathf.Sin(b)*rz), d=q-p;
                var s=GameObject.CreatePrimitive(PrimitiveType.Cube); s.transform.SetParent(track.transform); s.transform.position=(p+q)*.5f; s.transform.rotation=Quaternion.LookRotation(d); s.transform.localScale=new Vector3(width,.35f,d.magnitude+.4f); s.GetComponent<Renderer>().sharedMaterial=road;
                if(i%4==0){var w=new GameObject($"WP_{i:00}");w.transform.SetParent(track.transform);w.transform.position=p+Vector3.up*.5f;waypoints.Add(w.transform);}
                if(i%2==0)
                {
                    Vector3 r=Vector3.Cross(Vector3.up,d.normalized);
                    Barrier((p+q)*.5f+r*width*.55f,s.transform.rotation,d.magnitude,rail,track.transform);
                    Barrier((p+q)*.5f-r*width*.55f,s.transform.rotation,d.magnitude,rail,track.transform);
                }
            }

            Vector3[] cp={new(rx,1,0),new(0,1,rz),new(-rx,1,0),new(0,1,-rz)};
            Vector3[] cs={new(3,3,14),new(14,3,3),new(3,3,14),new(14,3,3)};
            for(int i=0;i<4;i++){var o=new GameObject($"Checkpoint_{i}");o.transform.position=cp[i];var c=o.AddComponent<BoxCollider>();c.isTrigger=true;c.size=cs[i];o.AddComponent<Checkpoint>().index=i;}

            for(int i=0;i<12;i++)
            {
                float a=i*Mathf.PI*2/12+.16f; var o=GameObject.CreatePrimitive(PrimitiveType.Cylinder);o.name="PowerStar";o.transform.position=new Vector3(Mathf.Cos(a)*(rx-1),1.2f,Mathf.Sin(a)*(rz-1));o.transform.localScale=new Vector3(.7f,.18f,.7f);o.GetComponent<Renderer>().sharedMaterial=pickup;o.GetComponent<Collider>().isTrigger=true;o.AddComponent<ItemPickup>();
            }

            var player=Kart("PLAYER_Flash",new Vector3(rx-8,1.2f,-3),new Color(.95f,.25f,.1f),true); player.transform.rotation=Quaternion.identity;
            Color[] colors={new(1,.7f,.1f),new(.45f,.25f,.78f),new(.2f,.7f,.92f)};
            for(int i=0;i<3;i++){var bot=Kart($"BOT_{i+1}",new Vector3(rx-12-i*3,1.2f,2+i*2.2f),colors[i],false);bot.transform.rotation=Quaternion.identity;bot.AddComponent<SimpleAIBot>().waypoints=waypoints.ToArray();}

            var cam=new GameObject("Main Camera").AddComponent<Camera>();cam.tag="MainCamera";cam.transform.position=player.transform.position+new Vector3(0,4,-8);var kc=cam.gameObject.AddComponent<KartCamera>();kc.target=player.transform;kc.targetBody=player.GetComponent<Rigidbody>();

            var manager=new GameObject("RaceManager").AddComponent<RaceManager>();manager.player=player.GetComponent<RaceProgress>();
            UI(player,manager);

            PlayerSettings.productName="Crazy Cars";
            PlayerSettings.companyName="Crazy Cars Studio";
            PlayerSettings.defaultInterfaceOrientation=UIOrientation.LandscapeLeft;
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android,"com.crazycars.game");

            System.IO.Directory.CreateDirectory("Assets/CrazyCars/Scenes");
            EditorSceneManager.SaveScene(scene,ScenePath);
            EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(ScenePath,true)};
            if(showDialog&&!Application.isBatchMode)EditorUtility.DisplayDialog("Crazy Cars","Prototype scene created. Press Play.","OK");
        }

        static Material Mat(string n,Color c,float metal,float smooth){var m=new Material(Lit){name=n,color=c};if(m.HasProperty("_Metallic"))m.SetFloat("_Metallic",metal);if(m.HasProperty("_Smoothness"))m.SetFloat("_Smoothness",smooth);return m;}
        static void Barrier(Vector3 p,Quaternion r,float len,Material m,Transform parent){var b=GameObject.CreatePrimitive(PrimitiveType.Cube);b.transform.SetParent(parent);b.transform.position=p+Vector3.up*.55f;b.transform.rotation=r;b.transform.localScale=new Vector3(.6f,.9f,len);b.GetComponent<Renderer>().sharedMaterial=m;}

        static GameObject Kart(string name,Vector3 pos,Color color,bool player)
        {
            var root=new GameObject(name);root.transform.position=pos;root.AddComponent<Rigidbody>();var col=root.AddComponent<BoxCollider>();col.size=new Vector3(1.6f,.85f,2.5f);col.center=new Vector3(0,.45f,0);
            var k=root.AddComponent<ArcadeKartController>();k.playerControlled=player;root.AddComponent<KartItemSystem>();root.AddComponent<RaceProgress>();
            var visual=new GameObject("Visual");visual.transform.SetParent(root.transform);k.visualRoot=visual.transform;
            var body=GameObject.CreatePrimitive(PrimitiveType.Cube);body.transform.SetParent(visual.transform);body.transform.localPosition=new Vector3(0,.42f,0);body.transform.localScale=new Vector3(1.5f,.5f,2.3f);body.GetComponent<Renderer>().sharedMaterial=Mat(name+"_Body",color,.1f,.55f);Object.DestroyImmediate(body.GetComponent<Collider>());
            var dark=Mat(name+"_Wheels",new Color(.03f,.04f,.05f),.1f,.3f);
            for(int x=-1;x<=1;x+=2)for(int z=-1;z<=1;z+=2){var w=GameObject.CreatePrimitive(PrimitiveType.Cylinder);w.transform.SetParent(visual.transform);w.transform.localPosition=new Vector3(.82f*x,.28f,.78f*z);w.transform.localRotation=Quaternion.Euler(0,0,90);w.transform.localScale=new Vector3(.45f,.22f,.45f);w.GetComponent<Renderer>().sharedMaterial=dark;Object.DestroyImmediate(w.GetComponent<Collider>());}
            var driver=GameObject.CreatePrimitive(PrimitiveType.Sphere);driver.transform.SetParent(visual.transform);driver.transform.localPosition=new Vector3(0,1.02f,-.1f);driver.transform.localScale=new Vector3(.72f,.85f,.72f);driver.GetComponent<Renderer>().sharedMaterial=Mat(name+"_Driver",Color.white,0,.3f);Object.DestroyImmediate(driver.GetComponent<Collider>());
            return root;
        }

        static Text Txt(Transform parent,string name,string text,Vector2 min,Vector2 max,int size)
        {
            var o=new GameObject(name);o.transform.SetParent(parent,false);var t=o.AddComponent<Text>();t.text=text;t.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");t.fontSize=size;t.fontStyle=FontStyle.Bold;t.alignment=TextAnchor.MiddleCenter;t.color=Color.white;var r=t.rectTransform;r.anchorMin=min;r.anchorMax=max;r.offsetMin=r.offsetMax=Vector2.zero;return t;
        }

        static void Btn(Transform parent,string label,Vector2 anchor,MobileButtonAction action)
        {
            var o=new GameObject(label);o.transform.SetParent(parent,false);var image=o.AddComponent<Image>();image.color=new Color(.04f,.05f,.08f,.68f);o.AddComponent<Button>();o.AddComponent<MobileHoldButton>().action=action;var r=o.GetComponent<RectTransform>();r.anchorMin=r.anchorMax=anchor;r.sizeDelta=new Vector2(120,120);Txt(o.transform,"Label",label,Vector2.zero,Vector2.one,30);
        }

        static void UI(GameObject player,RaceManager manager)
        {
            var es=new GameObject("EventSystem");es.AddComponent<EventSystem>();es.AddComponent<StandaloneInputModule>();
            var o=new GameObject("MobileHUD");var canvas=o.AddComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;var scaler=o.AddComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);o.AddComponent<GraphicRaycaster>();
            Btn(o.transform,"LEFT",new Vector2(.08f,.14f),MobileButtonAction.Left);Btn(o.transform,"RIGHT",new Vector2(.18f,.14f),MobileButtonAction.Right);Btn(o.transform,"GO",new Vector2(.91f,.15f),MobileButtonAction.Accelerate);Btn(o.transform,"BRAKE",new Vector2(.80f,.12f),MobileButtonAction.Brake);Btn(o.transform,"DRIFT",new Vector2(.72f,.25f),MobileButtonAction.Drift);Btn(o.transform,"ITEM",new Vector2(.91f,.31f),MobileButtonAction.Item);
            manager.countdownText=Txt(o.transform,"Countdown","3",new Vector2(.4f,.4f),new Vector2(.6f,.66f),88);manager.lapText=Txt(o.transform,"Lap","LAP 1 / 3",new Vector2(.02f,.88f),new Vector2(.22f,.98f),30);
            var speed=Txt(o.transform,"Speed","0 km/h",new Vector2(.78f,.88f),new Vector2(.98f,.98f),30);var item=Txt(o.transform,"Item","STAR",new Vector2(.82f,.72f),new Vector2(.98f,.83f),24);
            var hud=o.AddComponent<KartHUD>();hud.kart=player.GetComponent<ArcadeKartController>();hud.items=player.GetComponent<KartItemSystem>();hud.speedText=speed;hud.itemText=item;
        }
    }
}
#endif
