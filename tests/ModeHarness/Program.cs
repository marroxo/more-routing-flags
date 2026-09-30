using System;
using System.Collections.Generic;
using System.Reflection;
using MoreRoutingFlags;
using UnityEngine;

static class Program {
    static int checks;
    static void Check(bool ok,string message) { if(!ok) throw new Exception(message);checks++; }
    static void Main() {
        var routing=new RoutingFlag();Cache.routingFlag=routing;
        var set=new FlagSet();set.Add(new Flag());set.Add(new Flag());Plugin.currentSet=set;
        FlagMarkers.Rebuild(set);
        var marker=GameObject.Clones[0];
        Check(!marker.activeSelf,"Saved extra flags must be hidden when routing mode is off");
        routing.currentlyUsingFlag=true;Refresh();Check(marker.activeSelf && !marker.collider.enabled,"Routing mode reveals saved flags without enabling collisions");
        routing.currentlyUsingFlag=false;Refresh();Check(!marker.activeSelf,"Manual routing-mode off hides existing markers");
        routing.currentlyUsingFlag=true;Config.enabled.Value=false;Refresh();Check(!marker.activeSelf,"Disabled mod hides markers");
        Config.enabled.Value=true;Config.permaDeath=true;Refresh();Check(!marker.activeSelf,"Perma death hides markers");
        Config.permaDeath=false;Config.freeSolo=true;Refresh();Check(!marker.activeSelf,"Free Solo hides markers");
        Config.freeSolo=false;Config.showInactive.Value=false;Refresh();Check(!marker.activeSelf,"Show Inactive Flags off is respected");
        FlagMarkers.SetPickable(true);Refresh();Check(marker.activeSelf && marker.collider.enabled,"Picker can reveal and pick inactive flags");
        FlagMarkers.SetPickable(false);Refresh();Check(!marker.activeSelf && !marker.collider.enabled,"Closing picker restores display preference and collision safety");
        Config.showInactive.Value=true;
        PracticeMode.Enable();Check(routing.currentlyUsingFlag && routing.Watch.disableTimeAttack && !routing.Summit.gameObject.activeSelf,"Practice activation disables scoring immediately");
        PracticeMode.BeforeUpdate(routing);Check(routing.currentlyUsingFlag,"Saved flags retain practice mode");
        set.Remove();set.Remove();PlayerMove.current.grounded=false;PracticeMode.BeforeUpdate(routing);Check(routing.currentlyUsingFlag,"Airborne empty set retains practice mode");
        PlayerMove.current.grounded=true;Plugin.instance.picker.isOpen=true;PracticeMode.BeforeUpdate(routing);Check(routing.currentlyUsingFlag,"Open picker retains practice mode");
        Plugin.instance.picker.isOpen=false;FlyCamera.isActive=true;PracticeMode.BeforeUpdate(routing);Check(routing.currentlyUsingFlag,"Flight retains practice mode");
        FlyCamera.isActive=false;PracticeMode.BeforeUpdate(routing);Check(!routing.currentlyUsingFlag && routing.usedRoutingFlag[0]==0,"Grounded empty set clears owned practice mode");
        PracticeMode.Enable();routing.currentlyUsingFlag=false;PracticeMode.BeforeUpdate(routing);PlayerMove.current.grounded=false;PracticeMode.BeforeUpdate(routing);Check(!routing.currentlyUsingFlag,"Manual off is respected");
        PracticeMode.Enable();Check(routing.currentlyUsingFlag,"Next use reenables practice mode");
        routing.currentlyUsingFlag=false;Config.permaDeath=true;PracticeMode.Enable();Check(!routing.currentlyUsingFlag,"Perma death cannot enter mod practice mode");
        Config.permaDeath=false;Config.freeSolo=true;PracticeMode.Enable();Check(!routing.currentlyUsingFlag,"Free Solo cannot enter mod practice mode");
        Config.freeSolo=false;Config.enabled.Value=false;PracticeMode.Enable();Check(!routing.currentlyUsingFlag,"Disabled mod cannot enter practice mode");
        Console.WriteLine("PASS "+checks+" flag visibility and practice-mode checks");
    }
    static void Refresh() { typeof(FlagMarkers).GetMethod("UpdateVisibility",BindingFlags.Static|BindingFlags.NonPublic)?.Invoke(null,null); }
}
namespace UnityEngine {
    public class Object {
        public static T FindObjectOfType<T>() where T:class => PlayerMove.current as T;
    }
    public struct Vector3 { public static Vector3 operator -(Vector3 a)=>a; }
    public class Transform { public GameObject gameObject;public Vector3 position,forward; }
    public class MonoBehaviour:Object { }
    public class Collider { public bool enabled=true; }
    public class Renderer { public bool enabled;public object sharedMaterial; }
    public class GameObject:Object {
        public static List<GameObject> Clones=new List<GameObject>();
        public bool activeSelf=true;public string name,tag;public Transform transform;
        public Collider collider=new Collider();public Renderer renderer=new Renderer();
        public GameObject() { transform=new Transform{gameObject=this}; }
        public void SetActive(bool active) { activeSelf=active; }
        public T AddComponent<T>() where T:new() => new T();
        public T[] GetComponentsInChildren<T>(bool inactive=false) => !inactive && !activeSelf ? new T[0] : typeof(T)==typeof(Collider) ? new[]{(T)(object)collider} : new[]{(T)(object)renderer};
        public static GameObject Instantiate(GameObject source) { var result=new GameObject();Clones.Add(result);return result; }
        public static void Destroy(GameObject obj) { obj.SetActive(false); }
    }
}
class PlayerMove:UnityEngine.Object { public static PlayerMove current=new PlayerMove();public bool grounded;public bool IsGrounded()=>grounded; }
class TimeAttack { public bool disableTimeAttack; }
class StamperPeakSummit { public GameObject gameObject=new GameObject();public bool isAlps1,isAlps2,isAlps3; }
class RoutingFlag {
    public bool currentlyUsingFlag,customLevel_UsedRoutingFlag,isCustomLevel;
    public int currentPeak;public int[] usedRoutingFlag=new int[1];
    private TimeAttack pocketWatch=new TimeAttack();private StamperPeakSummit summitbox=new StamperPeakSummit();
    public TimeAttack Watch=>pocketWatch;public StamperPeakSummit Summit=>summitbox;
    public Transform routingFlagTransform=new GameObject().transform;public object routingFlagMat;
}
static class PlayerPrefsX { public static void SetIntArray(string key,int[] values) { } }
namespace HarmonyLib { static class AccessTools { public static FieldInfo Field(Type type,string name)=>type.GetField(name,BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance); } }
namespace MoreRoutingFlags {
    class Flag { public Vector3 normal; }
    class Setting<T> { public T Value;public Setting(T value) { Value=value; } }
    static class Config {
        internal static Setting<bool> enabled=new Setting<bool>(true),showInactive=new Setting<bool>(true);
        internal static bool permaDeath,freeSolo;
        internal static bool IsActive()=>enabled.Value && !permaDeath && !freeSolo;
    }
    static class Cache { internal static RoutingFlag routingFlag; }
    static class FlagPlacer { internal static Vector3 Resolve(Flag flag)=>new Vector3(); }
    class Picker { internal bool isOpen; }
    class Plugin { internal static Plugin instance=new Plugin();internal Picker picker=new Picker();internal static FlagSet currentSet; }
    static class FlyCamera { internal static bool isActive; }
}
