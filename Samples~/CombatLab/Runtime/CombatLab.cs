using UnityEngine;

namespace EasyCombat.Samples
{
    /// <summary>Interactive examples, not production input/UI. Recipes are serialized SO assets.</summary>
    public sealed class CombatLab:MonoBehaviour
    {
        public DamageEmitter Emitter;
        public AttackDefinition Hitscan,PoisonShot,Swarm;
        public ReactionDefinition PoisonReaction;
        public StatusDefinition Poison,Burst;
        public CombatMelee Melee;
        public MeleeDefinition Slash;
        public Transform Tip;
        public Health[] Targets;
        public CombatPresentationHub Hub;
        public CombatJournal Journal;
        public Camera View;
        public LineRenderer Weapon;
        public int Mode { get; private set; }
        public string Message { get; private set; }="Choose a recipe; edit its SO in the generated Definitions folder.";
        private readonly string[] modeNames={"Shooter","Melee","Mass combat","Status combo"};
        private struct Number { public Vector3 Point; public string Text; public Color Color; public float Until; }
        private readonly Number[] numbers=new Number[64];
        private int nextNumber;
        private float swing,traceUntil,fpsTime,fps;
        private int fpsFrames;
        private GUIStyle title,text;
        private void Start()
        {
            Melee.AutomaticAdvance=false; Melee.SetAnchors(Tip);
            Emitter.GetComponent<CombatReactions>().Configure(new[] { PoisonReaction });
            Emitter.Pool.Prewarm(Swarm.ProjectilePrefab,1000);
            Hub.Emitted+=Presented; Emitter.Fired+=Fired; SelectMode(0);
            Debug.Log("EASYCOMBAT_LAB_READY");
        }
        private void OnDestroy() { if(Hub!=null) Hub.Emitted-=Presented; if(Emitter!=null) Emitter.Fired-=Fired; }
        private void Presented(CombatPresentationEvent value)
        {
            if(value.Kind!=CombatPresentationKind.Damage && value.Kind!=CombatPresentationKind.Shield) return;
            numbers[nextNumber]=new Number { Point=value.Position,Until=Time.unscaledTime+1,
                Text=$"{(value.Kind==CombatPresentationKind.Shield ? "shield " : "")}{value.Amount:0.#}{(value.Critical ? "!" : "")}{(value.Count>1 ? " x"+value.Count : "")}",
                Color=value.Critical ? Color.yellow : value.Kind==CombatPresentationKind.Shield ? Color.cyan : Color.white };
            nextNumber=(nextNumber+1)%numbers.Length;
        }
        private void Fired(ShotInfo shot)
        {
            if(shot.Delivery!=DamageDelivery.Hitscan) return;
            Weapon.positionCount=2; Weapon.SetPosition(0,shot.Origin); Weapon.SetPosition(1,shot.End);
            Weapon.startWidth=Weapon.endWidth=Mathf.Max(.05f,shot.Radius*2); Weapon.enabled=true; traceUntil=Time.time+.12f;
        }
        public void SelectMode(int mode)
        {
            ResetArena(); Mode=Mathf.Clamp(mode,0,3);
            for(var i=0;i<Targets.Length;i++) Targets[i].gameObject.SetActive(Mode!=3 || i==0);
            Physics.SyncTransforms();
        }
        public void ResetArena()
        {
            Melee.Stop(); Emitter.Pool.ClearActive(); Emitter.AreaPool.ClearActive();
            foreach(var target in Targets) target.ResetToFull();
            Hub.Clear(); Journal.Clear(); Emitter.ResetCooldown(); Weapon.enabled=false;
            for(var i=0;i<numbers.Length;i++) numbers[i]=default;
            Message="Arena reset.";
        }
        public bool Fire()
        {
            Emitter.Attack=Mode==3 ? PoisonShot : Hitscan; Emitter.ResetCooldown(); Physics.SyncTransforms();
            var origin=Emitter.transform.position; var fired=Emitter.TryFire(origin,Targets[0].transform.position-origin);
            Message=fired ? Emitter.Attack.Label : "Shot rejected."; return fired;
        }
        public bool StartSlash()
        {
            if(Melee.IsRunning) return false;
            swing=0; PoseTip(0); Physics.SyncTransforms(); var begun=Melee.Begin(Slash); Weapon.enabled=begun;
            Message=begun ? "Swept anchor: only the active SO time window deals damage." : "Slash rejected."; return begun;
        }
        private void PoseTip(float progress)
        {
            var radians=Mathf.Lerp(-70,70,progress)*Mathf.Deg2Rad;
            Tip.position=Emitter.transform.position+new Vector3(Mathf.Sin(radians),0,Mathf.Cos(radians))*4;
            Weapon.positionCount=2; Weapon.SetPosition(0,Emitter.transform.position); Weapon.SetPosition(1,Tip.position);
            Weapon.startWidth=Weapon.endWidth=.12f;
        }
        public int SpawnVolley(int count)
        {
            Physics.SyncTransforms(); var spawned=0; count=Mathf.Clamp(count,1,1000);
            for(var i=0;i<count;i++)
            {
                var angle=(i+.25f)*Mathf.PI*2/count; var radial=new Vector3(Mathf.Sin(angle),0,Mathf.Cos(angle));
                var origin=Emitter.transform.position+radial*8; var direction=-radial;
                var damage=Swarm.RollRandom(Emitter.gameObject,origin,direction);
                if(Emitter.Pool.Spawn(origin,direction,damage,Emitter.transform,Swarm,null)!=null) spawned++;
            }
            Message=$"Spawned {spawned} visible pooled projectiles. Rendering and UI included in displayed FPS."; return spawned;
        }
        private void Update()
        {
            if(Melee.IsRunning)
            {
                swing+=Time.deltaTime; PoseTip(Mathf.Clamp01(swing/Slash.Duration)); Melee.Advance(Time.deltaTime);
                if(!Melee.IsRunning) Weapon.enabled=false;
            }
            else if(Time.time>=traceUntil) Weapon.enabled=false;
            fpsTime+=Time.unscaledDeltaTime; fpsFrames++;
            if(fpsTime>=.5f) { fps=fpsFrames/fpsTime; fpsFrames=0; fpsTime=0; }
        }
        private void OnGUI()
        {
            title ??=new GUIStyle(GUI.skin.label) { fontSize=25,fontStyle=FontStyle.Bold };
            text ??=new GUIStyle(GUI.skin.label) { fontSize=15,wordWrap=true };
            var scale=Mathf.Min(Screen.width/1200f,Screen.height/760f); GUI.matrix=Matrix4x4.Scale(Vector3.one*scale);
            GUILayout.BeginArea(new Rect(18,18,335,724),GUI.skin.box);
            GUILayout.Label("EASYCOMBAT / LAB",title); GUILayout.Label("Four recipes. One combat pipeline.",text); GUILayout.Space(10);
            for(var i=0;i<modeNames.Length;i++) if(GUILayout.Toggle(Mode==i,modeNames[i],GUI.skin.button,GUILayout.Height(32)) && Mode!=i) SelectMode(i);
            GUILayout.Space(12);
            if(Mode==0) { GUILayout.Label("Wide instant hitscan + random criticals. Target 2 has a shield.",text); if(GUILayout.Button("FIRE HITSCAN",GUILayout.Height(42))) Fire(); }
            if(Mode==1) { GUILayout.Label("A visible weapon anchor sweeps through a timed contact window. Each target is hit once per swing.",text); if(GUILayout.Button("SWING",GUILayout.Height(42))) StartSlash(); }
            if(Mode==2)
            {
                GUILayout.Label("Slow sphere projectiles, swept collision and piercing. Pool is prewarmed for 1000.",text);
                GUILayout.BeginHorizontal(); foreach(var count in new[] {100,500,1000}) if(GUILayout.Button("+"+count,GUILayout.Height(42))) SpawnVolley(count); GUILayout.EndHorizontal();
                GUILayout.Label($"Active: {Emitter.Pool.ActiveCount} / 1000   FPS: {fps:0}",text);
                GUILayout.Label("This is a load example, not a mobile performance guarantee.",text);
            }
            if(Mode==3)
            {
                GUILayout.Label("Each hit applies poison. The third stack is consumed by an SO reaction and becomes a 25 damage burst. Independent poison stacks retain their own timers.",text);
                if(GUILayout.Button("APPLY POISON SHOT",GUILayout.Height(42))) Fire();
                GUILayout.Label($"Poison: {Targets[0].Statuses.GetStacks(Poison)}   Burst: {Targets[0].Statuses.GetStacks(Burst)}",text);
            }
            if(GUILayout.Button("RESET",GUILayout.Height(30))) ResetArena();
            GUILayout.Label(Message,text); GUILayout.Space(8);
            var buffer=Hub.Buffer;
            GUILayout.Label($"Output queue {buffer.Count} | merged {buffer.Merged}\nDropped: capacity {buffer.DroppedCapacity}, rate {buffer.DroppedRate}");
            GUILayout.Label("RECENT DAMAGE",GUI.skin.box);
            for(var i=0;i<Mathf.Min(4,Journal.Count);i++)
            { var r=Journal.GetNewest(i); GUILayout.Label($"{r.Target.Name}: HP -{r.Result.AppliedAmount:0.#} / shield -{r.Result.ShieldAbsorbed:0.#}\n{r.Result.Damage.Origin.StatusName ?? r.Result.Damage.Origin.AttackName}"); }
            GUILayout.EndArea();
            foreach(var target in Targets) if(target.gameObject.activeInHierarchy)
            {
                var point=View.WorldToScreenPoint(target.transform.position+Vector3.up*1.3f);
                if(point.z>0) GUI.Box(new Rect(point.x/scale-70,(Screen.height-point.y)/scale,140,48),$"{target.name}\n{target.Current:0}/{target.Maximum:0}  S {target.ShieldCurrent:0}");
            }
            for(var i=0;i<numbers.Length;i++) if(numbers[i].Until>Time.unscaledTime)
            {
                var value=numbers[i]; var point=View.WorldToScreenPoint(value.Point+Vector3.up*(1.5f+1-(value.Until-Time.unscaledTime)));
                if(point.z<=0) continue; GUI.color=value.Color; GUI.Label(new Rect(point.x/scale,(Screen.height-point.y)/scale,160,30),value.Text,text);
            }
            GUI.color=Color.white; GUI.matrix=Matrix4x4.identity;
        }
    }
}
