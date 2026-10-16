using UnityEngine;

namespace EasyCombat.Samples
{
    /// <summary>Small playable fixture. All combat configuration is in the generated assets.</summary>
    public sealed class BasicArena : MonoBehaviour
    {
        public DamageEmitter Emitter;
        public AttackDefinition[] Attacks;
        public Health[] Targets;
        public CombatJournal Journal;
        public Camera View;
        private int selectedAttack, selectedTarget;
        private string message = "Select a weapon and target, then fire.";
        private LineRenderer tracer;
        private float traceUntil;
        private bool autoFire;
        private float nextAuto;
        private GUIStyle title, label;

        private void Start()
        {
            tracer = GetComponent<LineRenderer>();
            Emitter.Fired += Fired;
            Debug.Log("EASYCOMBAT_SAMPLE_READY");
        }
        private void OnDestroy() { if (Emitter != null) Emitter.Fired -= Fired; }
        private void Fired(ShotInfo shot)
        {
            if (tracer == null || shot.Delivery == DamageDelivery.Projectile) return;
            tracer.positionCount = 2;
            tracer.SetPosition(0, shot.Origin); tracer.SetPosition(1, shot.End);
            tracer.startWidth = tracer.endWidth = Mathf.Max(.04f, shot.Radius * 2);
            tracer.enabled = true; traceUntil = Time.time + .12f;
        }
        private void Update()
        {
            if (tracer != null && Time.time >= traceUntil) tracer.enabled = false;
            if (autoFire && Time.time >= nextAuto) { nextAuto = Time.time + .6f; Fire(); }
        }
        public void Fire()
        {
            var target = Targets[selectedTarget];
            Emitter.Attack = Attacks[selectedAttack];
            var origin = Emitter.transform.position;
            if (Emitter.TryFire(origin, target.transform.position - origin)) message = Emitter.Attack.Label;
            else message = "Cooling down or pool unavailable.";
        }
        public void ResetArena()
        {
            Emitter.Pool.ClearActive(); Emitter.AreaPool.ClearActive();
            foreach (var target in Targets) target.ResetToFull();
            Journal.Clear(); Emitter.ResetCooldown(); message = "Arena reset.";
        }
        private void OnGUI()
        {
            title ??= new GUIStyle(GUI.skin.label) { fontSize = 24, fontStyle = FontStyle.Bold };
            label ??= new GUIStyle(GUI.skin.label) { fontSize = 15, wordWrap = true };
            var scale = Mathf.Min(Screen.width / 1100f, Screen.height / 700f);
            GUI.matrix = Matrix4x4.Scale(Vector3.one * scale);
            GUILayout.BeginArea(new Rect(20, 18, 330, 655), GUI.skin.box);
            GUILayout.Label("EASYCOMBAT / ARENA", title);
            GUILayout.Label("ScriptableObject combat playground", label);
            GUILayout.Space(12);
            for (var i = 0; i < Attacks.Length; i++)
                if (GUILayout.Toggle(selectedAttack == i, Attacks[i].Label, GUI.skin.button, GUILayout.Height(36))) selectedAttack = i;
            GUILayout.Space(12);
            for (var i = 0; i < Targets.Length; i++)
                if (GUILayout.Toggle(selectedTarget == i, Targets[i].name, GUI.skin.button, GUILayout.Height(30))) selectedTarget = i;
            GUILayout.Space(12);
            if (GUILayout.Button("FIRE", GUILayout.Height(45))) Fire();
            autoFire = GUILayout.Toggle(autoFire, "Automatic fire");
            if (GUILayout.Button("Reset arena", GUILayout.Height(30))) ResetArena();
            GUILayout.Label(message, label);
            GUILayout.Space(8);
            GUILayout.Label("DAMAGE JOURNAL", GUI.skin.box);
            for (var i = 0; i < Mathf.Min(6, Journal.Count); i++)
            {
                var row = Journal.GetNewest(i); var r = row.Result;
                GUILayout.Label($"{row.Target.Name}: HP -{r.AppliedAmount:0.#} | shield -{r.ShieldAbsorbed:0.#}\n{r.Damage.Origin.AttackName}  {r.Rejection}");
            }
            GUILayout.EndArea();
            foreach (var target in Targets)
            {
                var p = View.WorldToScreenPoint(target.transform.position + Vector3.up * 1.3f);
                if (p.z <= 0) continue;
                var rect = new Rect(p.x / scale - 95, (Screen.height - p.y) / scale, 190, 58);
                GUI.Box(rect, $"{target.name}\nHP {target.Current:0.#}/{target.Maximum:0.#}  Shield {target.ShieldCurrent:0.#}");
            }
            GUI.matrix = Matrix4x4.identity;
        }
    }
}
